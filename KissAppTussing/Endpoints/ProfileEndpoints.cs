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

        profiles.MapPut("/me/privacy", UpdatePrivacyAsync)
            .WithName("UpdateMyPrivacy")
            .WithSummary("Save profile visibility and incognito settings");

        profiles.MapGet("/me/privacy", GetPrivacyAsync)
            .WithName("GetMyPrivacy")
            .WithSummary("Get profile visibility and incognito settings");

        profiles.MapPut("/me/location", UpdateLocationAsync)
            .WithName("UpdateMyLocation")
            .WithSummary("Save GPS coordinates for distance filtering")
            .ProducesValidationProblem();

        profiles.MapPost("/me/presence", MarkOnlineAsync)
            .WithName("UpdateMyPresence")
            .WithSummary("Update the current user's online status");

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
        double? radiusKm,
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

        if (radiusKm is < 1 or > 500)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid search radius",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Distance must be between 1 and 500 kilometers."
            });
        }

        var pageSize = Math.Clamp(limit ?? 20, 1, 150);
        var profiles = await datingService.DiscoverAsync(
            CurrentUser.GetId(principal), pageSize, search, gender, minAge, maxAge, city, lookingFor, interestId, radiusKm, cancellationToken);
        return profiles is null
            ? TypedResults.BadRequest(new ProblemDetails
            {
                Title = "GPS location required",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Hãy cập nhật vị trí GPS của bạn trước khi lọc theo khoảng cách."
            })
            : TypedResults.Ok(profiles);
    }

    private static async Task<Results<Ok<UserProfileResponse>, NotFound>> GetMyProfileAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var profile = await datingService.GetMyProfileAsync(CurrentUser.GetId(principal), cancellationToken);
        return profile is null ? TypedResults.NotFound() : TypedResults.Ok(profile);
    }

    private static async Task<Results<Ok<PrivacySettingsResponse>, NotFound>> UpdatePrivacyAsync(
        UpdatePrivacyRequest request,
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var settings = await datingService.UpdatePrivacyAsync(CurrentUser.GetId(principal), request, cancellationToken);
        return settings is null ? TypedResults.NotFound() : TypedResults.Ok(settings);
    }

    private static async Task<Results<Ok<PrivacySettingsResponse>, NotFound>> GetPrivacyAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var settings = await datingService.GetPrivacyAsync(CurrentUser.GetId(principal), cancellationToken);
        return settings is null ? TypedResults.NotFound() : TypedResults.Ok(settings);
    }

    private static async Task<Results<NoContent, NotFound>> UpdateLocationAsync(
        UpdateLocationRequest request,
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken) =>
        await datingService.UpdateLocationAsync(CurrentUser.GetId(principal), request, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();

    private static async Task<Results<NoContent, NotFound>> MarkOnlineAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken) =>
        await datingService.MarkOnlineAsync(CurrentUser.GetId(principal), cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();

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
