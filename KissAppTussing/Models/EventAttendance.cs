namespace KissAppTussing.Models;

internal sealed class EventAttendance
{
    public Guid EventId { get; set; }
    public CommunityEvent Event { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}
