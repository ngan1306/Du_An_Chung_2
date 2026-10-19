namespace KissAppTussing.Models;

internal sealed class Interest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public ICollection<UserInterest> Users { get; set; } = [];
}