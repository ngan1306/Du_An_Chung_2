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

        return endpoints;
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