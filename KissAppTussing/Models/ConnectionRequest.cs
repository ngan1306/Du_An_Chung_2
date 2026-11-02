namespace KissAppTussing.Models;

internal sealed class ConnectionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirstUserId { get; set; }
    public Guid SecondUserId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
