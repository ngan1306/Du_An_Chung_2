using System.Security.Claims;
using KissAppTussing.Contracts;
using KissAppTussing.Services;

namespace KissAppTussing.Endpoints;

internal static class CallEndpoints
{
    public static IEndpointRouteBuilder MapCallEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/calls").RequireAuthorization();
        group.MapPost("", async (StartCallRequest request, ClaimsPrincipal user, CallService service, CancellationToken ct) =>
        {
            var (status, call) = await service.StartAsync(CurrentUser.GetId(user), request, ct);
            return status switch
            {
                "Created" => Results.Created($"/api/calls/{call!.Id}", call),
                "Invalid" => Results.BadRequest(new { detail = "Chọn người nhận hoặc phòng chat để gọi." }),
                "NotFriends" => Results.BadRequest(new { detail = "Chỉ có thể gọi cho bạn bè đã kết nối." }),
                "Forbidden" => Results.Forbid(),
                _ => Results.NotFound()
            };
        });
        group.MapPost("/{id:guid}/answer", async (Guid id, ClaimsPrincipal user, CallService service, CancellationToken ct) =>
            Decision(await service.AnswerAsync(CurrentUser.GetId(user), id, ct)));
        group.MapPost("/{id:guid}/decline", async (Guid id, ClaimsPrincipal user, CallService service, CancellationToken ct) =>
            Decision(await service.DeclineAsync(CurrentUser.GetId(user), id, ct)));
        group.MapPost("/{id:guid}/end", async (Guid id, ClaimsPrincipal user, CallService service, CancellationToken ct) =>
            Decision(await service.EndAsync(CurrentUser.GetId(user), id, ct)));
        group.MapGet("/{id:guid}/members", async (Guid id, ClaimsPrincipal user, CallService service, CancellationToken ct) =>
        {
            var members = await service.MembersAsync(CurrentUser.GetId(user), id, ct);
            return members is null ? Results.NotFound() : Results.Ok(members);
        });
        return endpoints;
    }

    private static IResult Decision(string status) => status switch
    {
        "Answered" or "Declined" or "Ended" => Results.NoContent(),
        "Missing" => Results.NotFound(),
        _ => Results.Forbid()
    };
}
