using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace KissAppTussing.Services;

internal sealed class AuthService(
    DatingDbContext database,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration) : IAuthService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task<RegistrationResult> RegisterAsync(
        RegisterRequest request,
        string deviceName,
        CancellationToken cancellationToken)
    {
        if (!request.AcceptTerms)
        {
            return new RegistrationResult(RegistrationStatus.TermsNotAccepted, null);
        }

        var birthDate = request.BirthDate;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age))
        {
            age--;
        }

        if (age < 18)
        {
            return new RegistrationResult(RegistrationStatus.Underage, null);
        }

        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        var emailExists = await database.Users
            .AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            return new RegistrationResult(RegistrationStatus.EmailTaken, null);
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = string.Empty,
            BirthDate = birthDate,
            Gender = request.Gender,
            TermsAcceptedAt = DateTimeOffset.UtcNow,
            Bio = request.Bio.Trim()
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var session = CreateSession(user.Id, deviceName);
        database.Users.Add(user);
        database.UserSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);

        return new RegistrationResult(RegistrationStatus.Created, CreateAuthResponse(user, session.Id));
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, string deviceName, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await database.Users
            .Include(candidate => candidate.Photos)
            .Include(candidate => candidate.Interests)
                .ThenInclude(userInterest => userInterest.Interest)
            .AsSplitQuery()
            .SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (user.IsSuspended) return null;

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed) return null;

        var session = CreateSession(user.Id, deviceName);
        user.LastSeenAt = session.LastSeenAt;
        database.UserSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
        return CreateAuthResponse(user, session.Id);
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        Guid currentSessionId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await database.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            return false;
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        var now = DateTimeOffset.UtcNow;
        var otherSessions = await database.UserSessions
            .Where(session => session.UserId == userId && session.Id != currentSessionId && session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in otherSessions) session.RevokedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<UserSessionResponse>> GetSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var current = await database.UserSessions.SingleOrDefaultAsync(
            session => session.Id == currentSessionId && session.UserId == userId && session.RevokedAt == null,
            cancellationToken);
        if (current is null) return [];

        current.LastSeenAt = now;
        var user = await database.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is not null) user.LastSeenAt = now;
        await database.SaveChangesAsync(cancellationToken);

        return await database.UserSessions.AsNoTracking()
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .OrderByDescending(session => session.LastSeenAt)
            .Select(session => new UserSessionResponse(
                session.Id, session.DeviceName, session.CreatedAt, session.LastSeenAt, session.Id == currentSessionId))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await database.UserSessions.SingleOrDefaultAsync(
            candidate => candidate.UserId == userId && candidate.Id == sessionId && candidate.RevokedAt == null,
            cancellationToken);
        if (session is null) return false;
        session.RevokedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> RevokeOtherSessionsAsync(Guid userId, Guid currentSessionId, CancellationToken cancellationToken)
    {
        var sessions = await database.UserSessions
            .Where(session => session.UserId == userId && session.Id != currentSessionId && session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var session in sessions) session.RevokedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        return sessions.Count;
    }

    public Task<bool> RevokeCurrentSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        RevokeSessionAsync(userId, sessionId, cancellationToken);

    private static UserSession CreateSession(Guid userId, string deviceName) => new()
    {
        UserId = userId,
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Thiết bị không xác định" : deviceName.Trim()[..Math.Min(deviceName.Trim().Length, 160)]
    };

    private AuthResponse CreateAuthResponse(User user, Guid sessionId)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(TokenLifetime);
        var issuer = configuration["Jwt:Issuer"]!;
        var audience = configuration["Jwt:Audience"]!;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("sid", sessionId.ToString())
        };
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            expiresAt,
            ToProfile(user));
    }

    private static UserProfileResponse ToProfile(User user) => new(
        user.Id,
        user.Name,
        user.BirthDate,
        user.Bio,
        user.Gender,
        user.City,
        user.Occupation,
        user.Education,
        user.LookingFor,
        user.Photos.Select(photo => new ProfilePhotoResponse(photo.Id, photo.Url, photo.IsAvatar, photo.SortOrder)).ToArray(),
        user.Interests.Select(userInterest => new InterestResponse(userInterest.InterestId, userInterest.Interest.Name)).ToArray(),
        user.CreatedAt);
}
