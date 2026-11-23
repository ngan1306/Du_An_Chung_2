using System.Security.Claims;
using KissAppTussing.Contracts;
using KissAppTussing.Services;

namespace KissAppTussing.Endpoints;

internal static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications").RequireAuthorization();
        group.MapGet("", async (int? limit, ClaimsPrincipal user, NotificationService service, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store, private";
            return Results.Ok(await service.GetAsync(CurrentUser.GetId(user), limit ?? 50, ct));
        });
        group.MapPost("/{notificationId:guid}/read", async (Guid notificationId, ClaimsPrincipal user, NotificationService service, CancellationToken ct) =>
            await service.MarkReadAsync(CurrentUser.GetId(user), notificationId, ct) ? Results.NoContent() : Results.NotFound());
        group.MapPost("/read-all", async (ClaimsPrincipal user, NotificationService service, CancellationToken ct) =>
        {
            await service.MarkAllReadAsync(CurrentUser.GetId(user), ct);
            return Results.NoContent();
        });
        group.MapGet("/preferences", async (ClaimsPrincipal user, NotificationService service, CancellationToken ct) =>
        {
            var preferences = await service.GetPreferencesAsync(CurrentUser.GetId(user), ct);
            return preferences is null ? Results.NotFound() : Results.Ok(preferences);
        });
        group.MapPut("/preferences", async (NotificationPreferencesRequest request, ClaimsPrincipal user, NotificationService service, CancellationToken ct) =>
        {
            var preferences = await service.UpdatePreferencesAsync(CurrentUser.GetId(user), request, ct);
            return preferences is null ? Results.NotFound() : Results.Ok(preferences);
        });
        return endpoints;
    }
}
