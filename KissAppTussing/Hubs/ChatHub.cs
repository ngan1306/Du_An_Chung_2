using System.Security.Claims;
using KissAppTussing.Contracts;
using KissAppTussing.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Hubs;

/// <summary>Realtime chat hub: joins room groups, pushes room messages and online presence.</summary>
[Authorize]
internal sealed class ChatHub(DatingDbContext database) : Hub
{
    public static string UserGroup(Guid userId) => $"user:{userId}";
    public static string RoomGroup(Guid roomId) => $"room:{roomId}";

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId();
        if (userId is null) { Context.Abort(); return; }
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));
        var roomIds = await database.ChatRoomMembers.AsNoTracking()
            .Where(member => member.UserId == userId.Value)
            .Select(member => member.RoomId)
            .ToListAsync(Context.ConnectionAborted);
        foreach (var roomId in roomIds) await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
        await Clients.All.SendAsync("PresenceChanged", new { UserId = userId, IsOnline = true }, Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId();
        if (userId is not null)
            await Clients.All.SendAsync("PresenceChanged", new { UserId = userId, IsOnline = false }, Context.ConnectionAborted);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Receives a room message pushed through REST, forwarded to room members.</summary>
    public async Task JoinRoom(Guid roomId)
    {
        var userId = CurrentUserId();
        if (userId is null) return;
        var isMember = await database.ChatRoomMembers.AsNoTracking()
            .AnyAsync(member => member.RoomId == roomId && member.UserId == userId.Value, Context.ConnectionAborted);
        if (!isMember) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
    }

    public async Task LeaveRoom(Guid roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomId));
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}

/// <summary>Signaling hub for WebRTC voice/video calls.</summary>
[Authorize]
internal sealed class CallHub(DatingDbContext database) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId();
        if (userId is null) { Context.Abort(); return; }
        await Groups.AddToGroupAsync(Context.ConnectionId, ChatHub.UserGroup(userId.Value));
        await base.OnConnectedAsync();
    }

    /// <summary>Caller starts a call: notifies callee/call participants.</summary>
    public async Task StartCall(StartCallRequest request, Guid callId, string[] memberUserIds)
    {
        var callerId = CurrentUserId();
        if (callerId is null) return;
        foreach (var memberId in memberUserIds.Where(id => id != callerId.Value.ToString()))
            await Clients.Group(ChatHub.UserGroup(Guid.Parse(memberId)))
                .SendAsync("IncomingCall", new
                {
                    CallId = callId,
                    CallType = request.CallType,
                    CallerUserId = callerId,
                    RoomId = request.RoomId
                }, Context.ConnectionAborted);
    }

    /// <summary>Callee answers; both peers exchange SDP offer/answer and ICE candidates through this hub.</summary>
    public async Task AnswerCall(Guid callId, Guid toUserId) =>
        await Clients.Group(ChatHub.UserGroup(toUserId)).SendAsync("CallAnswered", new { CallId = callId, FromUserId = CurrentUserId() }, Context.ConnectionAborted);

    public async Task DeclineCall(Guid callId, Guid toUserId) =>
        await Clients.Group(ChatHub.UserGroup(toUserId)).SendAsync("CallDeclined", new { CallId = callId, FromUserId = CurrentUserId() }, Context.ConnectionAborted);

    public async Task SendOffer(Guid callId, Guid toUserId, string sdp) =>
        await Clients.Group(ChatHub.UserGroup(toUserId)).SendAsync("ReceiveOffer", new { CallId = callId, FromUserId = CurrentUserId(), Sdp = sdp }, Context.ConnectionAborted);

    public async Task SendAnswer(Guid callId, Guid toUserId, string sdp) =>
        await Clients.Group(ChatHub.UserGroup(toUserId)).SendAsync("ReceiveAnswer", new { CallId = callId, FromUserId = CurrentUserId(), Sdp = sdp }, Context.ConnectionAborted);

    public async Task SendIceCandidate(Guid callId, Guid toUserId, string candidate, string sdpMid, int sdpMLineIndex) =>
        await Clients.Group(ChatHub.UserGroup(toUserId)).SendAsync("ReceiveIceCandidate",
            new { CallId = callId, FromUserId = CurrentUserId(), Candidate = candidate, SdpMid = sdpMid, SdpMLineIndex = sdpMLineIndex }, Context.ConnectionAborted);

    public async Task EndCall(Guid callId, Guid[] memberUserIds)
    {
        var groups = new List<string>();
        foreach (var id in memberUserIds)
        {
            groups.Add($"user:{id}");
        }
        await Clients.Groups(groups)
            .SendAsync("CallEnded", new { CallId = callId }, Context.ConnectionAborted);
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
