using KissAppTussing.Contracts;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace KissAppTussing.Endpoints;

internal static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/matches", GetMatchesAsync)
            .RequireAuthorization()
            .WithName("GetMatches")
            .WithSummary("List the signed-in user's mutual matches");

        endpoints.MapDelete("/api/matches/{matchId:guid}", UnmatchAsync)
            .RequireAuthorization()
            .WithName("Unmatch")
            .WithSummary("End a match with another user")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<MatchResponse>>> GetMatchesAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await datingService.GetMatchesAsync(
            CurrentUser.GetId(principal), cancellationToken));

    private static async Task<Results<NoContent, NotFound>> UnmatchAsync(
        Guid matchId,
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken) =>
        await datingService.UnmatchAsync(CurrentUser.GetId(principal), matchId, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}
