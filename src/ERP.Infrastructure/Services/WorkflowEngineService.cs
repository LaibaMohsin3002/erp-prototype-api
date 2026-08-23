using ERP.Application.DTOs;
using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class WorkflowEngineService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly WorkflowDefinitionService _definitions;
    private readonly DelegationService _delegations;

    public WorkflowEngineService(AppDbContext db, AuditService audit, WorkflowDefinitionService definitions, DelegationService delegations)
    {
        _db = db;
        _audit = audit;
        _definitions = definitions;
        _delegations = delegations;
    }

    // Groups steps into ordered "stages" — a stage is either one sequential step,
    // or a set of steps sharing the same non-zero ParallelGroup, all activated together.
    private static List<List<WorkflowStep>> GetStages(List<WorkflowStep> steps)
    {
        var stages = new List<List<WorkflowStep>>();
        var remaining = steps.OrderBy(s => s.StepNo).ToList();
        var processedGroups = new HashSet<int>();

        foreach (var step in remaining)
        {
            if (step.ParallelGroup != 0)
            {
                if (processedGroups.Contains(step.ParallelGroup)) continue;
                processedGroups.Add(step.ParallelGroup);
                stages.Add(remaining.Where(s => s.ParallelGroup == step.ParallelGroup).ToList());
            }
            else
            {
                stages.Add(new List<WorkflowStep> { step });
            }
        }

        return stages.OrderBy(stage => stage.Min(s => s.StepNo)).ToList();
    }

    public async Task<(bool Success, string? Error, int? InstanceId)> StartWorkflowAsync(string documentType, int documentId, decimal amount, string startedBy)
    {
        var definition = await _definitions.FindMatchingDefinitionAsync(documentType, amount);
        if (definition is null)
            return (false, $"No active workflow definition found for {documentType} at amount {amount:C}.", null);

        var stages = GetStages(definition.Steps.ToList());
        if (stages.Count == 0)
            return (false, "Matched workflow definition has no steps configured.", null);

        var instance = new WorkflowInstance
        {
            WorkflowDefinitionId = definition.WorkflowDefinitionId,
            DocumentType = documentType,
            DocumentId = documentId,
            Status = "InProgress",
            CurrentStepNo = stages[0].Min(s => s.StepNo),
            StartedBy = startedBy
        };

        foreach (var step in stages[0])
        {
            instance.Tasks.Add(new WorkflowTask
            {
                StepNo = step.StepNo,
                AssignedRole = step.RequiredRole,
                Status = "Pending",
                DueDate = step.TimeLimitHours > 0 ? DateTime.UtcNow.AddHours(step.TimeLimitHours) : null
            });
        }

        _db.WorkflowInstances.Add(instance);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(startedBy, "Submit", documentType, documentId, $"Submitted for approval under '{definition.Name}'");

        return (true, null, instance.WorkflowInstanceId);
    }

    public async Task<(bool Success, string? Error, string? DocumentType, int? DocumentId, string? NewInstanceStatus)> ProcessActionAsync(int taskId, string action, string actingUser, string? comments)
    {
        var task = await _db.WorkflowTasks
            .Include(t => t.Instance)
            .ThenInclude(i => i.Definition)
            .ThenInclude(d => d.Steps)
            .FirstOrDefaultAsync(t => t.WorkflowTaskId == taskId);

        if (task is null) return (false, "Task not found.", null, null, null);
        if (task.Status != "Pending") return (false, "This task has already been actioned.", null, null, null);
        if (action is not ("Approve" or "Reject" or "Return")) return (false, "Invalid action.", null, null, null);

        var effectiveRoles = await _delegations.GetEffectiveRolesAsync(actingUser);
        if (!effectiveRoles.Contains(task.AssignedRole))
            return (false, "You are not authorized to act on this task.", null, null, null);

        var instance = task.Instance;

        var alreadyActedOnThisInstance = await _db.WorkflowTasks
            .AnyAsync(t => t.WorkflowInstanceId == instance.WorkflowInstanceId && t.ActionedBy == actingUser && t.WorkflowTaskId != task.WorkflowTaskId);
        if (alreadyActedOnThisInstance)
            return (false, "You cannot approve multiple steps of the same document.", null, null, null);

        task.Status = action switch { "Approve" => "Approved", "Reject" => "Rejected", "Return" => "Returned", _ => task.Status };
        task.ActionedBy = actingUser;
        task.ActionedDate = DateTime.UtcNow;
        task.Comments = comments;

        // Close out any sibling tasks at the same step (e.g. an escalated duplicate) once one of them is actioned
        var siblingTasks = await _db.WorkflowTasks
            .Where(t => t.WorkflowInstanceId == instance.WorkflowInstanceId && t.StepNo == task.StepNo && t.Status == "Pending" && t.WorkflowTaskId != task.WorkflowTaskId)
            .ToListAsync();
        foreach (var sibling in siblingTasks)
            sibling.Status = "Cancelled";

        await _audit.LogAsync(actingUser, action, instance.DocumentType, instance.DocumentId, comments);

        if (action is "Reject" or "Return")
        {
            instance.Status = action == "Reject" ? "Rejected" : "Returned";
            instance.CompletedDate = DateTime.UtcNow;

            var otherPending = await _db.WorkflowTasks
                .Where(t => t.WorkflowInstanceId == instance.WorkflowInstanceId && t.Status == "Pending" && t.WorkflowTaskId != task.WorkflowTaskId)
                .ToListAsync();
            foreach (var t in otherPending) t.Status = "Cancelled";

            await _db.SaveChangesAsync();
            return (true, null, instance.DocumentType, instance.DocumentId, instance.Status);
        }

        // Approve path — check if the current stage is fully approved before advancing
        var stillPendingInStage = await _db.WorkflowTasks
            .AnyAsync(t => t.WorkflowInstanceId == instance.WorkflowInstanceId && t.StepNo == task.StepNo && t.Status == "Pending");

        if (stillPendingInStage)
        {
            await _db.SaveChangesAsync();
            return (true, null, instance.DocumentType, instance.DocumentId, null); // instance not yet complete, no status change to report
        }

        var stages = GetStages(instance.Definition.Steps.ToList());
        var currentStageIndex = stages.FindIndex(s => s.Any(step => step.StepNo == task.StepNo));
        var nextStage = (currentStageIndex >= 0 && currentStageIndex + 1 < stages.Count) ? stages[currentStageIndex + 1] : null;

        if (nextStage is null)
        {
            instance.Status = "Approved";
            instance.CompletedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return (true, null, instance.DocumentType, instance.DocumentId, instance.Status);
        }

        instance.CurrentStepNo = nextStage.Min(s => s.StepNo);

        foreach (var step in nextStage)
        {
            _db.WorkflowTasks.Add(new WorkflowTask
            {
                WorkflowInstanceId = instance.WorkflowInstanceId,
                StepNo = step.StepNo,
                AssignedRole = step.RequiredRole,
                Status = "Pending",
                DueDate = step.TimeLimitHours > 0 ? DateTime.UtcNow.AddHours(step.TimeLimitHours) : null
            });
        }

        await _db.SaveChangesAsync();
        return (true, null, instance.DocumentType, instance.DocumentId, null);
    }

    public async Task<List<InboxTaskDto>> GetInboxTasksForRolesAsync(List<string> roles)
    {
        var tasks = await _db.WorkflowTasks
            .Include(t => t.Instance)
            .Where(t => t.Status == "Pending" && roles.Contains(t.AssignedRole))
            .OrderBy(t => t.CreatedDate)
            .ToListAsync();

        // DocumentLabel resolution: extend this switch as new document types are added
        var result = new List<InboxTaskDto>();
        foreach (var t in tasks)
        {
            string label = $"{t.Instance.DocumentType} #{t.Instance.DocumentId}";
            if (t.Instance.DocumentType == "PurchaseOrder")
            {
                var po = await _db.POHeaders.FindAsync(t.Instance.DocumentId);
                if (po is not null) label = $"{po.PONumber} ({po.TotalAmount:C})";
            }
            result.Add(new InboxTaskDto(t.WorkflowTaskId, t.WorkflowInstanceId, t.Instance.DocumentType, t.Instance.DocumentId, label, t.StepNo, t.CreatedDate));
        }
        return result;
    }
}