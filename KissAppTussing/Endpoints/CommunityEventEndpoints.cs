using System.Security.Claims;
using KissAppTussing.Contracts;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace KissAppTussing.Endpoints;

internal static class CommunityEventEndpoints
{
    public static IEndpointRouteBuilder MapCommunityEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events").RequireAuthorization("OwnerAdmin");
        group.MapGet("", GetEventsAsync);
        group.MapPost("", CreateEventAsync);
        group.MapDelete("/{eventId:guid}", DeleteEventAsync);
        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<CommunityEventResponse>>> GetEventsAsync(
        int? limit, CommunityEventService service, CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetAsync(limit ?? 50, cancellationToken));

    private static async Task<Results<Created<CommunityEventResponse>, BadRequest>> CreateEventAsync(
        CreateCommunityEventRequest request, ClaimsPrincipal user, CommunityEventService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(CurrentUser.GetId(user), request, cancellationToken);
        return result is null ? TypedResults.BadRequest() : TypedResults.Created($"/api/events/{result.Id}", result);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteEventAsync(
        Guid eventId, CommunityEventService service, CancellationToken cancellationToken) =>
        await service.DeleteAsync(eventId, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}
