using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/workflow-email-actions")]
public class WorkflowEmailActionsController : ControllerBase
{
    private readonly EmailApprovalService _emailApproval;
    private readonly WorkflowEngineService _engine;
    private readonly PurchaseOrderService _poService;

    public WorkflowEmailActionsController(EmailApprovalService emailApproval, WorkflowEngineService engine, PurchaseOrderService poService)
    {
        _emailApproval = emailApproval;
        _engine = engine;
        _poService = poService;
    }

    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> Redeem(string token)
    {
        var (success, error, workflowTaskId, documentType, documentId, action, actingUserName) = await _emailApproval.RedeemTokenAsync(token);

        if (!success)
            return Html($"<h2>{error}</h2>");

        var (actionSuccess, actionError, resultDocType, resultDocId, newInstanceStatus) =
            await _engine.ProcessActionAsync(workflowTaskId!.Value, action!, actingUserName!, "Actioned via email", $"Email:{token}");

        if (!actionSuccess)
            return Html($"<h2>{actionError}</h2>");

        if (newInstanceStatus is not null && resultDocType == "PurchaseOrder" && resultDocId is not null)
            await _poService.UpdateStatusAsync(resultDocId.Value, newInstanceStatus);

        var message = action == "Approve" ? "approved" : "rejected";
        return Html($"<h2>Task {message} successfully.</h2><p>You may close this window.</p>");
    }

    private ContentResult Html(string bodyHtml) =>
        Content($"<html><body style='font-family:sans-serif;padding:40px;max-width:480px;'>{bodyHtml}</body></html>", "text/html");
}