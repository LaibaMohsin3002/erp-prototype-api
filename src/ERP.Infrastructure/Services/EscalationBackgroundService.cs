using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

public class EscalationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EscalationBackgroundService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReminderLeadTime = TimeSpan.FromHours(2); // warn 2h before due

    public EscalationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<EscalationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CheckOverdueTasksAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "Escalation check failed."); }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckOverdueTasksAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<AuditService>();
        var emailApproval = scope.ServiceProvider.GetRequiredService<EmailApprovalService>();
        var engine = scope.ServiceProvider.GetRequiredService<WorkflowEngineService>();

        var now = DateTime.UtcNow;

        // --- Pre-due reminder pass ---
        var upcomingTasks = await db.WorkflowTasks
            .Include(t => t.Instance)
            .Where(t => t.Status == "Pending" && !t.ReminderSent && t.DueDate != null
                && t.DueDate >= now && t.DueDate <= now.Add(ReminderLeadTime))
            .ToListAsync();

        foreach (var task in upcomingTasks)
        {
            task.ReminderSent = true;
            await emailApproval.SendReminderEmailAsync(task.WorkflowTaskId, isOverdue: false);
            await audit.LogAsync("system", "UpcomingDueReminder", task.Instance.DocumentType, task.Instance.DocumentId,
                $"Reminder sent for step {task.StepNo}, due {task.DueDate:g}");
        }

        if (upcomingTasks.Count > 0)
            await db.SaveChangesAsync();

        // --- Existing overdue/escalation pass ---
        var overdueTasks = await db.WorkflowTasks
            .Include(t => t.Instance).ThenInclude(i => i.Definition).ThenInclude(d => d.Steps)
            .Where(t => t.Status == "Pending" && !t.IsEscalated && t.DueDate != null && t.DueDate < now)
            .ToListAsync();

        foreach (var task in overdueTasks)
        {
            var step = task.Instance.Definition.Steps.FirstOrDefault(s => s.StepNo == task.StepNo);
            task.IsEscalated = true;

            if (step is not null && !string.IsNullOrWhiteSpace(step.EscalationRole))
            {
                var escalatedTask = new Domain.Entities.WorkflowTask
                {
                    WorkflowInstanceId = task.WorkflowInstanceId,
                    StepNo = task.StepNo,
                    AssignedRole = step.EscalationRole,
                    Status = "Pending",
                    DueDate = null
                };
                db.WorkflowTasks.Add(escalatedTask);

                if (step.ReassignOnTimeout)
                    task.Status = "Cancelled";

                await db.SaveChangesAsync();
                await emailApproval.SendApprovalEmailAsync(escalatedTask.WorkflowTaskId);

                var actionLabel = step.ReassignOnTimeout ? "Reassigned" : "Escalate";
                var detail = step.ReassignOnTimeout
                    ? $"Task overdue at step {task.StepNo}, reassigned to {step.EscalationRole} (original assignee removed)"
                    : $"Task overdue at step {task.StepNo}, escalated to {step.EscalationRole} (original assignee retains access)";

                await audit.LogAsync("system", actionLabel, task.Instance.DocumentType, task.Instance.DocumentId, detail);
                await engine.LogSystemAuditAsync(task.WorkflowInstanceId, task.Instance.Definition.Version, task.StepNo,
                    task.AssignedRole, actionLabel, detail, task.Instance.Status, task.Instance.Status);
            }
            else
            {
                await emailApproval.SendReminderEmailAsync(task.WorkflowTaskId, isOverdue: true);
                await audit.LogAsync("system", "OverdueReminder", task.Instance.DocumentType, task.Instance.DocumentId,
                    $"Task overdue at step {task.StepNo}, no escalation role configured — reminder sent");
            }
        }

        if (overdueTasks.Count > 0)
            await db.SaveChangesAsync();
    }

}