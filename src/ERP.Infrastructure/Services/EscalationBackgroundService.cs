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
    //private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    public EscalationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<EscalationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOverdueTasksAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Escalation check failed.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckOverdueTasksAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<AuditService>();

        var now = DateTime.UtcNow;

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
                // Widen who can act on this task by creating an additional task at the same step for the escalation role
                db.WorkflowTasks.Add(new Domain.Entities.WorkflowTask
                {
                    WorkflowInstanceId = task.WorkflowInstanceId,
                    StepNo = task.StepNo,
                    AssignedRole = step.EscalationRole,
                    Status = "Pending",
                    DueDate = null
                });

                await audit.LogAsync("system", "Escalate", task.Instance.DocumentType, task.Instance.DocumentId,
                    $"Task overdue at step {task.StepNo}, escalated to {step.EscalationRole}");
            }
            else
            {
                await audit.LogAsync("system", "OverdueReminder", task.Instance.DocumentType, task.Instance.DocumentId,
                    $"Task overdue at step {task.StepNo}, no escalation role configured — reminder only");
            }
        }

        if (overdueTasks.Count > 0)
            await db.SaveChangesAsync();
    }
}