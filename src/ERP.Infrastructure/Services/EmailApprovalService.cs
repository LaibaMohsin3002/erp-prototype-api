//using ERP.Application.DTOs;
//using ERP.Domain.Entities;
//using ERP.Infrastructure.Data;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Configuration;
//using System.Security.Cryptography;
//using System.Text;

//namespace ERP.Infrastructure.Services;

//public class EmailApprovalService
//{
//    private readonly AppDbContext _db;
//    private readonly EmailService _email;
//    private readonly IConfiguration _config;
//    private readonly DelegationService _delegations;

//    public EmailApprovalService(AppDbContext db, EmailService email, IConfiguration config, DelegationService delegations)
//    {
//        _db = db;
//        _email = email;
//        _config = config;
//        _delegations = delegations;
//    }

//    public async Task SendApprovalEmailAsync(int workflowTaskId)
//    {
//        var task = await _db.WorkflowTasks
//            .Include(t => t.Instance)
//            .FirstOrDefaultAsync(t => t.WorkflowTaskId == workflowTaskId);
//        if (task is null) return;

//        var recipients = await GetEffectiveRoleHoldersAsync(task.AssignedRole);
//        if (recipients.Count == 0) return;

//        var canEmailApprove = recipients.Count == 1;

//        var expiresAt = DateTime.UtcNow.AddHours(48);
//        string? approveUrl = null;
//        string? rejectUrl = null;

//        if (canEmailApprove)
//        {
//            var approveToken = GenerateToken();
//            var rejectToken = GenerateToken();

//            _db.WorkflowActionTokens.AddRange(
//                new WorkflowActionToken { Token = approveToken, WorkflowTaskId = task.WorkflowTaskId, Action = "Approve", ExpiresAt = expiresAt },
//                new WorkflowActionToken { Token = rejectToken, WorkflowTaskId = task.WorkflowTaskId, Action = "Reject", ExpiresAt = expiresAt }
//            );
//            await _db.SaveChangesAsync();

//            var baseUrl = _config["App:BaseUrl"] ?? "https://localhost:7000";
//            approveUrl = $"{baseUrl}/api/workflow-email-actions/{approveToken}";
//            rejectUrl = $"{baseUrl}/api/workflow-email-actions/{rejectToken}";
//        }

//        // --- RICH CONTENT STARTS HERE ---

//        var docDetails = await GetDocumentDetailsAsync(task.Instance.DocumentType, task.Instance.DocumentId);
//        var requesterName = await GetUserNameAsync(task.Instance.StartedBy);
//        var previousComments = await GetPreviousCommentsAsync(task.Instance.WorkflowInstanceId, task.StepNo);

//        var totalAmountDisplay = docDetails.TotalAmount > 0 ? $" — {docDetails.TotalAmount:C}" : "";
//        var docLabel = $"{docDetails.DocumentNumber}{totalAmountDisplay}";

//        var html = BuildApprovalEmailHtml(
//            documentLabel: docLabel,
//            requesterName: requesterName,
//            stepNo: task.StepNo,
//            lineItems: docDetails.LineItems,
//            totalAmount: docDetails.TotalAmount,
//            previousComments: previousComments,
//            canEmailApprove: canEmailApprove,
//            approveUrl: approveUrl,
//            rejectUrl: rejectUrl
//        );

//        foreach (var user in recipients)
//        {
//            await _email.SendAsync(user.Email, $"Approval needed: {docLabel}", html);
//        }
//    }
//    public async Task SendReminderEmailAsync(int workflowTaskId)
//    {
//        var task = await _db.WorkflowTasks
//            .Include(t => t.Instance)
//            .FirstOrDefaultAsync(t => t.WorkflowTaskId == workflowTaskId);
//        if (task is null) return;

//        var recipients = await GetEffectiveRoleHoldersAsync(task.AssignedRole);

//        var docDetails = await GetDocumentDetailsAsync(task.Instance.DocumentType, task.Instance.DocumentId);
//        var requesterName = await GetUserNameAsync(task.Instance.StartedBy);
//        var docUrl = GetDocumentUrl(task.Instance.DocumentType, task.Instance.DocumentId);
//        var docLabel = docDetails.DocumentNumber;

