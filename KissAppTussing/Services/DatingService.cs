using KissAppTussing.Contracts;
using KissAppTussing.Data;
using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

namespace KissAppTussing.Services;

internal sealed class DatingService(DatingDbContext database, IWebHostEnvironment environment) : IDatingService
{
    private const int MaxProfilePhotos = 6;
    private const long MaxPhotoBytes = 5 * 1024 * 1024;

    public async Task<IReadOnlyList<UserProfileResponse>> DiscoverAsync(
        Guid userId,
        int limit,
        string? search,
        string? gender,
        int? minAge,
        int? maxAge,
        string? city,
        string? lookingFor,
        Guid? interestId,
        CancellationToken cancellationToken)
    {
        var query = database.Users
            .AsNoTracking()
            .Where(user => user.Id != userId)
            .Where(user => !database.Swipes.Any(swipe =>
                swipe.FromUserId == userId && swipe.ToUserId == user.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(user => user.Name.Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(gender))
        {
            query = query.Where(user => user.Gender == gender);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalizedCity = city.Trim();
            query = query.Where(user => user.City != null && user.City.Contains(normalizedCity));
        }

        if (!string.IsNullOrWhiteSpace(lookingFor))
        {
            query = query.Where(user => user.LookingFor == lookingFor);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (minAge.HasValue)
        {
            query = query.Where(user => user.BirthDate <= today.AddYears(-minAge.Value));
        }

        if (maxAge.HasValue && maxAge.Value < 120)
        {
            query = query.Where(user => user.BirthDate > today.AddYears(-maxAge.Value - 1));
        }

        if (interestId.HasValue)
        {
            query = query.Where(user => database.UserInterests.Any(userInterest =>
                userInterest.UserId == user.Id && userInterest.InterestId == interestId.Value));
        }

        var users = await query
            .Include(user => user.Photos)
            .Include(user => user.Interests)
                .ThenInclude(userInterest => userInterest.Interest)
            .AsSplitQuery()
            .OrderBy(user => user.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return users.Select(ToProfile).ToArray();
    }

    public async Task<UserProfileResponse?> GetProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var user = await database.Users
            .AsNoTracking()
            .Include(candidate => candidate.Photos)
            .Include(candidate => candidate.Interests)
                .ThenInclude(userInterest => userInterest.Interest)
            .AsSplitQuery()
            .SingleOrDefaultAsync(candidate => candidate.Id == profileId, cancellationToken);

        return user is null ? null : ToProfile(user);
    }

    public async Task<UserProfileResponse?> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await database.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - request.BirthDate.Year;
        if (request.BirthDate > today.AddYears(-age))
        {
            age--;
        }

        if (age < 18)
        {
            return null;
        }

        user.Name = request.Name.Trim();
        user.BirthDate = request.BirthDate;
        user.Gender = request.Gender;
        user.City = NormalizeOptional(request.City);
        user.Occupation = NormalizeOptional(request.Occupation);
        user.Education = NormalizeOptional(request.Education);
        user.LookingFor = request.LookingFor;
        user.Bio = request.Bio.Trim();
        await database.SaveChangesAsync(cancellationToken);

        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<InterestResponse>> GetInterestsAsync(CancellationToken cancellationToken) =>
        await database.Interests
            .AsNoTracking()
            .OrderBy(interest => interest.Name)
            .Select(interest => new InterestResponse(interest.Id, interest.Name))
            .ToListAsync(cancellationToken);

    public async Task<bool> UpdateInterestsAsync(
        Guid userId,
        IReadOnlyList<Guid> interestIds,
        CancellationToken cancellationToken)
    {
        if (interestIds.Count > 20)
        {
            return false;
        }

        var uniqueIds = interestIds.Distinct().ToArray();
        var validCount = await database.Interests
            .CountAsync(interest => uniqueIds.Contains(interest.Id), cancellationToken);
        if (validCount != uniqueIds.Length)
        {
            return false;
        }

        var userExists = await database.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        if (!userExists)
        {
            return false;
        }

        var previous = await database.UserInterests
            .Where(userInterest => userInterest.UserId == userId)
            .ToListAsync(cancellationToken);
        database.UserInterests.RemoveRange(previous);
        database.UserInterests.AddRange(uniqueIds.Select(interestId => new UserInterest
        {
            UserId = userId,
            InterestId = interestId
        }));
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PhotoOperationResult> AddPhotoAsync(
        Guid userId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaxPhotoBytes)
        {
            return new PhotoOperationResult(PhotoOperationStatus.InvalidImage);
        }

        var extension = await GetImageExtensionAsync(file, cancellationToken);
        if (extension is null)
        {
            return new PhotoOperationResult(PhotoOperationStatus.InvalidImage);
        }

        var userExists = await database.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        if (!userExists)
        {
            return new PhotoOperationResult(PhotoOperationStatus.UserNotFound);
        }

        var existingPhotos = await database.ProfilePhotos
            .Where(photo => photo.UserId == userId)
            .OrderBy(photo => photo.SortOrder)
            .ToListAsync(cancellationToken);
        if (existingPhotos.Count >= MaxProfilePhotos)
        {
            return new PhotoOperationResult(PhotoOperationStatus.PhotoLimitReached);
        }

        var photoId = Guid.NewGuid();
        var fileName = $"{photoId:N}{extension}";
        var directory = Path.Combine(GetWebRootPath(), "uploads", "profiles", userId.ToString("N"));
        Directory.CreateDirectory(directory);
        var fullPath = Path.Combine(directory, fileName);
        var publicUrl = $"/uploads/profiles/{userId:N}/{fileName}";

        try
        {
            await using (var input = file.OpenReadStream())
            await using (var output = File.Create(fullPath))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            var photo = new ProfilePhoto
            {
                Id = photoId,
                UserId = userId,
                Url = publicUrl,
                IsAvatar = existingPhotos.Count == 0,
                SortOrder = existingPhotos.Count
            };
            database.ProfilePhotos.Add(photo);
            await database.SaveChangesAsync(cancellationToken);
            return new PhotoOperationResult(PhotoOperationStatus.Created, ToPhotoResponse(photo));
        }
        catch
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            throw;
        }
    }

    public async Task<PhotoOperationResult> SetAvatarAsync(
        Guid userId,
        Guid photoId,
        CancellationToken cancellationToken)
    {
        var photos = await database.ProfilePhotos
            .Where(photo => photo.UserId == userId)
            .ToListAsync(cancellationToken);
        var selected = photos.SingleOrDefault(photo => photo.Id == photoId);
        if (selected is null)
        {
            var userExists = await database.Users.AnyAsync(user => user.Id == userId, cancellationToken);
            return new PhotoOperationResult(userExists
                ? PhotoOperationStatus.PhotoNotFound
                : PhotoOperationStatus.UserNotFound);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        foreach (var photo in photos)
        {
            photo.IsAvatar = false;
        }

        await database.SaveChangesAsync(cancellationToken);
        selected.IsAvatar = true;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PhotoOperationResult(PhotoOperationStatus.Updated, ToPhotoResponse(selected));
    }

    public async Task<PhotoOperationResult> DeletePhotoAsync(
        Guid userId,
        Guid photoId,
        CancellationToken cancellationToken)
    {
        var photo = await database.ProfilePhotos
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId && candidate.Id == photoId, cancellationToken);
        if (photo is null)
        {
            var userExists = await database.Users.AnyAsync(user => user.Id == userId, cancellationToken);
            return new PhotoOperationResult(userExists
                ? PhotoOperationStatus.PhotoNotFound
                : PhotoOperationStatus.UserNotFound);
        }

        var wasAvatar = photo.IsAvatar;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (wasAvatar)
        {
            var nextAvatar = await database.ProfilePhotos
                .Where(candidate => candidate.UserId == userId && candidate.Id != photoId)
                .OrderBy(candidate => candidate.SortOrder)
                .FirstOrDefaultAsync(cancellationToken);
            photo.IsAvatar = false;
            await database.SaveChangesAsync(cancellationToken);
            if (nextAvatar is not null)
            {
                nextAvatar.IsAvatar = true;
                await database.SaveChangesAsync(cancellationToken);
            }
        }

        database.ProfilePhotos.Remove(photo);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var localPath = Path.Combine(GetWebRootPath(), photo.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localPath))
        {
            File.Delete(localPath);
        }

        return new PhotoOperationResult(PhotoOperationStatus.Deleted);
    }

    public async Task<SwipeResult> SwipeAsync(
        Guid userId,
        CreateSwipeRequest request,
        CancellationToken cancellationToken)
    {
        if (userId == request.TargetUserId)
        {
            return new SwipeResult(SwipeStatus.SelfSwipe, null);
        }

        var targetExists = await database.Users
            .AnyAsync(user => user.Id == request.TargetUserId, cancellationToken);
        if (!targetExists)
        {
            return new SwipeResult(SwipeStatus.TargetNotFound, null);
        }

        var alreadySwiped = await database.Swipes.AnyAsync(
            swipe => swipe.FromUserId == userId && swipe.ToUserId == request.TargetUserId,
            cancellationToken);
        if (alreadySwiped)
        {
            return new SwipeResult(SwipeStatus.AlreadySwiped, null);
        }

        database.Swipes.Add(new Swipe
        {
            FromUserId = userId,
            ToUserId = request.TargetUserId,
            IsLike = request.IsLike
        });

        Guid? matchId = null;
        if (request.IsLike)
        {
            var mutualLike = await database.Swipes.AnyAsync(
                swipe => swipe.FromUserId == request.TargetUserId
                    && swipe.ToUserId == userId
                    && swipe.IsLike,
                cancellationToken);

            if (mutualLike)
            {
                var firstUserId = userId.CompareTo(request.TargetUserId) < 0 ? userId : request.TargetUserId;
                var secondUserId = userId.CompareTo(request.TargetUserId) < 0 ? request.TargetUserId : userId;
                var match = new DatingMatch
                {
                    FirstUserId = firstUserId,
                    SecondUserId = secondUserId
                };
                database.Matches.Add(match);
                matchId = match.Id;
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        return new SwipeResult(
            SwipeStatus.Created,
            new SwipeResponse(request.TargetUserId, request.IsLike, matchId is not null, matchId));
    }

    public async Task<IReadOnlyList<MatchResponse>> GetMatchesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var matches = await database.Matches
            .AsNoTracking()
            .Where(match => match.FirstUserId == userId || match.SecondUserId == userId)
            .Include(match => match.FirstUser)
                .ThenInclude(user => user.Photos)
            .Include(match => match.FirstUser)
                .ThenInclude(user => user.Interests)
                    .ThenInclude(userInterest => userInterest.Interest)
            .Include(match => match.SecondUser)
                .ThenInclude(user => user.Photos)
            .Include(match => match.SecondUser)
                .ThenInclude(user => user.Interests)
                    .ThenInclude(userInterest => userInterest.Interest)
            .AsSplitQuery()
            .OrderByDescending(match => match.MatchedAt)
            .ToListAsync(cancellationToken);

        return matches.Select(match => new MatchResponse(
            match.Id,
            ToProfile(match.FirstUserId == userId ? match.SecondUser : match.FirstUser),
            match.MatchedAt)).ToArray();
    }

    private static UserProfileResponse ToProfile(User user) => new(
        user.Id,
        user.Name,
        user.BirthDate,
        user.Bio,
        user.Gender,
        user.City,
        user.Occupation,
        user.Education,
        user.LookingFor,
        user.Photos.Select(ToPhotoResponse).OrderByDescending(photo => photo.IsAvatar).ThenBy(photo => photo.SortOrder).ToArray(),
        user.Interests.Select(userInterest => new InterestResponse(
            userInterest.InterestId,
            userInterest.Interest.Name)).OrderBy(interest => interest.Name).ToArray(),
        user.CreatedAt);

    private static ProfilePhotoResponse ToPhotoResponse(ProfilePhoto photo) =>
        new(photo.Id, photo.Url, photo.IsAvatar, photo.SortOrder);

    private string GetWebRootPath() =>
        environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<string?> GetImageExtensionAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var count = await stream.ReadAsync(header, cancellationToken);

        if (count >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            return ".png";
        }

        if (count >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ".jpg";
        }

        if (count >= 12
            && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }

        return null;
    }
}