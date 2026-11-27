namespace KissAppTussing.Contracts;

/// <summary>Payload for creating a chat room with an optional initial member list.</summary>
public sealed record CreateRoomRequest
{
    /// <summary>Display name of the room (2-80 characters).</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>Friend user ids to invite immediately.</summary>
    public Guid[] MemberUserIds { get; init; } = [];
}

/// <summary>A chat room visible to one of its members.</summary>
public sealed record ChatRoomResponse(
    Guid Id,
    string Name,
    Guid CreatorUserId,
    string CreatorName,
    bool IsOwner,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RoomMemberResponse> Members,
    string? LastMessage,
    DateTimeOffset UpdatedAt);

/// <summary>A member of a chat room.</summary>
public sealed record RoomMemberResponse(Guid UserId, string Name, string Role, bool IsOnline);

/// <summary>A message posted in a chat room.</summary>
public sealed record RoomMessageResponse(Guid Id, Guid RoomId, Guid SenderId, string SenderName, string Content, DateTimeOffset SentAt);

/// <summary>Text submitted as a room message.</summary>
public sealed record SendRoomMessageRequest
{
    /// <summary>Message content (1-2000 characters).</summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>A voice/video call record shown in call history.</summary>
public sealed record CallHistoryResponse(
    Guid Id,
    string CallType,
    string Status,
    Guid CallerUserId,
    string CallerName,
    Guid? RecipientUserId,
    string? RecipientName,
    Guid? RoomId,
    string? RoomName,
    DateTimeOffset StartedAt,
    DateTimeOffset? AnsweredAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<CallParticipantResponse> Participants);

/// <summary>One participant of a call.</summary>
public sealed record CallParticipantResponse(Guid UserId, string Name, string Status, DateTimeOffset? LeftAt);

/// <summary>Payload for starting a call.</summary>
public sealed record StartCallRequest
{
    /// <summary>Voice or Video.</summary>
    public string CallType { get; init; } = "Voice";
    /// <summary>Target user for a 1-1 call.</summary>
    public Guid? RecipientUserId { get; init; }
    /// <summary>Target room for a group call.</summary>
    public Guid? RoomId { get; init; }
}
