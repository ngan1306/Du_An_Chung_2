using KissAppTussing.Contracts;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace KissAppTussing.Endpoints;

internal static class ProfilePhotoEndpoints
{
    public static IEndpointRouteBuilder MapProfilePhotoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var photos = endpoints.MapGroup("/api/profiles/me/photos")
            .RequireAuthorization();

        photos.MapPost("", AddPhotoAsync)
            .DisableAntiforgery()
            .WithName("AddProfilePhoto")
            .WithSummary("Upload a PNG, JPEG, or WebP profile photo")
            .Accepts<IFormFile>("multipart/form-data")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        photos.MapPut("/{photoId:guid}/avatar", SetAvatarAsync)
            .WithName("SetProfileAvatar")
            .WithSummary("Set one of the user's photos as the profile avatar")
            .ProducesProblem(StatusCodes.Status404NotFound);

        photos.MapDelete("/{photoId:guid}", DeletePhotoAsync)
            .WithName("DeleteProfilePhoto")
            .WithSummary("Delete one of the user's profile photos")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Created<ProfilePhotoResponse>, BadRequest<ProblemDetails>, Conflict<ProblemDetails>, NotFound<ProblemDetails>>> AddPhotoAsync(
        IFormFile file,
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var result = await datingService.AddPhotoAsync(CurrentUser.GetId(principal), file, cancellationToken);
        return result.Status switch
        {
            PhotoOperationStatus.Created => TypedResults.Created(
                "/api/profiles/me", result.Photo!),
            PhotoOperationStatus.PhotoLimitReached => TypedResults.Conflict(new ProblemDetails
            {
                Title = "Photo limit reached",
                Status = StatusCodes.Status409Conflict,
                Detail = "A profile can contain up to six photos."
            }),
            PhotoOperationStatus.UserNotFound => TypedResults.NotFound(new ProblemDetails
            {
                Title = "Profile not found",
                Status = StatusCodes.Status404NotFound
            }),
            _ => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid image",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Upload a PNG, JPEG, or WebP image no larger than 5 MB."
            })
        };
    }

    private static async Task<Results<Ok<ProfilePhotoResponse>, NotFound<ProblemDetails>>> SetAvatarAsync(
        Guid photoId,
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var result = await datingService.SetAvatarAsync(CurrentUser.GetId(principal), photoId, cancellationToken);
        return result.Status == PhotoOperationStatus.Updated
            ? TypedResults.Ok(result.Photo!)
            : TypedResults.NotFound(new ProblemDetails
            {
                Title = "Photo not found",
                Status = StatusCodes.Status404NotFound
            });
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> DeletePhotoAsync(
        Guid photoId,
        System.Security.Claims.ClaimsPrincipal principal,
        IDatingService datingService,
        CancellationToken cancellationToken)
    {
        var result = await datingService.DeletePhotoAsync(CurrentUser.GetId(principal), photoId, cancellationToken);
        return result.Status == PhotoOperationStatus.Deleted
            ? TypedResults.NoContent()
            : TypedResults.NotFound(new ProblemDetails
            {
                Title = "Photo not found",
                Status = StatusCodes.Status404NotFound
            });
    }
}