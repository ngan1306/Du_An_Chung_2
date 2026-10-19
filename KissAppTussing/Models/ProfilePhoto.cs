namespace KissAppTussing.Models;

internal sealed class ProfilePhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Url { get; set; }
    public bool IsAvatar { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}