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

        return endpoints;
    }

    private static async Task<Results<Created<AuthResponse>, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> RegisterAsync(
        RegisterRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);

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
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        return response is null ? TypedResults.Unauthorized() : TypedResults.Ok(response);
    }

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
