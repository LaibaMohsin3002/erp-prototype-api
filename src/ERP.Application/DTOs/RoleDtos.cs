// RoleDtos.cs
namespace ERP.Application.DTOs;

public record RoleListDto(int RoleId, string RoleName, string? Description, bool IsActive);

public record RoleDetailDto(int RoleId, string RoleName, string? Description, bool IsActive, List<int> PageIds);

public record CreateRoleDto(string RoleName, string? Description, List<int> PageIds);

public record UpdateRoleDto(string? Description, bool IsActive, List<int> PageIds);