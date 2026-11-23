using System.Threading.Channels;

namespace KissAppTussing.Services;

internal interface IPasswordResetEmailQueue
{
    bool TryQueue(string email, string resetUrl);
    bool TryQueueVerificationCode(string email, string code);
}

internal sealed class PasswordResetEmailQueue(
    IServiceScopeFactory scopeFactory,
    ILogger<PasswordResetEmailQueue> logger) : BackgroundService, IPasswordResetEmailQueue
{
    private readonly Channel<QueuedEmail> _messages = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryQueue(string email, string resetUrl) => _messages.Writer.TryWrite(new QueuedEmail(email, resetUrl, false));
    public bool TryQueueVerificationCode(string email, string code) => _messages.Writer.TryWrite(new QueuedEmail(email, code, true));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _messages.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                if (message.IsVerificationCode)
                    await emailSender.SendEmailVerificationCodeAsync(message.Email, message.Value, stoppingToken);
                else
                    await emailSender.SendPasswordResetLinkAsync(message.Email, message.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not deliver a password reset email.");
            }
        }
    }

    private sealed record QueuedEmail(string Email, string Value, bool IsVerificationCode);
}
