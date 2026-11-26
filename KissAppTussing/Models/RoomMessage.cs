namespace KissAppTussing.Models;

internal sealed class RoomMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid SenderId { get; set; }
    public required string Content { get; set; }
    public string? MediaUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
