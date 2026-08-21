// RoleService.cs
using ERP.Application.DTOs;
using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class RoleService 
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    public RoleService(AppDbContext db, AuditService audit) { _db = db; _audit = audit; }
    public async Task<List<RoleListDto>> GetAllAsync() =>
        await _db.Roles.Select(r => new RoleListDto(r.RoleId, r.RoleName, r.Description, r.IsActive)).ToListAsync();

    public async Task<RoleDetailDto?> GetByIdAsync(int id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role is null) return null;
        var pageIds = await _db.RolePages.Where(rp => rp.RoleId == id && rp.IsActive).Select(rp => rp.PageId).ToListAsync();
        return new RoleDetailDto(role.RoleId, role.RoleName, role.Description, role.IsActive, pageIds);
    }

    public async Task<(bool Success, string? Error, int? RoleId)> CreateAsync(CreateRoleDto dto, string actingUser)
    {
        // RoleService.CreateAsync — add at the top
        if (string.IsNullOrWhiteSpace(dto.RoleName))
            return (false, "Role name is required.", null);

        if (await _db.Roles.AnyAsync(r => r.RoleName == dto.RoleName))
            return (false, "Role name already exists.", null);

        var role = new Role { RoleName = dto.RoleName, Description = dto.Description, IsActive = true };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        foreach (var pageId in dto.PageIds.Distinct())
            _db.RolePages.Add(new RolePage { RoleId = role.RoleId, PageId = pageId, IsActive = true });

        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Create", "Role", role.RoleId, $"Role {role.RoleName} created");
        return (true, null, role.RoleId);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, UpdateRoleDto dto, string actingUser)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role is null) return (false, "Role not found.");

        role.Description = dto.Description;
        role.IsActive = dto.IsActive;

        var existing = await _db.RolePages.Where(rp => rp.RoleId == id).ToListAsync();
        _db.RolePages.RemoveRange(existing);
        foreach (var pageId in dto.PageIds.Distinct())
            _db.RolePages.Add(new RolePage { RoleId = id, PageId = pageId, IsActive = true });

        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Update", "Role", id, $"Role {role.RoleName} updated");
        return (true, null);

    }
    public async Task<bool> SetActiveAsync(int id, bool isActive, string actingUser)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role is null) return false;
        role.IsActive = isActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, isActive ? "Activate" : "Deactivate", "Role", id, $"Role {role.RoleName} {(isActive ? "activated" : "deactivated")}");
        return true;
    }
}