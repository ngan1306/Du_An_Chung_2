using System.Security.Cryptography;
using System.Text;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Services;

internal interface IPasswordResetService
{
    Task RequestResetAsync(string email, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken);
}

internal sealed class PasswordResetService(
    DatingDbContext database,
    IPasswordHasher<User> passwordHasher,
    IPasswordResetEmailQueue emailQueue,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public async Task RequestResetAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await database.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (user is null)
        {
            return;
        }

        var previousTokens = await database.PasswordResetTokens
            .Where(reset => reset.UserId == user.Id)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (previousTokens.Any(reset => reset.CreatedAt > now.AddMinutes(-5)))
        {
            return;
        }

        database.PasswordResetTokens.RemoveRange(previousTokens);

        var token = CreateToken();
        database.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = now.Add(TokenLifetime)
        });
        await database.SaveChangesAsync(cancellationToken);

        var publicBaseUrl = configuration["App:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(publicBaseUrl) && environment.IsDevelopment())
        {
            publicBaseUrl = "http://localhost:5000";
        }

        if (string.IsNullOrWhiteSpace(publicBaseUrl)
            || !Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttps
                && !(environment.IsDevelopment() && baseUri.Scheme == Uri.UriSchemeHttp)))
        {
            logger.LogError("Password reset email is unavailable because App:PublicBaseUrl is not configured.");
            return;
        }

        var resetUrl = new UriBuilder(baseUri)
        {
            Path = "/",
            Query = $"resetToken={Uri.EscapeDataString(token)}",
            Fragment = string.Empty
        }.Uri.ToString();

        if (!emailQueue.TryQueue(user.Email, resetUrl))
        {
            logger.LogWarning("Password reset email queue is full; a reset message could not be queued.");
        }
    }

    public async Task<bool> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(token);
        var reset = await database.PasswordResetTokens
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);

        if (reset is null)
        {
            return false;
        }

        if (reset.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            database.PasswordResetTokens.Remove(reset);
            await database.SaveChangesAsync(cancellationToken);
            return false;
        }

        reset.User.PasswordHash = passwordHasher.HashPassword(reset.User, newPassword);
        database.PasswordResetTokens.Remove(reset);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static string CreateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static string HashToken(string token) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
