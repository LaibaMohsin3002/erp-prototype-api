namespace ERP.Domain.Entities;

public class AuditLog
{
    public int AuditLogId { get; set; }
    public string UserName { get; set; } = default!;
    public string Action { get; set; } = default!;      // e.g. "Create", "Update", "Deactivate"
    public string EntityType { get; set; } = default!;  // e.g. "User", "Role", "PurchaseOrder"
    public int? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}