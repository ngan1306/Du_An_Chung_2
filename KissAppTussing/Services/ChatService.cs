using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Services;

internal sealed class ChatService(DatingDbContext database, IWebHostEnvironment environment) : IChatService
{
    private const int MaxMessageLength = 2000;
    private const long MaxImageBytes = 8 * 1024 * 1024;
    private const long MaxVideoBytes = 20 * 1024 * 1024;

    public async Task<ChatMessagesResult> GetMessagesAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken)
    {
        if (!await CanChatAsync(userId, otherUserId, cancellationToken))
        {
            return new ChatMessagesResult(ChatOperationStatus.NotMatched, []);
        }

        var messages = await database.ChatMessages
            .AsNoTracking()
            .Include(message => message.Sender)
            .Where(message =>
                (message.SenderId == userId && message.RecipientId == otherUserId)
                || (message.SenderId == otherUserId && message.RecipientId == userId))
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        var responses = messages
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.Id)
            .Select(message => ToResponse(message, userId))
            .ToArray();

        return new ChatMessagesResult(ChatOperationStatus.Success, responses);
    }

    public async Task<ChatMessageResult> SendMessageAsync(
        Guid userId,
        Guid otherUserId,
        string? content,
        IFormFile? media,
        CancellationToken cancellationToken)
    {
        if (!await CanChatAsync(userId, otherUserId, cancellationToken))
        {
            return new ChatMessageResult(ChatOperationStatus.NotMatched);
        }

        var text = content?.Trim() ?? string.Empty;
        if (text.Length > MaxMessageLength)
        {
            return new ChatMessageResult(ChatOperationStatus.ContentTooLong);
        }

        if (text.Length == 0 && (media is null || media.Length == 0))
        {
            return new ChatMessageResult(ChatOperationStatus.EmptyMessage);
        }

        string? mediaFileName = null;
        string? mediaType = null;
        string? physicalPath = null;
        var messageId = Guid.NewGuid();

        if (media is { Length: > 0 })
        {
            var detectedMedia = await DetectMediaAsync(media, cancellationToken);
            if (detectedMedia is null)
            {
                return new ChatMessageResult(ChatOperationStatus.UnsupportedMedia);
            }

            var limit = detectedMedia.Value.MediaType.StartsWith("image/", StringComparison.Ordinal)
                ? MaxImageBytes
                : MaxVideoBytes;
            if (media.Length > limit)
            {
                return new ChatMessageResult(ChatOperationStatus.MediaTooLarge);
            }

            mediaFileName = $"{messageId:N}{detectedMedia.Value.Extension}";
            var directory = GetMessageDirectory();
            Directory.CreateDirectory(directory);
            physicalPath = Path.Combine(directory, mediaFileName);
            mediaType = detectedMedia.Value.MediaType;

            try
            {
                await using var input = media.OpenReadStream();
                await using var output = File.Create(physicalPath);
                await input.CopyToAsync(output, cancellationToken);
            }
            catch
            {
                if (File.Exists(physicalPath)) File.Delete(physicalPath);
                throw;
            }
        }

        var message = new ChatMessage
        {
            Id = messageId,
            SenderId = userId,
            RecipientId = otherUserId,
            Content = text.Length == 0 ? null : text,
            MediaFileName = mediaFileName,
            MediaType = mediaType
        };

        try
        {
            database.ChatMessages.Add(message);
            await database.SaveChangesAsync(cancellationToken);
            await database.Entry(message).Reference(item => item.Sender).LoadAsync(cancellationToken);
            return new ChatMessageResult(ChatOperationStatus.Success, ToResponse(message, userId));
        }
        catch
        {
            if (physicalPath is not null && File.Exists(physicalPath)) File.Delete(physicalPath);
            throw;
        }
    }

    public async Task<ChatMediaResult> GetMediaAsync(
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var message = await database.ChatMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == messageId
                && (item.SenderId == userId || item.RecipientId == userId), cancellationToken);

        if (message?.MediaFileName is null || message.MediaType is null)
        {
            return new ChatMediaResult(ChatOperationStatus.MediaNotFound);
        }

        var path = Path.Combine(GetMessageDirectory(), message.MediaFileName);
        if (!File.Exists(path))
        {
            return new ChatMediaResult(ChatOperationStatus.MediaNotFound);
        }

        return new ChatMediaResult(ChatOperationStatus.Success, File.OpenRead(path), message.MediaType);
    }

    private async Task<bool> CanChatAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        if (userId == otherUserId)
        {
            return false;
        }

        return await database.Matches.AnyAsync(match =>
            (match.FirstUserId == userId && match.SecondUserId == otherUserId)
            || (match.FirstUserId == otherUserId && match.SecondUserId == userId), cancellationToken);
    }

    private string GetMessageDirectory() =>
        Path.Combine(environment.ContentRootPath, "App_Data", "chat");

    private static ChatMessageResponse ToResponse(ChatMessage message, Guid currentUserId) => new(
        message.Id,
        message.SenderId,
        message.Sender.Name,
        message.Content,
        message.MediaFileName is null ? null : $"/api/chat/media/{message.Id}",
        message.MediaType,
        message.SentAt,
        message.SenderId == currentUserId);

    private static async Task<(string Extension, string MediaType)?> DetectMediaAsync(
        IFormFile media,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = media.OpenReadStream();
        var count = await stream.ReadAsync(header, cancellationToken);

        if (count >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return (".png", "image/png");
        if (count >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return (".jpg", "image/jpeg");
        if (count >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return (".webp", "image/webp");
        if (count >= 8 && header.AsSpan(4, 4).SequenceEqual("ftyp"u8))
            return (".mp4", "video/mp4");
        if (count >= 4 && header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
            return (".webm", "video/webm");

        return null;
    }
}
