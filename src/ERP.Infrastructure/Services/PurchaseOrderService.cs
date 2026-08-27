using ERP.Application.DTOs;
using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class PurchaseOrderService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    public PurchaseOrderService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }
    public async Task<List<POListDto>> GetAllAsync() =>
        await _db.POHeaders
            .Include(h => h.Supplier)
            .OrderByDescending(h => h.POId)
            .Select(h => new POListDto(h.POId, h.PONumber, h.PODate, h.Supplier.SupplierName, h.Status, h.TotalAmount))
            .ToListAsync();

    public async Task<PODetailDto?> GetByIdAsync(int id)
    {
        var header = await _db.POHeaders
            .Include(h => h.Supplier)
            .Include(h => h.Lines)
            .FirstOrDefaultAsync(h => h.POId == id);

        if (header is null) return null;

        var lines = header.Lines
           .OrderBy(l => l.LineNo)
           .Select(l => new POLineDto(l.POLineId, l.LineNo, l.ItemId, l.ItemCode, l.ItemDescription, l.Uom, l.Qty, l.UnitPrice, l.DiscountPercent, l.NetPrice, l.Amount))
           .ToList();

       return new PODetailDto(header.POId, header.PONumber, header.PODate, header.SupplierId, header.Supplier.SupplierName,
            header.Remarks, header.PaymentTerms, header.DeliveryTerms, header.DeliveryLocation, header.Status,
            header.CreatedBy, header.CreatedDate, header.TotalQty, header.TotalAmount, lines);

    }

    public async Task<(bool Success, string? Error, int? POId)> CreateAsync(CreatePODto dto, string createdBy)
    {
        if (dto.Lines is null || dto.Lines.Count == 0)
            return (false, "A purchase order must have at least one line item.", null);

        var supplier = await _db.Suppliers.FindAsync(dto.SupplierId);
        if (supplier is null || !supplier.IsActive)
            return (false, "Supplier not found or inactive.", null);

        var itemIds = dto.Lines.Select(l => l.ItemId).ToList();
        var items = await _db.Items.Where(i => itemIds.Contains(i.ItemId)).ToListAsync();
        if (items.Count != itemIds.Distinct().Count())
            return (false, "One or more items not found.", null);

        if (string.IsNullOrWhiteSpace(dto.PaymentTerms) || string.IsNullOrWhiteSpace(dto.DeliveryTerms) || string.IsNullOrWhiteSpace(dto.DeliveryLocation))
            return (false, "Payment terms, delivery terms, and delivery location are required.", null);

        if (dto.PODate < DateTime.Today.AddDays(-30) || dto.PODate > DateTime.Today.AddDays(30))
            return (false, "PO Date must be within 30 days of today.", null);

        foreach (var line in dto.Lines)
        {
            if (line.DiscountPercent < 0 || line.DiscountPercent > 100)
                return (false, "Discount percent must be between 0 and 100.", null);
        }

        foreach (var line in dto.Lines)
        {
            if (line.Qty <= 0) return (false, "Line quantity must be greater than zero.", null);
            if (line.UnitPrice <= 0) return (false, "Line unit price must be greater than zero.", null);
        }

        var duplicateItemIds = dto.Lines
            .GroupBy(l => l.ItemId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateItemIds.Any())
            return (false, "Each item can only appear once per purchase order. Combine quantities instead of adding duplicate lines.", null);

        var header = new POHeader
        {
            PONumber = await GenerateNextPONumberAsync(),
            PODate = dto.PODate,
            SupplierId = dto.SupplierId,
            Remarks = dto.Remarks,
            PaymentTerms = dto.PaymentTerms,
            DeliveryTerms = dto.DeliveryTerms,
            DeliveryLocation = dto.DeliveryLocation,
            Status = "Draft",
            CreatedBy = createdBy,
            CreatedDate = DateTime.UtcNow
        };

        decimal totalQty = 0;
        decimal totalAmount = 0;
        int lineNo = 1;

        foreach (var lineInput in dto.Lines)
        {
            var item = items.First(i => i.ItemId == lineInput.ItemId);
            var netPrice = Math.Round(lineInput.UnitPrice * (1 - lineInput.DiscountPercent / 100m), 2);
            var amount = Math.Round(lineInput.Qty * netPrice, 2); // backend calculates, never trusts client math

            header.Lines.Add(new POLine
            {
                LineNo = lineNo++,
                ItemId = item.ItemId,
                ItemCode = item.ItemCode,
                ItemDescription = item.ItemDescription,
                Uom = item.Uom,
                Qty = lineInput.Qty,
                UnitPrice = lineInput.UnitPrice,
                DiscountPercent = lineInput.DiscountPercent,
                NetPrice = netPrice,
                Amount = amount
            });

            totalQty += lineInput.Qty;
            totalAmount += amount;
        }

        header.TotalQty = totalQty;
        header.TotalAmount = totalAmount;

        _db.POHeaders.Add(header);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(createdBy, "Create", "PurchaseOrder", header.POId, $"PO {header.PONumber} created, total {header.TotalAmount:C}");
        return (true, null, header.POId);
    }

    private async Task<string> GenerateNextPONumberAsync()
    {
        var count = await _db.POHeaders.CountAsync();
        return $"PO-{(count + 1):D6}"; // PO-000001, PO-000002, ...
    }
    public async Task<(bool Success, string? Error)> SubmitAsync(int poId, string actingUser, WorkflowEngineService engine)
    {
        var po = await _db.POHeaders.FindAsync(poId);
        if (po is null) return (false, "Purchase order not found.");
        if (po.Status != "Draft" && po.Status != "Returned")
            return (false, "Only draft or returned purchase orders can be submitted.");

        var alreadyActive = await _db.WorkflowInstances
            .AnyAsync(i => i.DocumentType == "PurchaseOrder" && i.DocumentId == poId && i.Status == "InProgress");
        if (alreadyActive)
            return (false, "This purchase order already has an active approval workflow in progress.");

        var (success, error, _) = await engine.StartWorkflowAsync("PurchaseOrder", poId, po.TotalAmount, actingUser);
        if (!success) return (false, error);

        po.Status = "Submitted";
        await _db.SaveChangesAsync();
        return (true, null);
    }
    public async Task UpdateStatusAsync(int poId, string status)
    {
        var po = await _db.POHeaders.FindAsync(poId);
        if (po is null) return;
        po.Status = status;
        await _db.SaveChangesAsync();
    }
    public async Task<List<ApprovalHistoryDto>> GetApprovalHistoryAsync(int poId)
    {
        var instance = await _db.WorkflowInstances
            .Include(i => i.Tasks)
            .Where(i => i.DocumentType == "PurchaseOrder" && i.DocumentId == poId)
            .OrderByDescending(i => i.StartedDate)
            .FirstOrDefaultAsync();

        if (instance is null) return new List<ApprovalHistoryDto>();

        return await _db.WorkflowAuditEntries
               .Where(a => a.DocumentType == "PurchaseOrder" && a.DocumentId == poId)
               .OrderBy(a => a.WorkflowAuditEntryId)
               .Select(a => new ApprovalHistoryDto(
                   a.StepNo, a.AssignedApproverRole, a.Action, a.NewStatus,
                   a.ActingUser, a.DelegatedFromUser, a.ActionDate, a.Comments))
               .ToListAsync();
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int poId, CreatePODto dto, string actingUser)
    {
        var po = await _db.POHeaders.Include(h => h.Lines).FirstOrDefaultAsync(h => h.POId == poId);
        if (po is null) return (false, "Purchase order not found.");
        if (po.Status != "Draft" && po.Status != "Returned")
            return (false, "Only draft or returned purchase orders can be edited.");

        if (dto.Lines is null || dto.Lines.Count == 0)
            return (false, "A purchase order must have at least one line item.");

        var supplier = await _db.Suppliers.FindAsync(dto.SupplierId);
        if (supplier is null || !supplier.IsActive)
            return (false, "Supplier not found or inactive.");

        var itemIds = dto.Lines.Select(l => l.ItemId).ToList();
        var items = await _db.Items.Where(i => itemIds.Contains(i.ItemId)).ToListAsync();
        if (items.Count != itemIds.Distinct().Count())
            return (false, "One or more items not found.");

        if (dto.PODate < DateTime.Today.AddDays(-30) || dto.PODate > DateTime.Today.AddDays(30))
            return (false, "PO Date must be within 30 days of today.");

        foreach (var line in dto.Lines)
        {
            if (line.Qty <= 0) return (false, "Line quantity must be greater than zero.");
            if (line.UnitPrice <= 0) return (false, "Line unit price must be greater than zero.");
        }

        var duplicateItemIds = dto.Lines.GroupBy(l => l.ItemId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateItemIds.Any())
            return (false, "Each item can only appear once per purchase order.");

        po.SupplierId = dto.SupplierId;
        po.PODate = dto.PODate;
        po.Remarks = dto.Remarks;
        po.PaymentTerms = dto.PaymentTerms;
        po.DeliveryTerms = dto.DeliveryTerms;
        po.DeliveryLocation = dto.DeliveryLocation;

        _db.POLines.RemoveRange(po.Lines);
        po.Lines.Clear();

        decimal totalQty = 0;
        decimal totalAmount = 0;
        int lineNo = 1;

        foreach (var lineInput in dto.Lines)
        {
            var item = items.First(i => i.ItemId == lineInput.ItemId);
            var netPrice = Math.Round(lineInput.UnitPrice * (1 - lineInput.DiscountPercent / 100m), 2);
            var amount = Math.Round(lineInput.Qty * netPrice, 2);

            po.Lines.Add(new POLine
            {
                LineNo = lineNo++,
                ItemId = item.ItemId,
                ItemCode = item.ItemCode,
                ItemDescription = item.ItemDescription,
                Uom = item.Uom,
                Qty = lineInput.Qty,
                UnitPrice = lineInput.UnitPrice,
                DiscountPercent = lineInput.DiscountPercent,
                NetPrice = netPrice,
                Amount = amount
            });

            totalQty += lineInput.Qty;
            totalAmount += amount;
        }

        po.TotalQty = totalQty;
        po.TotalAmount = totalAmount;
        po.Status = "Draft"; // a returned PO goes back to Draft once edited, requiring an explicit re-submit

        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Update", "PurchaseOrder", po.POId, $"PO {po.PONumber} edited after being returned");
        return (true, null);
    }
}