using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;

using KissAppTussing.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace KissAppTussing.Services;

internal interface IRoomService
{
    Task<IReadOnlyList<ChatRoomResponse>> GetRoomsAsync(Guid userId, CancellationToken cancellationToken);
    Task<(string Status, ChatRoomResponse? Room)> CreateAsync(Guid userId, CreateRoomRequest request, CancellationToken cancellationToken);
    Task<string> InviteAsync(Guid userId, Guid roomId, Guid targetUserId, CancellationToken cancellationToken);
    Task<(string Status, ChatRoomResponse? Room)> JoinAsync(Guid userId, Guid roomId, CancellationToken cancellationToken);
    Task<string> LeaveAsync(Guid userId, Guid roomId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RoomMessageResponse>?> GetMessagesAsync(Guid userId, Guid roomId, CancellationToken cancellationToken);
    Task<(string Status, RoomMessageResponse? Message)> SendMessageAsync(Guid userId, Guid roomId, string content, CancellationToken cancellationToken);
    Task<IReadOnlyList<CallHistoryResponse>> GetCallHistoryAsync(Guid userId, CancellationToken cancellationToken);
}

internal sealed class RoomService(DatingDbContext database, IHubContext<ChatHub> hubContext) : IRoomService
{
    public async Task<IReadOnlyList<ChatRoomResponse>> GetRoomsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rooms = await database.ChatRooms.AsNoTracking()
            .Include(room => room.Creator)
            .Include(room => room.Members).ThenInclude(member => member.User)
            .Include(room => room.Messages.OrderByDescending(message => message.SentAt).Take(1))
            .Where(room => room.Members.Any(member => member.UserId == userId))
            .OrderByDescending(room => room.UpdatedAt)
            .ToListAsync(cancellationToken);
        return rooms.Select(room => ToResponse(room, userId)).ToArray();
    }

    public async Task<(string Status, ChatRoomResponse? Room)> CreateAsync(
        Guid userId, CreateRoomRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length < 2 || name.Length > 80) return ("Invalid", null);

        var memberIds = request.MemberUserIds.Distinct().Where(id => id != userId).ToArray();
        var acceptedFriends = await database.ConnectionRequests.AsNoTracking()
            .Where(connection => connection.Status == "Accepted"
                && ((connection.FirstUserId == userId && memberIds.Contains(connection.SecondUserId))
                    || (connection.SecondUserId == userId && memberIds.Contains(connection.FirstUserId))))
            .Select(connection => connection.FirstUserId == userId ? connection.SecondUserId : connection.FirstUserId)
            .ToListAsync(cancellationToken);
        if (memberIds.Any(id => !acceptedFriends.Contains(id))) return ("NotFriends", null);

        var creator = await database.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (creator is null) return ("Missing", null);

        var room = new ChatRoom { Name = name, CreatorUserId = userId, Creator = creator, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        room.Members.Add(new ChatRoomMember { RoomId = room.Id, UserId = userId, User = creator, Role = "Owner", JoinedAt = DateTimeOffset.UtcNow });
        foreach (var friendId in memberIds)
        {
            var friend = await database.Users.SingleAsync(user => user.Id == friendId, cancellationToken);
            room.Members.Add(new ChatRoomMember { RoomId = room.Id, UserId = friendId, User = friend, Role = "Member", JoinedAt = DateTimeOffset.UtcNow });
            await NotificationWriter.AddAsync(database, friendId, "Chat", "Được thêm vào phòng chat", $"{creator.Name} đã thêm bạn vào phòng \"{room.Name}\".", room.Id, cancellationToken);
        }
        database.ChatRooms.Add(room);
        await database.SaveChangesAsync(cancellationToken);
        return ("Created", ToResponse(room, userId));
    }

