using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace ERP.Infrastructure.Services;

public class EmailApprovalService
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;
    private readonly IConfiguration _config;
    private readonly DelegationService _delegations;

    public EmailApprovalService(AppDbContext db, EmailService email, IConfiguration config, DelegationService delegations)
    {
        _db = db;
        _email = email;
        _config = config;
        _delegations = delegations;
    }

    public async Task SendApprovalEmailAsync(int workflowTaskId)
    {
        var task = await _db.WorkflowTasks
            .Include(t => t.Instance)
            .FirstOrDefaultAsync(t => t.WorkflowTaskId == workflowTaskId);
        if (task is null) return;

        var recipients = await GetEffectiveRoleHoldersAsync(task.AssignedRole);
        if (recipients.Count == 0) return;

        var canEmailApprove = recipients.Count == 1;

        var expiresAt = DateTime.UtcNow.AddHours(48);
        string? approveUrl = null;
        string? rejectUrl = null;

        if (canEmailApprove)
        {
            var approveToken = GenerateToken();
            var rejectToken = GenerateToken();

            _db.WorkflowActionTokens.AddRange(
                new WorkflowActionToken { Token = approveToken, WorkflowTaskId = task.WorkflowTaskId, Action = "Approve", ExpiresAt = expiresAt },
                new WorkflowActionToken { Token = rejectToken, WorkflowTaskId = task.WorkflowTaskId, Action = "Reject", ExpiresAt = expiresAt }
            );
            await _db.SaveChangesAsync();

            var baseUrl = _config["App:BaseUrl"] ?? "https://localhost:7000";
            approveUrl = $"{baseUrl}/api/workflow-email-actions/{approveToken}";
            rejectUrl = $"{baseUrl}/api/workflow-email-actions/{rejectToken}";
        }

        var docLabel = task.Instance.DocumentType == "PurchaseOrder"
            ? $"Purchase Order #{task.Instance.DocumentId}"
            : $"{task.Instance.DocumentType} #{task.Instance.DocumentId}";

        var actionsHtml = canEmailApprove
            ? $@"<p>
                    <a href='{approveUrl}' style='background:#12151c;color:white;padding:10px 20px;border-radius:6px;text-decoration:none;margin-right:8px;'>Approve</a>
                    <a href='{rejectUrl}' style='background:#dc2626;color:white;padding:10px 20px;border-radius:6px;text-decoration:none;'>Reject</a>
                 </p>
                 <p style='color:#666;font-size:12px;'>This link expires in 48 hours and can only be used once.</p>"
            : $@"<p>Please log in to the ERP system to review this task — multiple people currently share this approval role, so this action must be completed in-app.</p>";

        var html = $@"
            <div style='font-family: sans-serif; max-width: 480px;'>
                <h2>Approval needed: {docLabel}</h2>
                <p>You have a pending approval task (Step {task.StepNo}).</p>
                {actionsHtml}
            </div>";

        foreach (var user in recipients)
        {
            await _email.SendAsync(user.Email, $"Approval needed: {docLabel}", html);
        }
    }

    public async Task SendReminderEmailAsync(int workflowTaskId)
    {
        var task = await _db.WorkflowTasks.Include(t => t.Instance).FirstOrDefaultAsync(t => t.WorkflowTaskId == workflowTaskId);
        if (task is null) return;

        var recipients = await GetEffectiveRoleHoldersAsync(task.AssignedRole);

        var docLabel = task.Instance.DocumentType == "PurchaseOrder"
            ? $"Purchase Order #{task.Instance.DocumentId}"
            : $"{task.Instance.DocumentType} #{task.Instance.DocumentId}";

        var html = $@"
            <div style='font-family: sans-serif; max-width: 480px;'>
                <h2>Reminder: {docLabel} is overdue for your approval</h2>
                <p>This task (Step {task.StepNo}) was due on {task.DueDate:g} and is still pending.</p>
                <p>Please log in to the ERP system to review and act on it.</p>
            </div>";

        foreach (var user in recipients)
            await _email.SendAsync(user.Email, $"Reminder: {docLabel} needs your approval", html);
    }

    public async Task<(bool Success, string? Error, int? WorkflowTaskId, string? DocumentType, int? DocumentId, string? Action, string? ActingUserName)> RedeemTokenAsync(string token)
    {
        var actionToken = await _db.WorkflowActionTokens
            .Include(t => t.Task).ThenInclude(wt => wt.Instance)
            .FirstOrDefaultAsync(t => t.Token == token);

        if (actionToken is null) return (false, "Invalid or unrecognized link.", null, null, null, null, null);
        if (actionToken.IsUsed) return (false, "This link has already been used.", null, null, null, null, null);
        if (actionToken.ExpiresAt < DateTime.UtcNow) return (false, "This link has expired.", null, null, null, null, null);
        if (actionToken.Task.Status != "Pending") return (false, "This task has already been actioned.", null, null, null, null, null);

        var holder = await GetEffectiveRoleHoldersAsync(actionToken.Task.AssignedRole);

        if (holder.Count != 1)
            return (false, "This action can no longer be completed via email — please log in to the system to review it.", null, null, null, null, null);

        actionToken.IsUsed = true;

        var siblingTokens = await _db.WorkflowActionTokens
            .Where(t => t.WorkflowTaskId == actionToken.WorkflowTaskId && t.WorkflowActionTokenId != actionToken.WorkflowActionTokenId && !t.IsUsed)
            .ToListAsync();
        foreach (var sibling in siblingTokens) sibling.IsUsed = true;

        await _db.SaveChangesAsync();

        return (true, null, actionToken.WorkflowTaskId, actionToken.Task.Instance.DocumentType, actionToken.Task.Instance.DocumentId, actionToken.Action, holder[0].UserName);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private async Task<List<User>> GetEffectiveRoleHoldersAsync(string roleName)
    {
        var directHolders = await _db.UserRoles
            .Where(ur => ur.IsActive)
            .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => new { ur.UserId, r.RoleName })
            .Where(x => x.RoleName == roleName)
            .Join(_db.Users, x => x.UserId, u => u.UserId, (x, u) => u)
            .Where(u => u.IsActive)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var activeDelegateIds = await _db.Delegations
            .Where(d => d.IsActive && d.StartDate <= now && d.EndDate >= now)
            .Select(d => new { d.DelegatorUserId, d.DelegateUserId })
            .ToListAsync();

        var delegateUsers = new List<User>();
        foreach (var link in activeDelegateIds)
        {
            var delegatorHasRole = directHolders.Any(u => u.UserId == link.DelegatorUserId);
            if (delegatorHasRole)
            {
                var delegateUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == link.DelegateUserId && u.IsActive);
                if (delegateUser is not null) delegateUsers.Add(delegateUser);
            }
        }

        return directHolders.Concat(delegateUsers).DistinctBy(u => u.UserId).ToList();
    }
}