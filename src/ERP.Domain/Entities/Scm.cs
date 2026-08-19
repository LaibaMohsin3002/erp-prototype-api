namespace ERP.Domain.Entities;
public class Supplier
{
    public int SupplierId { get; set; }
    public string SupplierCode { get; set; } = default!;
    public string SupplierName { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

public class Item
{
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = default!;
    public string ItemDescription { get; set; } = default!;
    public string Uom { get; set; } = default!;
    public decimal DefaultPurchasePrice { get; set; }
    public bool IsActive { get; set; } = true;
}

public class POHeader
{
    public int POId { get; set; }
    public string PONumber { get; set; } = default!;
    public DateTime PODate { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;
    public string? Remarks { get; set; }
    public decimal TotalQty { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Draft"; // Draft/Submitted/InApproval/Approved/Rejected/Returned
    public string CreatedBy { get; set; } = default!;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }

    public ICollection<POLine> Lines { get; set; } = new List<POLine>();
}

public class POLine
{
    public int POLineId { get; set; }
    public int POId { get; set; }
    public POHeader POHeader { get; set; } = default!;
    public int LineNo { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = default!;
    public string ItemDescription { get; set; } = default!; // snapshot at time of PO
    public string Uom { get; set; } = default!;
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; } // Qty * UnitPrice, backend-calculated
}