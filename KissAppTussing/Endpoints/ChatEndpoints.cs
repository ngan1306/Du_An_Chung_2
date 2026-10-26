using KissAppTussing.Contracts;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace KissAppTussing.Endpoints;

internal static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var chat = endpoints.MapGroup("/api/chat")
            .RequireAuthorization();

        chat.MapGet("/{otherUserId:guid}/messages", GetMessagesAsync)
            .WithName("GetChatMessages")
            .WithSummary("Get messages with a matched user");

        chat.MapPost("/{otherUserId:guid}/messages", SendMessageAsync)
            .DisableAntiforgery()
            .WithName("SendChatMessage")
            .WithSummary("Send text, an image, or a video to a matched user")
            .Accepts<IFormFile>("multipart/form-data")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        chat.MapGet("/media/{messageId:guid}", GetMediaAsync)
            .WithName("GetChatMedia")
            .WithSummary("Get an attachment belonging to the signed-in user's conversation");

        return endpoints;
    }

    private static async Task<Results<Ok<IReadOnlyList<ChatMessageResponse>>, NotFound<ProblemDetails>>> GetMessagesAsync(
        Guid otherUserId,
        System.Security.Claims.ClaimsPrincipal principal,
        IChatService chatService,
        CancellationToken cancellationToken)
    {
        var result = await chatService.GetMessagesAsync(CurrentUser.GetId(principal), otherUserId, cancellationToken);
        return result.Status == ChatOperationStatus.Success
            ? TypedResults.Ok(result.Messages)
            : TypedResults.NotFound(new ProblemDetails
            {
                Title = "Conversation not found",
                Status = StatusCodes.Status404NotFound
            });
    }

    private static async Task<Results<Created<ChatMessageResponse>, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> SendMessageAsync(
        Guid otherUserId,
        [FromForm] string? content,
        [FromForm] IFormFile? media,
        System.Security.Claims.ClaimsPrincipal principal,
        IChatService chatService,
        CancellationToken cancellationToken)
    {
        var result = await chatService.SendMessageAsync(
            CurrentUser.GetId(principal), otherUserId, content, media, cancellationToken);

        return result.Status switch
        {
            ChatOperationStatus.Success => TypedResults.Created(
                $"/api/chat/{otherUserId}/messages", result.Message!),
            ChatOperationStatus.NotMatched => TypedResults.NotFound(new ProblemDetails
            {
                Title = "Conversation not found",
                Status = StatusCodes.Status404NotFound
            }),
            ChatOperationStatus.UnsupportedMedia => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Unsupported file",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Choose a PNG, JPEG, WebP, MP4, or WebM file."
            }),
            ChatOperationStatus.MediaTooLarge => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "File too large",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Images may be up to 8 MB and videos up to 20 MB."
            }),
            ChatOperationStatus.ContentTooLong => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Message too long",
                Status = StatusCodes.Status400BadRequest,
                Detail = "A message may contain up to 2,000 characters."
            }),
            _ => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Empty message",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Write a message or attach an image or video."
            })
        };
    }

    private static async Task<Results<FileStreamHttpResult, NotFound<ProblemDetails>>> GetMediaAsync(
        Guid messageId,
        System.Security.Claims.ClaimsPrincipal principal,
        IChatService chatService,
        CancellationToken cancellationToken)
    {
        var result = await chatService.GetMediaAsync(CurrentUser.GetId(principal), messageId, cancellationToken);
        return result.Status == ChatOperationStatus.Success
            ? TypedResults.File(result.Stream!, result.ContentType!, enableRangeProcessing: true)
            : TypedResults.NotFound(new ProblemDetails
            {
                Title = "Attachment not found",
                Status = StatusCodes.Status404NotFound
            });
    }
}
