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