//        // Build the HTML string first
//        var html = $@"
//    <div style='font-family: sans-serif; max-width: 600px;'>
//        <h2 style='color:#b45309;'>Reminder: {docLabel} is overdue for your approval</h2>
//        <p><strong>{requesterName}</strong> submitted this document on <strong>{task.Instance.StartedDate:g}</strong>.</p>
//        <p>This task (Step {task.StepNo}) was due on <strong>{task.DueDate:g}</strong> and is still pending.</p>";

//        // Add the "View full details" link if available
//        if (!string.IsNullOrEmpty(docUrl))
//        {
//            html += $"<p><a href='{docUrl}' style='color:#3454d1;'>View full document details →</a></p>";
//        }

//        html += @"
//        <p style='margin-top:16px;'>Please log in to the ERP system to review and act on it.</p>
//        <p style='margin-top:24px;font-size:12px;color:#9ca3af;'>This is an automated reminder.</p>
//    </div>";

//        foreach (var user in recipients)
//            await _email.SendAsync(user.Email, $"Reminder: {docLabel} needs your approval", html);
//    }

//    public async Task<(bool Success, string? Error, int? WorkflowTaskId, string? DocumentType, int? DocumentId, string? Action, string? ActingUserName)> RedeemTokenAsync(string token)
//    {
//        var actionToken = await _db.WorkflowActionTokens
//            .Include(t => t.Task).ThenInclude(wt => wt.Instance)
//            .FirstOrDefaultAsync(t => t.Token == token);

//        if (actionToken is null) return (false, "Invalid or unrecognized link.", null, null, null, null, null);
//        if (actionToken.IsUsed) return (false, "This link has already been used.", null, null, null, null, null);
//        if (actionToken.ExpiresAt < DateTime.UtcNow) return (false, "This link has expired.", null, null, null, null, null);
//        if (actionToken.Task.Status != "Pending") return (false, "This task has already been actioned.", null, null, null, null, null);

//        var holder = await GetEffectiveRoleHoldersAsync(actionToken.Task.AssignedRole);

//        if (holder.Count != 1)
//            return (false, "This action can no longer be completed via email — please log in to the system to review it.", null, null, null, null, null);

//        actionToken.IsUsed = true;

//        var siblingTokens = await _db.WorkflowActionTokens
//            .Where(t => t.WorkflowTaskId == actionToken.WorkflowTaskId && t.WorkflowActionTokenId != actionToken.WorkflowActionTokenId && !t.IsUsed)
//            .ToListAsync();
//        foreach (var sibling in siblingTokens) sibling.IsUsed = true;

//        await _db.SaveChangesAsync();

//        return (true, null, actionToken.WorkflowTaskId, actionToken.Task.Instance.DocumentType, actionToken.Task.Instance.DocumentId, actionToken.Action, holder[0].UserName);
//    }

//    private static string GenerateToken()
//    {
//        var bytes = RandomNumberGenerator.GetBytes(32);
//        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
//    }

//    private async Task<List<User>> GetEffectiveRoleHoldersAsync(string roleName)
//    {
//        var directHolders = await _db.UserRoles
//            .Where(ur => ur.IsActive)
//            .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => new { ur.UserId, r.RoleName })
//            .Where(x => x.RoleName == roleName)
//            .Join(_db.Users, x => x.UserId, u => u.UserId, (x, u) => u)
//            .Where(u => u.IsActive)
//            .ToListAsync();

//        var now = DateTime.UtcNow;
//        var activeDelegateIds = await _db.Delegations
//            .Where(d => d.IsActive && d.StartDate <= now && d.EndDate >= now)
//            .Select(d => new { d.DelegatorUserId, d.DelegateUserId })
//            .ToListAsync();

//        var delegateUsers = new List<User>();
//        foreach (var link in activeDelegateIds)
//        {
//            var delegatorHasRole = directHolders.Any(u => u.UserId == link.DelegatorUserId);
//            if (delegatorHasRole)
//            {
//                var delegateUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == link.DelegateUserId && u.IsActive);
//                if (delegateUser is not null) delegateUsers.Add(delegateUser);
//            }
//        }

//        return directHolders.Concat(delegateUsers).DistinctBy(u => u.UserId).ToList();
//    }

