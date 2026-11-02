using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin")
            .RequireAuthorization("OwnerAdmin");

        admin.MapGet("/overview", GetOverviewAsync)
            .WithName("GetAdminOverview")
            .WithSummary("Get an overview of application data for an authorized owner");

        admin.MapGet("/export", ExportDataAsync)
            .WithName("ExportAdminData")
            .WithSummary("Export profiles and social activity as JSON; private chat contents are excluded");

        admin.MapGet("/users", GetUsersAsync)
            .WithName("GetAdminUsers")
            .WithSummary("List user accounts for an authorized administrator");

        admin.MapPost("/users", CreateUserAsync)
            .WithName("CreateAdminUser")
            .WithSummary("Create a user account");

        admin.MapPut("/users/{userId:guid}", UpdateUserAsync)
            .WithName("UpdateAdminUser")
            .WithSummary("Update a user account");

        admin.MapDelete("/users/{userId:guid}", DeleteUserAsync)
            .WithName("DeleteAdminUser")
            .WithSummary("Delete a user account and its related social data");

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<AdminManagedUser>>> GetUsersAsync(
        DatingDbContext database,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store, private";
        var users = await database.Users.AsNoTracking()
            .OrderByDescending(user => user.CreatedAt)
            .Select(user => new AdminManagedUser(
                user.Id,
                user.Name,
                user.Email,
                user.BirthDate,
                user.Gender,
                user.Bio,
                user.City,
                user.Occupation,
                user.Education,
                user.LookingFor,
                user.CreatedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<AdminManagedUser>>(users);
    }

    private static async Task<IResult> CreateUserAsync(
        AdminCreateUserRequest request,
        DatingDbContext database,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRequest(request);
        AddBirthDateErrorIfInvalid(request.BirthDate, errors);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        if (await database.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return TypedResults.Problem("Email này đã được sử dụng.", statusCode: StatusCodes.Status409Conflict);
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = string.Empty,
            BirthDate = request.BirthDate,
            Gender = request.Gender,
            Bio = request.Bio?.Trim() ?? string.Empty,
            City = NormalizeOptionalText(request.City),
            Occupation = NormalizeOptionalText(request.Occupation),
            Education = NormalizeOptionalText(request.Education),
            LookingFor = request.LookingFor,
            TermsAcceptedAt = null
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/users/{user.Id}", ToAdminManagedUser(user));
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid userId,
        AdminUpdateUserRequest request,
        DatingDbContext database,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRequest(request);
        AddBirthDateErrorIfInvalid(request.BirthDate, errors);
        AddNewPasswordErrorIfInvalid(request.NewPassword, errors);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var user = await database.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            return TypedResults.Problem("Không tìm thấy tài khoản.", statusCode: StatusCodes.Status404NotFound);
        }

        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        if (await database.Users.AnyAsync(candidate => candidate.Id != userId && candidate.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return TypedResults.Problem("Email này đã được sử dụng.", statusCode: StatusCodes.Status409Conflict);
        }

        user.Name = request.Name.Trim();
        user.Email = email;
        user.NormalizedEmail = normalizedEmail;
        user.BirthDate = request.BirthDate;
        user.Gender = request.Gender;
        user.Bio = request.Bio?.Trim() ?? string.Empty;
        user.City = NormalizeOptionalText(request.City);
        user.Occupation = NormalizeOptionalText(request.Occupation);
        user.Education = NormalizeOptionalText(request.Education);
        user.LookingFor = request.LookingFor;
        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
            await database.PasswordResetTokens
                .Where(token => token.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToAdminManagedUser(user));
    }

    private static async Task<IResult> DeleteUserAsync(
        Guid userId,
        DatingDbContext database,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<AdminDataExport> logger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var signedInUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var configuredAdminIds = configuration.GetSection("Admin:UserIds").Get<Guid[]>() ?? [];
        if (string.Equals(signedInUserId, userId.ToString(), StringComparison.OrdinalIgnoreCase)
            || configuredAdminIds.Contains(userId))
        {
            return TypedResults.Problem("Không thể xóa tài khoản quản trị viên đang được cấu hình.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!await database.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return TypedResults.Problem("Không tìm thấy tài khoản.", statusCode: StatusCodes.Status404NotFound);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var conversationIds = await database.Conversations.AsNoTracking()
            .Where(conversation => conversation.FirstUserId == userId || conversation.SecondUserId == userId)
            .Select(conversation => conversation.Id)
            .ToListAsync(cancellationToken);

        await database.JourneyPostLikes.Where(like => like.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.JourneyComments.Where(comment => comment.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.JourneyPosts.Where(post => post.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.Swipes.Where(swipe => swipe.FromUserId == userId || swipe.ToUserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.Matches.Where(match => match.FirstUserId == userId || match.SecondUserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.ChatMessages.Where(message => message.SenderId == userId || message.RecipientId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.ConnectionRequests
            .Where(connection => connection.FirstUserId == userId || connection.SecondUserId == userId || connection.RequestedByUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Messages
            .Where(message => message.SenderId == userId || conversationIds.Contains(message.ConversationId))
            .ExecuteDeleteAsync(cancellationToken);
        await database.Conversations
            .Where(conversation => conversation.FirstUserId == userId || conversation.SecondUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.PasswordResetTokens.Where(token => token.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.UserInterests.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.ProfilePhotos.Where(photo => photo.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        var deletedUsers = await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync(cancellationToken);
        if (deletedUsers == 0)
        {
            return TypedResults.Problem("Không tìm thấy tài khoản.", statusCode: StatusCodes.Status404NotFound);
        }

        await transaction.CommitAsync(cancellationToken);
        DeleteUserUploads(environment, userId, logger);
        return TypedResults.NoContent();
    }

    private static void DeleteUserUploads(IWebHostEnvironment environment, Guid userId, ILogger logger)
    {
        var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        var uploadsRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads"));
        foreach (var category in new[] { "profiles", "journey" })
        {
            var directory = Path.GetFullPath(Path.Combine(uploadsRoot, category, userId.ToString("N")));
            var relativePath = Path.GetRelativePath(uploadsRoot, directory);
            if (relativePath == "." || relativePath == ".." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not remove upload files for deleted account {UserId}", userId);
            }
        }
    }

    private static Dictionary<string, string[]> ValidateRequest(object request)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);
        return validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
                .Select(member => (Member: member, Message: result.ErrorMessage ?? "Giá trị không hợp lệ.")))
            .GroupBy(error => error.Member)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).Distinct().ToArray());
    }

    private static void AddBirthDateErrorIfInvalid(DateOnly birthDate, IDictionary<string, string[]> errors)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (birthDate > today.AddYears(-18) || birthDate < today.AddYears(-120))
        {
            errors[nameof(AdminCreateUserRequest.BirthDate)] = ["Ngày sinh phải hợp lệ và người dùng phải từ 18 đến 120 tuổi."];
        }
    }

    private static void AddNewPasswordErrorIfInvalid(string? newPassword, IDictionary<string, string[]> errors)
    {
        if (!string.IsNullOrEmpty(newPassword) && newPassword.Length < 8)
        {
            errors[nameof(AdminUpdateUserRequest.NewPassword)] = ["Mật khẩu mới phải có ít nhất 8 ký tự."];
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static AdminManagedUser ToAdminManagedUser(User user) => new(
        user.Id,
        user.Name,
        user.Email,
        user.BirthDate,
        user.Gender,
        user.Bio,
        user.City,
        user.Occupation,
        user.Education,
        user.LookingFor,
        user.CreatedAt);

    private static async Task<Ok<AdminOverviewResponse>> GetOverviewAsync(
        DatingDbContext database,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store, private";
        var overview = new AdminOverviewResponse(
            await database.Users.CountAsync(cancellationToken),
            await database.Swipes.CountAsync(cancellationToken),
            await database.Matches.CountAsync(cancellationToken),
            await database.JourneyPosts.CountAsync(cancellationToken),
            await database.JourneyComments.CountAsync(cancellationToken),
            await database.ChatMessages.CountAsync(cancellationToken),
            await database.Users.AsNoTracking()
                .OrderByDescending(user => user.CreatedAt)
                .Take(10)
                .Select(user => new AdminUserSummary(user.Id, user.Name, user.Email, user.CreatedAt))
                .ToListAsync(cancellationToken));

        return TypedResults.Ok(overview);
    }

    private static async Task<Ok<AdminDataExport>> ExportDataAsync(
        DatingDbContext database,
        HttpContext httpContext,
        ILogger<AdminDataExport> logger,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store, private";
        var export = new AdminDataExport(
            DateTimeOffset.UtcNow,
            await database.Users.AsNoTracking()
                .OrderBy(user => user.CreatedAt)
                .Select(user => new AdminUserExport(
                    user.Id, user.Name, user.Email, user.BirthDate, user.Bio, user.Gender,
                    user.City, user.Occupation, user.Education, user.LookingFor,
                    user.TermsAcceptedAt, user.CreatedAt))
                .ToListAsync(cancellationToken),
            await database.UserInterests.AsNoTracking()
                .OrderBy(item => item.UserId)
                .Select(item => new AdminInterestExport(item.UserId, item.InterestId, item.Interest.Name))
                .ToListAsync(cancellationToken),
            await database.ProfilePhotos.AsNoTracking()
                .OrderBy(photo => photo.UserId).ThenBy(photo => photo.SortOrder)
                .Select(photo => new AdminPhotoExport(photo.Id, photo.UserId, photo.Url, photo.IsAvatar, photo.SortOrder, photo.CreatedAt))
                .ToListAsync(cancellationToken),
            await database.Swipes.AsNoTracking()
                .OrderBy(swipe => swipe.CreatedAt)
                .Select(swipe => new AdminSwipeExport(swipe.Id, swipe.FromUserId, swipe.ToUserId, swipe.IsLike, swipe.CreatedAt))
                .ToListAsync(cancellationToken),
            await database.Matches.AsNoTracking()
                .OrderBy(match => match.MatchedAt)
                .Select(match => new AdminMatchExport(match.Id, match.FirstUserId, match.SecondUserId, match.MatchedAt))
                .ToListAsync(cancellationToken),
            await database.JourneyPosts.AsNoTracking()
                .OrderBy(post => post.CreatedAt)
                .Select(post => new AdminJourneyPostExport(post.Id, post.UserId, post.Content, post.MediaUrl, post.MediaType, post.CreatedAt))
                .ToListAsync(cancellationToken),
            await database.JourneyPostLikes.AsNoTracking()
                .OrderBy(like => like.CreatedAt)
                .Select(like => new AdminJourneyLikeExport(like.PostId, like.UserId, like.CreatedAt))
                .ToListAsync(cancellationToken),
            await database.JourneyComments.AsNoTracking()
                .OrderBy(comment => comment.CreatedAt)
                .Select(comment => new AdminJourneyCommentExport(comment.Id, comment.PostId, comment.UserId, comment.Content, comment.CreatedAt))
                .ToListAsync(cancellationToken));

        var adminUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        logger.LogInformation("Owner data export requested by account {AdminUserId}", adminUserId);
        return TypedResults.Ok(export);
    }
}
