namespace KissAppTussing.Models;

internal sealed class JourneyPostLike
{
    public Guid PostId { get; set; }
    public JourneyPost Post { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