//    private record DocumentDetails(string DocumentNumber, string DocumentLabel, decimal TotalAmount, List<EmailLineItemDto> LineItems);

//    private async Task<DocumentDetails> GetDocumentDetailsAsync(string documentType, int documentId)
//    {
//        if (documentType == "PurchaseOrder")
//        {
//            var po = await _db.POHeaders
//                .Include(h => h.Supplier)
//                .Include(h => h.Lines)
//                .FirstOrDefaultAsync(h => h.POId == documentId);

//            if (po is null) return new DocumentDetails("Unknown", "Unknown", 0, new List<EmailLineItemDto>());

//            var lineItems = po.Lines.Select(l => new EmailLineItemDto(
//                l.ItemCode,
//                l.ItemDescription,
//                l.Qty,
//                l.UnitPrice,
//                l.DiscountPercent,
//                l.NetPrice,
//                l.Amount
//            )).ToList();

//            var supplierName = po.Supplier?.SupplierName ?? "Unknown Supplier";
//            return new DocumentDetails(
//                po.PONumber,
//                $"{po.PONumber} — {supplierName}",
//                po.TotalAmount,
//                lineItems
//            );
//        }

//        // Extend for other document types here (Invoices, LeaveRequests, etc.)
//        return new DocumentDetails($"{documentType} #{documentId}", $"{documentType} #{documentId}", 0, new List<EmailLineItemDto>());
//    }

//    private async Task<string> GetUserNameAsync(string userName)
//    {
//        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
//        return user?.FullName ?? userName;
//    }

//    private async Task<string> GetPreviousCommentsAsync(int instanceId, int currentStepNo)
//    {
//        var previousTasks = await _db.WorkflowTasks
//            .Where(t => t.WorkflowInstanceId == instanceId && t.StepNo < currentStepNo && !string.IsNullOrEmpty(t.Comments))
//            .OrderByDescending(t => t.StepNo)
//            .FirstOrDefaultAsync();

//        return previousTasks?.Comments ?? string.Empty;
//    }

//    private string BuildApprovalEmailHtml(
//    string documentLabel,
//    string requesterName,
//    int stepNo,
//    List<EmailLineItemDto> lineItems,
//    decimal totalAmount,
//    string previousComments,
//    bool canEmailApprove,
//    string? approveUrl,
//    string? rejectUrl,
//    string documentType,
//    int documentId)
//    {
//        var sb = new StringBuilder();

//        // Header
//        sb.AppendLine("<div style='font-family: sans-serif; max-width: 600px;'>");
//        sb.AppendLine($"<h2 style='margin-bottom:4px;'>Approval needed: {documentLabel}</h2>");
//        sb.AppendLine($"<p style='color:#666;margin-top:0;'><strong>{requesterName}</strong> submitted this document for approval.</p>");

//        // Summary
//        if (totalAmount > 0)
//        {
//            sb.AppendLine($"<p><strong>Total Amount:</strong> {totalAmount:C}</p>");
//        }

//        // Requester
//        sb.AppendLine($"<p><strong>Requester:</strong> {requesterName}</p>");

//        // --- FIX: Get the document URL using the passed parameters, not "task" ---
//        var docUrl = GetDocumentUrl(documentType, documentId);
//        if (!string.IsNullOrEmpty(docUrl))
//        {
//            sb.AppendLine($"<p style='margin-top:12px;'><a href='{docUrl}' style='color:#3454d1;'>View full document details →</a></p>");
//        }

//        sb.AppendLine($"<p><strong>Step:</strong> {stepNo}</p>");

//        // Line items
//        if (lineItems.Any())
//        {
//            sb.AppendLine("<h3 style='margin-top:24px;margin-bottom:8px;'>Line Items</h3>");
//            sb.AppendLine("<table style='border-collapse:collapse;width:100%;font-size:13px;'>");
//            sb.AppendLine("<thead><tr style='background:#f3f4f6;'>");
//            sb.AppendLine("<th style='padding:8px;text-align:left;border:1px solid #e5e7eb;'>Item</th>");
//            sb.AppendLine("<th style='padding:8px;text-align:left;border:1px solid #e5e7eb;'>Description</th>");
//            sb.AppendLine("<th style='padding:8px;text-align:right;border:1px solid #e5e7eb;'>Qty</th>");
//            sb.AppendLine("<th style='padding:8px;text-align:right;border:1px solid #e5e7eb;'>Unit Price</th>");
//            sb.AppendLine("<th style='padding:8px;text-align:right;border:1px solid #e5e7eb;'>Disc %</th>");
//            sb.AppendLine("<th style='padding:8px;text-align:right;border:1px solid #e5e7eb;'>Net Price</th>");
//            sb.AppendLine("<th style='padding:8px;text-align:right;border:1px solid #e5e7eb;'>Amount</th>");
//            sb.AppendLine("</tr></thead><tbody>");

