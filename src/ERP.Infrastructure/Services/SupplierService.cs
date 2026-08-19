// SupplierService.cs
using ERP.Application.DTOs;
using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class SupplierService
{
    private readonly AppDbContext _db;
    public SupplierService(AppDbContext db) => _db = db;

    public async Task<List<SupplierDto>> GetAllAsync() =>
        await _db.Suppliers
            .Select(s => new SupplierDto(s.SupplierId, s.SupplierCode, s.SupplierName, s.IsActive))
            .ToListAsync();

    public async Task<(bool Success, string? Error, int? SupplierId)> CreateAsync(CreateSupplierDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SupplierCode) || string.IsNullOrWhiteSpace(dto.SupplierName))
            return (false, "Supplier code and name are required.", null);
        if (await _db.Suppliers.AnyAsync(s => s.SupplierCode == dto.SupplierCode))
            return (false, "Supplier code already exists.", null);

        var supplier = new Supplier { SupplierCode = dto.SupplierCode, SupplierName = dto.SupplierName, IsActive = true };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        return (true, null, supplier.SupplierId);
    }

    public async Task<bool> SetActiveAsync(int id, bool isActive)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return false;
        supplier.IsActive = isActive;
        await _db.SaveChangesAsync();
        return true;
    }
    public async Task<(bool Success, string? Error)> UpdateAsync(int id, CreateSupplierDto dto)
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
        return (true, null);
    }
}