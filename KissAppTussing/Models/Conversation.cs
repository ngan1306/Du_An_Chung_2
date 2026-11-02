namespace KissAppTussing.Models;

internal sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirstUserId { get; set; }
    public Guid SecondUserId { get; set; }
    public DateTimeOffset? FirstUserReadAt { get; set; }
    public DateTimeOffset? SecondUserReadAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Message> Messages { get; set; } = [];
}
