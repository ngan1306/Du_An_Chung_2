using System.Text;
using KissAppTussing.Data;
using KissAppTussing.Endpoints;
using KissAppTussing.Models;
using KissAppTussing.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
	throw new InvalidOperationException("Jwt:Key must contain at least 32 UTF-8 bytes.");
}

if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
	throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");
}

builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<DatingDbContext>(options =>
	options.UseSqlServer(builder.Configuration.GetConnectionString("DatingApp")));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<PasswordResetEmailQueue>();
builder.Services.AddSingleton<IPasswordResetEmailQueue>(serviceProvider =>
	serviceProvider.GetRequiredService<PasswordResetEmailQueue>());
builder.Services.AddHostedService(serviceProvider =>
	serviceProvider.GetRequiredService<PasswordResetEmailQueue>());
builder.Services.AddScoped<IDatingService, DatingService>();
builder.Services.AddScoped<IJourneyService, JourneyService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = jwtIssuer,
			ValidateAudience = true,
			ValidAudience = jwtAudience,
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromSeconds(30)
		};
	});
var configuredAdminUserIds = builder.Configuration.GetSection("Admin:UserIds").Get<Guid[]>() ?? [];
builder.Services.AddAuthorization(options =>
{
	options.AddPolicy("OwnerAdmin", policy => policy
		.RequireAuthenticatedUser()
		.RequireAssertion(context =>
			Guid.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId)
			&& configuredAdminUserIds.Contains(userId)));
});
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.AddPolicy("password-reset", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
		context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
		_ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
		{
			PermitLimit = 5,
			Window = TimeSpan.FromMinutes(15),
			QueueLimit = 0,
			AutoReplenishment = true
		}));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapInterestEndpoints();
app.MapProfilePhotoEndpoints();
app.MapSwipeEndpoints();
app.MapMatchEndpoints();
app.MapJourneyEndpoints();
app.MapChatEndpoints();
app.MapAdminEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
	var database = scope.ServiceProvider.GetRequiredService<DatingDbContext>();
	await DatabaseInitializer.InitializeAsync(database);
}

app.Run();
