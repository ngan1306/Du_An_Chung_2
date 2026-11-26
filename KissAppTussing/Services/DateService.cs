using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Services;

internal sealed class DateService(DatingDbContext database)
{
    public async Task<IReadOnlyList<DateInvitationResponse>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var invitations = await database.DateInvitations.AsNoTracking()
            .Include(item => item.Organizer)
            .Include(item => item.Recipient)
            .Include(item => item.Reviews).ThenInclude(review => review.Reviewer)
            .Where(item => item.OrganizerUserId == userId || item.RecipientUserId == userId)
            .OrderBy(item => item.StartsAt)
            .ToListAsync(cancellationToken);
        return invitations.Select(item => ToResponse(item, userId)).ToArray();
    }

    public async Task<IReadOnlyList<AdminDateInvitationResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var invitations = await database.DateInvitations.AsNoTracking()
            .Include(item => item.Organizer)
            .Include(item => item.Recipient)
            .Include(item => item.Reviews).ThenInclude(review => review.Reviewer)
            .OrderBy(item => item.StartsAt < now)
            .ThenBy(item => item.StartsAt)
            .ToListAsync(cancellationToken);
        return invitations.Select(item => new AdminDateInvitationResponse(
            item.Id, item.OrganizerUserId, item.Organizer.Name, item.RecipientUserId, item.Recipient.Name,
            item.VenueName, item.VenueAddress, item.VenueUrl, item.StartsAt, item.Message, item.Status,
            item.CreatedAt, item.Reviews.OrderBy(review => review.CreatedAt)
                .Select(review => new DateReviewResponse(review.Id, review.ReviewerUserId, review.Reviewer.Name,
                    review.Rating, review.Comment, review.CreatedAt))
                .ToArray())).ToArray();
    }

    public async Task<(string Status, DateInvitationResponse? Invitation)> CreateAsync(
        Guid userId,
        CreateDateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (request.RecipientUserId == userId || request.StartsAt <= now.AddMinutes(5) || request.StartsAt > now.AddYears(1))
            return ("Invalid", null);

        var matched = await database.Matches.AnyAsync(match => match.EndedAt == null
            && ((match.FirstUserId == userId && match.SecondUserId == request.RecipientUserId)
                || (match.FirstUserId == request.RecipientUserId && match.SecondUserId == userId)), cancellationToken);
        if (!matched) return ("Forbidden", null);

        var organizer = await database.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        var recipient = await database.Users.SingleOrDefaultAsync(user => user.Id == request.RecipientUserId, cancellationToken);
        if (organizer is null || recipient is null) return ("Missing", null);

        var invitation = new DateInvitation
        {
            OrganizerUserId = userId,
            Organizer = organizer,
            RecipientUserId = recipient.Id,
            Recipient = recipient,
            VenueName = request.VenueName.Trim(),
            VenueAddress = Normalize(request.VenueAddress),
            VenueUrl = Normalize(request.VenueUrl),
            StartsAt = request.StartsAt.ToUniversalTime(),
            Message = request.Message.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        database.DateInvitations.Add(invitation);
        await NotificationWriter.AddAsync(database, recipient.Id, "Date", "Lời mời hẹn hò mới", $"{organizer.Name} mời bạn đến {invitation.VenueName}.", invitation.Id, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return ("Created", ToResponse(invitation, userId));
    }

    public async Task<string> RespondAsync(Guid userId, Guid invitationId, bool accept, CancellationToken cancellationToken)
    {
        var invitation = await database.DateInvitations
            .Include(item => item.Organizer)
            .Include(item => item.Recipient)
            .SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken);
        if (invitation is null) return "Missing";
        if (invitation.RecipientUserId != userId || invitation.Status != "Pending") return "Forbidden";

        invitation.Status = accept ? "Accepted" : "Declined";
        invitation.UpdatedAt = DateTimeOffset.UtcNow;
        await NotificationWriter.AddAsync(database, invitation.OrganizerUserId, "Date",
            accept ? "Lời mời đã được chấp nhận" : "Lời mời đã bị từ chối",
            $"{invitation.Recipient.Name} đã {(accept ? "đồng ý" : "từ chối")} cuộc hẹn.", invitation.Id, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return accept ? "Accepted" : "Declined";
    }

    public async Task<string> CancelAsync(Guid userId, Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await database.DateInvitations
            .Include(item => item.Organizer)
            .Include(item => item.Recipient)
            .SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken);
        if (invitation is null) return "Missing";
        if ((invitation.OrganizerUserId != userId && invitation.RecipientUserId != userId)
            || invitation.Status is not ("Pending" or "Accepted")) return "Forbidden";

        invitation.Status = "Cancelled";
        invitation.UpdatedAt = DateTimeOffset.UtcNow;
        var otherUserId = invitation.OrganizerUserId == userId ? invitation.RecipientUserId : invitation.OrganizerUserId;
        var actorName = invitation.OrganizerUserId == userId ? invitation.Organizer.Name : invitation.Recipient.Name;
        await NotificationWriter.AddAsync(database, otherUserId, "Date", "Lịch hẹn đã bị hủy", $"{actorName} đã hủy cuộc hẹn tại {invitation.VenueName}.", invitation.Id, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return "Cancelled";
    }

    public async Task<string> ReviewAsync(Guid userId, Guid invitationId, DateReviewRequest request, CancellationToken cancellationToken)
    {
        var invitation = await database.DateInvitations.SingleOrDefaultAsync(item => item.Id == invitationId, cancellationToken);
        if (invitation is null) return "Missing";
        if (invitation.Status != "Accepted" || invitation.StartsAt > DateTimeOffset.UtcNow
            || (invitation.OrganizerUserId != userId && invitation.RecipientUserId != userId)) return "Forbidden";
        if (await database.DateReviews.AnyAsync(review => review.InvitationId == invitationId && review.ReviewerUserId == userId, cancellationToken))
            return "Duplicate";

        database.DateReviews.Add(new DateReview
        {
            InvitationId = invitationId,
            ReviewerUserId = userId,
            Rating = request.Rating,
            Comment = request.Comment.Trim()
        });
        await database.SaveChangesAsync(cancellationToken);
        return "Created";
    }

    private static DateInvitationResponse ToResponse(DateInvitation item, Guid userId) => new(
        item.Id,
        item.OrganizerUserId == userId ? item.RecipientUserId : item.OrganizerUserId,
        item.OrganizerUserId == userId ? item.Recipient.Name : item.Organizer.Name,
        item.OrganizerUserId == userId,
        item.VenueName,
        item.VenueAddress,
        item.VenueUrl,
        item.StartsAt,
        item.Message,
        item.Status,
        item.CreatedAt,
        item.Reviews.Any(review => review.ReviewerUserId == userId),
        item.Reviews.OrderBy(review => review.CreatedAt)
            .Select(review => new DateReviewResponse(review.Id, review.ReviewerUserId, review.Reviewer.Name, review.Rating, review.Comment, review.CreatedAt))
            .ToArray());

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed class DateReminderService(IServiceScopeFactory scopeFactory, ILogger<DateReminderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<DatingDbContext>();
                var now = DateTimeOffset.UtcNow;
                var upcoming = await database.DateInvitations
                    .Include(item => item.Organizer)
                    .Include(item => item.Recipient)
                    .Where(item => item.Status == "Accepted" && item.ReminderSentAt == null
                        && item.StartsAt > now && item.StartsAt <= now.AddHours(24))
                    .ToListAsync(stoppingToken);
                foreach (var item in upcoming)
                {
                    var when = item.StartsAt.ToString("g");
                    await NotificationWriter.AddAsync(database, item.OrganizerUserId, "Date", "Nhắc lịch hẹn", $"Cuộc hẹn với {item.Recipient.Name} tại {item.VenueName} sắp diễn ra ({when}).", item.Id, stoppingToken);
                    await NotificationWriter.AddAsync(database, item.RecipientUserId, "Date", "Nhắc lịch hẹn", $"Cuộc hẹn với {item.Organizer.Name} tại {item.VenueName} sắp diễn ra ({when}).", item.Id, stoppingToken);
                    item.ReminderSentAt = now;
                }
                if (upcoming.Count > 0) await database.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Could not send date reminders."); }

            try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
