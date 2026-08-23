using ERP.Application.DTOs;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/delegations")]
[Authorize]
public class DelegationsController : ControllerBase
{
    private readonly DelegationService _service;
    public DelegationsController(DelegationService service) => _service = service;

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userName = User.Identity?.Name ?? "unknown";
        return Ok(await _service.GetMyDelegationsAsync(userName));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDelegationDto dto)
    {
        var userName = User.Identity?.Name ?? "unknown";
        var (success, error, id) = await _service.CreateAsync(dto, userName);
        return success ? Ok(new { delegationId = id }) : BadRequest(error);
    }

    [HttpPatch("{id}/revoke")]
    public async Task<IActionResult> Revoke(int id)
    {
        var userName = User.Identity?.Name ?? "unknown";
        return await _service.RevokeAsync(id, userName) ? NoContent() : NotFound();
    }
}