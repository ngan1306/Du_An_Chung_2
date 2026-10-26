namespace KissAppTussing.Models;

internal sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public Guid RecipientId { get; set; }
    public User Recipient { get; set; } = null!;
    public string? Content { get; set; }
    public string? MediaFileName { get; set; }
    public string? MediaType { get; set; }
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
