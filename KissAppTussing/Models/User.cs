namespace KissAppTussing.Models;

internal sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public DateOnly BirthDate { get; set; }
    public string Bio { get; set; } = string.Empty;
    public string Gender { get; set; } = "PreferNotToSay";
    public string? City { get; set; }
    public string? Occupation { get; set; }
    public string? Education { get; set; }
    public string LookingFor { get; set; } = "Dating";
    public string Role { get; set; } = "User";
    public DateTimeOffset? TermsAcceptedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool HideAge { get; set; }
    public bool HideCity { get; set; }
    public bool HideOccupation { get; set; }
    public bool HideEducation { get; set; }
    public bool IsIncognito { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public bool NotificationsLikeEnabled { get; set; } = true;
    public bool NotificationsMatchEnabled { get; set; } = true;
    public bool NotificationsChatEnabled { get; set; } = true;
    public bool NotificationsCommentEnabled { get; set; } = true;
    public bool NotificationsDateEnabled { get; set; } = true;
    public bool IsSuspended { get; set; }
    public ICollection<UserInterest> Interests { get; set; } = [];
    public ICollection<ProfilePhoto> Photos { get; set; } = [];
}
