using KissAppTussing.Contracts;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace KissAppTussing.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/register", RegisterAsync)
            .AllowAnonymous()
            .WithName("Register")
            .WithSummary("Create an account and return an access token")
            .WithDescription("Creates an account for an adult user. Email addresses must be unique.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPost("/api/auth/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Sign in and return an access token")
            .WithDescription("Authenticates an account using its email address and password.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapPost("/api/auth/forgot-password", ForgotPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting("password-reset")
            .WithName("ForgotPassword")
            .WithSummary("Send a one-time password reset link when the account email exists")
            .Produces<ForgotPasswordResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status429TooManyRequests);

        endpoints.MapPost("/api/auth/reset-password", ResetPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting("password-reset")
            .WithName("ResetPassword")
            .WithSummary("Change a password using a valid, unexpired one-time reset token")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        endpoints.MapPost("/api/auth/change-password", ChangePasswordAsync)
            .RequireAuthorization()
            .WithName("ChangePassword")
            .WithSummary("Change the signed-in user's password")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        endpoints.MapGet("/api/auth/sessions", GetSessionsAsync)
            .RequireAuthorization()
            .WithName("GetSessions")
            .WithSummary("List active login sessions for the signed-in user");

        endpoints.MapDelete("/api/auth/sessions/{sessionId:guid}", RevokeSessionAsync)
            .RequireAuthorization()
            .WithName("RevokeSession")
            .WithSummary("Sign out a login session");

        endpoints.MapPost("/api/auth/sessions/revoke-others", RevokeOtherSessionsAsync)
            .RequireAuthorization()
            .WithName("RevokeOtherSessions")
            .WithSummary("Sign out all other devices");

        endpoints.MapPost("/api/auth/logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("Logout")
            .WithSummary("Revoke the current login session");

        endpoints.MapGet("/api/auth/email-verification", GetEmailVerificationStatusAsync)
            .RequireAuthorization()
            .WithName("GetEmailVerificationStatus")
            .WithSummary("Get the signed-in user's email verification status");

        endpoints.MapPost("/api/auth/email-verification/request", RequestEmailVerificationAsync)
            .RequireAuthorization()
            .RequireRateLimiting("email-verification")
            .WithName("RequestEmailVerification")
            .WithSummary("Email a one-time verification code");

        endpoints.MapPost("/api/auth/email-verification/verify", VerifyEmailAsync)
            .RequireAuthorization()
            .RequireRateLimiting("email-verification")
            .WithName("VerifyEmail")
            .WithSummary("Verify the signed-in user's email with a one-time code")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<Results<Created<AuthResponse>, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> RegisterAsync(
        RegisterRequest request,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, httpContext.Request.Headers.UserAgent.ToString(), cancellationToken);

        return result.Status switch
        {
            RegistrationStatus.Created => TypedResults.Created(
                $"/api/profiles/{result.Response!.Profile.Id}", result.Response),
            RegistrationStatus.EmailTaken => TypedResults.Conflict(new ProblemDetails
            {
                Title = "Email already registered",
                Status = StatusCodes.Status409Conflict,
                Detail = "An account with this email address already exists."
            }),
            RegistrationStatus.TermsNotAccepted => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Terms must be accepted",
                Status = StatusCodes.Status400BadRequest,
                Detail = "You must accept the terms of service to create an account."
            }),
            _ => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Age requirement not met",
                Status = StatusCodes.Status400BadRequest,
                Detail = "You must be at least 18 years old to create an account."
            })
        };
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest request,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, httpContext.Request.Headers.UserAgent.ToString(), cancellationToken);
        return response is null ? TypedResults.Unauthorized() : TypedResults.Ok(response);
    }

    private static async Task<Results<NoContent, BadRequest<ProblemDetails>>> ChangePasswordAsync(
        ChangePasswordRequest request,
        System.Security.Claims.ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var changed = await authService.ChangePasswordAsync(
            CurrentUser.GetId(principal), CurrentUser.GetSessionId(principal), request, cancellationToken);
        return changed
            ? TypedResults.NoContent()
            : TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Không thể đổi mật khẩu",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Mật khẩu hiện tại không chính xác."
            });
    }

    private static async Task<Ok<IReadOnlyList<UserSessionResponse>>> GetSessionsAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await authService.GetSessionsAsync(
            CurrentUser.GetId(principal), CurrentUser.GetSessionId(principal), cancellationToken));

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> RevokeSessionAsync(
        Guid sessionId,
        System.Security.Claims.ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var revoked = await authService.RevokeSessionAsync(CurrentUser.GetId(principal), sessionId, cancellationToken);
        return revoked ? TypedResults.NoContent() : TypedResults.NotFound(new ProblemDetails
        {
            Title = "Phiên đăng nhập không tồn tại",
            Status = StatusCodes.Status404NotFound
        });
    }

    private static async Task<Ok<object>> RevokeOtherSessionsAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok<object>(new { revokedCount = await authService.RevokeOtherSessionsAsync(
            CurrentUser.GetId(principal), CurrentUser.GetSessionId(principal), cancellationToken) });

    private static async Task<NoContent> LogoutAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        await authService.RevokeCurrentSessionAsync(
            CurrentUser.GetId(principal), CurrentUser.GetSessionId(principal), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<EmailVerificationStatusResponse>, NotFound>> GetEmailVerificationStatusAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IEmailVerificationService service,
        CancellationToken cancellationToken)
    {
        var status = await service.GetStatusAsync(CurrentUser.GetId(principal), cancellationToken);
        return status is null ? TypedResults.NotFound() : TypedResults.Ok(status);
    }

    private static async Task<NoContent> RequestEmailVerificationAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        IEmailVerificationService service,
        CancellationToken cancellationToken)
    {
        await service.RequestCodeAsync(CurrentUser.GetId(principal), cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, BadRequest<ProblemDetails>>> VerifyEmailAsync(
        EmailVerificationRequest request,
        System.Security.Claims.ClaimsPrincipal principal,
        IEmailVerificationService service,
        CancellationToken cancellationToken) =>
        await service.VerifyAsync(CurrentUser.GetId(principal), request.Code, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Mã xác minh không hợp lệ",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Mã đã hết hạn hoặc không chính xác. Hãy yêu cầu mã mới."
            });

    private static async Task<Accepted<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        IPasswordResetService passwordResetService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        await passwordResetService.RequestResetAsync(request.Email, cancellationToken);
        return TypedResults.Accepted((string?)null, new ForgotPasswordResponse(
            "Nếu email khớp với tài khoản, bạn sẽ nhận được liên kết đặt lại mật khẩu."));
    }

    private static async Task<Results<NoContent, BadRequest<ProblemDetails>>> ResetPasswordAsync(
        ResetPasswordRequest request,
        IPasswordResetService passwordResetService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        var reset = await passwordResetService.ResetPasswordAsync(
            request.Token, request.NewPassword, cancellationToken);
        return reset
            ? TypedResults.NoContent()
            : TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid or expired reset link",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Hãy yêu cầu liên kết mới."
            });
    }
}
