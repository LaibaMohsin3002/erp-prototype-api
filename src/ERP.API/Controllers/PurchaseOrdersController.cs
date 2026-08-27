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
    [Authorize(Roles = "SCM User,SCM Manager")]
    public async Task<IActionResult> Create(CreatePODto dto)
    {
        var userName = User.Identity?.Name ?? "unknown";
        var (success, error, poId) = await _service.CreateAsync(dto, userName);
        return success ? CreatedAtAction(nameof(GetById), new { id = poId }, null) : BadRequest(error);
    }

    [HttpPost("{id}/submit")]
    [Authorize(Roles = "SCM User,SCM Manager")]
    public async Task<IActionResult> Submit(int id, [FromServices] WorkflowEngineService engine)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error) = await _service.SubmitAsync(id, actingUser, engine);
        return success ? NoContent() : BadRequest(error);
    }

    [HttpGet("{id}/approval-history")]
    public async Task<IActionResult> GetApprovalHistory(int id) => Ok(await _service.GetApprovalHistoryAsync(id));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CreatePODto dto)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error) = await _service.UpdateAsync(id, dto, actingUser);
        return success ? NoContent() : BadRequest(error);
    }

}