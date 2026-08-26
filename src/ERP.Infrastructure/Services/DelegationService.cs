using ERP.Application.DTOs;
using ERP.Domain.Entities;
using ERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class DelegationService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    public DelegationService(AppDbContext db, AuditService audit) { _db = db; _audit = audit; }

    public async Task<(bool Success, string? Error, int? Id)> CreateAsync(CreateDelegationDto dto, string delegatorUserName)
    {
        var delegator = await _db.Users.FirstOrDefaultAsync(u => u.UserName == delegatorUserName);
        var delegateUser = await _db.Users.FirstOrDefaultAsync(u => u.UserName == dto.DelegateUserName);

        if (delegator is null) return (false, "Delegator not found.", null);
        if (delegateUser is null) return (false, "Delegate user not found.", null);
        if (delegator.UserId == delegateUser.UserId) return (false, "You cannot delegate to yourself.", null);
        if (dto.EndDate <= dto.StartDate) return (false, "End date must be after start date.", null);

        // Loop prevention: reject if the delegate has an active/overlapping delegation back to this delegator
        var wouldLoop = await _db.Delegations.AnyAsync(d =>
            d.IsActive &&
            d.DelegatorUserId == delegateUser.UserId &&
            d.DelegateUserId == delegator.UserId &&
            d.StartDate < dto.EndDate && d.EndDate > dto.StartDate);

        if (wouldLoop) return (false, "This would create a delegation loop with an existing delegation.", null);

        var delegation = new Delegation
        {
            DelegatorUserId = delegator.UserId,
            DelegateUserId = delegateUser.UserId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Reason = dto.Reason,
            IsActive = true
        };

        _db.Delegations.Add(delegation);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(delegatorUserName, "Create", "Delegation", delegation.DelegationId, $"Delegated to {dto.DelegateUserName} from {dto.StartDate:d} to {dto.EndDate:d}");

        return (true, null, delegation.DelegationId);
    }

    public async Task<List<DelegationDto>> GetMyDelegationsAsync(string userName)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
        if (user is null) return new List<DelegationDto>();

        return await _db.Delegations
            .Include(d => d.Delegator).Include(d => d.Delegate)
            .Where(d => d.DelegatorUserId == user.UserId)
            .OrderByDescending(d => d.StartDate)
            .Select(d => new DelegationDto(d.DelegationId, d.Delegator.UserName, d.Delegate.UserName, d.StartDate, d.EndDate, d.Reason, d.IsActive))
            .ToListAsync();
    }

    public async Task<bool> RevokeAsync(int id, string actingUser)
    {
        var delegation = await _db.Delegations.FindAsync(id);
        if (delegation is null) return false;
        delegation.IsActive = false;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actingUser, "Revoke", "Delegation", id, null);
        return true;
    }

    // Effective roles = the user's own roles + roles held by anyone who has an active delegation to them right now
    public async Task<List<string>> GetEffectiveRolesAsync(string userName)
    {
        var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserName == userName);
        if (user is null) return new List<string>();

        var ownRoles = user.UserRoles.Where(ur => ur.IsActive).Select(ur => ur.Role.RoleName).ToList();

        var now = DateTime.UtcNow;
        var delegatorIds = await _db.Delegations
            .Where(d => d.IsActive && d.DelegateUserId == user.UserId && d.StartDate <= now && d.EndDate >= now)
            .Select(d => d.DelegatorUserId)
            .ToListAsync();

        var delegatedRoles = await _db.UserRoles
            .Where(ur => delegatorIds.Contains(ur.UserId) && ur.IsActive)
            .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => r.RoleName)
            .ToListAsync();

        return ownRoles.Concat(delegatedRoles).Distinct().ToList();
    }

    public async Task<string?> GetDelegatorIfActingViaDelegationAsync(string actingUserName, string roleName)
    {
        var actingUser = await _db.Users.FirstOrDefaultAsync(u => u.UserName == actingUserName);
        if (actingUser is null) return null;

        var directlyHoldsRole = await _db.UserRoles
            .Where(ur => ur.IsActive && ur.UserId == actingUser.UserId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => r.RoleName)
            .AnyAsync(rn => rn == roleName);

        if (directlyHoldsRole) return null; // acting on their own authority, not via delegation

        var now = DateTime.UtcNow;
        var delegation = await _db.Delegations
            .Include(d => d.Delegator)
            .Where(d => d.IsActive && d.DelegateUserId == actingUser.UserId && d.StartDate <= now && d.EndDate >= now)
            .FirstOrDefaultAsync();

        if (delegation is null) return null;

        var delegatorHoldsRole = await _db.UserRoles
            .Where(ur => ur.IsActive && ur.UserId == delegation.DelegatorUserId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => r.RoleName)
            .AnyAsync(rn => rn == roleName);

        return delegatorHoldsRole ? delegation.Delegator.UserName : null;
    }
}