using System.Security.Claims;
using KissAppTussing.Contracts;
using KissAppTussing.Services;

namespace KissAppTussing.Endpoints;

internal static class SocialEndpoints
{
    public static IEndpointRouteBuilder MapSocialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").RequireAuthorization();
        group.MapGet("/users/search", async (string? q, ClaimsPrincipal user, SocialService service, CancellationToken ct) => Results.Ok(await service.SearchAsync(CurrentUser.GetId(user), q, ct)));
        group.MapGet("/connections", async (string? status, ClaimsPrincipal user, SocialService service, CancellationToken ct) => Results.Ok(await service.ConnectionsAsync(CurrentUser.GetId(user), status, ct)));
        group.MapPost("/connections/{userId:guid}", async (Guid userId, ClaimsPrincipal user, SocialService service, CancellationToken ct) =>
        {
            var result = await service.RequestAsync(CurrentUser.GetId(user), userId, ct);
            return result switch { "Created" => Results.Accepted(), "Self" => Results.BadRequest(), "Missing" => Results.NotFound(), "Duplicate" or "Accepted" => Results.Conflict(), _ => Results.Conflict(new { detail = "A request from this user is already waiting for your response." }) };
        });
        group.MapPost("/connections/{id:guid}/accept", async (Guid id, ClaimsPrincipal user, SocialService service, CancellationToken ct) => Decision(await service.DecideAsync(CurrentUser.GetId(user), id, true, ct)));
        group.MapPost("/connections/{id:guid}/decline", async (Guid id, ClaimsPrincipal user, SocialService service, CancellationToken ct) => Decision(await service.DecideAsync(CurrentUser.GetId(user), id, false, ct)));
        group.MapDelete("/connections/{id:guid}", async (Guid id, ClaimsPrincipal user, SocialService service, CancellationToken ct) => Decision(await service.CancelAsync(CurrentUser.GetId(user), id, ct)));
        group.MapPost("/connections/{id:guid}/disconnect", async (Guid id, ClaimsPrincipal user, SocialService service, CancellationToken ct) => Decision(await service.DisconnectAsync(CurrentUser.GetId(user), id, ct)));
        group.MapGet("/conversations", async (ClaimsPrincipal user, SocialService service, CancellationToken ct) => Results.Ok(await service.ConversationsAsync(CurrentUser.GetId(user), ct)));
        group.MapGet("/conversations/{id:guid}/messages", async (Guid id, DateTimeOffset? before, int? limit, ClaimsPrincipal user, SocialService service, CancellationToken ct) =>
        {
            var messages = await service.MessagesAsync(CurrentUser.GetId(user), id, before, limit ?? 50, ct);
            return messages is null ? Results.NotFound() : Results.Ok(messages);
        });
        group.MapPost("/conversations/{id:guid}/messages", async (Guid id, SendMessageRequest request, ClaimsPrincipal user, SocialService service, CancellationToken ct) =>
        {
            var result = await service.SendAsync(CurrentUser.GetId(user), id, request.Content, ct);
            return result.Status switch { "Created" => Results.Created($"/api/conversations/{id}/messages", result.Message), "Invalid" => Results.BadRequest(new { detail = "Message must contain between 1 and 2000 characters." }), _ => Results.NotFound() };
        });
        return endpoints;
    }

    private static IResult Decision(string result) => result switch
    {
        "Accepted" or "Declined" or "Deleted" => Results.NoContent(),
        "Missing" => Results.NotFound(),
        _ => Results.Forbid()
    };
}
