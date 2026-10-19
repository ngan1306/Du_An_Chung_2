using KissAppTussing.Contracts;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace KissAppTussing.Endpoints;

internal static class InterestEndpoints
{
    public static IEndpointRouteBuilder MapInterestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/interests", GetInterestsAsync)
            .RequireAuthorization()
            .WithName("GetInterests")
            .WithSummary("List interests that can be added to a profile");

        endpoints.MapPut("/api/profiles/me/interests", UpdateInterestsAsync)
            .RequireAuthorization()
            .WithName("UpdateMyInterests")
            .WithSummary("Replace the signed-in user's interests")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<InterestResponse>>> GetInterestsAsync(
        IDatingService datingService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await datingService.GetInterestsAsync(cancellationToken));

    private static async Task<Results<NoContent, NotFound, BadRequest<ProblemDetails>>> UpdateInterestsAsync(
        UpdateInterestsRequest request,
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var updated = await datingService.UpdateInterestsAsync(
            CurrentUser.GetId(principal), request.InterestIds, cancellationToken);
        return updated
            ? TypedResults.NoContent()
            : TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid interests",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Choose up to twenty interests from the available catalog."
            });
    }
}