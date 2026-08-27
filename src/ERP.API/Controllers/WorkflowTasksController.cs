using ERP.Application.DTOs;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/workflow-tasks")]
[Authorize]
public class WorkflowTasksController : ControllerBase
{
    private readonly WorkflowEngineService _engine;
    private readonly PurchaseOrderService _poService;

    public WorkflowTasksController(WorkflowEngineService engine, PurchaseOrderService poService)
    {
        _engine = engine;
        _poService = poService;
    }

    [HttpGet("my-inbox")]
    public async Task<IActionResult> GetMyInbox()
    {
        var userName = User.Identity?.Name ?? "unknown";
        return Ok(await _engine.GetInboxTasksForUserAsync(userName));
    }

    [HttpPost("{id}/action")]
    public async Task<IActionResult> TakeAction(int id, WorkflowActionDto dto)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error, documentType, documentId, newStatus) = await _engine.ProcessActionAsync(id, dto.Action, actingUser, dto.Comments);

        if (!success) return BadRequest(error);

        // Dispatch: when an instance completes, sync the originating document's status.
        // Extend this switch as new document types plug into the engine.
        if (newStatus is not null && documentType == "PurchaseOrder" && documentId is not null)
            await _poService.UpdateStatusAsync(documentId.Value, newStatus);

        return NoContent();
    }
}