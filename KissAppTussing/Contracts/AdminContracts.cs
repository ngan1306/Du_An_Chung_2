using System.ComponentModel.DataAnnotations;

namespace KissAppTussing.Contracts;

/// <summary>Aggregated data available to an authorized application owner.</summary>
public sealed record AdminOverviewResponse(
    int UserCount,
    int SwipeCount,
    int MatchCount,
    int JourneyPostCount,
    int JourneyCommentCount,
    int ChatMessageCount,
    IReadOnlyList<AdminUserSummary> RecentUsers);

/// <summary>A user row shown in the owner dashboard.</summary>
public sealed record AdminUserSummary(Guid Id, string Name, string Email, DateTimeOffset CreatedAt);

/// <summary>A user account visible to an authorized administrator.</summary>
public sealed record AdminManagedUser(
    Guid Id,
    string Name,
    string Email,
    DateOnly BirthDate,
    string Gender,
    string Bio,
    string? City,
    string? Occupation,
    string? Education,
    string LookingFor,
    DateTimeOffset CreatedAt);

/// <summary>Profile and login details for an administrator-created account.</summary>
public sealed record AdminCreateUserRequest
{
    /// <summary>The name displayed on the profile.</summary>
    [Required, StringLength(80, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>The account email address.</summary>
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    /// <summary>The initial password, at least eight characters.</summary>
    [Required, MinLength(8), StringLength(128)]
    public required string Password { get; init; }

    /// <summary>The user's date of birth.</summary>
    public required DateOnly BirthDate { get; init; }

    /// <summary>The user's gender identifier.</summary>
    [Required, RegularExpression("^(Woman|Man|NonBinary|PreferNotToSay)$")]
    public required string Gender { get; init; }

    /// <summary>A short introduction shown on the profile.</summary>
    [StringLength(500)]
    public string Bio { get; init; } = string.Empty;

    /// <summary>The user's city.</summary>
    [StringLength(100)]
    public string? City { get; init; }

    /// <summary>The user's occupation.</summary>
    [StringLength(120)]
    public string? Occupation { get; init; }

    /// <summary>The user's education.</summary>
    [StringLength(120)]
    public string? Education { get; init; }

    /// <summary>The kind of connection the user is looking for.</summary>
    [Required, RegularExpression("^(Friendship|Dating|Relationship)$")]
    public required string LookingFor { get; init; }
}

/// <summary>Updated account details; leave NewPassword empty to keep the current password.</summary>
public sealed record AdminUpdateUserRequest
{
    /// <summary>The name displayed on the profile.</summary>
    [Required, StringLength(80, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>The account email address.</summary>
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    /// <summary>An optional replacement password; must be at least eight characters when supplied.</summary>
    [StringLength(128)]
    public string? NewPassword { get; init; }

    /// <summary>The user's date of birth.</summary>
    public required DateOnly BirthDate { get; init; }

    /// <summary>The user's gender identifier.</summary>
    [Required, RegularExpression("^(Woman|Man|NonBinary|PreferNotToSay)$")]
    public required string Gender { get; init; }

    /// <summary>A short introduction shown on the profile.</summary>
    [StringLength(500)]
    public string Bio { get; init; } = string.Empty;

    /// <summary>The user's city.</summary>
    [StringLength(100)]
    public string? City { get; init; }

    /// <summary>The user's occupation.</summary>
    [StringLength(120)]
    public string? Occupation { get; init; }

    /// <summary>The user's education.</summary>
    [StringLength(120)]
    public string? Education { get; init; }

    /// <summary>The kind of connection the user is looking for.</summary>
    [Required, RegularExpression("^(Friendship|Dating|Relationship)$")]
    public required string LookingFor { get; init; }
}

/// <summary>Exportable application data. Password hashes and private chat contents are excluded.</summary>
public sealed record AdminDataExport(
    DateTimeOffset ExportedAt,
    IReadOnlyList<AdminUserExport> Users,
    IReadOnlyList<AdminInterestExport> Interests,
    IReadOnlyList<AdminPhotoExport> ProfilePhotos,
    IReadOnlyList<AdminSwipeExport> Swipes,
    IReadOnlyList<AdminMatchExport> Matches,
    IReadOnlyList<AdminJourneyPostExport> JourneyPosts,
    IReadOnlyList<AdminJourneyLikeExport> JourneyLikes,
    IReadOnlyList<AdminJourneyCommentExport> JourneyComments);

/// <summary>A user's profile and account registration details.</summary>
public sealed record AdminUserExport(
    Guid Id,
    string Name,
    string Email,
    DateOnly BirthDate,
    string Bio,
    string Gender,
    string? City,
    string? Occupation,
    string? Education,
    string LookingFor,
    DateTimeOffset? TermsAcceptedAt,
    DateTimeOffset CreatedAt);

/// <summary>An interest selected by a user.</summary>
public sealed record AdminInterestExport(Guid UserId, Guid InterestId, string InterestName);
/// <summary>A profile photo reference and its display settings.</summary>
public sealed record AdminPhotoExport(Guid Id, Guid UserId, string Url, bool IsAvatar, int SortOrder, DateTimeOffset CreatedAt);
/// <summary>A like or pass interaction between two users.</summary>
public sealed record AdminSwipeExport(Guid Id, Guid FromUserId, Guid ToUserId, bool IsLike, DateTimeOffset CreatedAt);
/// <summary>A mutual match between two users.</summary>
public sealed record AdminMatchExport(Guid Id, Guid FirstUserId, Guid SecondUserId, DateTimeOffset MatchedAt);
/// <summary>A public journey post and its media reference.</summary>
public sealed record AdminJourneyPostExport(Guid Id, Guid UserId, string Content, string? MediaUrl, string? MediaType, DateTimeOffset CreatedAt);
/// <summary>A like on a journey post.</summary>
public sealed record AdminJourneyLikeExport(Guid PostId, Guid UserId, DateTimeOffset CreatedAt);
/// <summary>A comment on a journey post.</summary>
public sealed record AdminJourneyCommentExport(Guid Id, Guid PostId, Guid UserId, string Content, DateTimeOffset CreatedAt);
