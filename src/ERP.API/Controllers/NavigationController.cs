// NavigationController.cs
using ERP.Application.DTOs;
using ERP.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/navigation")]
[Authorize]
public class NavigationController : ControllerBase
{
    private readonly AppDbContext _db;
    public NavigationController(AppDbContext db) => _db = db;

    [HttpGet("me")]
    public async Task<IActionResult> GetMyNavigation()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var roleIds = await _db.UserRoles
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        var roles = await _db.Roles
            .Where(r => roleIds.Contains(r.RoleId) && r.IsActive)
            .ToListAsync();

        var result = new List<NavRoleDto>();

        foreach (var role in roles)
        {
             var pages = await _db.RolePages
            .Where(rp => rp.RoleId == role.RoleId && rp.IsActive)
            .Join(_db.AppPages.Where(p => p.IsActive), rp => rp.PageId, p => p.PageId,
             (rp, p) => p) // just keep the AppPage entity here, don't project to DTO yet
            .OrderBy(p => p.PageCode)
            .Select(p => new NavPageDto(p.PageId, p.PageCode, p.PageName, p.PageRoute, p.ModuleName))
            .ToListAsync();

            result.Add(new NavRoleDto(role.RoleId, role.RoleName, pages));
        }

        return Ok(result);
    }
}