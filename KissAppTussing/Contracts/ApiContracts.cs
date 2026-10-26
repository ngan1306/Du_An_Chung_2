using System.ComponentModel.DataAnnotations;

namespace KissAppTussing.Contracts;

/// <summary>Payload for creating a dating app account.</summary>
public sealed record RegisterRequest
{
    /// <summary>The display name shown on the user's profile.</summary>
    [Required, StringLength(80, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>The account email address.</summary>
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    /// <summary>A password containing at least eight characters.</summary>
    [Required, MinLength(8), StringLength(128)]
    public required string Password { get; init; }

    /// <summary>The user's date of birth.</summary>
    public required DateOnly BirthDate { get; init; }

    /// <summary>The user's gender.</summary>
    [Required, RegularExpression("^(Woman|Man|NonBinary|PreferNotToSay)$")]
    public required string Gender { get; init; }

    /// <summary>Whether the user accepted the terms of service.</summary>
    public bool AcceptTerms { get; init; }

    /// <summary>A short introduction shown on the profile.</summary>
    [StringLength(500)]
    public string Bio { get; init; } = string.Empty;
}

/// <summary>Payload for signing in to an existing account.</summary>
public sealed record LoginRequest
{
    /// <summary>The account email address.</summary>
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    /// <summary>The account password.</summary>
    [Required, StringLength(128)]
    public required string Password { get; init; }
}

/// <summary>Payload for requesting a password reset message.</summary>
public sealed record ForgotPasswordRequest
{
    /// <summary>The account email address.</summary>
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }
}

/// <summary>A generic response that does not reveal whether an email is registered.</summary>
public sealed record ForgotPasswordResponse(string Message);

/// <summary>Payload for setting a new password with a one-time reset token.</summary>
public sealed record ResetPasswordRequest
{
    /// <summary>The one-time token from the reset email.</summary>
    [Required, StringLength(128)]
    public required string Token { get; init; }

