using System.Security.Cryptography;
using KissAppTussing.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Data;

internal static class DatabaseInitializer
{
    private static readonly string[] DefaultInterests =
    [
        "Âm nhạc", "Du lịch", "Đọc sách", "Nấu ăn", "Phim ảnh", "Thể thao", "Cà phê",
        "Nhiếp ảnh", "Thiên nhiên", "Công nghệ", "Nghệ thuật", "Yoga", "Thú cưng", "Game",
        "Chạy bộ", "Ẩm thực", "Board game", "Cắm trại", "Thiết kế", "Làm vườn", "Thời trang",
        "Thiện nguyện", "Podcast", "Bóng đá", "Cầu lông", "Lập trình", "Vẽ tranh", "Nhảy",
        "Phượt", "Trà", "Mèo", "Chó"
    ];

    private static readonly DemoProfile[] DemoProfiles =
    [
        new("m01", "An", "Woman", 24, "Hà Nội", "Thích cà phê, đọc sách và những buổi dạo phố.", ["Cà phê", "Đọc sách", "Nhiếp ảnh"]),
        new("m02", "Linh", "Woman", 27, "TP. Hồ Chí Minh", "Thích nấu món mới và đi tìm nơi nhiều cây xanh.", ["Nấu ăn", "Thiên nhiên", "Du lịch"]),
        new("m03", "Mai", "Woman", 22, "Đà Nẵng", "Mê nhạc sống, phim hay và ngắm biển lúc chiều muộn.", ["Âm nhạc", "Phim ảnh", "Du lịch"]),
        new("m04", "Thảo", "Woman", 31, "Cần Thơ", "Yêu những điều giản dị: nấu bữa ngon và chăm cây.", ["Nấu ăn", "Làm vườn", "Đọc sách"]),
        new("m05", "Vy", "Woman", 26, "Hải Phòng", "Thích đi xem triển lãm và chọn đồ vintage.", ["Nghệ thuật", "Thời trang", "Nhiếp ảnh"]),
        new("m06", "Hà", "Woman", 29, "Huế", "Một cuốn sách hay và tách trà là ngày thư thả.", ["Đọc sách", "Trà", "Thú cưng"]),
        new("m07", "Ngọc", "Woman", 34, "TP. Hồ Chí Minh", "Thích vận động nhẹ, cà phê sáng và trò chuyện chân thành.", ["Yoga", "Thể thao", "Cà phê"]),
        new("m08", "My", "Woman", 21, "Hà Nội", "Mê nhảy, phim hài và những chuyến đi ngẫu hứng.", ["Nhảy", "Phim ảnh", "Du lịch"]),
        new("m09", "Trâm", "Woman", 38, "Nha Trang", "Thích gặp bạn mới và tham gia hoạt động cộng đồng.", ["Thiện nguyện", "Du lịch", "Nấu ăn"]),
        new("m10", "Trang", "Woman", 33, "Hà Nội", "Thích đọc sách, nấu món mới và trò chuyện sâu sắc.", ["Đọc sách", "Nấu ăn", "Trà"]),

        new("m11", "Minh", "Man", 25, "Hà Nội", "Thích cà phê yên tĩnh, sách và chuyện vui.", ["Cà phê", "Đọc sách", "Công nghệ"]),
        new("m12", "Quân", "Man", 29, "TP. Hồ Chí Minh", "Đi du lịch để thử món mới và nghe chuyện địa phương.", ["Du lịch", "Ẩm thực", "Nhiếp ảnh"]),
        new("m13", "Duy", "Man", 23, "Đà Nẵng", "Cuối tuần có thể xem bóng đá hoặc tự nấu món mới.", ["Bóng đá", "Nấu ăn", "Phim ảnh"]),
        new("m14", "Khang", "Man", 32, "Hải Phòng", "Thích cắm trại, thể thao và những chuyến đi ngắn.", ["Cắm trại", "Thể thao", "Thiên nhiên"]),
        new("m15", "Phúc", "Man", 28, "Cần Thơ", "Thích chụp ảnh đời thường và chơi với thú cưng.", ["Nhiếp ảnh", "Thú cưng", "Cà phê"]),
        new("m16", "Hoàng", "Man", 36, "Huế", "Nhạc hay, đồ ăn ngon và sự tử tế làm mình vui.", ["Âm nhạc", "Ẩm thực", "Nấu ăn"]),
        new("m17", "Bảo", "Man", 20, "Hà Nội", "Thích game co-op, phim viễn tưởng và công nghệ.", ["Game", "Phim ảnh", "Công nghệ"]),
        new("m18", "Nam", "Man", 41, "TP. Hồ Chí Minh", "Tìm bạn đồng hành cho những chuyến đi và bữa ăn ngon.", ["Ẩm thực", "Du lịch", "Cà phê"]),
        new("m19", "Tuấn", "Man", 33, "Nha Trang", "Mỗi sáng chạy bộ, mỗi tối dành thời gian cho thiên nhiên.", ["Chạy bộ", "Thiên nhiên", "Thể thao"]),
        new("m20", "Đức", "Man", 26, "Đà Lạt", "Cà phê rang xay, cắm trại và những cung đường đẹp.", ["Cà phê", "Cắm trại", "Phượt"]),

        new("m21", "Bình", "NonBinary", 24, "TP. Hồ Chí Minh", "Thích vẽ, đọc truyện và góc nhìn mới.", ["Nghệ thuật", "Vẽ tranh", "Đọc sách"]),
        new("m22", "Khánh", "NonBinary", 27, "Hà Nội", "Thích nhạc indie, ảnh film và phố cổ.", ["Âm nhạc", "Nhiếp ảnh", "Du lịch"]),
        new("m23", "Gia", "NonBinary", 22, "Đà Nẵng", "Một buổi chơi game hoặc xem phim đều tuyệt.", ["Game", "Phim ảnh", "Công nghệ"]),
        new("m24", "Trúc", "NonBinary", 30, "Huế", "Tìm người cùng tập yoga, chăm cây và sống chậm.", ["Thiên nhiên", "Yoga", "Làm vườn"]),
        new("m25", "Nhật", "NonBinary", 35, "TP. Hồ Chí Minh", "Thích podcast, công nghệ và phim tài liệu.", ["Công nghệ", "Podcast", "Phim ảnh"]),
        new("m26", "Thiên", "NonBinary", 26, "Cần Thơ", "Thích đi xa và học nấu món địa phương.", ["Phượt", "Nấu ăn", "Nhiếp ảnh"]),
        new("m27", "Hải", "NonBinary", 39, "Hải Phòng", "Thích đọc sách, đi dạo và chăm thú cưng.", ["Đọc sách", "Chó", "Thiên nhiên"]),
        new("m28", "Mộc", "NonBinary", 18, "Hà Nội", "Thích vẽ tranh, trồng cây và trò chơi bàn.", ["Vẽ tranh", "Làm vườn", "Board game"]),

        new("m29", "Nhi", "PreferNotToSay", 23, "Nha Trang", "Thích nhạc nhẹ, cà phê và ngắm hoàng hôn.", ["Âm nhạc", "Cà phê", "Du lịch"]),
        new("m30", "Lam", "PreferNotToSay", 28, "TP. Hồ Chí Minh", "Cắm trại và một cuốn sách hay là cuối tuần lý tưởng.", ["Cắm trại", "Đọc sách", "Thiên nhiên"]),
        new("m31", "Vân", "PreferNotToSay", 31, "Hà Nội", "Thích yoga, làm việc thiện và gặp gỡ bạn mới.", ["Yoga", "Thiện nguyện", "Nấu ăn"]),
        new("m32", "Tâm", "PreferNotToSay", 25, "Đà Nẵng", "Thích đi biển, chụp ảnh và quán ăn nhỏ.", ["Du lịch", "Nhiếp ảnh", "Ẩm thực"]),
        new("m33", "Yến", "PreferNotToSay", 37, "Huế", "Thích nghệ thuật, nấu ăn và tối yên bình.", ["Nghệ thuật", "Nấu ăn", "Trà"]),
        new("m34", "Sơn", "PreferNotToSay", 29, "Cần Thơ", "Chơi cầu lông, xem phim và khám phá miền Tây.", ["Cầu lông", "Phim ảnh", "Du lịch"]),
        new("m35", "Diệp", "PreferNotToSay", 20, "Hải Phòng", "Thích thời trang, nhảy và đi chơi cùng bạn bè.", ["Thời trang", "Nhảy", "Âm nhạc"]),
        new("m36", "Lộc", "PreferNotToSay", 42, "TP. Hồ Chí Minh", "Yêu công nghệ, thiên nhiên và chuyến đi có kế hoạch.", ["Công nghệ", "Thiên nhiên", "Phượt"])
    ];

