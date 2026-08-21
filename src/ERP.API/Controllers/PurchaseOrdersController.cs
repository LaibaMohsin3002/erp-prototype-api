using ERP.Application.DTOs;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/purchaseorders")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
    {
    private readonly PurchaseOrderService _service;
    public PurchaseOrdersController(PurchaseOrderService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var po = await _service.GetByIdAsync(id);
        return po is null ? NotFound() : Ok(po);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePODto dto)
    {
        var userName = User.Identity?.Name ?? "unknown";
        var (success, error, poId) = await _service.CreateAsync(dto, userName);
        return success ? CreatedAtAction(nameof(GetById), new { id = poId }, null) : BadRequest(error);
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> Submit(int id, [FromServices] WorkflowEngineService engine)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error) = await _service.SubmitAsync(id, actingUser, engine);
        return success ? NoContent() : BadRequest(error);
    }

    [HttpGet("{id}/approval-history")]
    public async Task<IActionResult> GetApprovalHistory(int id) => Ok(await _service.GetApprovalHistoryAsync(id));

}