    /// <summary>The replacement password.</summary>
    [Required, MinLength(8), StringLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>Payload for updating the signed-in user's profile.</summary>
public sealed record UpdateProfileRequest
{
    /// <summary>The display name shown on the profile.</summary>
    [Required, StringLength(80, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>The user's date of birth.</summary>
    public required DateOnly BirthDate { get; init; }

    /// <summary>The user's gender.</summary>
    [Required, RegularExpression("^(Woman|Man|NonBinary|PreferNotToSay)$")]
    public required string Gender { get; init; }

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

    /// <summary>A short introduction shown on the profile.</summary>
    [StringLength(500)]
    public string Bio { get; init; } = string.Empty;
}

/// <summary>Interest identifiers selected for the signed-in user's profile.</summary>
public sealed record UpdateInterestsRequest
{
    /// <summary>Up to twenty selected interest identifiers.</summary>
    [Required, MaxLength(20)]
    public required IReadOnlyList<Guid> InterestIds { get; init; }
}

/// <summary>Payload for liking or passing on another profile.</summary>
public sealed record CreateSwipeRequest
{
    /// <summary>The profile being viewed.</summary>
    public required Guid TargetUserId { get; init; }

    /// <summary>True to like the profile; false to pass.</summary>
    public bool IsLike { get; init; }
}

/// <summary>Public profile information visible to other users.</summary>
/// <param name="Id">The profile identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="BirthDate">The user's date of birth.</param>
/// <param name="Bio">The user's introduction.</param>
/// <param name="Gender">The user's gender.</param>
/// <param name="City">The user's city.</param>
/// <param name="Occupation">The user's occupation.</param>
/// <param name="Education">The user's education.</param>
/// <param name="LookingFor">The kind of connection sought.</param>
/// <param name="Photos">The user's profile photos.</param>
/// <param name="Interests">The user's selected interests.</param>
/// <param name="CreatedAt">When the account was created.</param>
public sealed record UserProfileResponse(
    Guid Id,
    string Name,
    DateOnly BirthDate,
    string Bio,
    string Gender,
    string? City,
    string? Occupation,
    string? Education,
    string LookingFor,
    IReadOnlyList<ProfilePhotoResponse> Photos,
    IReadOnlyList<InterestResponse> Interests,
    DateTimeOffset CreatedAt);

/// <summary>A photo shown on a dating profile.</summary>
/// <param name="Id">The photo identifier.</param>
/// <param name="Url">The public photo URL.</param>
/// <param name="IsAvatar">Whether the photo is the profile avatar.</param>
/// <param name="SortOrder">The display order.</param>
public sealed record ProfilePhotoResponse(Guid Id, string Url, bool IsAvatar, int SortOrder);

/// <summary>An interest available to users.</summary>
/// <param name="Id">The interest identifier.</param>
/// <param name="Name">The interest name.</param>
public sealed record InterestResponse(Guid Id, string Name);

/// <summary>Authentication token and the signed-in user's profile.</summary>
/// <param name="AccessToken">The bearer token used for authenticated requests.</param>
/// <param name="TokenType">The token type, always Bearer.</param>
/// <param name="ExpiresAt">When the token expires.</param>
/// <param name="Profile">The signed-in user's public profile.</param>
public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    UserProfileResponse Profile);

/// <summary>Result of liking or passing on a profile.</summary>
/// <param name="TargetUserId">The profile that was viewed.</param>
/// <param name="IsLike">Whether the profile was liked.</param>
/// <param name="IsMatch">Whether this action created a mutual match.</param>
/// <param name="MatchId">The new match identifier, when a match was created.</param>
public sealed record SwipeResponse(
    Guid TargetUserId,
    bool IsLike,
    bool IsMatch,
    Guid? MatchId);

/// <summary>A match and the other user's profile.</summary>
/// <param name="Id">The match identifier.</param>
/// <param name="Profile">The other matched user's profile.</param>
/// <param name="MatchedAt">When the mutual match was created.</param>
public sealed record MatchResponse(
    Guid Id,
    UserProfileResponse Profile,
    DateTimeOffset MatchedAt);

/// <summary>A private message sent between two matched users.</summary>
/// <param name="Id">The message identifier.</param>
/// <param name="SenderId">The sender's user identifier.</param>
/// <param name="SenderName">The sender's display name.</param>
/// <param name="Content">The optional text content.</param>
/// <param name="MediaUrl">The authenticated media URL, when an attachment exists.</param>
/// <param name="MediaType">The validated image or video MIME type.</param>
/// <param name="SentAt">When the message was sent.</param>
/// <param name="IsMine">Whether the message belongs to the signed-in user.</param>
public sealed record ChatMessageResponse(
    Guid Id,
    Guid SenderId,
    string SenderName,
    string? Content,
    string? MediaUrl,
    string? MediaType,
    DateTimeOffset SentAt,
    bool IsMine);

/// <summary>A journey post shown in the authenticated feed.</summary>
/// <param name="Id">The post identifier.</param>
/// <param name="UserId">The author's user identifier.</param>
/// <param name="AuthorName">The author's display name.</param>
/// <param name="AuthorPhotoUrl">The author's profile photo URL, if present.</param>
/// <param name="Content">The text of the post.</param>
/// <param name="MediaUrl">The uploaded image or video URL, if present.</param>
/// <param name="MediaType">The validated MIME type, if media is present.</param>
/// <param name="CreatedAt">When the post was published.</param>
/// <param name="IsMine">Whether the post belongs to the current user.</param>
/// <param name="LikesCount">The number of likes on the post.</param>
/// <param name="CommentsCount">The number of comments on the post.</param>
/// <param name="IsLikedByMe">Whether the current user liked the post.</param>
public sealed record JourneyPostResponse(
    Guid Id,
    Guid UserId,
    string AuthorName,
    string? AuthorPhotoUrl,
    string Content,
    string? MediaUrl,
    string? MediaType,
    DateTimeOffset CreatedAt,
    bool IsMine,
    int LikesCount,
    int CommentsCount,
    bool IsLikedByMe);

/// <summary>A like action on a journey post.</summary>
/// <param name="PostId">The post that was liked or unliked.</param>
/// <param name="Liked">Whether the current user now likes the post.</param>
/// <param name="LikesCount">Current like count for the post.</param>
public sealed record JourneyLikeResponse(
    Guid PostId,
    bool Liked,
    int LikesCount);

/// <summary>The content of a comment on a journey post.</summary>
/// <param name="Id">The comment identifier.</param>
/// <param name="PostId">The parent journey post.</param>
/// <param name="UserId">The comment author.</param>
/// <param name="AuthorName">The display name of the author.</param>
/// <param name="Content">Comment text.</param>
/// <param name="CreatedAt">When the comment was written.</param>
public sealed record JourneyCommentResponse(
    Guid Id,
    Guid PostId,
    Guid UserId,
    string AuthorName,
    string Content,
    DateTimeOffset CreatedAt);

/// <summary>A comment creation request.</summary>
/// <param name="Content">Comment text.</param>
public sealed record CreateJourneyCommentRequest(string Content);
