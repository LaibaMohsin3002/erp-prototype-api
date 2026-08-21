using ERP.Application.DTOs;
using ERP.Application.Services;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Security Administrator")]
public class UsersController : ControllerBase
{
    private readonly UserService _service;

    public UsersController(UserService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _service.GetByIdAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    //[HttpPost]
    //public async Task<IActionResult> Create(CreateUserDto dto)
    //{
    //    var (success, error, userId) = await _service.CreateAsync(dto);
    //    return success ? CreatedAtAction(nameof(GetById), new { id = userId }, null) : BadRequest(error);
    //}

    //[HttpPut("{id}")]
    //public async Task<IActionResult> Update(int id, UpdateUserDto dto)
    //{
    //    var (success, error) = await _service.UpdateAsync(id, dto);
    //    return success ? NoContent() : NotFound(error);
    //}

    //[HttpPatch("{id}/activate")]
    //public async Task<IActionResult> Activate(int id) =>
    //    await _service.SetActiveAsync(id, true) ? NoContent() : NotFound();

    //[HttpPatch("{id}/deactivate")]
    //public async Task<IActionResult> Deactivate(int id) =>
    //    await _service.SetActiveAsync(id, false) ? NoContent() : NotFound();


    [HttpPost]
    public async Task<IActionResult> Create(CreateUserDto dto)
    {
        var actingUser = User.Identity?.Name ?? "unknown";
        var (success, error, userId) = await _service.CreateAsync(dto, actingUser);
        return success ? CreatedAtAction(nameof(GetById), new { id = userId }, null) : BadRequest(error);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateUserDto dto)
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