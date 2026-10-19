using System.Security.Claims;

namespace KissAppTussing.Endpoints;

internal static class CurrentUser
{
    public static Guid GetId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}