using ERP.Application.DTOs;
using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;

namespace ERP.Infrastructure.Services;
public class UserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db) => _db = db;

    public async Task<List<UserListDto>> GetAllAsync() =>
        await _db.Users
            .Select(u => new UserListDto(u.UserId, u.UserName, u.FullName, u.Email, u.IsActive))
            .ToListAsync();

    public async Task<UserDetailDto?> GetByIdAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return null;

        var roleIds = await _db.UserRoles
            .Where(ur => ur.UserId == id && ur.IsActive)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        return new UserDetailDto(user.UserId, user.UserName, user.FullName, user.Email, user.IsActive, roleIds);
    }

    public async Task<(bool Success, string? Error, int? UserId)> CreateAsync(CreateUserDto dto)
    {
        // UserService.CreateAsync — add at the top
        if (string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return (false, "Username, full name, email, and password are required.", null);

        if (await _db.Users.AnyAsync(u => u.UserName == dto.UserName))
            return (false, "Username already exists.", null);

        var user = new User
        {
            UserName = dto.UserName,
            FullName = dto.FullName,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        foreach (var roleId in dto.RoleIds.Distinct())
            _db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = roleId, IsActive = true });

        await _db.SaveChangesAsync();
        return (true, null, user.UserId);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return (false, "User not found.");

        user.FullName = dto.FullName;
        user.Email = dto.Email;

        var existingRoles = await _db.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
        _db.UserRoles.RemoveRange(existingRoles);
        foreach (var roleId in dto.RoleIds.Distinct())
            _db.UserRoles.Add(new UserRole { UserId = id, RoleId = roleId, IsActive = true });

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> SetActiveAsync(int id, bool isActive)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return false;
        user.IsActive = isActive;
        await _db.SaveChangesAsync();
        return true;
    }
}