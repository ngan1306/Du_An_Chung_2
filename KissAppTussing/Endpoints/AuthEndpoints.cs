using KissAppTussing.Contracts;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Http.HttpResults;
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
}