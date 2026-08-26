using ERP.Application.DTOs;
using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class WorkflowDefinitionService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    public WorkflowDefinitionService(AppDbContext db, AuditService audit) { _db = db; _audit = audit; }

    public async Task<List<WorkflowDefinitionListDto>> GetAllAsync() =>
        await _db.WorkflowDefinitions
            .Where(d => d.IsLatestVersion)
            .Select(d => new WorkflowDefinitionListDto(d.WorkflowDefinitionId, d.Name, d.DocumentType, d.MinAmount, d.MaxAmount, d.IsActive, d.Steps.Count, d.Version))
            .ToListAsync();

    public async Task<WorkflowDefinitionDetailDto?> GetByIdAsync(int id)
    {
        var def = await _db.WorkflowDefinitions
            .Include(d => d.Steps)
            .FirstOrDefaultAsync(d => d.WorkflowDefinitionId == id);

        if (def is null) return null;

        var steps = def.Steps
            .OrderBy(s => s.StepNo)
            .Select(s => new WorkflowStepDto(s.WorkflowStepId, s.StepNo, s.RequiredRole, s.ParallelGroup, s.TimeLimitHours, s.EscalationRole, s.Priority))
            .ToList();
        return new WorkflowDefinitionDetailDto(def.WorkflowDefinitionId, def.Name, def.DocumentType, def.MinAmount, def.MaxAmount, def.IsActive, def.Version, def.IsLatestVersion, def.PreviousVersionId, steps);
        //return new WorkflowDefinitionDetailDto(def.WorkflowDefinitionId, def.Name, def.DocumentType, def.MinAmount, def.MaxAmount, def.IsActive, steps);
    }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(CreateWorkflowDefinitionDto dto, string actingUser)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.DocumentType))
            return (false, "Name and document type are required.", null);
        if (dto.MinAmount < 0 || dto.MaxAmount <= dto.MinAmount)
            return (false, "Max amount must be greater than min amount, and neither can be negative.", null);
        if (dto.Steps is null || dto.Steps.Count == 0)
            return (false, "A workflow definition must have at least one step.", null);
        if (dto.Steps.Any(s => string.IsNullOrWhiteSpace(s.RequiredRole)))
            return (false, "Every step must have a required role.", null);

        // check for overlapping amount ranges within the same document type — prevents ambiguous routing
        var overlapping = await _db.WorkflowDefinitions
            .Where(d => d.DocumentType == dto.DocumentType && d.IsActive)
            .Where(d => dto.MinAmount <= d.MaxAmount && dto.MaxAmount >= d.MinAmount)
            .AnyAsync();
        if (overlapping)
            return (false, "This amount range overlaps with an existing active workflow definition for this document type.", null);

        var definition = new WorkflowDefinition
        {
            Name = dto.Name,
            DocumentType = dto.DocumentType,
            MinAmount = dto.MinAmount,
            MaxAmount = dto.MaxAmount,
            IsActive = true,
            Version = 1,
            IsLatestVersion = true
        };

        foreach (var step in dto.Steps.OrderBy(s => s.StepNo))
        {
            definition.Steps.Add(new WorkflowStep
            {
                StepNo = step.StepNo,
                RequiredRole = step.RequiredRole,
                ParallelGroup = step.ParallelGroup,
                TimeLimitHours = step.TimeLimitHours,
                EscalationRole = step.EscalationRole,
                Priority = string.IsNullOrWhiteSpace(step.Priority) ? "Normal" : step.Priority
            });
        }

        _db.WorkflowDefinitions.Add(definition);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Create", "WorkflowDefinition", definition.WorkflowDefinitionId, $"Workflow '{definition.Name}' created with {definition.Steps.Count} step(s)");

        return (true, null, definition.WorkflowDefinitionId);
    }

    public async Task<bool> SetActiveAsync(int id, bool isActive, string actingUser)
    {
        var def = await _db.WorkflowDefinitions.FindAsync(id);
        if (def is null) return false;
        def.IsActive = isActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, isActive ? "Activate" : "Deactivate", "WorkflowDefinition", id, null);
        return true;
    }

    // Used by the PO submission flow to find the right definition for a given amount
    public async Task<WorkflowDefinition?> FindMatchingDefinitionAsync(string documentType, decimal amount) =>
       await _db.WorkflowDefinitions
           .Include(d => d.Steps)
           .Where(d => d.DocumentType == documentType && d.IsActive && d.IsLatestVersion && amount >= d.MinAmount && amount <= d.MaxAmount)
           .FirstOrDefaultAsync();
    
    public async Task<(bool Success, string? Error, int? NewDefinitionId)> UpdateAsync(int id, CreateWorkflowDefinitionDto dto, string actingUser)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.DocumentType))
            return (false, "Name and document type are required.", null);
        if (dto.MinAmount < 0 || dto.MaxAmount <= dto.MinAmount)
            return (false, "Max amount must be greater than min amount, and neither can be negative.", null);
        if (dto.Steps is null || dto.Steps.Count == 0)
            return (false, "A workflow definition must have at least one step.", null);
        if (dto.Steps.Any(s => string.IsNullOrWhiteSpace(s.RequiredRole)))
            return (false, "Every step must have a required role.", null);

        var definition = await _db.WorkflowDefinitions.Include(d => d.Steps).FirstOrDefaultAsync(d => d.WorkflowDefinitionId == id);
        if (definition is null) return (false, "Workflow definition not found.", null);

        var overlapping = await _db.WorkflowDefinitions
            .Where(d => d.DocumentType == dto.DocumentType && d.IsActive && d.IsLatestVersion && d.WorkflowDefinitionId != id)
            .Where(d => dto.MinAmount <= d.MaxAmount && dto.MaxAmount >= d.MinAmount)
            .AnyAsync();
        if (overlapping) return (false, "This amount range overlaps with an existing active workflow definition for this document type.", null);

        var hasBeenUsed = await _db.WorkflowInstances.AnyAsync(i => i.WorkflowDefinitionId == id);

        if (!hasBeenUsed)
        {
            // Never used — safe to edit in place, no new version needed.
            definition.Name = dto.Name;
            definition.DocumentType = dto.DocumentType;
            definition.MinAmount = dto.MinAmount;
            definition.MaxAmount = dto.MaxAmount;

            _db.WorkflowSteps.RemoveRange(definition.Steps);
            definition.Steps.Clear();
            foreach (var step in dto.Steps.OrderBy(s => s.StepNo))
            {
                definition.Steps.Add(new WorkflowStep
                {
                    StepNo = step.StepNo,
                    RequiredRole = step.RequiredRole,
                    ParallelGroup = step.ParallelGroup,
                    TimeLimitHours = step.TimeLimitHours,
                    EscalationRole = step.EscalationRole,
                    Priority = string.IsNullOrWhiteSpace(step.Priority) ? "Normal" : step.Priority
                });
            }

            await _db.SaveChangesAsync();
            await _audit.LogAsync(actingUser, "Update", "WorkflowDefinition", id, $"Workflow '{definition.Name}' updated");
            return (true, null, id);
        }

        // Has real approval history — fork a new version instead of mutating, so existing instances
        // keep pointing at the definition/steps as they were when they were actually used.
        var newVersion = new WorkflowDefinition
        {
            Name = dto.Name,
            DocumentType = dto.DocumentType,
            MinAmount = dto.MinAmount,
            MaxAmount = dto.MaxAmount,
            IsActive = true,
            Version = definition.Version + 1,
            PreviousVersionId = definition.WorkflowDefinitionId,
            IsLatestVersion = true
        };
        foreach (var step in dto.Steps.OrderBy(s => s.StepNo))
        {
            newVersion.Steps.Add(new WorkflowStep
            {
                StepNo = step.StepNo,
                RequiredRole = step.RequiredRole,
                ParallelGroup = step.ParallelGroup,
                TimeLimitHours = step.TimeLimitHours,
                EscalationRole = step.EscalationRole
            });
        }

        definition.IsLatestVersion = false;
        definition.IsActive = false; // stop routing new submissions to the old version; it stays viewable for history

        _db.WorkflowDefinitions.Add(newVersion);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Version", "WorkflowDefinition", newVersion.WorkflowDefinitionId,
            $"Workflow '{definition.Name}' had prior approval history — created version {newVersion.Version} instead of editing version {definition.Version} in place");

        return (true, null, newVersion.WorkflowDefinitionId);
    }
    public async Task<(bool Success, string? Error)> DeleteAsync(int id, string actingUser)
    {
        var hasInstances = await _db.WorkflowInstances.AnyAsync(i => i.WorkflowDefinitionId == id);
        if (hasInstances)
            return (false, "This workflow has already been used by one or more documents and cannot be deleted. Deactivate it instead.");

        var definition = await _db.WorkflowDefinitions.Include(d => d.Steps).FirstOrDefaultAsync(d => d.WorkflowDefinitionId == id);
        if (definition is null) return (false, "Workflow definition not found.");

        _db.WorkflowSteps.RemoveRange(definition.Steps);
        _db.WorkflowDefinitions.Remove(definition);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Delete", "WorkflowDefinition", id, $"Workflow '{definition.Name}' deleted");
        return (true, null);
    }
}