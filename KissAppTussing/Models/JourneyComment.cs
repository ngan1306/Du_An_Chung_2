namespace KissAppTussing.Models;

internal sealed class JourneyComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public JourneyPost Post { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsHidden { get; set; }
    public Guid? ParentCommentId { get; set; }
    public JourneyComment? ParentComment { get; set; }
    public ICollection<JourneyComment> Replies { get; set; } = [];
}
