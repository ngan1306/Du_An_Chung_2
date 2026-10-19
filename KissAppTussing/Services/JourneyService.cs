using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Services;

internal sealed class JourneyService(DatingDbContext database, IWebHostEnvironment environment) : IJourneyService
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const long MaxVideoBytes = 50 * 1024 * 1024;

    public async Task<IReadOnlyList<JourneyPostResponse>> GetFeedAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken)
    {
        var posts = await database.JourneyPosts
            .AsNoTracking()
            .Include(post => post.User)
            .Include(post => post.Likes)
            .Include(post => post.Comments)
            .OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return posts.Select(post => ToResponse(post, userId)).ToArray();
    }

    public async Task<JourneyPostOperationResult> CreatePostAsync(
        Guid userId,
        string? content,
        IFormFile? media,
        CancellationToken cancellationToken)
    {
        var text = content?.Trim() ?? string.Empty;
        if (text.Length > 2000 || (text.Length == 0 && (media is null || media.Length == 0)))
        {
            return new JourneyPostOperationResult(JourneyPostOperationStatus.EmptyPost);
        }

        var user = await database.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            return new JourneyPostOperationResult(JourneyPostOperationStatus.NotFound);
        }

        string? mediaUrl = null;
        string? mediaType = null;
        string? physicalPath = null;
        var postId = Guid.NewGuid();

        if (media is { Length: > 0 })
        {
            var detectedMedia = await DetectMediaAsync(media, cancellationToken);
            if (detectedMedia is null)
            {
                return new JourneyPostOperationResult(JourneyPostOperationStatus.UnsupportedMedia);
            }

            var limit = detectedMedia.Value.MediaType.StartsWith("image/", StringComparison.Ordinal)
                ? MaxImageBytes
                : MaxVideoBytes;
            if (media.Length > limit)
            {
                return new JourneyPostOperationResult(JourneyPostOperationStatus.MediaTooLarge);
            }

            var fileName = $"{postId:N}{detectedMedia.Value.Extension}";
            var relativeDirectory = Path.Combine("uploads", "journey", userId.ToString("N"));
            var directory = Path.Combine(GetWebRootPath(), relativeDirectory);
            Directory.CreateDirectory(directory);
            physicalPath = Path.Combine(directory, fileName);
            mediaUrl = $"/{relativeDirectory.Replace(Path.DirectorySeparatorChar, '/')}/{fileName}";
            mediaType = detectedMedia.Value.MediaType;

            try
            {
                await using var input = media.OpenReadStream();
                await using var output = File.Create(physicalPath);
                await input.CopyToAsync(output, cancellationToken);
            }
            catch
            {
                if (File.Exists(physicalPath)) File.Delete(physicalPath);
                throw;
            }
        }

        var post = new JourneyPost
        {
            Id = postId,
            UserId = userId,
            User = user,
            Content = text,
            MediaUrl = mediaUrl,
            MediaType = mediaType
        };

        try
        {
            database.JourneyPosts.Add(post);
            await database.SaveChangesAsync(cancellationToken);
            return new JourneyPostOperationResult(JourneyPostOperationStatus.Created, ToResponse(post, userId));
        }
        catch
        {
            if (physicalPath is not null && File.Exists(physicalPath)) File.Delete(physicalPath);
            throw;
        }
    }

    public async Task<JourneyPostOperationStatus> DeletePostAsync(
        Guid userId,
        Guid postId,
        CancellationToken cancellationToken)
    {
        var post = await database.JourneyPosts
            .SingleOrDefaultAsync(candidate => candidate.Id == postId && candidate.UserId == userId, cancellationToken);
        if (post is null)
        {
            return JourneyPostOperationStatus.NotFound;
        }

        database.JourneyPosts.Remove(post);
        await database.SaveChangesAsync(cancellationToken);

        if (post.MediaUrl is not null)
        {
            var localPath = Path.Combine(
                GetWebRootPath(),
                post.MediaUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(localPath)) File.Delete(localPath);
        }

        return JourneyPostOperationStatus.Deleted;
    }

    public async Task<JourneyLikeResponse> ToggleLikeAsync(Guid userId, Guid postId, CancellationToken cancellationToken)
    {
        var post = await database.JourneyPosts
            .SingleOrDefaultAsync(candidate => candidate.Id == postId, cancellationToken);
        if (post is null)
        {
            throw new KeyNotFoundException("Journey post not found.");
        }

        var existing = await database.JourneyPostLikes
            .SingleOrDefaultAsync(like => like.PostId == postId && like.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            database.JourneyPostLikes.Remove(existing);
            await database.SaveChangesAsync(cancellationToken);
            return new JourneyLikeResponse(postId, false, await database.JourneyPostLikes.CountAsync(like => like.PostId == postId, cancellationToken));
        }

        database.JourneyPostLikes.Add(new JourneyPostLike { PostId = postId, UserId = userId });
        await database.SaveChangesAsync(cancellationToken);
        return new JourneyLikeResponse(postId, true, await database.JourneyPostLikes.CountAsync(like => like.PostId == postId, cancellationToken));
    }

    public async Task<IReadOnlyList<JourneyCommentResponse>> GetCommentsAsync(Guid userId, Guid postId, CancellationToken cancellationToken)
    {
        var postExists = await database.JourneyPosts.AnyAsync(candidate => candidate.Id == postId, cancellationToken);
        if (!postExists)
        {
            return [];
        }

        var comments = await database.JourneyComments
            .AsNoTracking()
            .Include(comment => comment.User)
            .Where(comment => comment.PostId == postId)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToListAsync(cancellationToken);

        return comments.Select(comment => new JourneyCommentResponse(
            comment.Id,
            comment.PostId,
            comment.UserId,
            comment.User.Name,
            comment.Content,
            comment.CreatedAt)).ToArray();
    }

    public async Task<JourneyCommentResponse> AddCommentAsync(Guid userId, Guid postId, string content, CancellationToken cancellationToken)
    {
        var post = await database.JourneyPosts.SingleOrDefaultAsync(candidate => candidate.Id == postId, cancellationToken);
        if (post is null)
        {
            throw new KeyNotFoundException("Journey post not found.");
        }

        var trimmed = (content ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > 500)
        {
            throw new InvalidOperationException("Comment text must be between 1 and 500 characters.");
        }

        var user = await database.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        var comment = new JourneyComment
        {
            PostId = postId,
            UserId = userId,
            Content = trimmed,
            User = user,
            Post = post
        };

        database.JourneyComments.Add(comment);
        await database.SaveChangesAsync(cancellationToken);
        return new JourneyCommentResponse(comment.Id, postId, userId, user.Name, trimmed, comment.CreatedAt);
    }

    private string GetWebRootPath() =>
        environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");

    private static JourneyPostResponse ToResponse(JourneyPost post, Guid currentUserId) => new(
        post.Id,
        post.UserId,
        post.User.Name,
        post.Content,
        post.MediaUrl,
        post.MediaType,
        post.CreatedAt,
        post.UserId == currentUserId,
        post.Likes.Count,
        post.Comments.Count,
        post.Likes.Any(like => like.UserId == currentUserId));

    private static async Task<(string Extension, string MediaType)?> DetectMediaAsync(
        IFormFile media,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = media.OpenReadStream();
        var count = await stream.ReadAsync(header, cancellationToken);

        if (count >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return (".png", "image/png");
        if (count >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return (".jpg", "image/jpeg");
        if (count >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return (".webp", "image/webp");
        if (count >= 8 && header.AsSpan(4, 4).SequenceEqual("ftyp"u8))
            return (".mp4", "video/mp4");
        if (count >= 4 && header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
            return (".webm", "video/webm");

        return null;
    }
}