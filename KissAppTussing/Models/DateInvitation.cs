namespace KissAppTussing.Models;

internal sealed class DateInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizerUserId { get; set; }
    public User Organizer { get; set; } = null!;
    public Guid RecipientUserId { get; set; }
    public User Recipient { get; set; } = null!;
    public required string VenueName { get; set; }
    public string? VenueAddress { get; set; }
    public string? VenueUrl { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReminderSentAt { get; set; }
    public ICollection<DateReview> Reviews { get; set; } = [];
}
