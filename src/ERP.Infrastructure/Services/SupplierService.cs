// SupplierService.cs
using ERP.Application.DTOs;
using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class SupplierService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    public SupplierService(AppDbContext db, AuditService audit) { _db = db; _audit = audit; }
    public async Task<List<SupplierDto>> GetAllAsync() =>
        await _db.Suppliers
            .Select(s => new SupplierDto(s.SupplierId, s.SupplierCode, s.SupplierName, s.IsActive))
            .ToListAsync();

    public async Task<(bool Success, string? Error, int? SupplierId)> CreateAsync(CreateSupplierDto dto, string actingUser)
    {
        if (string.IsNullOrWhiteSpace(dto.SupplierCode) || string.IsNullOrWhiteSpace(dto.SupplierName))
            return (false, "Supplier code and name are required.", null);
        if (await _db.Suppliers.AnyAsync(s => s.SupplierCode == dto.SupplierCode))
            return (false, "Supplier code already exists.", null);

        var supplier = new Supplier { SupplierCode = dto.SupplierCode, SupplierName = dto.SupplierName, IsActive = true };

        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Create", "Supplier", supplier.SupplierId, $"Supplier {supplier.SupplierCode} created");
        return (true, null, supplier.SupplierId);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, CreateSupplierDto dto, string actingUser)
    {
        if (string.IsNullOrWhiteSpace(dto.SupplierCode) || string.IsNullOrWhiteSpace(dto.SupplierName))
            return (false, "Supplier code and name are required.");

        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return (false, "Supplier not found.");

        if (await _db.Suppliers.AnyAsync(s => s.SupplierCode == dto.SupplierCode && s.SupplierId != id))
            return (false, "Supplier code already exists.");

        supplier.SupplierCode = dto.SupplierCode;
        supplier.SupplierName = dto.SupplierName;

        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Update", "Supplier", id, $"Supplier {supplier.SupplierCode} updated");
        return (true, null);
    }
    public async Task<bool> SetActiveAsync(int id, bool isActive, string actingUser)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return false;
        supplier.IsActive = isActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, isActive ? "Activate" : "Deactivate", "Supplier", id, $"Supplier {supplier.SupplierCode} {(isActive ? "activated" : "deactivated")}");
        return true;
    }
}