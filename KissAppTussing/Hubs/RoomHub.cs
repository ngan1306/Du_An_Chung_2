using System.Collections.Concurrent;
using System.Security.Claims;
using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Hubs;

[Authorize]
internal sealed class RoomHub(DatingDbContext db) : Hub
{
    private static readonly ConcurrentDictionary<Guid, DateTimeOffset> LastMessageAt = new();

    public async Task JoinRoom(Guid roomId)
    {
        var userId = GetUserId();
        if (!await IsMemberAsync(roomId, userId)) throw new HubException("Bạn không phải là thành viên của phòng.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(roomId));
        await Clients.Group(GroupName(roomId)).SendAsync("member:joined", new { roomId, userId }, Context.ConnectionAborted);
    }

    public async Task LeaveRoom(Guid roomId)
    {
        var userId = GetUserId();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(roomId));
        await Clients.Group(GroupName(roomId)).SendAsync("member:left", new { roomId, userId }, Context.ConnectionAborted);
    }

    public async Task SendMessage(Guid roomId, string content, string? mediaUrl = null)
    {
        var userId = GetUserId();
        if (!await IsMemberAsync(roomId, userId)) throw new HubException("Bạn không phải là thành viên của phòng.");
        var text = content?.Trim() ?? string.Empty;
        if (text.Length is 0 or > 1000) throw new HubException("Tin nhắn phải dài từ 1 đến 1000 ký tự.");
        var now = DateTimeOffset.UtcNow;
        if (LastMessageAt.TryGetValue(userId, out var previous) && now - previous < TimeSpan.FromMilliseconds(700)) throw new HubException("Bạn gửi tin nhắn quá nhanh.");
        LastMessageAt[userId] = now;

        var message = new RoomMessage { RoomId = roomId, SenderId = userId, Content = text, MediaUrl = mediaUrl };
        db.RoomMessages.Add(message);
        await db.SaveChangesAsync(Context.ConnectionAborted);
        var sender = await db.Users.AsNoTracking().Include(user => user.Photos).FirstAsync(user => user.Id == userId, Context.ConnectionAborted);
        var response = new RoomMessageResponse(message.Id, roomId, userId, sender.Name, sender.Photos.FirstOrDefault(photo => photo.IsAvatar)?.Url, message.Content, message.MediaUrl, message.CreatedAt);
        await Clients.Group(GroupName(roomId)).SendAsync("message:new", response, Context.ConnectionAborted);
    }

    public async Task Typing(Guid roomId, bool isTyping)
    {
        var userId = GetUserId();
        if (await IsMemberAsync(roomId, userId)) await Clients.OthersInGroup(GroupName(roomId)).SendAsync("typing", new { roomId, userId, isTyping }, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        LastMessageAt.TryRemove(GetUserId(), out _);
        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetUserId() => Guid.Parse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new HubException("Unauthorized"));
    private Task<bool> IsMemberAsync(Guid roomId, Guid userId) => db.RoomMembers.AnyAsync(member => member.RoomId == roomId && member.UserId == userId, Context.ConnectionAborted);
    private static string GroupName(Guid roomId) => $"room:{roomId:N}";
}
