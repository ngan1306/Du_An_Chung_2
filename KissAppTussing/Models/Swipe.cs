namespace KissAppTussing.Models;

internal sealed class Swipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public bool IsLike { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}