//            foreach (var line in lineItems)
//            {
//                sb.AppendLine("<tr>");
//                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;'>{line.ItemCode}</td>");
//                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;'>{line.ItemDescription}</td>");
//                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;text-align:right;'>{line.Qty}</td>");
//                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;text-align:right;'>{line.UnitPrice:C}</td>");
//                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;text-align:right;'>{line.DiscountPercent}%</td>");
//                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;text-align:right;'>{line.NetPrice:C}</td>");
//                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;text-align:right;font-weight:bold;'>{line.Amount:C}</td>");
//                sb.AppendLine("</tr>");
//            }

//            sb.AppendLine("</tbody></table>");
//        }

//        // Previous comments
//        if (!string.IsNullOrEmpty(previousComments))
//        {
//            sb.AppendLine("<h3 style='margin-top:24px;margin-bottom:8px;'>Previous Comments</h3>");
//            sb.AppendLine($"<p style='background:#f9fafb;padding:12px;border-left:4px solid #c98a3a;border-radius:4px;'>{previousComments}</p>");
//        }

//        // Actions
//        if (canEmailApprove && approveUrl is not null && rejectUrl is not null)
//        {
//            sb.AppendLine("<div style='margin-top:24px;padding-top:16px;border-top:1px solid #e5e7eb;'>");
//            sb.AppendLine($"<a href='{approveUrl}' style='display:inline-block;padding:10px 24px;background:#12151c;color:white;text-decoration:none;border-radius:6px;font-weight:500;margin-right:8px;'>Approve</a>");
//            sb.AppendLine($"<a href='{rejectUrl}' style='display:inline-block;padding:10px 24px;background:#dc2626;color:white;text-decoration:none;border-radius:6px;font-weight:500;'>Reject</a>");
//            sb.AppendLine("<p style='color:#666;font-size:12px;margin-top:8px;'>This link expires in 48 hours and can only be used once.</p>");
//            sb.AppendLine("</div>");
//        }
//        else
//        {
//            sb.AppendLine("<div style='margin-top:24px;padding-top:16px;border-top:1px solid #e5e7eb;'>");
//            sb.AppendLine("<p>Please log in to the ERP system to review this task — multiple people share this approval role, so this action must be completed in-app.</p>");
//            sb.AppendLine("</div>");
//        }

//        // Footer
//        sb.AppendLine("<p style='margin-top:24px;font-size:12px;color:#9ca3af;'>If you weren't expecting this email, you can ignore it.</p>");
//        sb.AppendLine("</div>");

//        return sb.ToString();
//    }

//    private string GetDocumentUrl(string documentType, int documentId)
//    {
//        var baseUrl = _config["App:BaseUrl"] ?? "https://localhost:7000";

//        if (documentType == "PurchaseOrder")
//            return $"{baseUrl}/scm/purchase-orders/{documentId}";

//        // Extend for other document types
//        return string.Empty;
//    }
//}


