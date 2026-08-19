// SuppliersController.cs
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
        var (success, error, id) = await _service.CreateAsync(dto);
        return success ? Ok(new { supplierId = id }) : BadRequest(error);
    }

    [HttpPatch("{id}/activate")]
    [Authorize(Roles = "SCM Manager")]
    public async Task<IActionResult> Activate(int id) =>
        await _service.SetActiveAsync(id, true) ? NoContent() : NotFound();

    [HttpPatch("{id}/deactivate")]
    [Authorize(Roles = "SCM Manager")]
    public async Task<IActionResult> Deactivate(int id) =>
        await _service.SetActiveAsync(id, false) ? NoContent() : NotFound();

    [HttpPut("{id}")]
    [Authorize(Roles = "SCM Manager")]
    public async Task<IActionResult> Update(int id, CreateSupplierDto dto)
    {
        var (success, error) = await _service.UpdateAsync(id, dto);
        return success ? NoContent() : NotFound(error);
    }
}