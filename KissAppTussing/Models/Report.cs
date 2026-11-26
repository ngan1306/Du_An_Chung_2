namespace KissAppTussing.Models;

internal sealed class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterId { get; set; }
    public Guid? TargetUserId { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? MessageId { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
