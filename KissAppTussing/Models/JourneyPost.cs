namespace KissAppTussing.Models;

internal sealed class JourneyPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<JourneyPostLike> Likes { get; set; } = [];
    public ICollection<JourneyComment> Comments { get; set; } = [];
}