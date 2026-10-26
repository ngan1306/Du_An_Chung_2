# Password reset

The login screen's **Quên mật khẩu?** link requests a one-time reset link. The link expires after 30 minutes. The database stores only a SHA-256 hash of the token, a new request invalidates older tokens, successful use consumes the token, and reset requests are rate-limited. The API returns the same message whether or not the email belongs to an account.

## Local development

If SMTP is not configured while running in Development, the app writes a development-only reset URL to the server console. Open that link to set a new password. The token is not returned by the API.

## Configure email delivery

Set the following environment variables on the application host, then restart the app:

```powershell
$env:Smtp__Host = "your-smtp-host"
$env:Smtp__Port = "587"
$env:Smtp__Username = "your-smtp-user"
$env:Smtp__Password = "your-smtp-password"
$env:Email__FromAddress = "account@your-domain.example"
$env:Email__FromName = "Kiss"
$env:App__PublicBaseUrl = "https://your-app-domain.example"
```

Keep the SMTP password in the host's secret store or environment, not in a checked-in settings file. In Production, `App:PublicBaseUrl` must be set to the public HTTPS address so reset emails point to the correct site. The message is sent over SMTP with TLS enabled by default. Configure a relay that supports this authentication method.
