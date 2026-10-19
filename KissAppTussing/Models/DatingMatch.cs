namespace KissAppTussing.Models;

internal sealed class DatingMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirstUserId { get; set; }
    public Guid SecondUserId { get; set; }
    public User FirstUser { get; set; } = null!;
    public User SecondUser { get; set; } = null!;
    public DateTimeOffset MatchedAt { get; set; } = DateTimeOffset.UtcNow;
}