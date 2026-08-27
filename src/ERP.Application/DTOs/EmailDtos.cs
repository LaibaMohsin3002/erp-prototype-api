namespace ERP.Application.DTOs;

public record EmailLineItemDto(
    string ItemCode,
    string ItemDescription,
    decimal Qty,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal NetPrice,
    decimal Amount
);