namespace KissAppTussing.Models;

internal sealed class DateReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvitationId { get; set; }
    public DateInvitation Invitation { get; set; } = null!;
    public Guid ReviewerUserId { get; set; }
    public User Reviewer { get; set; } = null!;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
