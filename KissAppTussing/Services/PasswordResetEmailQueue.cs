using System.Threading.Channels;

namespace KissAppTussing.Services;

internal interface IPasswordResetEmailQueue
{
    bool TryQueue(string email, string resetUrl);
}

internal sealed class PasswordResetEmailQueue(
    IServiceScopeFactory scopeFactory,
    ILogger<PasswordResetEmailQueue> logger) : BackgroundService, IPasswordResetEmailQueue
{
    private readonly Channel<ResetEmail> _messages = Channel.CreateBounded<ResetEmail>(
        new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryQueue(string email, string resetUrl) => _messages.Writer.TryWrite(new ResetEmail(email, resetUrl));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _messages.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                await emailSender.SendPasswordResetLinkAsync(message.Email, message.ResetUrl, stoppingToken);
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

    private sealed record ResetEmail(string Email, string ResetUrl);
}
