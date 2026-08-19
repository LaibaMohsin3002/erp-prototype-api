// PurchaseOrderDtos.cs
namespace ERP.Application.DTOs;

public record POLineInputDto(int ItemId, decimal Qty, decimal UnitPrice);

public record CreatePODto(int SupplierId, DateTime PODate, string? Remarks, List<POLineInputDto> Lines);

public record POLineDto(int POLineId, int LineNo, int ItemId, string ItemCode, string ItemDescription, string Uom, decimal Qty, decimal UnitPrice, decimal Amount);

public record POListDto(int POId, string PONumber, DateTime PODate, string SupplierName, string Status, decimal TotalAmount);

public record PODetailDto(int POId, string PONumber, DateTime PODate, int SupplierId, string SupplierName, string? Remarks, string Status, decimal TotalQty, decimal TotalAmount, List<POLineDto> Lines);