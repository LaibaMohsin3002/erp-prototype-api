// ItemDtos.cs
namespace ERP.Application.DTOs;

public record ItemDto(int ItemId, string ItemCode, string ItemDescription, string Uom, decimal DefaultPurchasePrice, bool IsActive);
public record CreateItemDto(string ItemCode, string ItemDescription, string Uom, decimal DefaultPurchasePrice);