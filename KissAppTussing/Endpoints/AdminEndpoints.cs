using KissAppTussing.Contracts;
using KissAppTussing.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin")
            .RequireAuthorization("OwnerAdmin");

        admin.MapGet("/overview", GetOverviewAsync)
            .WithName("GetAdminOverview")
            .WithSummary("Get an overview of application data for an authorized owner");

        admin.MapGet("/export", ExportDataAsync)
            .WithName("ExportAdminData")
            .WithSummary("Export profiles and social activity as JSON; private chat contents are excluded");

        return endpoints;
    }

    private static async Task<Ok<AdminOverviewResponse>> GetOverviewAsync(
        DatingDbContext database,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store, private";
        var overview = new AdminOverviewResponse(
            await database.Users.CountAsync(cancellationToken),
            await database.Swipes.CountAsync(cancellationToken),
            await database.Matches.CountAsync(cancellationToken),
            await database.JourneyPosts.CountAsync(cancellationToken),
            await database.JourneyComments.CountAsync(cancellationToken),
            await database.ChatMessages.CountAsync(cancellationToken),
            await database.Users.AsNoTracking()
                .OrderByDescending(user => user.CreatedAt)
                .Take(10)
                .Select(user => new AdminUserSummary(user.Id, user.Name, user.Email, user.CreatedAt))
                .ToListAsync(cancellationToken));

        return TypedResults.Ok(overview);
    }

    private static async Task<Ok<AdminDataExport>> ExportDataAsync(
        DatingDbContext database,
        HttpContext httpContext,
        ILogger<AdminDataExport> logger,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store, private";
        var export = new AdminDataExport(
            DateTimeOffset.UtcNow,
            await database.Users.AsNoTracking()
                .OrderBy(user => user.CreatedAt)
                .Select(user => new AdminUserExport(
                    user.Id, user.Name, user.Email, user.BirthDate, user.Bio, user.Gender,
                    user.City, user.Occupation, user.Education, user.LookingFor,
                    user.TermsAcceptedAt, user.CreatedAt))
                .ToListAsync(cancellationToken),
            await database.UserInterests.AsNoTracking()
                .OrderBy(item => item.UserId)
                .Select(item => new AdminInterestExport(item.UserId, item.InterestId, item.Interest.Name))
                .ToListAsync(cancellationToken),
            await database.ProfilePhotos.AsNoTracking()
                .OrderBy(photo => photo.UserId).ThenBy(photo => photo.SortOrder)
                .Select(photo => new AdminPhotoExport(photo.Id, photo.UserId, photo.Url, photo.IsAvatar, photo.SortOrder, photo.CreatedAt))
                .ToListAsync(cancellationToken),
            await database.Swipes.AsNoTracking()
                .OrderBy(swipe => swipe.CreatedAt)
                .Select(swipe => new AdminSwipeExport(swipe.Id, swipe.FromUserId, swipe.ToUserId, swipe.IsLike, swipe.CreatedAt))
                .ToListAsync(cancellationToken),
            await database.Matches.AsNoTracking()
                .OrderBy(match => match.MatchedAt)
                .Select(match => new AdminMatchExport(match.Id, match.FirstUserId, match.SecondUserId, match.MatchedAt))
                .ToListAsync(cancellationToken),
            await database.JourneyPosts.AsNoTracking()
                .OrderBy(post => post.CreatedAt)
                .Select(post => new AdminJourneyPostExport(post.Id, post.UserId, post.Content, post.MediaUrl, post.MediaType, post.CreatedAt))
                .ToListAsync(cancellationToken),
            await database.JourneyPostLikes.AsNoTracking()
                .OrderBy(like => like.CreatedAt)
                .Select(like => new AdminJourneyLikeExport(like.PostId, like.UserId, like.CreatedAt))
                .ToListAsync(cancellationToken),
            await database.JourneyComments.AsNoTracking()
                .OrderBy(comment => comment.CreatedAt)
                .Select(comment => new AdminJourneyCommentExport(comment.Id, comment.PostId, comment.UserId, comment.Content, comment.CreatedAt))
                .ToListAsync(cancellationToken));

        var adminUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        logger.LogInformation("Owner data export requested by account {AdminUserId}", adminUserId);
        return TypedResults.Ok(export);
    }
}
