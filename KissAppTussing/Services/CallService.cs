using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using KissAppTussing.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Services;

internal interface ICallService
{
    Task<(string Status, CallHistoryResponse? Call)> StartAsync(Guid userId, StartCallRequest request, CancellationToken cancellationToken);
    Task<string> AnswerAsync(Guid userId, Guid callId, CancellationToken cancellationToken);
    Task<string> DeclineAsync(Guid userId, Guid callId, CancellationToken cancellationToken);
    Task<string> EndAsync(Guid userId, Guid callId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>?> MembersAsync(Guid userId, Guid callId, CancellationToken cancellationToken);
}

internal sealed class CallService(DatingDbContext database, IHubContext<CallHub> callHub) : ICallService
{
    public async Task<(string Status, CallHistoryResponse? Call)> StartAsync(
        Guid userId, StartCallRequest request, CancellationToken cancellationToken)
    {
        var callType = request.CallType is "Voice" or "Video" ? request.CallType : "Voice";
        var caller = await database.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (caller is null) return ("Missing", null);

        var call = new CallSession { CallType = callType, Status = "Ringing", CallerUserId = userId, StartedAt = DateTimeOffset.UtcNow };

        if (request.RoomId is { } roomId)
        {
            var isMember = await database.ChatRoomMembers.AsNoTracking()
                .AnyAsync(member => member.RoomId == roomId && member.UserId == userId, cancellationToken);
            if (!isMember) return ("Forbidden", null);
            call.RoomId = roomId;
            var memberIds = await database.ChatRoomMembers.AsNoTracking()
                .Where(member => member.RoomId == roomId)
                .Select(member => member.UserId)
                .ToListAsync(cancellationToken);
            foreach (var memberId in memberIds)
                call.Participants.Add(new CallParticipant { CallId = call.Id, UserId = memberId, Status = memberId == userId ? "Joined" : "Invited", JoinedAt = DateTimeOffset.UtcNow });
        }
        else if (request.RecipientUserId is { } recipientId)
        {
            if (recipientId == userId) return ("Invalid", null);
            var connected = await database.ConnectionRequests.AsNoTracking().AnyAsync(connection => connection.Status == "Accepted"
                && ((connection.FirstUserId == userId && connection.SecondUserId == recipientId)
                    || (connection.SecondUserId == userId && connection.FirstUserId == recipientId)), cancellationToken);
            if (!connected) return ("NotFriends", null);
            call.RecipientUserId = recipientId;
            call.Participants.Add(new CallParticipant { CallId = call.Id, UserId = userId, Status = "Joined", JoinedAt = DateTimeOffset.UtcNow });
            call.Participants.Add(new CallParticipant { CallId = call.Id, UserId = recipientId, Status = "Invited", JoinedAt = DateTimeOffset.UtcNow });
        }
        else return ("Invalid", null);

        database.CallSessions.Add(call);
        await database.SaveChangesAsync(cancellationToken);

        var memberUserIds = call.Participants.Select(participant => participant.UserId.ToString()).ToArray();
        await callHub.Clients.All.SendAsync("IncomingCall", new
        {
            CallId = call.Id,
            CallType = call.CallType,
            CallerUserId = userId,
            CallerName = caller.Name,
            RecipientUserId = call.RecipientUserId,
            RoomId = call.RoomId
        }, cancellationToken);

        var response = await ToHistoryAsync(call.Id, cancellationToken);
        return ("Created", response);
    }

    public async Task<string> AnswerAsync(Guid userId, Guid callId, CancellationToken cancellationToken)
    {
        var call = await database.CallSessions.Include(item => item.Participants)
            .SingleOrDefaultAsync(item => item.Id == callId, cancellationToken);
        if (call is null) return "Missing";
        var participant = call.Participants.SingleOrDefault(item => item.UserId == userId);
        if (participant is null) return "Forbidden";
        if (call.Status == "Ended") return "Ended";
        call.Status = "Active";
        call.AnsweredAt ??= DateTimeOffset.UtcNow;
        participant.Status = "Joined";
        await database.SaveChangesAsync(cancellationToken);
        return "Answered";
    }

    public async Task<string> DeclineAsync(Guid userId, Guid callId, CancellationToken cancellationToken)
    {
        var call = await database.CallSessions.Include(item => item.Participants)
            .SingleOrDefaultAsync(item => item.Id == callId, cancellationToken);
        if (call is null) return "Missing";
        var participant = call.Participants.SingleOrDefault(item => item.UserId == userId);
        if (participant is null) return "Forbidden";
        participant.Status = "Declined";
        participant.LeftAt = DateTimeOffset.UtcNow;
        if (call.Participants.All(item => item.Status is "Declined" or "Left")) MarkEnded(call, "Declined");
        await database.SaveChangesAsync(cancellationToken);
        return "Declined";
    }

    public async Task<string> EndAsync(Guid userId, Guid callId, CancellationToken cancellationToken)
    {
        var call = await database.CallSessions.Include(item => item.Participants)
            .SingleOrDefaultAsync(item => item.Id == callId, cancellationToken);
        if (call is null) return "Missing";
        var participant = call.Participants.SingleOrDefault(item => item.UserId == userId);
        if (participant is null) return "Forbidden";
        participant.Status = "Left";
        participant.LeftAt = DateTimeOffset.UtcNow;
        var otherActive = call.Participants.Any(item => item.UserId != userId && item.Status == "Joined");
        if (!otherActive) MarkEnded(call, "Completed");
        await database.SaveChangesAsync(cancellationToken);

        var memberUserIds = call.Participants.Select(participant2 => participant2.UserId.ToString()).ToArray();
        var groups = memberUserIds.Select(id => $"user:{id}").ToArray();
        await callHub.Clients.Groups(groups).SendAsync("CallEnded", new { CallId = callId }, cancellationToken);
        return "Ended";
    }

    public async Task<IReadOnlyList<Guid>?> MembersAsync(Guid userId, Guid callId, CancellationToken cancellationToken)
    {
        var call = await database.CallSessions.AsNoTracking().Include(item => item.Participants)
            .SingleOrDefaultAsync(item => item.Id == callId, cancellationToken);
        if (call is null || call.Participants.All(participant => participant.UserId != userId)) return null;
        return call.Participants.Select(participant => participant.UserId).ToArray();
    }

    private static void MarkEnded(CallSession call, string status)
    {
        call.Status = status;
        call.EndedAt ??= DateTimeOffset.UtcNow;
    }

    private async Task<CallHistoryResponse?> ToHistoryAsync(Guid callId, CancellationToken cancellationToken)
    {
        var call = await database.CallSessions.AsNoTracking()
            .Include(item => item.Caller)
            .Include(item => item.Recipient)
            .Include(item => item.Room)
            .Include(item => item.Participants).ThenInclude(participant => participant.User)
            .SingleOrDefaultAsync(item => item.Id == callId, cancellationToken);
        if (call is null) return null;
        return new CallHistoryResponse(
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
            call.Participants.Select(participant => new CallParticipantResponse(participant.UserId, participant.User.Name, participant.Status, participant.LeftAt)).ToArray());
    }
}
