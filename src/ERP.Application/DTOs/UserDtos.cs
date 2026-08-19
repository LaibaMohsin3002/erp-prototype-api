// UserDtos.cs
namespace ERP.Application.DTOs;

public record UserListDto(int UserId, string UserName, string FullName, string Email, bool IsActive);

public record UserDetailDto(int UserId, string UserName, string FullName, string Email, bool IsActive, List<int> RoleIds);

public record CreateUserDto(string UserName, string FullName, string Password, string Email, List<int> RoleIds);

public record UpdateUserDto(string FullName, string Email, List<int> RoleIds);