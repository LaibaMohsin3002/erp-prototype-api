// DTOs — add to a new NavigationDtos.cs in ERP.Application/DTOs/
namespace ERP.Application.DTOs;

public record NavPageDto(int PageId, string PageCode, string PageName, string PageRoute, string ModuleName);
public record NavRoleDto(int RoleId, string RoleName, List<NavPageDto> Pages);