namespace KissAppTussing.Models;

internal sealed class UserBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlockerUserId { get; set; }
    public Guid BlockedUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
