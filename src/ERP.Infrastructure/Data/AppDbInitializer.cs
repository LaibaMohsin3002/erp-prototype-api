using ERP.Domain.Entities;

namespace ERP.Infrastructure.Data;

public static class AppDbInitializer
{
    public static void Seed(AppDbContext context)
    {
        if (context.Roles.Any()) return; // already seeded

        var secAdminRole = new Role { RoleName = "Security Administrator", Description = "Manages users, roles, pages", IsActive = true };
        var scmUserRole = new Role { RoleName = "SCM User", Description = "Creates purchase orders", IsActive = true };
        context.Roles.AddRange(secAdminRole, scmUserRole);

        var pages = new[]
        {
            new AppPage { PageCode = "SEC_USER", PageName = "User Registration", ModuleName = "Security", PageRoute = "/security-admin/users", DisplaySequence = 1, IsActive = true },
            new AppPage { PageCode = "SEC_PAGE", PageName = "Pages in App", ModuleName = "Security", PageRoute = "/security-admin/pages", DisplaySequence = 2, IsActive = true },
            new AppPage { PageCode = "SEC_ROLE", PageName = "Roles Registration", ModuleName = "Security", PageRoute = "/security-admin/roles", DisplaySequence = 3, IsActive = true },
            new AppPage { PageCode = "SCM_PO", PageName = "Purchase Order", ModuleName = "SCM", PageRoute = "/scm/purchase-orders", DisplaySequence = 4, IsActive = true },
        };
        context.AppPages.AddRange(pages);
        context.SaveChanges(); // need IDs before linking

        context.RolePages.AddRange(
            new RolePage { RoleId = secAdminRole.RoleId, PageId = pages[0].PageId, IsActive = true },
            new RolePage { RoleId = secAdminRole.RoleId, PageId = pages[1].PageId, IsActive = true },
            new RolePage { RoleId = secAdminRole.RoleId, PageId = pages[2].PageId, IsActive = true },
            new RolePage { RoleId = scmUserRole.RoleId, PageId = pages[3].PageId, IsActive = true }
        );

        var adminUser = new User
        {
            UserName = "admin",
            FullName = "System Administrator",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Email = "admin@erp.local",
            IsActive = true
        };
        context.Users.Add(adminUser);
        context.SaveChanges();

        context.UserRoles.Add(new UserRole { UserId = adminUser.UserId, RoleId = secAdminRole.RoleId, IsActive = true });

        var supplier = new Supplier { SupplierCode = "SUP001", SupplierName = "Acme Supplies", IsActive = true };
        var item = new Item { ItemCode = "ITM001", ItemDescription = "A4 Paper Ream", Uom = "Box", DefaultPurchasePrice = 25.00m, IsActive = true };
        context.Suppliers.Add(supplier);
        context.Items.Add(item);

        context.SaveChanges();
    }
}