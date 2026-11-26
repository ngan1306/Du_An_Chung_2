namespace KissAppTussing.Models;

internal sealed class ContentReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterUserId { get; set; }
    public required string TargetType { get; set; }
    public Guid TargetId { get; set; }
    public required string Reason { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
}