    public async Task<string> InviteAsync(Guid userId, Guid roomId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var room = await database.ChatRooms.Include(item => item.Members).ThenInclude(member => member.User)
            .SingleOrDefaultAsync(item => item.Id == roomId, cancellationToken);
        if (room is null) return "Missing";
        var inviter = room.Members.SingleOrDefault(member => member.UserId == userId);
        if (inviter is null) return "Forbidden";
        if (room.Members.Any(member => member.UserId == targetUserId)) return "Duplicate";

        var friends = await database.ConnectionRequests.AsNoTracking().Where(connection => connection.Status == "Accepted"
                && ((connection.FirstUserId == userId && connection.SecondUserId == targetUserId)
                    || (connection.SecondUserId == userId && connection.FirstUserId == targetUserId)))
            .AnyAsync(cancellationToken);
        if (!friends) return "NotFriends";

        var target = await database.Users.SingleOrDefaultAsync(user => user.Id == targetUserId, cancellationToken);
        if (target is null) return "Missing";
        room.Members.Add(new ChatRoomMember { RoomId = roomId, UserId = targetUserId, User = target, Role = "Member", JoinedAt = DateTimeOffset.UtcNow });
        room.UpdatedAt = DateTimeOffset.UtcNow;
        await NotificationWriter.AddAsync(database, targetUserId, "Chat", "Được mời vào phòng chat", $"{inviter.User.Name} đã mời bạn vào phòng \"{room.Name}\".", roomId, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return "Invited";
    }

    public async Task<(string Status, ChatRoomResponse? Room)> JoinAsync(Guid userId, Guid roomId, CancellationToken cancellationToken)
    {
        var room = await database.ChatRooms.Include(item => item.Creator).Include(item => item.Members).ThenInclude(member => member.User)
            .SingleOrDefaultAsync(item => item.Id == roomId, cancellationToken);
        if (room is null) return ("Missing", null);
        var member = room.Members.SingleOrDefault(item => item.UserId == userId);
        if (member is null) return ("Forbidden", null);
        return ("Joined", ToResponse(room, userId));
    }

    public async Task<string> LeaveAsync(Guid userId, Guid roomId, CancellationToken cancellationToken)
    {
        var room = await database.ChatRooms.Include(item => item.Members).ThenInclude(member => member.User)
            .SingleOrDefaultAsync(item => item.Id == roomId, cancellationToken);
        if (room is null) return "Missing";
        var member = room.Members.SingleOrDefault(item => item.UserId == userId);
        if (member is null) return "Forbidden";
        room.Members.Remove(member);
        room.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return "Left";
    }

    public async Task<IReadOnlyList<RoomMessageResponse>?> GetMessagesAsync(Guid userId, Guid roomId, CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(userId, roomId, cancellationToken)) return null;
        var messages = await database.ChatRoomMessages.AsNoTracking()
            .Include(message => message.Sender)
            .Where(message => message.RoomId == roomId)
            .OrderBy(message => message.SentAt)
            .Take(200)
            .Select(message => new RoomMessageResponse(message.Id, message.RoomId, message.SenderId, message.Sender.Name, message.Content, message.SentAt))
            .ToListAsync(cancellationToken);
        return messages;
    }

    public async Task<(string Status, RoomMessageResponse? Message)> SendMessageAsync(
        Guid userId, Guid roomId, string content, CancellationToken cancellationToken)
    {
        var text = content?.Trim() ?? string.Empty;
        if (text.Length is 0 or > 2000) return ("Invalid", null);
        if (!await IsMemberAsync(userId, roomId, cancellationToken)) return ("Forbidden", null);
        var room = await database.ChatRooms.SingleOrDefaultAsync(item => item.Id == roomId, cancellationToken);
        if (room is null) return ("Missing", null);
        var senderName = await database.Users.AsNoTracking().Where(user => user.Id == userId).Select(user => user.Name).SingleOrDefaultAsync(cancellationToken);
        var message = new ChatRoomMessage { RoomId = roomId, SenderId = userId, Content = text, SentAt = DateTimeOffset.UtcNow };
        database.ChatRoomMessages.Add(message);
        room.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        var response = new RoomMessageResponse(message.Id, roomId, userId, senderName ?? string.Empty, text, message.SentAt);
        await hubContext.Clients.Group(ChatHub.RoomGroup(roomId)).SendAsync("RoomMessage", response, cancellationToken);
        return ("Created", response);
    }

    public async Task<IReadOnlyList<CallHistoryResponse>> GetCallHistoryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var calls = await database.CallSessions.AsNoTracking()
            .Include(call => call.Caller)
            .Include(call => call.Recipient)
            .Include(call => call.Room)
            .Include(call => call.Participants).ThenInclude(participant => participant.User)
            .Where(call => call.CallerUserId == userId
                || call.RecipientUserId == userId
                || call.Participants.Any(participant => participant.UserId == userId))
            .OrderByDescending(call => call.StartedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        return calls.Select(call => new CallHistoryResponse(
            call.Id,
            call.CallType,
            call.Status,
            call.CallerUserId,
            call.Caller.Name,
            call.RecipientUserId,
            call.Recipient?.Name,
            call.RoomId,
            call.Room?.Name,
            call.StartedAt,
            call.AnsweredAt,
            call.EndedAt,
            call.Participants.Select(participant => new CallParticipantResponse(participant.UserId, participant.User.Name, participant.Status, participant.LeftAt)).ToArray())).ToArray();
    }

    private async Task<bool> IsMemberAsync(Guid userId, Guid roomId, CancellationToken cancellationToken) =>
        await database.ChatRoomMembers.AsNoTracking().AnyAsync(member => member.RoomId == roomId && member.UserId == userId, cancellationToken);

    private static ChatRoomResponse ToResponse(ChatRoom room, Guid userId) => new(
        room.Id,
        room.Name,
        room.CreatorUserId,
        room.Creator.Name,
        room.CreatorUserId == userId,
        room.CreatedAt,
        room.Members
            .OrderBy(member => member.JoinedAt)
            .Select(member => new RoomMemberResponse(member.UserId, member.User.Name, member.Role, member.User.LastSeenAt > DateTimeOffset.UtcNow.AddMinutes(-3)))
            .ToArray(),
        room.Messages.OrderByDescending(message => message.SentAt).Select(message => message.Content).FirstOrDefault(),
        room.UpdatedAt);
}
