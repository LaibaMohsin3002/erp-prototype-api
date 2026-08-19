namespace ERP.Domain.Entities;
public class User
{
    public int UserId { get; set; }
    public string UserName { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string Email { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePage> RolePages { get; set; } = new List<RolePage>();
}

public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = default!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

public class AppPage
{
    public int PageId { get; set; }
    public string PageCode { get; set; } = default!;
    public string PageName { get; set; } = default!;
    public string ModuleName { get; set; } = default!;
    public string PageRoute { get; set; } = default!;
    public int DisplaySequence { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RolePage> RolePages { get; set; } = new List<RolePage>();
}

public class RolePage
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = default!;
    public int PageId { get; set; }
    public AppPage Page { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}