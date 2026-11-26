using System.Security.Claims;
using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Endpoints;

internal static class RoomEndpoints
{
    private const int DefaultMaxMembers = 50;
    private const int MaxPageSize = 50;

    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var rooms = endpoints.MapGroup("/api/rooms").RequireAuthorization();
        rooms.MapPost("", CreateAsync).DisableAntiforgery();
        rooms.MapGet("", ListAsync);
        rooms.MapGet("/{roomId:guid}", GetAsync);
        rooms.MapPatch("/{roomId:guid}", UpdateAsync);
        rooms.MapDelete("/{roomId:guid}", DeleteAsync);
        rooms.MapPost("/{roomId:guid}/join", JoinAsync);
        rooms.MapPost("/{roomId:guid}/leave", LeaveAsync);
        rooms.MapDelete("/{roomId:guid}/members/{userId:guid}", KickAsync);
        rooms.MapGet("/{roomId:guid}/messages", MessagesAsync);
        rooms.MapPost("/{roomId:guid}/messages", SendMessageAsync).DisableAntiforgery();

        endpoints.MapPost("/api/reports", ReportAsync).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateRoomRequest request, ClaimsPrincipal principal, DatingDbContext db, CancellationToken ct)
    {
        var userId = CurrentUser.GetId(principal);
        var validation = ValidateRoom(request.Name, request.MaxMembers, request.IsPrivate, request.InviteCode);
        if (validation is not null) return Results.BadRequest(new ProblemDetails { Title = validation, Status = 400 });
        var room = new Room { Name = request.Name.Trim(), Topic = Clean(request.Topic, 80), Description = Clean(request.Description, 1000), AvatarUrl = Clean(request.AvatarUrl, 500), OwnerId = userId, IsPrivate = request.IsPrivate, InviteCode = request.IsPrivate ? request.InviteCode!.Trim() : null, MaxMembers = request.MaxMembers ?? DefaultMaxMembers };
        db.Rooms.Add(room);
        db.RoomMembers.Add(new RoomMember { RoomId = room.Id, UserId = userId, Role = "owner" });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/rooms/{room.Id}", await ToDetailsAsync(db, room, ct));
    }

    private static async Task<IResult> ListAsync(string? search, string? topic, int? page, int? pageSize, DatingDbContext db, CancellationToken ct)
    {
        var size = Math.Clamp(pageSize ?? 20, 1, MaxPageSize);
        var currentPage = Math.Max(page ?? 1, 1);
        var query = db.Rooms.AsNoTracking().Where(room => !room.IsPrivate);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(room => room.Name.Contains(search) || (room.Description != null && room.Description.Contains(search)));
        if (!string.IsNullOrWhiteSpace(topic)) query = query.Where(room => room.Topic == topic);
        var rooms = await query.OrderByDescending(room => room.CreatedAt).Skip((currentPage - 1) * size).Take(size).Select(room => new RoomSummaryResponse(room.Id, room.Name, room.Topic, room.Description, room.AvatarUrl, room.OwnerId, room.IsPrivate, db.RoomMembers.Count(member => member.RoomId == room.Id), room.MaxMembers, room.CreatedAt)).ToListAsync(ct);
        return Results.Ok(rooms);
    }

    private static async Task<IResult> GetAsync(Guid roomId, ClaimsPrincipal principal, DatingDbContext db, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(room => room.Id == roomId, ct);
        if (room is null) return Results.NotFound();
        var userId = CurrentUser.GetId(principal);
        if (room.IsPrivate && !await IsMemberAsync(db, roomId, userId, ct)) return Results.NotFound();
        return Results.Ok(await ToDetailsAsync(db, room, ct));
    }

    private static async Task<IResult> JoinAsync(Guid roomId, JoinRoomRequest request, ClaimsPrincipal principal, DatingDbContext db, CancellationToken ct)
    {
        var userId = CurrentUser.GetId(principal);
        var room = await db.Rooms.FirstOrDefaultAsync(room => room.Id == roomId, ct);
        if (room is null) return Results.NotFound();
        if (await IsMemberAsync(db, roomId, userId, ct)) return Results.Ok(await ToDetailsAsync(db, room, ct));
        if (room.IsPrivate && !string.Equals(room.InviteCode, request.InviteCode?.Trim(), StringComparison.Ordinal)) return Results.BadRequest(new ProblemDetails { Title = "Sai mã mời", Status = 400 });
        if (await db.RoomMembers.CountAsync(member => member.RoomId == roomId, ct) >= room.MaxMembers) return Results.Conflict(new ProblemDetails { Title = "Phòng đã đầy", Status = 409 });
        db.RoomMembers.Add(new RoomMember { RoomId = roomId, UserId = userId, Role = "member" });
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ToDetailsAsync(db, room, ct));
    }

    private static async Task<IResult> LeaveAsync(Guid roomId, ClaimsPrincipal principal, DatingDbContext db, CancellationToken ct)
    {
        var userId = CurrentUser.GetId(principal);
        var member = await db.RoomMembers.FirstOrDefaultAsync(item => item.RoomId == roomId && item.UserId == userId, ct);
        if (member is null) return Results.NotFound();
        if (member.Role == "owner") return Results.BadRequest(new ProblemDetails { Title = "Owner cần xóa phòng hoặc chuyển quyền trước khi rời phòng", Status = 400 });
        db.RoomMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateAsync(Guid roomId, UpdateRoomRequest request, ClaimsPrincipal principal, DatingDbContext db, CancellationToken ct)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(item => item.Id == roomId, ct);
        if (room is null) return Results.NotFound();
        if (!await IsOwnerAsync(db, roomId, CurrentUser.GetId(principal), ct)) return Results.Forbid();
        if (request.Name is not null) room.Name = request.Name.Trim();
        if (request.Topic is not null) room.Topic = Clean(request.Topic, 80);
        if (request.Description is not null) room.Description = Clean(request.Description, 1000);
        if (request.AvatarUrl is not null) room.AvatarUrl = Clean(request.AvatarUrl, 500);
        if (request.MaxMembers is not null) room.MaxMembers = Math.Clamp(request.MaxMembers.Value, 2, 500);
        if (request.IsPrivate is not null) { room.IsPrivate = request.IsPrivate.Value; room.InviteCode = room.IsPrivate ? Clean(request.InviteCode ?? room.InviteCode, 64) : null; }
        await db.SaveChangesAsync(ct);
        return Results.Ok(await ToDetailsAsync(db, room, ct));
    }

    private static async Task<IResult> DeleteAsync(Guid roomId, ClaimsPrincipal principal, DatingDbContext db, CancellationToken ct)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(item => item.Id == roomId, ct);
        if (room is null) return Results.NotFound();
        if (room.OwnerId != CurrentUser.GetId(principal)) return Results.Forbid();
        db.Rooms.Remove(room);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> KickAsync(Guid roomId, Guid userId, ClaimsPrincipal principal, DatingDbContext db, CancellationToken ct)
    {
        if (!await IsOwnerAsync(db, roomId, CurrentUser.GetId(principal), ct)) return Results.Forbid();
        var member = await db.RoomMembers.FirstOrDefaultAsync(item => item.RoomId == roomId && item.UserId == userId, ct);
        if (member is null || member.Role == "owner") return Results.NotFound();
        db.RoomMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> MessagesAsync(Guid roomId, string? cursor, int? limit, ClaimsPrincipal principal, DatingDbContext db, CancellationToken ct)
    {
        if (!await IsMemberAsync(db, roomId, CurrentUser.GetId(principal), ct)) return Results.Forbid();
        var size = Math.Clamp(limit ?? 40, 1, MaxPageSize);
        var query = db.RoomMessages.AsNoTracking().Where(message => message.RoomId == roomId);
        if (long.TryParse(cursor, out var timestamp)) query = query.Where(message => message.CreatedAt.ToUnixTimeMilliseconds() < timestamp);
        var messages = await query.OrderByDescending(message => message.CreatedAt).Take(size + 1).Join(db.Users, message => message.SenderId, user => user.Id, (message, user) => new RoomMessageResponse(message.Id, message.RoomId, message.SenderId, user.Name, user.Photos.Where(photo => photo.IsAvatar).Select(photo => photo.Url).FirstOrDefault(), message.Content, message.MediaUrl, message.CreatedAt)).ToListAsync(ct);
        var hasMore = messages.Count > size;
        var page = messages.Take(size).Reverse().ToList();
        return Results.Ok(new RoomMessagesResponse(page, hasMore ? page.First().CreatedAt.ToUnixTimeMilliseconds().ToString() : null));
    }

    private static async Task<IResult> SendMessageAsync(Guid roomId, [FromForm] string? content, [FromForm] IFormFile? media, ClaimsPrincipal principal, DatingDbContext db, CancellationToken ct)
    {
        if (!await IsMemberAsync(db, roomId, CurrentUser.GetId(principal), ct)) return Results.Forbid();
        var text = content?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Length > 1000) return Results.BadRequest(new ProblemDetails { Title = "Tin nhắn phải dài từ 1 đến 1000 ký tự", Status = 400 });
        var message = new RoomMessage { RoomId = roomId, SenderId = CurrentUser.GetId(principal), Content = text };
        db.RoomMessages.Add(message);
        await db.SaveChangesAsync(ct);
        var sender = await db.Users.AsNoTracking().Include(user => user.Photos).FirstAsync(user => user.Id == message.SenderId, ct);
        return Results.Created($"/api/rooms/{roomId}/messages/{message.Id}", new RoomMessageResponse(message.Id, roomId, sender.Id, sender.Name, sender.Photos.FirstOrDefault(photo => photo.IsAvatar)?.Url, message.Content, null, message.CreatedAt));
    }

    private static async Task<IResult> ReportAsync(CreateReportRequest request, ClaimsPrincipal principal, DatingDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500 || (request.TargetUserId is null && request.MessageId is null)) return Results.BadRequest();
        db.Reports.Add(new Report { ReporterId = CurrentUser.GetId(principal), TargetUserId = request.TargetUserId, RoomId = request.RoomId, MessageId = request.MessageId, Reason = request.Reason.Trim() });
        await db.SaveChangesAsync(ct);
        return Results.Accepted();
    }

    private static async Task<RoomDetailsResponse> ToDetailsAsync(DatingDbContext db, Room room, CancellationToken ct)
    {
        var members = await db.RoomMembers.AsNoTracking().Where(member => member.RoomId == room.Id).Join(db.Users.Include(user => user.Photos), member => member.UserId, user => user.Id, (member, user) => new RoomMemberResponse(user.Id, user.Name, user.Photos.Where(photo => photo.IsAvatar).Select(photo => photo.Url).FirstOrDefault(), member.Role, member.JoinedAt)).ToListAsync(ct);
        return new RoomDetailsResponse(new RoomSummaryResponse(room.Id, room.Name, room.Topic, room.Description, room.AvatarUrl, room.OwnerId, room.IsPrivate, members.Count, room.MaxMembers, room.CreatedAt), members);
    }

    private static Task<bool> IsMemberAsync(DatingDbContext db, Guid roomId, Guid userId, CancellationToken ct) => db.RoomMembers.AnyAsync(member => member.RoomId == roomId && member.UserId == userId, ct);
    private static Task<bool> IsOwnerAsync(DatingDbContext db, Guid roomId, Guid userId, CancellationToken ct) => db.Rooms.AnyAsync(room => room.Id == roomId && room.OwnerId == userId, ct);
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static string? ValidateRoom(string name, int? maxMembers, bool isPrivate, string? inviteCode) => string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 ? "Tên phòng không hợp lệ" : maxMembers is < 2 or > 500 ? "Số thành viên không hợp lệ" : isPrivate && string.IsNullOrWhiteSpace(inviteCode) ? "Phòng riêng tư cần mã mời" : null;
}
