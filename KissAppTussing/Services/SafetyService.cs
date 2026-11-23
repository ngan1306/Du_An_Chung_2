using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Services;

internal sealed class SafetyService(DatingDbContext database)
{
    public async Task<string> BlockAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        if (userId == otherUserId) return "Self";
        if (!await database.Users.AnyAsync(user => user.Id == otherUserId, cancellationToken)) return "Missing";
        var alreadyBlocked = await database.UserBlocks.AnyAsync(
            item => item.BlockerUserId == userId && item.BlockedUserId == otherUserId, cancellationToken);
        if (!alreadyBlocked)
        {
            database.UserBlocks.Add(new UserBlock { BlockerUserId = userId, BlockedUserId = otherUserId });
            await database.SaveChangesAsync(cancellationToken);
        }
        return "Created";
    }

    public async Task<bool> UnblockAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        var block = await database.UserBlocks.SingleOrDefaultAsync(
            item => item.BlockerUserId == userId && item.BlockedUserId == otherUserId, cancellationToken);
        if (block is null) return false;
        database.UserBlocks.Remove(block);
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<BlockedUserResponse>> GetBlockedAsync(Guid userId, CancellationToken cancellationToken) =>
        await database.UserBlocks.AsNoTracking()
            .Where(item => item.BlockerUserId == userId)
            .Join(database.Users, block => block.BlockedUserId, user => user.Id,
                (block, user) => new BlockedUserResponse(user.Id, user.Name, block.CreatedAt))
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

    public async Task<(string Status, ContentReportResponse? Report)> ReportAsync(
        Guid userId,
        string targetType,
        Guid targetId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) return ("Invalid", null);
        var exists = targetType switch
        {
            "User" => targetId != userId && await database.Users.AnyAsync(user => user.Id == targetId, cancellationToken),
            "Post" => await database.JourneyPosts.AnyAsync(post => post.Id == targetId, cancellationToken),
            "Comment" => await database.JourneyComments.AnyAsync(comment => comment.Id == targetId, cancellationToken),
            "Message" => await database.ChatMessages.AnyAsync(message => message.Id == targetId
                && message.SenderId != userId && message.RecipientId == userId, cancellationToken),
            _ => false
        };
        if (!exists) return ("Missing", null);

        var duplicate = await database.ContentReports.AnyAsync(report => report.ReporterUserId == userId
            && report.TargetType == targetType && report.TargetId == targetId && report.Status == "Pending", cancellationToken);
        if (duplicate) return ("Duplicate", null);

        var report = new ContentReport
        {
            ReporterUserId = userId,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason.Trim()
        };
        database.ContentReports.Add(report);
        await database.SaveChangesAsync(cancellationToken);
        return ("Created", new ContentReportResponse(report.Id, report.TargetType, report.TargetId, report.Reason, report.Status, report.CreatedAt));
    }
}

internal sealed record BlockedUserResponse(Guid UserId, string Name, DateTimeOffset CreatedAt);
