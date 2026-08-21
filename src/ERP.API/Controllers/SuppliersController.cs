using ERP.Application.DTOs;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly SupplierService _service;
    public SuppliersController(SupplierService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpPost]
    [Authorize(Roles = "SCM Manager")]
    public async Task<IActionResult> Create(CreateSupplierDto dto)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error, id) = await _service.CreateAsync(dto, actingUser);
        return success ? Ok(new { supplierId = id }) : BadRequest(error);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "SCM Manager")]
    public async Task<IActionResult> Update(int id, CreateSupplierDto dto)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error) = await _service.UpdateAsync(id, dto, actingUser);
        return success ? NoContent() : NotFound(error);
    }

    [HttpPatch("{id}/activate")]
    [Authorize(Roles = "SCM Manager")]
    public async Task<IActionResult> Activate(int id) =>
        await _service.SetActiveAsync(id, true, User.Identity?.Name ?? "unknown") ? NoContent() : NotFound();

    [HttpPatch("{id}/deactivate")]
    [Authorize(Roles = "SCM Manager")]
    public async Task<IActionResult> Deactivate(int id) =>
        await _service.SetActiveAsync(id, false, User.Identity?.Name ?? "unknown") ? NoContent() : NotFound();
}