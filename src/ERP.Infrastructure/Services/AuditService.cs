using ERP.Domain.Entities;
using ERP.Infrastructure.Data;

namespace ERP.Infrastructure.Services;

public class AuditService
{
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) => _db = db;

    public async Task LogAsync(string userName, string action, string entityType, int? entityId, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserName = userName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        });
        await _db.SaveChangesAsync();
    }
}