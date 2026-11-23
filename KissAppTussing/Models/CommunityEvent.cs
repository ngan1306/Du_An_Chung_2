namespace KissAppTussing.Models;

internal sealed class CommunityEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CreatorUserId { get; set; }
    public User Creator { get; set; } = null!;
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public required string VenueName { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<EventAttendance> Attendees { get; set; } = [];
}
