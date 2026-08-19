// PageDtos.cs
namespace ERP.Application.DTOs;

public record PageDto(int PageId, string PageCode, string PageName, string ModuleName, string PageRoute, int DisplaySequence, bool IsActive);

public record CreatePageDto(string PageCode, string PageName, string ModuleName, string PageRoute, int DisplaySequence);