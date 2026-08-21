// PageService.cs
using ERP.Application.DTOs;
using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class PageService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    public PageService(AppDbContext db, AuditService audit) { _db = db; _audit = audit; }

    public async Task<List<PageDto>> GetAllAsync() =>
        await _db.AppPages
            .OrderBy(p => p.DisplaySequence)
            .Select(p => new PageDto(p.PageId, p.PageCode, p.PageName, p.ModuleName, p.PageRoute, p.DisplaySequence, p.IsActive))
            .ToListAsync();

    public async Task<(bool Success, string? Error, int? PageId)> CreateAsync(CreatePageDto dto, string actingUser)
    {
        // PageService.CreateAsync — add at the top
        if (string.IsNullOrWhiteSpace(dto.PageCode) || string.IsNullOrWhiteSpace(dto.PageName) || string.IsNullOrWhiteSpace(dto.ModuleName) || string.IsNullOrWhiteSpace(dto.PageRoute))
            return (false, "Page code, name, module, and route are required.", null);

        if (await _db.AppPages.AnyAsync(p => p.PageCode == dto.PageCode))
            return (false, "Page code already exists.", null);

        var page = new AppPage
        {
            PageCode = dto.PageCode,
            PageName = dto.PageName,
            ModuleName = dto.ModuleName,
            PageRoute = dto.PageRoute,
            DisplaySequence = dto.DisplaySequence,
            IsActive = true
        };

        _db.AppPages.Add(page);

        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Create", "Page", page.PageId, $"Page {page.PageCode} created");
        return (true, null, page.PageId);
    }

    // PageService.cs
    public async Task<bool> SetActiveAsync(int id, bool isActive, string actingUser)
    {
        var page = await _db.AppPages.FindAsync(id);
        if (page is null) return false;
        page.IsActive = isActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, isActive ? "Activate" : "Deactivate", "Page", id, $"Page {page.PageCode} {(isActive ? "activated" : "deactivated")}");
        return true;
    }
}