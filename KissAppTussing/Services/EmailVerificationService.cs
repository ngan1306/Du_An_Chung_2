using System.Security.Cryptography;
using System.Text;
using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Services;

internal interface IEmailVerificationService
{
    Task<bool> RequestCodeAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> VerifyAsync(Guid userId, string code, CancellationToken cancellationToken);
    Task<EmailVerificationStatusResponse?> GetStatusAsync(Guid userId, CancellationToken cancellationToken);
}

internal sealed class EmailVerificationService(
    DatingDbContext database,
    IPasswordResetEmailQueue emailQueue) : IEmailVerificationService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RequestCooldown = TimeSpan.FromMinutes(1);
    private const int MaxAttempts = 5;

    public async Task<bool> RequestCodeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await database.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || user.EmailVerifiedAt is not null) return false;

        var now = DateTimeOffset.UtcNow;
        var tokens = await database.EmailVerificationTokens
            .Where(token => token.UserId == userId && token.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        if (tokens.Any(token => token.CreatedAt > now - RequestCooldown)) return false;
        database.EmailVerificationTokens.RemoveRange(tokens);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        database.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = userId,
            CodeHash = HashCode(userId, code),
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime)
        });
        await database.SaveChangesAsync(cancellationToken);
        return emailQueue.TryQueueVerificationCode(user.Email, code);
    }

    public async Task<bool> VerifyAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await database.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null) return false;
        if (user.EmailVerifiedAt is not null) return true;

        var now = DateTimeOffset.UtcNow;
        var token = await database.EmailVerificationTokens
            .Where(candidate => candidate.UserId == userId && candidate.ConsumedAt == null && candidate.ExpiresAt > now)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (token is null || token.Attempts >= MaxAttempts) return false;

        token.Attempts++;
        var expected = Convert.FromHexString(token.CodeHash);
        var provided = Convert.FromHexString(HashCode(userId, code));
        if (!CryptographicOperations.FixedTimeEquals(expected, provided))
        {
            if (token.Attempts >= MaxAttempts) token.ConsumedAt = now;
            await database.SaveChangesAsync(cancellationToken);
            return false;
        }

        token.ConsumedAt = now;
        user.EmailVerifiedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmailVerificationStatusResponse?> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == userId, cancellationToken);
        return user is null ? null : new EmailVerificationStatusResponse(
            user.EmailVerifiedAt is not null, user.EmailVerifiedAt);
    }

    private static string HashCode(Guid userId, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}:{code}")));
}
