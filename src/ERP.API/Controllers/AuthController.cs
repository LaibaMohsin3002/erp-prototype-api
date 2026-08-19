using ERP.Application.Services;
using ERP.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;

    public AuthController(AppDbContext db, TokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public record LoginRequest(string UserName, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserName == request.UserName);

        if (user is null || !user.IsActive)
            return Unauthorized("Invalid username or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid username or password.");

        var roles = await _db.UserRoles
            .Where(ur => ur.UserId == user.UserId && ur.IsActive)
            .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => r.RoleName)
            .ToListAsync();

        var token = _tokenService.CreateToken(user, roles);

        return Ok(new { token, userName = user.UserName, fullName = user.FullName, roles });
    }
}