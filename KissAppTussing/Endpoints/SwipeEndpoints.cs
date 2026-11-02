using KissAppTussing.Contracts;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace KissAppTussing.Endpoints;

internal static class SwipeEndpoints
{
    public static IEndpointRouteBuilder MapSwipeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/swipes", SwipeAsync)
            .RequireAuthorization()
            .WithName("CreateSwipe")
            .WithSummary("Like or pass on a profile")
            .WithDescription("A match is created when both users like each other.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        endpoints.MapGet("/api/swipes/liked", GetLikedProfilesAsync)
            .RequireAuthorization()
            .WithName("GetLikedProfiles")
            .WithSummary("List profiles the signed-in user liked");

        endpoints.MapGet("/api/swipes/rejected", GetRejectedProfilesAsync)
            .RequireAuthorization()
            .WithName("GetRejectedProfiles")
            .WithSummary("List profiles the signed-in user passed on");

        endpoints.MapDelete("/api/swipes/{targetUserId:guid}", UndoPassAsync)
            .RequireAuthorization()
            .WithName("UndoPass")
            .WithSummary("Undo a pass on a profile")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<LikedProfileResponse>>> GetLikedProfilesAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await datingService.GetLikedProfilesAsync(
            CurrentUser.GetId(principal), cancellationToken));

    private static async Task<Ok<IReadOnlyList<RejectedProfileResponse>>> GetRejectedProfilesAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await datingService.GetRejectedProfilesAsync(
            CurrentUser.GetId(principal), cancellationToken));

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> UndoPassAsync(
        Guid targetUserId,
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var removed = await datingService.UndoPassAsync(
            CurrentUser.GetId(principal), targetUserId, cancellationToken);

        return removed
            ? TypedResults.NoContent()
            : TypedResults.NotFound(new ProblemDetails
            {
                Title = "Passed profile not found",
                Status = StatusCodes.Status404NotFound
            });
    }

    private static async Task<Results<Ok<SwipeResponse>, NotFound<ProblemDetails>, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> SwipeAsync(
        CreateSwipeRequest request,
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var result = await datingService.SwipeAsync(
            CurrentUser.GetId(principal), request, cancellationToken);

        return result.Status switch
        {
            SwipeStatus.Created => TypedResults.Ok(result.Response!),
            SwipeStatus.TargetNotFound => TypedResults.NotFound(new ProblemDetails
            {
                Title = "Profile not found",
                Status = StatusCodes.Status404NotFound
            }),
            SwipeStatus.AlreadySwiped => TypedResults.Conflict(new ProblemDetails
            {
                Title = "Profile already swiped",
                Status = StatusCodes.Status409Conflict
            }),
            _ => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid swipe",
                Status = StatusCodes.Status400BadRequest,
                Detail = "You cannot swipe on your own profile."
            })
        };
    }
}