    public static async Task InitializeAsync(DatingDbContext database, CancellationToken cancellationToken = default)
    {
        await database.Database.MigrateAsync(cancellationToken);

        var interests = await database.Interests.ToListAsync(cancellationToken);
        var interestsByName = interests.ToDictionary(interest => interest.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var name in DefaultInterests)
        {
            if (interestsByName.ContainsKey(name))
            {
                continue;
            }

            var interest = new Interest { Name = name };
            database.Interests.Add(interest);
            interestsByName.Add(name, interest);
        }

        await database.SaveChangesAsync(cancellationToken);

        var demoEmails = DemoProfiles.Select(profile => DemoEmail(profile.Key).ToUpperInvariant()).ToArray();
        var existingDemoUsers = await database.Users
            .Include(user => user.Photos)
            .Where(user => demoEmails.Contains(user.NormalizedEmail))
            .ToListAsync(cancellationToken);
        var existingUsersByEmail = existingDemoUsers.ToDictionary(
            user => user.NormalizedEmail,
            StringComparer.OrdinalIgnoreCase);
        var passwordHasher = new PasswordHasher<User>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seededAt = DateTimeOffset.UtcNow;
        var newDemoUsers = new List<User>();

        for (var index = 0; index < DemoProfiles.Length; index++)
        {
            var profile = DemoProfiles[index];
            var email = DemoEmail(profile.Key);
            var normalizedEmail = email.ToUpperInvariant();
            var photoUrl = DemoPhotoUrl(profile);

            if (existingUsersByEmail.TryGetValue(normalizedEmail, out var existingUser))
            {
                existingUser.Name = profile.Name;
                if (!existingUser.Photos.Any(photo => photo.Url == photoUrl))
                {
                    database.ProfilePhotos.Add(new ProfilePhoto
                    {
                        UserId = existingUser.Id,
                        Url = photoUrl,
                        IsAvatar = existingUser.Photos.All(photo => !photo.IsAvatar),
                        SortOrder = existingUser.Photos.Count
                    });
                }

                continue;
            }

            var user = new User
            {
                Name = profile.Name,
                Email = email,
                NormalizedEmail = normalizedEmail,
                PasswordHash = string.Empty,
                BirthDate = today.AddYears(-profile.Age).AddDays(-((index * 37) % 330)),
                Bio = profile.Bio,
                Gender = profile.Gender,
                City = profile.City,
                LookingFor = "Dating",
                CreatedAt = seededAt.AddDays(index - DemoProfiles.Length),
                Interests = profile.Interests.Select(name => new UserInterest
                {
                    InterestId = interestsByName[name].Id
                }).ToList(),
                Photos =
                [
                    new ProfilePhoto
                    {
                        Url = photoUrl,
                        IsAvatar = true,
                        SortOrder = 0
                    }
                ]
            };

            var unsharedPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            user.PasswordHash = passwordHasher.HashPassword(user, unsharedPassword);
            newDemoUsers.Add(user);
        }

        if (newDemoUsers.Count > 0)
        {
            database.Users.AddRange(newDemoUsers);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private static string DemoPhotoUrl(DemoProfile profile) => $"/demo-profiles/{profile.Key}.jpg";
    private static string DemoEmail(string key) => $"demo.{key}@kiss.local";

    private sealed record DemoProfile(
        string Key,
        string Name,
        string Gender,
        int Age,
        string City,
        string Bio,
        string[] Interests);
}
