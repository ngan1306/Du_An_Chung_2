using KissAppTussing.Contracts;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace KissAppTussing.Endpoints;

internal static class JourneyEndpoints
{
    public static IEndpointRouteBuilder MapJourneyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var journey = endpoints.MapGroup("/api/journey")
            .RequireAuthorization();

        journey.MapGet("", GetFeedAsync)
            .WithName("GetJourneyFeed")
            .WithSummary("Get the latest journey posts")
            .WithDescription("Returns text, image, and video posts from the newest to the oldest.");

        journey.MapPost("", CreatePostAsync)
            .DisableAntiforgery()
            .WithName("CreateJourneyPost")
            .WithSummary("Publish a status with an optional image or video")
            .Accepts<IFormFile>("multipart/form-data")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        journey.MapDelete("/{postId:guid}", DeletePostAsync)
            .WithName("DeleteJourneyPost")
            .WithSummary("Delete one of your own journey posts")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        journey.MapPost("/{postId:guid}/like", ToggleLikeAsync)
            .WithName("ToggleJourneyLike")
            .WithSummary("Like or unlike a journey post")
            .Produces<JourneyLikeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        journey.MapGet("/{postId:guid}/comments", GetCommentsAsync)
            .WithName("GetJourneyComments")
            .WithSummary("Get comments for a journey post")
            .Produces<IReadOnlyList<JourneyCommentResponse>>();

        journey.MapPost("/{postId:guid}/comments", AddCommentAsync)
            .WithName("AddJourneyComment")
            .WithSummary("Add a comment to a journey post")
            .Produces<JourneyCommentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<JourneyPostResponse>>> GetFeedAsync(
        int? limit,
        System.Security.Claims.ClaimsPrincipal principal,
        IJourneyService journeyService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await journeyService.GetFeedAsync(
            CurrentUser.GetId(principal), Math.Clamp(limit ?? 30, 1, 100), cancellationToken));

    private static async Task<Results<Created<JourneyPostResponse>, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> CreatePostAsync(
        [FromForm] string? content,
        [FromForm] IFormFile? media,
        System.Security.Claims.ClaimsPrincipal principal,
        IJourneyService journeyService,
        CancellationToken cancellationToken)
    {
        var result = await journeyService.CreatePostAsync(
            CurrentUser.GetId(principal), content, media, cancellationToken);

        return result.Status switch
        {
            JourneyPostOperationStatus.Created => TypedResults.Created("/api/journey", result.Post!),
            JourneyPostOperationStatus.NotFound => TypedResults.NotFound(new ProblemDetails
            {
                Title = "User not found",
                Status = StatusCodes.Status404NotFound
            }),
            JourneyPostOperationStatus.UnsupportedMedia => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Unsupported media",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Choose a PNG, JPEG, WebP image, MP4 video, or WebM video."
            }),
            JourneyPostOperationStatus.MediaTooLarge => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Media too large",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Images may be up to 5 MB and videos up to 50 MB."
            }),
            _ => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Post is empty or too long",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Add text or media. Text may contain up to 2,000 characters."
            })
        };
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> DeletePostAsync(
        Guid postId,
        System.Security.Claims.ClaimsPrincipal principal,
        IJourneyService journeyService,
        CancellationToken cancellationToken)
    {
        var status = await journeyService.DeletePostAsync(
            CurrentUser.GetId(principal), postId, cancellationToken);
        return status == JourneyPostOperationStatus.Deleted
            ? TypedResults.NoContent()
            : TypedResults.NotFound(new ProblemDetails
            {
                Title = "Post not found",
                Status = StatusCodes.Status404NotFound
            });
    }

    private static async Task<Results<Ok<JourneyLikeResponse>, NotFound<ProblemDetails>>> ToggleLikeAsync(
        Guid postId,
        System.Security.Claims.ClaimsPrincipal principal,
        IJourneyService journeyService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await journeyService.ToggleLikeAsync(CurrentUser.GetId(principal), postId, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "Post not found",
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    private static async Task<Ok<IReadOnlyList<JourneyCommentResponse>>> GetCommentsAsync(
        Guid postId,
        System.Security.Claims.ClaimsPrincipal principal,
        IJourneyService journeyService,
        CancellationToken cancellationToken)
    {
        var comments = await journeyService.GetCommentsAsync(CurrentUser.GetId(principal), postId, cancellationToken);
        return TypedResults.Ok(comments);
    }

    private static async Task<Results<Ok<JourneyCommentResponse>, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> AddCommentAsync(
        Guid postId,
        [FromBody] CreateJourneyCommentRequest request,
        System.Security.Claims.ClaimsPrincipal principal,
        IJourneyService journeyService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await journeyService.AddCommentAsync(CurrentUser.GetId(principal), postId, request.Content, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "Post not found",
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid comment",
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message
            });
        }
    }
}