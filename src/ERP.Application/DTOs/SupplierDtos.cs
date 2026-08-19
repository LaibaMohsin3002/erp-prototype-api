// SupplierDtos.cs
namespace ERP.Application.DTOs;

public record SupplierDto(int SupplierId, string SupplierCode, string SupplierName, bool IsActive);
public record CreateSupplierDto(string SupplierCode, string SupplierName);