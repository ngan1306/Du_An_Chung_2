using KissAppTussing.Models;

namespace KissAppTussing.Models;

/// <summary>A group chat room; RoomType is "Group". Private 1-1 chats keep using Conversation.</summary>
internal sealed class ChatRoom
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoomType { get; set; } = "Group";
    public Guid CreatorUserId { get; set; }
    public User Creator { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<ChatRoomMember> Members { get; set; } = [];
    public ICollection<ChatRoomMessage> Messages { get; set; } = [];
}

/// <summary>Membership of a user inside a chat room with a role (Owner/Member).</summary>
internal sealed class ChatRoomMember
{
    public Guid RoomId { get; set; }
    public ChatRoom Room { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Role { get; set; } = "Member";
    public DateTimeOffset JoinedAt { get; set; }
}

/// <summary>A message posted inside a chat room.</summary>
internal sealed class ChatRoomMessage
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public ChatRoom Room { get; set; } = null!;
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
}

/// <summary>A voice or video call between two users or inside a chat room.</summary>
internal sealed class CallSession
{
    public Guid Id { get; set; }
    public string CallType { get; set; } = "Voice";
    public string Status { get; set; } = "Ringing";
    public Guid CallerUserId { get; set; }
    public User Caller { get; set; } = null!;
    public Guid? RecipientUserId { get; set; }
    public User? Recipient { get; set; }
    public Guid? RoomId { get; set; }
    public ChatRoom? Room { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public ICollection<CallParticipant> Participants { get; set; } = [];
}

/// <summary>A user that joined (or was invited to) a call.</summary>
internal sealed class CallParticipant
{
    public Guid CallId { get; set; }
    public CallSession Call { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Status { get; set; } = "Invited";
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
}
