namespace KissAppTussing.Contracts;

public sealed record CreateRoomRequest(
    string Name,
    string? Topic,
    string? Description,
    string? AvatarUrl,
    bool IsPrivate,
    string? InviteCode,
    int? MaxMembers);

public sealed record UpdateRoomRequest(
    string? Name,
    string? Topic,
    string? Description,
    string? AvatarUrl,
    bool? IsPrivate,
    string? InviteCode,
    int? MaxMembers);

public sealed record JoinRoomRequest(string? InviteCode);
public sealed record CreateReportRequest(Guid? TargetUserId, Guid? RoomId, Guid? MessageId, string Reason);

public sealed record RoomSummaryResponse(
    Guid Id,
    string Name,
    string? Topic,
    string? Description,
    string? AvatarUrl,
    Guid OwnerId,
    bool IsPrivate,
    int MemberCount,
    int MaxMembers,
    DateTimeOffset CreatedAt);

public sealed record RoomMemberResponse(
    Guid UserId,
    string Name,
    string? AvatarUrl,
    string Role,
    DateTimeOffset JoinedAt);

public sealed record RoomDetailsResponse(
    RoomSummaryResponse Room,
    IReadOnlyList<RoomMemberResponse> Members);

public sealed record RoomMessageResponse(
    Guid Id,
    Guid RoomId,
    Guid SenderId,
    string SenderName,
    string? SenderAvatarUrl,
    string Content,
    string? MediaUrl,
    DateTimeOffset CreatedAt);

public sealed record RoomMessagesResponse(
    IReadOnlyList<RoomMessageResponse> Messages,
    string? NextCursor);
