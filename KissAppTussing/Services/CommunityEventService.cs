using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Services;

internal sealed class CommunityEventService(DatingDbContext database)
{
    public async Task<IReadOnlyList<CommunityEventResponse>> GetAsync(Guid userId, int limit, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var events = await database.CommunityEvents.AsNoTracking()
            .Include(item => item.Creator)
            .Include(item => item.Attendees)
            .Where(item => item.StartsAt >= now && !item.Creator.IsSuspended
                && !database.UserBlocks.Any(block =>
                    (block.BlockerUserId == userId && block.BlockedUserId == item.CreatorUserId)
                    || (block.BlockerUserId == item.CreatorUserId && block.BlockedUserId == userId)))
            .OrderBy(item => item.StartsAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
        return events.Select(item => ToResponse(item, userId)).ToArray();
    }

    public async Task<CommunityEventResponse?> CreateAsync(Guid userId, CreateCommunityEventRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (request.StartsAt < now.AddMinutes(10) || request.StartsAt > now.AddYears(1)) return null;
        var creator = await database.Users.SingleOrDefaultAsync(user => user.Id == userId && !user.IsSuspended, cancellationToken);
        if (creator is null) return null;
        var item = new CommunityEvent
        {
            CreatorUserId = userId,
            Creator = creator,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            VenueName = request.VenueName.Trim(),
            StartsAt = request.StartsAt.ToUniversalTime()
        };
        database.CommunityEvents.Add(item);
        await database.SaveChangesAsync(cancellationToken);
        return ToResponse(item, userId);
    }

    public async Task<string> AttendAsync(Guid userId, Guid eventId, bool attend, CancellationToken cancellationToken)
    {
        var item = await database.CommunityEvents.Include(candidate => candidate.Attendees)
            .SingleOrDefaultAsync(candidate => candidate.Id == eventId && candidate.StartsAt > DateTimeOffset.UtcNow, cancellationToken);
        if (item is null) return "Missing";
        if (await database.UserBlocks.AnyAsync(block =>
            (block.BlockerUserId == userId && block.BlockedUserId == item.CreatorUserId)
            || (block.BlockerUserId == item.CreatorUserId && block.BlockedUserId == userId), cancellationToken)) return "Forbidden";
        var existing = item.Attendees.SingleOrDefault(row => row.UserId == userId);
        if (attend && existing is null) database.EventAttendances.Add(new EventAttendance { EventId = eventId, UserId = userId });
        if (!attend && existing is not null) database.EventAttendances.Remove(existing);
        await database.SaveChangesAsync(cancellationToken);
        return "Updated";
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var deleted = await database.CommunityEvents.Where(item => item.Id == eventId && item.CreatorUserId == userId && item.StartsAt > DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    private static CommunityEventResponse ToResponse(CommunityEvent item, Guid userId) => new(
        item.Id, item.CreatorUserId, item.Creator.Name, item.Name, item.Description, item.VenueName,
        item.StartsAt, item.Attendees.Count, item.Attendees.Any(row => row.UserId == userId), item.CreatorUserId == userId);
}
