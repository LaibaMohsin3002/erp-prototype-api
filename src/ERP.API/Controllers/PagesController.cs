// PagesController.cs
using ERP.Application.DTOs;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/pages")]
[Authorize(Roles = "Security Administrator")]
public class PagesController : ControllerBase
{
    private readonly PageService _service;
    public PagesController(PageService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create(CreatePageDto dto)
    {
        var (success, error, pageId) = await _service.CreateAsync(dto);
        return success ? Ok(new { pageId }) : BadRequest(error);
    }

    // PagesController.cs
    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> Activate(int id) =>
        await _service.SetActiveAsync(id, true) ? NoContent() : NotFound();

    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(int id) =>
        await _service.SetActiveAsync(id, false) ? NoContent() : NotFound();
}