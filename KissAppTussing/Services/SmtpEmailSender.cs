using System.Net;
using System.Net.Mail;
using System.Text;

namespace KissAppTussing.Services;

internal interface IEmailSender
{
    Task SendPasswordResetLinkAsync(string email, string resetUrl, CancellationToken cancellationToken);
    Task SendEmailVerificationCodeAsync(string email, string code, CancellationToken cancellationToken);
}

internal sealed class SmtpEmailSender(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendPasswordResetLinkAsync(
        string email,
        string resetUrl,
        CancellationToken cancellationToken)
    {
        var host = configuration["Smtp:Host"];
        var senderAddress = configuration["Email:FromAddress"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(senderAddress))
        {
            if (environment.IsDevelopment())
            {
                logger.LogInformation("SMTP is not configured. Development-only password reset link: {ResetUrl}", resetUrl);
            }
            else
            {
                logger.LogWarning("SMTP is not configured; password reset email was not sent.");
            }

            return;
        }

        var senderName = configuration["Email:FromName"] ?? "Kiss";
        using var message = new MailMessage
        {
            From = new MailAddress(senderAddress, senderName, Encoding.UTF8),
            Subject = "Đặt lại mật khẩu Kiss",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
            Body = $"Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản Kiss của bạn.\n\n"
                + $"Mở liên kết này trong vòng 30 phút để tạo mật khẩu mới:\n{resetUrl}\n\n"
                + "Nếu bạn không yêu cầu đổi mật khẩu, hãy bỏ qua email này."
        };
        message.To.Add(new MailAddress(email));

        var port = configuration.GetValue<int?>("Smtp:Port") ?? 587;
        var enableSsl = configuration.GetValue("Smtp:EnableSsl", true);
        if (!enableSsl && !environment.IsDevelopment())
        {
            logger.LogError("Password reset email requires TLS outside Development.");
            return;
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Timeout = 15000
        };

        var username = configuration["Smtp:Username"];
        var password = configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password ?? string.Empty);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendEmailVerificationCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        var host = configuration["Smtp:Host"];
        var senderAddress = configuration["Email:FromAddress"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(senderAddress))
        {
            if (environment.IsDevelopment()) logger.LogInformation("SMTP is not configured. Development-only email verification code: {Code}", code);
            else logger.LogWarning("SMTP is not configured; email verification code was not sent.");
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(senderAddress, configuration["Email:FromName"] ?? "Kiss", Encoding.UTF8),
            Subject = "Mã xác minh email Kiss",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
            Body = $"Mã xác minh email Kiss của bạn là: {code}\n\nMã có hiệu lực trong 10 phút. Nếu bạn không yêu cầu mã này, hãy bỏ qua email."
        };
        message.To.Add(new MailAddress(email));

        var port = configuration.GetValue<int?>("Smtp:Port") ?? 587;
        var enableSsl = configuration.GetValue("Smtp:EnableSsl", true);
        if (!enableSsl && !environment.IsDevelopment()) return;
        using var client = new SmtpClient(host, port) { EnableSsl = enableSsl, Timeout = 15000 };
        var username = configuration["Smtp:Username"];
        var password = configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username)) client.Credentials = new NetworkCredential(username, password ?? string.Empty);
        await client.SendMailAsync(message, cancellationToken);
    }
}
