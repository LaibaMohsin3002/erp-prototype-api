using ERP.Application.DTOs;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize(Roles = "Security Administrator")]
public class RolesController : ControllerBase
{
    private readonly RoleService _service;
    public RolesController(RoleService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var role = await _service.GetByIdAsync(id);
        return role is null ? NotFound() : Ok(role);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateRoleDto dto)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error, roleId) = await _service.CreateAsync(dto, actingUser);
        return success ? CreatedAtAction(nameof(GetById), new { id = roleId }, null) : BadRequest(error);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateRoleDto dto)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error) = await _service.UpdateAsync(id, dto, actingUser);
        return success ? NoContent() : NotFound(error);
    }

    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> Activate(int id) =>
        await _service.SetActiveAsync(id, true, User.Identity?.Name ?? "unknown") ? NoContent() : NotFound();

    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(int id) =>
        await _service.SetActiveAsync(id, false, User.Identity?.Name ?? "unknown") ? NoContent() : NotFound();
}