using System.Security.Claims;
using KissAppTussing.Contracts;
using KissAppTussing.Services;

namespace KissAppTussing.Endpoints;

internal static class DateEndpoints
{
    public static IEndpointRouteBuilder MapDateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dates").RequireAuthorization();
        group.MapGet("", async (ClaimsPrincipal user, DateService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(CurrentUser.GetId(user), ct)));
        group.MapPost("", async (CreateDateInvitationRequest request, ClaimsPrincipal user, DateService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(CurrentUser.GetId(user), request, ct);
            return result.Status switch
            {
                "Created" => Results.Created($"/api/dates/{result.Invitation!.Id}", result.Invitation),
                "Invalid" => Results.BadRequest(new { detail = "Choose a future time within the next year and a different matched user." }),
                "Forbidden" => Results.BadRequest(new { detail = "You can invite someone after you have matched." }),
                _ => Results.NotFound()
            };
        });
        group.MapPost("/{id:guid}/accept", async (Guid id, ClaimsPrincipal user, DateService service, CancellationToken ct) =>
            Decision(await service.RespondAsync(CurrentUser.GetId(user), id, true, ct)));
        group.MapPost("/{id:guid}/decline", async (Guid id, ClaimsPrincipal user, DateService service, CancellationToken ct) =>
            Decision(await service.RespondAsync(CurrentUser.GetId(user), id, false, ct)));
        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, DateService service, CancellationToken ct) =>
            Decision(await service.CancelAsync(CurrentUser.GetId(user), id, ct)));
        group.MapPost("/{id:guid}/reviews", async (Guid id, DateReviewRequest request, ClaimsPrincipal user, DateService service, CancellationToken ct) =>
        {
            var result = await service.ReviewAsync(CurrentUser.GetId(user), id, request, ct);
            return result switch
            {
                "Created" => Results.NoContent(),
                "Duplicate" => Results.Conflict(new { detail = "You have already reviewed this date." }),
                "Missing" => Results.NotFound(),
                _ => Results.BadRequest(new { detail = "A review is available only after an accepted date has happened." })
            };
        });
        return endpoints;
    }

    private static IResult Decision(string status) => status switch
    {
        "Accepted" or "Declined" or "Cancelled" => Results.NoContent(),
        "Missing" => Results.NotFound(),
        _ => Results.Forbid()
    };
}
