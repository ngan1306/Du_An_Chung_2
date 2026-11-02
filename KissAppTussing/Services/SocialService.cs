using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Services;

internal sealed class SocialService(DatingDbContext db)
{
    public async Task<IReadOnlyList<SocialUserResponse>> SearchAsync(Guid me, string? query, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking().Where(user => user.Id != me);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            users = users.Where(user => user.Name.Contains(term) || user.Bio.Contains(term) || (user.City != null && user.City.Contains(term)));
        }
        return await users.OrderBy(user => user.Name).Take(30)
            .Select(user => new SocialUserResponse(user.Id, user.Name, user.Bio, user.City,
                db.ProfilePhotos.Where(photo => photo.UserId == user.Id).OrderByDescending(photo => photo.IsAvatar).ThenBy(photo => photo.SortOrder).Select(photo => photo.Url).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ConnectionResponse>> ConnectionsAsync(Guid me, string? status, CancellationToken ct)
    {
        var query = db.ConnectionRequests.AsNoTracking().Where(row => row.FirstUserId == me || row.SecondUserId == me);
        if (status is not null) query = query.Where(row => row.Status == status);
        var rows = await query.OrderByDescending(row => row.CreatedAt).Take(200).ToListAsync(ct);
        var ids = rows.Select(row => row.FirstUserId == me ? row.SecondUserId : row.FirstUserId).Distinct().ToArray();
        var users = await UserCards(ids, ct);
        return rows.Where(row => users.ContainsKey(row.FirstUserId == me ? row.SecondUserId : row.FirstUserId))
            .Select(row => new ConnectionResponse(row.Id, users[row.FirstUserId == me ? row.SecondUserId : row.FirstUserId], row.Status, row.RequestedByUserId == me, row.CreatedAt)).ToArray();
    }

    public async Task<string> RequestAsync(Guid me, Guid other, CancellationToken ct)
    {
        if (me == other) return "Self";
        if (!await db.Users.AnyAsync(user => user.Id == other, ct)) return "Missing";
        var (first, second) = Pair(me, other);
        var row = await db.ConnectionRequests.SingleOrDefaultAsync(item => item.FirstUserId == first && item.SecondUserId == second, ct);
        if (row is { Status: "Accepted" }) return "Accepted";
        if (row is { Status: "Pending" }) return row.RequestedByUserId == me ? "Duplicate" : "Incoming";
        if (row is null)
        {
            db.ConnectionRequests.Add(new ConnectionRequest { FirstUserId = first, SecondUserId = second, RequestedByUserId = me });
        }
        else
        {
            row.RequestedByUserId = me;
            row.Status = "Pending";
            row.CreatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return "Created";
    }

    public async Task<string> DecideAsync(Guid me, Guid id, bool accept, CancellationToken ct)
    {
        var row = await db.ConnectionRequests.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (row is null) return "Missing";
        if (row.Status != "Pending" || row.RequestedByUserId == me || (row.FirstUserId != me && row.SecondUserId != me)) return "Forbidden";
        if (!accept)
        {
            row.Status = "Declined";
            await db.SaveChangesAsync(ct);
            return "Declined";
        }
        row.Status = "Accepted";
        row.CreatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await GetOrCreateConversationAsync(row.FirstUserId, row.SecondUserId, ct);
        return "Accepted";
    }

    public async Task<string> CancelAsync(Guid me, Guid id, CancellationToken ct)
    {
        var row = await db.ConnectionRequests.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (row is null) return "Missing";
        if (row.Status != "Pending" || row.RequestedByUserId != me) return "Forbidden";
        db.ConnectionRequests.Remove(row);
        await db.SaveChangesAsync(ct);
        return "Deleted";
    }

    public async Task<string> DisconnectAsync(Guid me, Guid id, CancellationToken ct)
    {
        var row = await db.ConnectionRequests.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (row is null) return "Missing";
        if (row.Status != "Accepted" || (row.FirstUserId != me && row.SecondUserId != me)) return "Forbidden";
        row.Status = "Disconnected";
        await db.SaveChangesAsync(ct);
        return "Deleted";
    }

    public async Task<IReadOnlyList<ConversationResponse>> ConversationsAsync(Guid me, CancellationToken ct)
    {
        var pairs = await db.ConnectionRequests.AsNoTracking()
            .Where(row => row.Status == "Accepted" && (row.FirstUserId == me || row.SecondUserId == me))
            .Select(row => new { row.FirstUserId, row.SecondUserId }).ToListAsync(ct);
        foreach (var pair in pairs) await GetOrCreateConversationAsync(pair.FirstUserId, pair.SecondUserId, ct);
        var conversations = await db.Conversations.AsNoTracking()
            .Where(item => item.FirstUserId == me || item.SecondUserId == me)
            .OrderByDescending(item => item.UpdatedAt).ToListAsync(ct);
        var result = new List<ConversationResponse>();
        foreach (var item in conversations)
        {
            var other = item.FirstUserId == me ? item.SecondUserId : item.FirstUserId;
            var card = (await UserCards([other], ct)).GetValueOrDefault(other);
            if (card is null) continue;
            var latest = await db.Messages.AsNoTracking().Where(message => message.ConversationId == item.Id).OrderByDescending(message => message.SentAt).Select(message => message.Content).FirstOrDefaultAsync(ct);
            var readAt = item.FirstUserId == me ? item.FirstUserReadAt : item.SecondUserReadAt;
            var unread = await db.Messages.CountAsync(message => message.ConversationId == item.Id && message.SenderId != me && (readAt == null || message.SentAt > readAt), ct);
            result.Add(new ConversationResponse(item.Id, card, latest, item.UpdatedAt, unread));
        }
        return result;
    }

    public async Task<IReadOnlyList<MessageResponse>?> MessagesAsync(Guid me, Guid conversationId, DateTimeOffset? before, int limit, CancellationToken ct)
    {
        var conversation = await AuthorizedConversation(me, conversationId, ct);
        if (conversation is null) return null;
        var readAt = DateTimeOffset.UtcNow;
        if (conversation.FirstUserId == me) conversation.FirstUserReadAt = readAt; else conversation.SecondUserReadAt = readAt;
        await db.SaveChangesAsync(ct);
        var query = db.Messages.AsNoTracking().Where(message => message.ConversationId == conversationId);
        if (before.HasValue) query = query.Where(message => message.SentAt < before.Value);
        var rows = await query.OrderByDescending(message => message.SentAt).Take(Math.Clamp(limit, 1, 100)).ToListAsync(ct);
        return rows.OrderBy(message => message.SentAt).Select(message => new MessageResponse(message.Id, message.SenderId, message.Content, message.SentAt, message.SenderId == me || message.SentAt <= readAt)).ToArray();
    }

    public async Task<(string Status, MessageResponse? Message)> SendAsync(Guid me, Guid conversationId, string content, CancellationToken ct)
    {
        var text = content.Trim();
        if (text.Length == 0 || text.Length > 2000) return ("Invalid", null);
        var conversation = await AuthorizedConversation(me, conversationId, ct);
        if (conversation is null) return ("Missing", null);
        var message = new Message { ConversationId = conversationId, SenderId = me, Content = text };
        conversation.UpdatedAt = message.SentAt;
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        return ("Created", new MessageResponse(message.Id, me, message.Content, message.SentAt, true));
    }

    private async Task<Conversation?> AuthorizedConversation(Guid me, Guid id, CancellationToken ct)
    {
        var item = await db.Conversations.SingleOrDefaultAsync(conversation => conversation.Id == id && (conversation.FirstUserId == me || conversation.SecondUserId == me), ct);
        if (item is null) return null;
        var accepted = await db.ConnectionRequests.AnyAsync(row => row.Status == "Accepted" && row.FirstUserId == item.FirstUserId && row.SecondUserId == item.SecondUserId, ct);
        return accepted ? item : null;
    }

    private async Task<Conversation> GetOrCreateConversationAsync(Guid first, Guid second, CancellationToken ct)
    {
        var (a, b) = Pair(first, second);
        var item = await db.Conversations.SingleOrDefaultAsync(row => row.FirstUserId == a && row.SecondUserId == b, ct);
        if (item is not null) return item;
        item = new Conversation { FirstUserId = a, SecondUserId = b };
        db.Conversations.Add(item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.Entry(item).State = EntityState.Detached; item = await db.Conversations.SingleAsync(row => row.FirstUserId == a && row.SecondUserId == b, ct); }
        return item;
    }

    private async Task<Dictionary<Guid, SocialUserResponse>> UserCards(Guid[] ids, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(user => ids.Contains(user.Id)).ToDictionaryAsync(user => user.Id,
            user => new SocialUserResponse(user.Id, user.Name, user.Bio, user.City,
                db.ProfilePhotos.Where(photo => photo.UserId == user.Id).OrderByDescending(photo => photo.IsAvatar).ThenBy(photo => photo.SortOrder).Select(photo => photo.Url).FirstOrDefault()), ct);

    private static (Guid First, Guid Second) Pair(Guid left, Guid right) => left.CompareTo(right) < 0 ? (left, right) : (right, left);
}
