using KissAppTussing.Data;
using KissAppTussing.Models;
using System.Security.Claims;

namespace KissAppTussing.Services;

internal static class AdminAuditWriter
{
    public static void Add(DatingDbContext database, ClaimsPrincipal principal, string action, string targetType, Guid? targetId, string details)
    {
        var adminId = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        database.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = adminId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Details = details.Length > 500 ? details[..500] : details
        });
    }
}
