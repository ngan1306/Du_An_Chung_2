namespace KissAppTussing.Models;

internal sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Topic { get; set; }
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public Guid OwnerId { get; set; }
    public bool IsPrivate { get; set; }
    public string? InviteCode { get; set; }
    public int MaxMembers { get; set; } = 50;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
