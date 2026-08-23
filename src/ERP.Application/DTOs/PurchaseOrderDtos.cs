// PurchaseOrderDtos.cs
namespace ERP.Application.DTOs;
public record POLineInputDto(int ItemId, decimal Qty, decimal UnitPrice, decimal DiscountPercent);
public record CreatePODto(int SupplierId, DateTime PODate, string? Remarks, string PaymentTerms, string DeliveryTerms, string DeliveryLocation, List<POLineInputDto> Lines);
public record POLineDto(int POLineId, int LineNo, int ItemId, string ItemCode, string ItemDescription, string Uom, decimal Qty, decimal UnitPrice, decimal DiscountPercent, decimal NetPrice, decimal Amount);
public record POListDto(int POId, string PONumber, DateTime PODate, string SupplierName, string Status, decimal TotalAmount);
//public record PODetailDto(int POId, string PONumber, DateTime PODate, int SupplierId, string SupplierName, string? Remarks, string PaymentTerms, string DeliveryTerms, string DeliveryLocation, string Status, decimal TotalQty, decimal TotalAmount, List<POLineDto> Lines);
public record ApprovalHistoryDto(int StepNo, string AssignedRole, string Status, string? ActionedBy, DateTime? ActionedDate, string? Comments);
public record PODetailDto(int POId, string PONumber, DateTime PODate, int SupplierId, string SupplierName, string? Remarks, string PaymentTerms, string DeliveryTerms, string DeliveryLocation, string Status, string CreatedBy, DateTime CreatedDate, decimal TotalQty, decimal TotalAmount, List<POLineDto> Lines);