using System.Security.Claims;
using KissAppTussing.Contracts;
using KissAppTussing.Services;

namespace KissAppTussing.Endpoints;

internal static class RoomEndpoints
{
    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/rooms").RequireAuthorization();
        group.MapGet("", async (ClaimsPrincipal user, RoomService service, CancellationToken ct) =>
            Results.Ok(await service.GetRoomsAsync(CurrentUser.GetId(user), ct)));
        group.MapPost("", async (CreateRoomRequest request, ClaimsPrincipal user, RoomService service, CancellationToken ct) =>
        {
            var (status, room) = await service.CreateAsync(CurrentUser.GetId(user), request, ct);
            return status switch
            {
                "Created" => Results.Created($"/api/rooms/{room!.Id}", room),
                "Invalid" => Results.BadRequest(new { detail = "Tên phòng cần 2-80 ký tự." }),
                "NotFriends" => Results.BadRequest(new { detail = "Chỉ có thể mời bạn bè đã kết nối vào phòng." }),
                _ => Results.NotFound()
            };
        });
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, RoomService service, CancellationToken ct) =>
        {
            var (status, room) = await service.JoinAsync(CurrentUser.GetId(user), id, ct);
            return status switch
            {
                "Joined" => Results.Ok(room),
                "Missing" => Results.NotFound(),
                _ => Results.Forbid()
            };
        });
        group.MapGet("/{id:guid}/messages", async (Guid id, ClaimsPrincipal user, RoomService service, CancellationToken ct) =>
        {
            var messages = await service.GetMessagesAsync(CurrentUser.GetId(user), id, ct);
            return messages is null ? Results.NotFound() : Results.Ok(messages);
        });
        group.MapPost("/{id:guid}/messages", async (Guid id, SendRoomMessageRequest request, ClaimsPrincipal user, RoomService service, CancellationToken ct) =>
        {
            var (status, message) = await service.SendMessageAsync(CurrentUser.GetId(user), id, request.Content, ct);
            return status switch
            {
                "Created" => Results.Created($"/api/rooms/{id}/messages", message),
                "Invalid" => Results.BadRequest(new { detail = "Tin nhắn cần 1-2000 ký tự." }),
                "Missing" => Results.NotFound(),
                _ => Results.Forbid()
            };
        });
        group.MapPost("/{id:guid}/invite/{userId:guid}", async (Guid id, Guid userId, ClaimsPrincipal user, RoomService service, CancellationToken ct) =>
        {
            var status = await service.InviteAsync(CurrentUser.GetId(user), id, userId, ct);
            return status switch
            {
                "Invited" => Results.NoContent(),
                "Missing" => Results.NotFound(),
                "NotFriends" => Results.BadRequest(new { detail = "Chỉ có thể mời bạn bè đã kết nối." }),
                "Duplicate" => Results.Conflict(new { detail = "Người này đã ở trong phòng." }),
                _ => Results.Forbid()
            };
        });
        group.MapPost("/{id:guid}/leave", async (Guid id, ClaimsPrincipal user, RoomService service, CancellationToken ct) =>
        {
            var status = await service.LeaveAsync(CurrentUser.GetId(user), id, ct);
            return status switch
            {
                "Left" => Results.NoContent(),
                "Missing" => Results.NotFound(),
                _ => Results.Forbid()
            };
        });
        group.MapGet("/calls/history", async (ClaimsPrincipal user, RoomService service, CancellationToken ct) =>
            Results.Ok(await service.GetCallHistoryAsync(CurrentUser.GetId(user), ct)));
        return endpoints;
    }
}
