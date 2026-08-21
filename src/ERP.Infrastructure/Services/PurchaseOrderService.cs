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
            .Select(l => new POLineDto(l.POLineId, l.LineNo, l.ItemId, l.ItemCode, l.ItemDescription, l.Uom, l.Qty, l.UnitPrice, l.Amount))
            .ToList();

        return new PODetailDto(header.POId, header.PONumber, header.PODate, header.SupplierId, header.Supplier.SupplierName,
            header.Remarks, header.Status, header.TotalQty, header.TotalAmount, lines);
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
            var amount = Math.Round(lineInput.Qty * lineInput.UnitPrice, 2); // backend calculates, never trusts client math

            header.Lines.Add(new POLine
            {
                LineNo = lineNo++,
                ItemId = item.ItemId,
                ItemCode = item.ItemCode,
                ItemDescription = item.ItemDescription, // snapshot at creation time, per spec 4.2
                Uom = item.Uom,
                Qty = lineInput.Qty,
                UnitPrice = lineInput.UnitPrice,
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
        if (po.Status != "Draft") return (false, "Only draft purchase orders can be submitted.");

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

        return instance.Tasks
            .OrderBy(t => t.StepNo)
            .Select(t => new ApprovalHistoryDto(t.StepNo, t.AssignedRole, t.Status, t.ActionedBy, t.ActionedDate, t.Comments))
            .ToList();
    }
}