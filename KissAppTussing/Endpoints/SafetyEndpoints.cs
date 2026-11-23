using System.Security.Claims;
using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Endpoints;

internal static class SafetyEndpoints
{
    public static IEndpointRouteBuilder MapSafetyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var safety = endpoints.MapGroup("/api/safety").RequireAuthorization();
        safety.MapGet("/blocks", async (ClaimsPrincipal user, SafetyService service, CancellationToken ct) =>
            Results.Ok(await service.GetBlockedAsync(CurrentUser.GetId(user), ct)));
        safety.MapPost("/users/{userId:guid}/block", async (Guid userId, ClaimsPrincipal user, SafetyService service, CancellationToken ct) =>
        {
            var result = await service.BlockAsync(CurrentUser.GetId(user), userId, ct);
            return result switch { "Created" => Results.NoContent(), "Self" => Results.BadRequest(), _ => Results.NotFound() };
        });
        safety.MapDelete("/users/{userId:guid}/block", async (Guid userId, ClaimsPrincipal user, SafetyService service, CancellationToken ct) =>
            await service.UnblockAsync(CurrentUser.GetId(user), userId, ct) ? Results.NoContent() : Results.NotFound());

        safety.MapPost("/users/{userId:guid}/reports", (Guid userId, CreateContentReportRequest request, ClaimsPrincipal user, SafetyService service, CancellationToken ct) =>
            SubmitReport(service, CurrentUser.GetId(user), "User", userId, request, ct));
        safety.MapPost("/posts/{postId:guid}/reports", (Guid postId, CreateContentReportRequest request, ClaimsPrincipal user, SafetyService service, CancellationToken ct) =>
            SubmitReport(service, CurrentUser.GetId(user), "Post", postId, request, ct));
        safety.MapPost("/comments/{commentId:guid}/reports", (Guid commentId, CreateContentReportRequest request, ClaimsPrincipal user, SafetyService service, CancellationToken ct) =>
            SubmitReport(service, CurrentUser.GetId(user), "Comment", commentId, request, ct));
        safety.MapPost("/messages/{messageId:guid}/reports", (Guid messageId, CreateContentReportRequest request, ClaimsPrincipal user, SafetyService service, CancellationToken ct) =>
            SubmitReport(service, CurrentUser.GetId(user), "Message", messageId, request, ct));

        var admin = endpoints.MapGroup("/api/admin/reports");
        admin.MapGet("", GetReportsAsync).RequireAuthorization("AdminStaff");
        admin.MapPut("/{reportId:guid}", UpdateReportAsync).RequireAuthorization("AdminModeration");
        return endpoints;
    }

    private static async Task<IResult> SubmitReport(
        SafetyService service, Guid userId, string targetType, Guid targetId,
        CreateContentReportRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ReportAsync(userId, targetType, targetId, request.Reason, cancellationToken);
        return result.Status switch
        {
            "Created" => Results.Created($"/api/safety/reports/{result.Report!.Id}", result.Report),
            "Duplicate" => Results.Conflict(new { detail = "Bạn đã gửi báo cáo này và đang chờ xử lý." }),
            "Invalid" => Results.BadRequest(),
            _ => Results.NotFound()
        };
    }

    private static async Task<Ok<IReadOnlyList<AdminContentReportResponse>>> GetReportsAsync(
        DatingDbContext database,
        CancellationToken cancellationToken)
    {
        var reports = await (from report in database.ContentReports.AsNoTracking()
            join reporter in database.Users.AsNoTracking() on report.ReporterUserId equals reporter.Id
            orderby report.Status == "Pending" ? 0 : 1, report.CreatedAt descending
            select new AdminContentReportResponse(
                report.Id, report.ReporterUserId, reporter.Name, report.TargetType, report.TargetId,
                report.Reason, report.Status, report.CreatedAt))
            .Take(500)
            .ToListAsync(cancellationToken);
        var postIds = reports.Where(report => report.TargetType == "Post").Select(report => report.TargetId).Distinct().ToArray();
        var commentIds = reports.Where(report => report.TargetType == "Comment").Select(report => report.TargetId).Distinct().ToArray();
        var messageIds = reports.Where(report => report.TargetType == "Message").Select(report => report.TargetId).Distinct().ToArray();
        var posts = await database.JourneyPosts.AsNoTracking().Where(post => postIds.Contains(post.Id))
            .ToDictionaryAsync(post => post.Id, post => post.Content, cancellationToken);
        var comments = await database.JourneyComments.AsNoTracking().Where(comment => commentIds.Contains(comment.Id))
            .ToDictionaryAsync(comment => comment.Id, comment => comment.Content, cancellationToken);
        var messages = await database.ChatMessages.AsNoTracking().Where(message => messageIds.Contains(message.Id))
            .ToDictionaryAsync(message => message.Id, message => message.Content ?? "(Tệp đính kèm)", cancellationToken);
        var enriched = reports.Select(report => report with
        {
            TargetPreview = report.TargetType switch
            {
                "User" => "Tài khoản người dùng",
                "Post" => posts.GetValueOrDefault(report.TargetId),
                "Comment" => comments.GetValueOrDefault(report.TargetId),
                "Message" => messages.GetValueOrDefault(report.TargetId),
                _ => null
            }
        }).ToArray();
        return TypedResults.Ok<IReadOnlyList<AdminContentReportResponse>>(enriched);
    }

    private static async Task<Results<NoContent, NotFound, BadRequest>> UpdateReportAsync(
        Guid reportId,
        UpdateAdminReportRequest request,
        ClaimsPrincipal principal,
        DatingDbContext database,
        CancellationToken cancellationToken)
    {
        var report = await database.ContentReports.SingleOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report is null) return TypedResults.NotFound();
        if (request.HideTarget && request.Status != "Resolved") return TypedResults.BadRequest();
        if (request.HideTarget)
        {
            switch (report.TargetType)
            {
                case "User":
                    var user = await database.Users.SingleOrDefaultAsync(item => item.Id == report.TargetId, cancellationToken);
                    if (user is not null) user.IsSuspended = true;
                    await database.UserSessions.Where(session => session.UserId == report.TargetId && session.RevokedAt == null)
                        .ExecuteUpdateAsync(update => update.SetProperty(session => session.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);
                    break;
                case "Post":
                    await database.JourneyPosts.Where(post => post.Id == report.TargetId)
                        .ExecuteUpdateAsync(update => update.SetProperty(post => post.IsHidden, true), cancellationToken);
                    break;
                case "Comment":
                    await database.JourneyComments.Where(comment => comment.Id == report.TargetId)
                        .ExecuteUpdateAsync(update => update.SetProperty(comment => comment.IsHidden, true), cancellationToken);
                    break;
                case "Message":
                    await database.ChatMessages.Where(message => message.Id == report.TargetId)
                        .ExecuteUpdateAsync(update => update.SetProperty(message => message.IsModerated, true), cancellationToken);
                    break;
                default:
                    return TypedResults.BadRequest();
            }
        }
        report.Status = request.Status;
        report.ReviewedAt = DateTimeOffset.UtcNow;
        report.ReviewedByUserId = CurrentUser.GetId(principal);
        AdminAuditWriter.Add(database, principal, request.Status == "Resolved" ? "ReportResolved" : "ReportDismissed",
            report.TargetType, report.TargetId, $"Report {report.Id}: {report.Reason}");
        await database.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
