using ERP.Application.DTOs;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/workflow-definitions")]
[Authorize(Roles = "Security Administrator")]
public class WorkflowDefinitionsController : ControllerBase
{
    private readonly WorkflowDefinitionService _service;
    public WorkflowDefinitionsController(WorkflowDefinitionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var def = await _service.GetByIdAsync(id);
        return def is null ? NotFound() : Ok(def);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWorkflowDefinitionDto dto)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error, id) = await _service.CreateAsync(dto, actingUser);
        return success ? CreatedAtAction(nameof(GetById), new { id }, null) : BadRequest(error);
    }

    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> Activate(int id) =>
        await _service.SetActiveAsync(id, true, User.Identity?.Name ?? "unknown") ? NoContent() : NotFound();

    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(int id) =>
        await _service.SetActiveAsync(id, false, User.Identity?.Name ?? "unknown") ? NoContent() : NotFound();

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CreateWorkflowDefinitionDto dto)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error, newDefinitionId) = await _service.UpdateAsync(id, dto, actingUser);
        return success ? Ok(new { workflowDefinitionId = newDefinitionId }) : BadRequest(error);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error) = await _service.DeleteAsync(id, actingUser);
        return success ? NoContent() : BadRequest(error);
    }
}