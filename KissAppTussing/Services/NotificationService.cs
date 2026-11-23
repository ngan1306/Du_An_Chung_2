using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Services;

internal sealed class NotificationService(DatingDbContext database)
{
    public async Task<IReadOnlyList<UserNotificationResponse>> GetAsync(Guid userId, int limit, CancellationToken cancellationToken) =>
        await database.UserNotifications.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(item => new UserNotificationResponse(item.Id, item.Type, item.Title, item.Body, item.RelatedId, item.CreatedAt, item.ReadAt))
            .ToListAsync(cancellationToken);

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await database.UserNotifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.UserId == userId, cancellationToken);
        if (notification is null) return false;
        notification.ReadAt ??= DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await database.UserNotifications.Where(item => item.UserId == userId && item.ReadAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.ReadAt, now), cancellationToken);
    }

    public async Task<NotificationPreferencesResponse?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        return user is null ? null : ToPreferences(user);
    }

    public async Task<NotificationPreferencesResponse?> UpdatePreferencesAsync(
        Guid userId,
        NotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var user = await database.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null) return null;
        user.NotificationsLikeEnabled = request.LikeEnabled;
        user.NotificationsMatchEnabled = request.MatchEnabled;
        user.NotificationsChatEnabled = request.ChatEnabled;
        user.NotificationsCommentEnabled = request.CommentEnabled;
        user.NotificationsDateEnabled = request.DateEnabled;
        await database.SaveChangesAsync(cancellationToken);
        return ToPreferences(user);
    }

    private static NotificationPreferencesResponse ToPreferences(User user) => new(
        user.NotificationsLikeEnabled,
        user.NotificationsMatchEnabled,
        user.NotificationsChatEnabled,
        user.NotificationsCommentEnabled,
        user.NotificationsDateEnabled);
}

internal static class NotificationWriter
{
    public static async Task AddAsync(
        DatingDbContext database,
        Guid userId,
        string type,
        string title,
        string body,
        Guid? relatedId,
        CancellationToken cancellationToken)
    {
        var enabled = await database.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => type == "Like" || type == "SuperLike" ? user.NotificationsLikeEnabled
                : type == "Match" ? user.NotificationsMatchEnabled
                : type == "Chat" ? user.NotificationsChatEnabled
                : type == "Comment" ? user.NotificationsCommentEnabled
                : type == "Date" ? user.NotificationsDateEnabled
                : true)
            .SingleOrDefaultAsync(cancellationToken);
        if (!enabled) return;
        database.UserNotifications.Add(new UserNotification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            RelatedId = relatedId
        });
    }
}
