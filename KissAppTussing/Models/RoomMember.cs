namespace KissAppTussing.Models;

internal sealed class RoomMember
{
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public required string Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}
