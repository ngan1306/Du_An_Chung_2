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

    public async Task<IReadOnlyList<UserProfileResponse>> GetContactsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var matchedUserIds = await database.Matches.AsNoTracking()
            .Where(match => match.EndedAt == null && (match.FirstUserId == userId || match.SecondUserId == userId))
            .OrderByDescending(match => match.MatchedAt)
            .Select(match => match.FirstUserId == userId ? match.SecondUserId : match.FirstUserId)
            .ToListAsync(cancellationToken);

        var outgoingLikes = await database.Swipes.AsNoTracking()
            .Where(swipe => swipe.FromUserId == userId && swipe.IsLike
                && !database.Swipes.Any(reply => reply.FromUserId == swipe.ToUserId
                    && reply.ToUserId == userId && !reply.IsLike))
            .OrderByDescending(swipe => swipe.CreatedAt)
            .Select(swipe => swipe.ToUserId)
            .ToListAsync(cancellationToken);

        var incomingLikes = await database.Swipes.AsNoTracking()
            .Where(swipe => swipe.ToUserId == userId && swipe.IsLike
                && !database.Swipes.Any(reply => reply.FromUserId == userId
                    && reply.ToUserId == swipe.FromUserId && !reply.IsLike))
            .OrderByDescending(swipe => swipe.CreatedAt)
            .Select(swipe => swipe.FromUserId)
            .ToListAsync(cancellationToken);

        var endedMatchUserIds = await database.Matches.AsNoTracking()
            .Where(match => match.EndedAt != null && (match.FirstUserId == userId || match.SecondUserId == userId))
            .Select(match => match.FirstUserId == userId ? match.SecondUserId : match.FirstUserId)
            .ToHashSetAsync(cancellationToken);
        var contactIds = matchedUserIds.Concat(outgoingLikes).Concat(incomingLikes)
            .Where(contactId => !endedMatchUserIds.Contains(contactId))
            .Distinct().ToArray();
        if (contactIds.Length == 0) return [];

        var profiles = await database.Users.AsNoTracking()
            .Where(user => contactIds.Contains(user.Id))
            .Include(user => user.Photos)
            .Include(user => user.Interests)
                .ThenInclude(userInterest => userInterest.Interest)
            .AsSplitQuery()
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        return contactIds
            .Where(contactId => profiles.ContainsKey(contactId))
            .Select(contactId => ToProfile(profiles[contactId]))
            .ToArray();
    }

    public async Task<ChatMessagesResult> GetMessagesAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken)
    {
        if (!await CanChatAsync(userId, otherUserId, cancellationToken))
        {
            return new ChatMessagesResult(ChatOperationStatus.NotMatched, []);
        }

        var readAt = DateTimeOffset.UtcNow;
        await database.ChatMessages
            .Where(message => message.SenderId == otherUserId && message.RecipientId == userId && message.ReadAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(message => message.ReadAt, readAt), cancellationToken);

        var messages = await database.ChatMessages
            .AsNoTracking()
            .Include(message => message.Sender)
            .Where(message =>
                (message.SenderId == userId && message.RecipientId == otherUserId)
                || (message.SenderId == otherUserId && message.RecipientId == userId))
            .Where(message => !database.HiddenChatMessages.Any(hidden => hidden.MessageId == message.Id && hidden.UserId == userId))
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
            await NotificationWriter.AddAsync(database, otherUserId, "Chat", "Tin nhắn mới", "Bạn có tin nhắn mới.", message.Id, cancellationToken);
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

        if (message?.MediaFileName is null || message.MediaType is null || message.RevokedAt is not null || message.IsModerated)
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

    public async Task<bool> HideMessageAsync(Guid userId, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await database.ChatMessages.AsNoTracking().SingleOrDefaultAsync(candidate =>
            candidate.Id == messageId && (candidate.SenderId == userId || candidate.RecipientId == userId), cancellationToken);
        if (message is null) return false;
        var alreadyHidden = await database.HiddenChatMessages.AnyAsync(
            item => item.MessageId == messageId && item.UserId == userId, cancellationToken);
        if (!alreadyHidden)
        {
            database.HiddenChatMessages.Add(new HiddenChatMessage { MessageId = messageId, UserId = userId });
            await database.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public async Task<bool> RecallMessageAsync(Guid userId, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await database.ChatMessages.SingleOrDefaultAsync(
            candidate => candidate.Id == messageId && candidate.SenderId == userId && candidate.RevokedAt == null,
            cancellationToken);
        if (message is null || message.SentAt < DateTimeOffset.UtcNow.AddMinutes(-15)) return false;

        if (message.MediaFileName is not null)
        {
            var path = Path.Combine(GetMessageDirectory(), Path.GetFileName(message.MediaFileName));
            if (File.Exists(path)) File.Delete(path);
        }
        message.Content = null;
        message.MediaFileName = null;
        message.MediaType = null;
        message.RevokedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> CanChatAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        if (userId == otherUserId)
        {
            return false;
        }

        if (await database.UserBlocks.AnyAsync(block =>
            (block.BlockerUserId == userId && block.BlockedUserId == otherUserId)
            || (block.BlockerUserId == otherUserId && block.BlockedUserId == userId), cancellationToken))
        {
            return false;
        }

        if (await database.Matches.AnyAsync(match => match.EndedAt != null
            && ((match.FirstUserId == userId && match.SecondUserId == otherUserId)
                || (match.FirstUserId == otherUserId && match.SecondUserId == userId)), cancellationToken))
        {
            return false;
        }

        if (await database.Matches.AnyAsync(match =>
            match.EndedAt == null && ((match.FirstUserId == userId && match.SecondUserId == otherUserId)
            || (match.FirstUserId == otherUserId && match.SecondUserId == userId)), cancellationToken))
        {
            return true;
        }

        var pendingOutgoingLike = await database.Swipes.AnyAsync(swipe =>
            swipe.FromUserId == userId && swipe.ToUserId == otherUserId && swipe.IsLike
            && !database.Swipes.Any(reply => reply.FromUserId == otherUserId
                && reply.ToUserId == userId && !reply.IsLike), cancellationToken);
        if (pendingOutgoingLike) return true;

        return await database.Swipes.AnyAsync(swipe =>
            swipe.FromUserId == otherUserId && swipe.ToUserId == userId && swipe.IsLike
            && !database.Swipes.Any(reply => reply.FromUserId == userId
                && reply.ToUserId == otherUserId && !reply.IsLike), cancellationToken);
    }

    private static UserProfileResponse ToProfile(User user) => new(
        user.Id,
        user.Name,
        user.HideAge ? null : user.BirthDate,
        user.Bio,
        user.Gender,
        user.HideCity ? null : user.City,
        user.HideOccupation ? null : user.Occupation,
        user.HideEducation ? null : user.Education,
        user.LookingFor,
        user.Photos.Select(photo => new ProfilePhotoResponse(
            photo.Id, photo.Url, photo.IsAvatar, photo.SortOrder))
            .OrderByDescending(photo => photo.IsAvatar)
            .ThenBy(photo => photo.SortOrder)
            .ToArray(),
        user.Interests.Select(userInterest => new InterestResponse(
            userInterest.InterestId, userInterest.Interest.Name))
            .OrderBy(interest => interest.Name)
            .ToArray(),
        user.CreatedAt,
        IsOnline: user.LastSeenAt >= DateTimeOffset.UtcNow.AddMinutes(-5));

    private string GetMessageDirectory() =>
        Path.Combine(environment.ContentRootPath, "App_Data", "chat");

    private static ChatMessageResponse ToResponse(ChatMessage message, Guid currentUserId) => new(
        message.Id,
        message.SenderId,
        message.Sender.Name,
        message.IsModerated ? "Tin nhắn đã bị ẩn do vi phạm quy định." : message.RevokedAt is not null ? "Tin nhắn đã được thu hồi." : message.Content,
        message.IsModerated || message.RevokedAt is not null || message.MediaFileName is null ? null : $"/api/chat/media/{message.Id}",
        message.MediaType,
        message.SentAt,
        message.SenderId == currentUserId,
        message.SenderId == currentUserId || message.ReadAt is not null,
        message.RevokedAt is not null || message.IsModerated,
        message.SenderId == currentUserId && message.RevokedAt is null && !message.IsModerated && message.SentAt >= DateTimeOffset.UtcNow.AddMinutes(-15));

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
