// ItemService.cs
using ERP.Application.DTOs;
using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class ItemService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    public ItemService(AppDbContext db, AuditService audit) { _db = db; _audit = audit; }
    public async Task<List<ItemDto>> GetAllAsync() =>
        await _db.Items
            .Select(i => new ItemDto(i.ItemId, i.ItemCode, i.ItemDescription, i.Uom, i.DefaultPurchasePrice, i.IsActive))
            .ToListAsync();

    public async Task<(bool Success, string? Error, int? ItemId)> CreateAsync(CreateItemDto dto, string actingUser)
    {
        if (string.IsNullOrWhiteSpace(dto.ItemCode) || string.IsNullOrWhiteSpace(dto.ItemDescription) || string.IsNullOrWhiteSpace(dto.Uom))
            return (false, "Item code, description, and UOM are required.", null);
        if (dto.DefaultPurchasePrice <= 0)
            return (false, "Default purchase price must be greater than zero.", null);
        if (await _db.Items.AnyAsync(i => i.ItemCode == dto.ItemCode))
            return (false, "Item code already exists.", null);

        var item = new Item
        {
            ItemCode = dto.ItemCode,
            ItemDescription = dto.ItemDescription,
            Uom = dto.Uom,
            DefaultPurchasePrice = dto.DefaultPurchasePrice,
            IsActive = true
        };
        _db.Items.Add(item);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Create", "Item", item.ItemId, $"Item {item.ItemCode} created");
        return (true, null, item.ItemId);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, CreateItemDto dto, string actingUser)
    {
        if (string.IsNullOrWhiteSpace(dto.ItemCode) || string.IsNullOrWhiteSpace(dto.ItemDescription) || string.IsNullOrWhiteSpace(dto.Uom))
            return (false, "Item code, description, and UOM are required.");
        if (dto.DefaultPurchasePrice <= 0)
            return (false, "Default purchase price must be greater than zero.");

        var item = await _db.Items.FindAsync(id);
        if (item is null) return (false, "Item not found.");

        if (await _db.Items.AnyAsync(i => i.ItemCode == dto.ItemCode && i.ItemId != id))
            return (false, "Item code already exists.");

        item.ItemCode = dto.ItemCode;
        item.ItemDescription = dto.ItemDescription;
        item.Uom = dto.Uom;
        item.DefaultPurchasePrice = dto.DefaultPurchasePrice;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Update", "Item", id, $"Item {item.ItemCode} updated");
        return (true, null);
    }
    public async Task<bool> SetActiveAsync(int id, bool isActive, string actingUser)
    {
        var item = await _db.Items.FindAsync(id);
        if (item is null) return false;
        item.IsActive = isActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, isActive ? "Activate" : "Deactivate", "Item", id, $"Item {item.ItemCode} {(isActive ? "activated" : "deactivated")}");
        return true;
    }

}