using ERP.Application.DTOs;
using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

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
        var baseUrl = _config["App:BaseUrl"] ?? "https://localhost:7000";

        // --- RICH CONTENT STARTS HERE ---

        var docDetails = await GetDocumentDetailsAsync(task.Instance.DocumentType, task.Instance.DocumentId);
        var requesterName = await GetUserNameAsync(task.Instance.StartedBy);
        var previousComments = await GetPreviousCommentsAsync(task.Instance.WorkflowInstanceId, task.StepNo);

        var totalAmountDisplay = docDetails.TotalAmount > 0 ? $" — {docDetails.TotalAmount:C}" : "";
        var docLabel = $"{docDetails.DocumentNumber}{totalAmountDisplay}";

        // Build the HTML once (without approve/reject URLs since they're per-user)
        var htmlBase = BuildApprovalEmailHtml(
            documentLabel: docLabel,
            requesterName: requesterName,
            stepNo: task.StepNo,
            lineItems: docDetails.LineItems,
            totalAmount: docDetails.TotalAmount,
            previousComments: previousComments,
            canEmailApprove: canEmailApprove,
            approveUrl: null,  // Will be replaced per-user
            rejectUrl: null,   // Will be replaced per-user
            documentType: task.Instance.DocumentType,
            documentId: task.Instance.DocumentId
        );

        foreach (var user in recipients)
        {
            string? approveUrl = null;
            string? rejectUrl = null;

            if (canEmailApprove)
            {
                var approveToken = GenerateToken();
                var rejectToken = GenerateToken();

                _db.WorkflowActionTokens.AddRange(
                    new WorkflowActionToken
                    {
                        Token = approveToken,
                        WorkflowTaskId = task.WorkflowTaskId,
                        Action = "Approve",
                        RecipientEmail = user.Email,  // Store recipient email
                        ExpiresAt = expiresAt
                    },
                    new WorkflowActionToken
                    {
                        Token = rejectToken,
                        WorkflowTaskId = task.WorkflowTaskId,
                        Action = "Reject",
                        RecipientEmail = user.Email,  // Store recipient email
                        ExpiresAt = expiresAt
                    }
                );
                await _db.SaveChangesAsync();

                approveUrl = $"{baseUrl}/api/workflow-email-actions/{approveToken}?email={Uri.EscapeDataString(user.Email)}";
                rejectUrl = $"{baseUrl}/api/workflow-email-actions/{rejectToken}?email={Uri.EscapeDataString(user.Email)}";
            }

            // Replace placeholders with user-specific URLs
            var html = htmlBase;
            if (canEmailApprove && approveUrl is not null && rejectUrl is not null)
            {
                // Replace the placeholder action buttons with real URLs
                html = html.Replace("{{APPROVE_URL}}", approveUrl)
                           .Replace("{{REJECT_URL}}", rejectUrl);
            }

            await _email.SendAsync(user.Email, $"Approval needed: {docLabel}", html);
        }
    }
    public async Task SendReminderEmailAsync(int workflowTaskId, bool isOverdue)
    {
        var task = await _db.WorkflowTasks
            .Include(t => t.Instance)
            .FirstOrDefaultAsync(t => t.WorkflowTaskId == workflowTaskId);
        if (task is null) return;

        var recipients = await GetEffectiveRoleHoldersAsync(task.AssignedRole);
        if (recipients.Count == 0) return;

        var docDetails = await GetDocumentDetailsAsync(task.Instance.DocumentType, task.Instance.DocumentId);
        var requesterName = await GetUserNameAsync(task.Instance.StartedBy);
        var docUrl = GetDocumentUrl(task.Instance.DocumentType, task.Instance.DocumentId);
        var docLabel = docDetails.DocumentNumber;

        var heading = isOverdue
            ? $"Reminder: {docLabel} is overdue for your approval"
            : $"Reminder: {docLabel} is due soon";

        var dueLine = isOverdue
            ? $"This task (Step {task.StepNo}) was due on <strong>{task.DueDate:g}</strong> and is still pending."
            : $"This task (Step {task.StepNo}) is due on <strong>{task.DueDate:g}</strong>.";

        var headingColor = isOverdue ? "#b45309" : "#3454d1";

        var html = $@"
<div style='font-family: sans-serif; max-width: 600px;'>
    <h2 style='color:{headingColor};'>{heading}</h2>
    <p><strong>{requesterName}</strong> submitted this document on <strong>{task.Instance.StartedDate:g}</strong>.</p>
    <p>{dueLine}</p>";

        if (!string.IsNullOrEmpty(docUrl))
        {
            html += $"<p><a href='{docUrl}' style='color:#3454d1;'>View full document details →</a></p>";
        }

        html += @"
    <p style='margin-top:16px;'>Please log in to the ERP system to review and act on it.</p>
    <p style='margin-top:24px;font-size:12px;color:#9ca3af;'>This is an automated reminder.</p>
</div>";

        var subject = isOverdue
            ? $"Reminder: {docLabel} needs your approval"
            : $"Reminder: {docLabel} is due soon";

        foreach (var user in recipients)
            await _email.SendAsync(user.Email, subject, html);
    }

    public async Task<(bool Success, string? Error, int? WorkflowTaskId, string? DocumentType, int? DocumentId, string? Action, string? ActingUserName)>
        RedeemTokenAsync(string token, string userEmail)
    {
        var actionToken = await _db.WorkflowActionTokens
            .Include(t => t.Task).ThenInclude(wt => wt.Instance)
            .FirstOrDefaultAsync(t => t.Token == token);

        if (actionToken is null) return (false, "Invalid or unrecognized link.", null, null, null, null, null);
        if (actionToken.IsUsed) return (false, "This link has already been used.", null, null, null, null, null);
        if (actionToken.ExpiresAt < DateTime.UtcNow) return (false, "This link has expired.", null, null, null, null, null);
        if (actionToken.Task.Status != "Pending") return (false, "This task has already been actioned.", null, null, null, null, null);

        // --- NEW: Validate the email matches the recipient ---
        if (!string.Equals(actionToken.RecipientEmail, userEmail, StringComparison.OrdinalIgnoreCase))
            return (false, "This approval link was sent to a different recipient. Please contact the original approver.", null, null, null, null, null);

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

    private record DocumentDetails(string DocumentNumber, string DocumentLabel, decimal TotalAmount, List<EmailLineItemDto> LineItems);

    private async Task<DocumentDetails> GetDocumentDetailsAsync(string documentType, int documentId)
    {
        if (documentType == "PurchaseOrder")
        {
            var po = await _db.POHeaders
                .Include(h => h.Supplier)
                .Include(h => h.Lines)
                .FirstOrDefaultAsync(h => h.POId == documentId);

            if (po is null) return new DocumentDetails("Unknown", "Unknown", 0, new List<EmailLineItemDto>());

            var lineItems = po.Lines.Select(l => new EmailLineItemDto(
                l.ItemCode,
                l.ItemDescription,
                l.Qty,
                l.UnitPrice,
                l.DiscountPercent,
                l.NetPrice,
                l.Amount
            )).ToList();

            var supplierName = po.Supplier?.SupplierName ?? "Unknown Supplier";
            return new DocumentDetails(
                po.PONumber,
                $"{po.PONumber} — {supplierName}",
                po.TotalAmount,
                lineItems
            );
        }

        // Extend for other document types here (Invoices, LeaveRequests, etc.)
        return new DocumentDetails($"{documentType} #{documentId}", $"{documentType} #{documentId}", 0, new List<EmailLineItemDto>());
    }

    private async Task<string> GetUserNameAsync(string userName)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
        return user?.FullName ?? userName;
    }

    private async Task<string> GetPreviousCommentsAsync(int instanceId, int currentStepNo)
    {
        var previousTasks = await _db.WorkflowTasks
            .Where(t => t.WorkflowInstanceId == instanceId && t.StepNo < currentStepNo && !string.IsNullOrEmpty(t.Comments))
            .OrderByDescending(t => t.StepNo)
            .FirstOrDefaultAsync();

        return previousTasks?.Comments ?? string.Empty;
    }

    private string BuildApprovalEmailHtml(
        string documentLabel,
        string requesterName,
        int stepNo,
        List<EmailLineItemDto> lineItems,
        decimal totalAmount,
        string previousComments,
        bool canEmailApprove,
        string? approveUrl,
        string? rejectUrl,
        string documentType,
        int documentId)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("<div style='font-family: sans-serif; max-width: 600px;'>");
        sb.AppendLine($"<h2 style='margin-bottom:4px;'>Approval needed: {documentLabel}</h2>");
        sb.AppendLine($"<p style='color:#666;margin-top:0;'><strong>{requesterName}</strong> submitted this document for approval.</p>");

        // Summary
        if (totalAmount > 0)
        {
            sb.AppendLine($"<p><strong>Total Amount:</strong> {totalAmount:C}</p>");
        }

        // Requester
        sb.AppendLine($"<p><strong>Requester:</strong> {requesterName}</p>");

        // Document URL
        var docUrl = GetDocumentUrl(documentType, documentId);
        if (!string.IsNullOrEmpty(docUrl))
        {
            sb.AppendLine($"<p style='margin-top:12px;'><a href='{docUrl}' style='color:#3454d1;'>View full document details →</a></p>");
        }

        sb.AppendLine($"<p><strong>Step:</strong> {stepNo}</p>");

        // Line items
        if (lineItems.Any())
        {
            sb.AppendLine("<h3 style='margin-top:24px;margin-bottom:8px;'>Line Items</h3>");
            sb.AppendLine("<table style='border-collapse:collapse;width:100%;font-size:13px;'>");
            sb.AppendLine("<thead><tr style='background:#f3f4f6;'>");
            sb.AppendLine("<th style='padding:8px;text-align:left;border:1px solid #e5e7eb;'>Item</th>");
            sb.AppendLine("<th style='padding:8px;text-align:left;border:1px solid #e5e7eb;'>Description</th>");
            sb.AppendLine("<th style='padding:8px;text-align:right;border:1px solid #e5e7eb;'>Qty</th>");
            sb.AppendLine("<th style='padding:8px;text-align:right;border:1px solid #e5e7eb;'>Unit Price</th>");
            sb.AppendLine("<th style='padding:8px;text-align:right;border:1px solid #e5e7eb;'>Disc %</th>");
            sb.AppendLine("<th style='padding:8px;text-align:right;border:1px solid #e5e7eb;'>Net Price</th>");
            sb.AppendLine("<th style='padding:8px;text-align:right;border:1px solid #e5e7eb;'>Amount</th>");
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var line in lineItems)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;'>{line.ItemCode}</td>");
                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;'>{line.ItemDescription}</td>");
                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;text-align:right;'>{line.Qty}</td>");
                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;text-align:right;'>{line.UnitPrice:C}</td>");
                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;text-align:right;'>{line.DiscountPercent}%</td>");
                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;text-align:right;'>{line.NetPrice:C}</td>");
                sb.AppendLine($"<td style='padding:8px;border:1px solid #e5e7eb;text-align:right;font-weight:bold;'>{line.Amount:C}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");
        }

        // Previous comments
        if (!string.IsNullOrEmpty(previousComments))
        {
            sb.AppendLine("<h3 style='margin-top:24px;margin-bottom:8px;'>Previous Comments</h3>");
            sb.AppendLine($"<p style='background:#f9fafb;padding:12px;border-left:4px solid #c98a3a;border-radius:4px;'>{previousComments}</p>");
        }

        // Actions - use placeholders for per-user URLs
        if (canEmailApprove)
        {
            sb.AppendLine("<div style='margin-top:24px;padding-top:16px;border-top:1px solid #e5e7eb;'>");
            sb.AppendLine($"<a href='{{{{APPROVE_URL}}}}' style='display:inline-block;padding:10px 24px;background:#12151c;color:white;text-decoration:none;border-radius:6px;font-weight:500;margin-right:8px;'>Approve</a>");
            sb.AppendLine($"<a href='{{{{REJECT_URL}}}}' style='display:inline-block;padding:10px 24px;background:#dc2626;color:white;text-decoration:none;border-radius:6px;font-weight:500;'>Reject</a>");
            sb.AppendLine("<p style='color:#666;font-size:12px;margin-top:8px;'>This link expires in 48 hours and can only be used once.</p>");
            sb.AppendLine("</div>");
        }
        else
        {
            sb.AppendLine("<div style='margin-top:24px;padding-top:16px;border-top:1px solid #e5e7eb;'>");
            sb.AppendLine("<p>Please log in to the ERP system to review this task — multiple people share this approval role, so this action must be completed in-app.</p>");
            sb.AppendLine("</div>");
        }

        // Footer
        sb.AppendLine("<p style='margin-top:24px;font-size:12px;color:#9ca3af;'>If you weren't expecting this email, you can ignore it.</p>");
        sb.AppendLine("</div>");

        return sb.ToString();
    }

    private string GetDocumentUrl(string documentType, int documentId)
    {
        var frontendUrl = _config["App:FrontendUrl"] ?? "http://localhost:4200";

        if (documentType == "PurchaseOrder")
            return $"{frontendUrl}/scm/purchase-orders/{documentId}";

        // Extend for other document types (e.g., Invoices, LeaveRequests)
        return string.Empty;
    }
}