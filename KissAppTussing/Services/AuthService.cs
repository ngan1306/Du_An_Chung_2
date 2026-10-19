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

        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken);

        return new RegistrationResult(RegistrationStatus.Created, CreateAuthResponse(user));
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
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

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        return verification == PasswordVerificationResult.Failed ? null : CreateAuthResponse(user);
    }

    private AuthResponse CreateAuthResponse(User user)
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
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
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