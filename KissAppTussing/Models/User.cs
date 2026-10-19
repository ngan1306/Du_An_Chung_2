namespace KissAppTussing.Models;

internal sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public DateOnly BirthDate { get; set; }
    public string Bio { get; set; } = string.Empty;
    public string Gender { get; set; } = "PreferNotToSay";
    public string? City { get; set; }
    public string? Occupation { get; set; }
    public string? Education { get; set; }
    public string LookingFor { get; set; } = "Dating";
    public DateTimeOffset? TermsAcceptedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<UserInterest> Interests { get; set; } = [];
    public ICollection<ProfilePhoto> Photos { get; set; } = [];
}