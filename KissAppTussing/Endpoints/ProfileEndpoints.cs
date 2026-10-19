using KissAppTussing.Contracts;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace KissAppTussing.Endpoints;

internal static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var profiles = endpoints.MapGroup("/api/profiles")
            .RequireAuthorization();

        profiles.MapGet("/discover", DiscoverAsync)
            .WithName("DiscoverProfiles")
            .WithSummary("Discover profiles that have not been swiped")
            .WithDescription("Filters profiles by name, age, gender, city, connection intent, and shared interest.")
            .ProducesValidationProblem();

        profiles.MapGet("/me", GetMyProfileAsync)
            .WithName("GetMyProfile")
            .WithSummary("Get the signed-in user's profile");

        profiles.MapPut("/me", UpdateMyProfileAsync)
            .WithName("UpdateMyProfile")
            .WithSummary("Update the signed-in user's profile")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        profiles.MapGet("/{profileId:guid}", GetProfileAsync)
            .WithName("GetProfile")
            .WithSummary("Get a public dating profile");

        return endpoints;
    }

    private static async Task<Results<Ok<IReadOnlyList<UserProfileResponse>>, BadRequest<ProblemDetails>>> DiscoverAsync(
        int? limit,
        string? search,
        string? gender,
        int? minAge,
        int? maxAge,
        string? city,
        string? lookingFor,
        Guid? interestId,
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        if (minAge is < 18 or > 120 || maxAge is < 18 or > 120 || (minAge.HasValue && maxAge.HasValue && minAge > maxAge))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid age range",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Age filters must be between 18 and 120, and minimum age cannot exceed maximum age."
            });
        }

        if (gender is not null && gender is not ("Woman" or "Man" or "NonBinary" or "PreferNotToSay"))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid gender filter",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (lookingFor is not null && lookingFor is not ("Friendship" or "Dating" or "Relationship"))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid connection intent",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var pageSize = Math.Clamp(limit ?? 20, 1, 100);
        return TypedResults.Ok(await datingService.DiscoverAsync(
            CurrentUser.GetId(principal), pageSize, search, gender, minAge, maxAge, city, lookingFor, interestId, cancellationToken));
    }

    private static async Task<Results<Ok<UserProfileResponse>, NotFound>> GetMyProfileAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var profile = await datingService.GetProfileAsync(CurrentUser.GetId(principal), cancellationToken);
        return profile is null ? TypedResults.NotFound() : TypedResults.Ok(profile);
    }

    private static async Task<Results<Ok<UserProfileResponse>, NotFound, BadRequest<ProblemDetails>>> UpdateMyProfileAsync(
        UpdateProfileRequest request,
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var profile = await datingService.UpdateProfileAsync(
            CurrentUser.GetId(principal), request, cancellationToken);

        if (profile is not null)
        {
            return TypedResults.Ok(profile);
        }

        var existingProfile = await datingService.GetProfileAsync(CurrentUser.GetId(principal), cancellationToken);
        return existingProfile is null
            ? TypedResults.NotFound()
            : TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Age requirement not met",
                Status = StatusCodes.Status400BadRequest,
                Detail = "A profile must belong to a user who is at least 18 years old."
            });
    }

    private static async Task<Results<Ok<UserProfileResponse>, NotFound>> GetProfileAsync(
        Guid profileId,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var profile = await datingService.GetProfileAsync(profileId, cancellationToken);
        return profile is null ? TypedResults.NotFound() : TypedResults.Ok(profile);
    }
}