using KissAppTussing.Contracts;

namespace KissAppTussing.Services;

internal interface IAuthService
{
    Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

internal interface IDatingService
{
    Task<IReadOnlyList<UserProfileResponse>> DiscoverAsync(
        Guid userId,
        int limit,
        string? search,
        string? gender,
        int? minAge,
        int? maxAge,
        string? city,
        string? lookingFor,
        Guid? interestId,
        CancellationToken cancellationToken);
    Task<UserProfileResponse?> GetProfileAsync(Guid profileId, CancellationToken cancellationToken);
    Task<UserProfileResponse?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<InterestResponse>> GetInterestsAsync(CancellationToken cancellationToken);
    Task<bool> UpdateInterestsAsync(Guid userId, IReadOnlyList<Guid> interestIds, CancellationToken cancellationToken);
    Task<PhotoOperationResult> AddPhotoAsync(Guid userId, IFormFile file, CancellationToken cancellationToken);
    Task<PhotoOperationResult> SetAvatarAsync(Guid userId, Guid photoId, CancellationToken cancellationToken);
    Task<PhotoOperationResult> DeletePhotoAsync(Guid userId, Guid photoId, CancellationToken cancellationToken);
    Task<SwipeResult> SwipeAsync(Guid userId, CreateSwipeRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchResponse>> GetMatchesAsync(Guid userId, CancellationToken cancellationToken);
}

internal interface IJourneyService
{
    Task<IReadOnlyList<JourneyPostResponse>> GetFeedAsync(Guid userId, int limit, CancellationToken cancellationToken);
    Task<JourneyPostOperationResult> CreatePostAsync(Guid userId, string? content, IFormFile? media, CancellationToken cancellationToken);
    Task<JourneyPostOperationStatus> DeletePostAsync(Guid userId, Guid postId, CancellationToken cancellationToken);
    Task<JourneyLikeResponse> ToggleLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken);
    Task<IReadOnlyList<JourneyCommentResponse>> GetCommentsAsync(Guid userId, Guid postId, CancellationToken cancellationToken);
    Task<JourneyCommentResponse> AddCommentAsync(Guid userId, Guid postId, string content, CancellationToken cancellationToken);
}

internal interface IChatService
{
    Task<ChatMessagesResult> GetMessagesAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);
    Task<ChatMessageResult> SendMessageAsync(Guid userId, Guid otherUserId, string? content, IFormFile? media, CancellationToken cancellationToken);
    Task<ChatMediaResult> GetMediaAsync(Guid userId, Guid messageId, CancellationToken cancellationToken);
}

internal enum RegistrationStatus
{
    Created,
    EmailTaken,
    Underage,
    TermsNotAccepted
}

internal sealed record RegistrationResult(RegistrationStatus Status, AuthResponse? Response);

internal enum SwipeStatus
{
    Created,
    TargetNotFound,
    SelfSwipe,
    AlreadySwiped
}

internal sealed record SwipeResult(SwipeStatus Status, SwipeResponse? Response);

internal enum PhotoOperationStatus
{
    Created,
    Updated,
    Deleted,
    UserNotFound,
    PhotoNotFound,
    InvalidImage,
    PhotoLimitReached
}

internal sealed record PhotoOperationResult(PhotoOperationStatus Status, ProfilePhotoResponse? Photo = null);

internal enum JourneyPostOperationStatus
{
    Created,
    Deleted,
    NotFound,
    EmptyPost,
    UnsupportedMedia,
    MediaTooLarge
}

internal sealed record JourneyPostOperationResult(JourneyPostOperationStatus Status, JourneyPostResponse? Post = null);

internal enum ChatOperationStatus
{
    Success,
    NotMatched,
    EmptyMessage,
    ContentTooLong,
    UnsupportedMedia,
    MediaTooLarge,
    MediaNotFound
}

internal sealed record ChatMessagesResult(ChatOperationStatus Status, IReadOnlyList<ChatMessageResponse> Messages);
internal sealed record ChatMessageResult(ChatOperationStatus Status, ChatMessageResponse? Message = null);
internal sealed record ChatMediaResult(ChatOperationStatus Status, Stream? Stream = null, string? ContentType = null);
