using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KissAppTussing.Data;

internal sealed class DatingDbContext(DbContextOptions<DatingDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateTimeOffset, long> UtcDateTimeOffsetConverter = new(
        value => value.ToUnixTimeMilliseconds(),
        value => DateTimeOffset.FromUnixTimeMilliseconds(value));

    public DbSet<User> Users => Set<User>();
    public DbSet<Swipe> Swipes => Set<Swipe>();
    public DbSet<DatingMatch> Matches => Set<DatingMatch>();
    public DbSet<ProfilePhoto> ProfilePhotos => Set<ProfilePhoto>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<UserInterest> UserInterests => Set<UserInterest>();
    public DbSet<JourneyPost> JourneyPosts => Set<JourneyPost>();
    public DbSet<JourneyPostLike> JourneyPostLikes => Set<JourneyPostLike>();
    public DbSet<JourneyComment> JourneyComments => Set<JourneyComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(user => user.NormalizedEmail)
            .IsUnique();
        modelBuilder.Entity<User>()
            .Property(user => user.CreatedAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<User>()
            .Property(user => user.TermsAcceptedAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<User>()
            .Property(user => user.Name)
            .HasMaxLength(80);
        modelBuilder.Entity<User>()
            .Property(user => user.Bio)
            .HasMaxLength(500);
        modelBuilder.Entity<User>()
            .Property(user => user.Gender)
            .HasMaxLength(32);
        modelBuilder.Entity<User>()
            .Property(user => user.City)
            .HasMaxLength(100);
        modelBuilder.Entity<User>()
            .Property(user => user.Occupation)
            .HasMaxLength(120);
        modelBuilder.Entity<User>()
            .Property(user => user.Education)
            .HasMaxLength(120);
        modelBuilder.Entity<User>()
            .Property(user => user.LookingFor)
            .HasMaxLength(32);

        modelBuilder.Entity<Swipe>()
            .HasIndex(swipe => new { swipe.FromUserId, swipe.ToUserId })
            .IsUnique();
        modelBuilder.Entity<Swipe>()
            .Property(swipe => swipe.CreatedAt)
            .HasConversion(UtcDateTimeOffsetConverter);

        modelBuilder.Entity<DatingMatch>()
            .HasIndex(match => new { match.FirstUserId, match.SecondUserId })
            .IsUnique();
        modelBuilder.Entity<DatingMatch>()
            .Property(match => match.MatchedAt)
            .HasConversion(UtcDateTimeOffsetConverter);

        modelBuilder.Entity<DatingMatch>()
            .HasOne(match => match.FirstUser)
            .WithMany()
            .HasForeignKey(match => match.FirstUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DatingMatch>()
            .HasOne(match => match.SecondUser)
            .WithMany()
            .HasForeignKey(match => match.SecondUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProfilePhoto>()
            .Property(photo => photo.CreatedAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<ProfilePhoto>()
            .Property(photo => photo.Url)
            .HasMaxLength(500);
        modelBuilder.Entity<ProfilePhoto>()
            .HasIndex(photo => new { photo.UserId, photo.IsAvatar })
            .IsUnique()
            .HasFilter("\"IsAvatar\" = 1");
        modelBuilder.Entity<ProfilePhoto>()
            .HasOne(photo => photo.User)
            .WithMany(user => user.Photos)
            .HasForeignKey(photo => photo.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Interest>()
            .Property(interest => interest.Name)
            .HasMaxLength(50);
        modelBuilder.Entity<Interest>()
            .HasIndex(interest => interest.Name)
            .IsUnique();

        modelBuilder.Entity<UserInterest>()
            .HasKey(userInterest => new { userInterest.UserId, userInterest.InterestId });
        modelBuilder.Entity<UserInterest>()
            .HasOne(userInterest => userInterest.User)
            .WithMany(user => user.Interests)
            .HasForeignKey(userInterest => userInterest.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserInterest>()
            .HasOne(userInterest => userInterest.Interest)
            .WithMany(interest => interest.Users)
            .HasForeignKey(userInterest => userInterest.InterestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JourneyPost>()
            .Property(post => post.CreatedAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<JourneyPost>()
            .Property(post => post.Content)
            .HasMaxLength(2000);
        modelBuilder.Entity<JourneyPost>()
            .Property(post => post.MediaUrl)
            .HasMaxLength(500);
        modelBuilder.Entity<JourneyPost>()
            .Property(post => post.MediaType)
            .HasMaxLength(16);
        modelBuilder.Entity<JourneyPost>()
            .HasIndex(post => new { post.CreatedAt, post.Id });
        modelBuilder.Entity<JourneyPost>()
            .HasOne(post => post.User)
            .WithMany()
            .HasForeignKey(post => post.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JourneyPostLike>()
            .HasKey(like => new { like.PostId, like.UserId });
        modelBuilder.Entity<JourneyPostLike>()
            .Property(like => like.CreatedAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<JourneyPostLike>()
            .HasOne(like => like.Post)
            .WithMany(post => post.Likes)
            .HasForeignKey(like => like.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<JourneyPostLike>()
            .HasOne(like => like.User)
            .WithMany()
            .HasForeignKey(like => like.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JourneyComment>()
            .Property(comment => comment.Content)
            .HasMaxLength(500);
        modelBuilder.Entity<JourneyComment>()
            .Property(comment => comment.CreatedAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<JourneyComment>()
            .HasIndex(comment => new { comment.PostId, comment.CreatedAt });
        modelBuilder.Entity<JourneyComment>()
            .HasOne(comment => comment.Post)
            .WithMany(post => post.Comments)
            .HasForeignKey(comment => comment.PostId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<JourneyComment>()
            .HasOne(comment => comment.User)
            .WithMany()
            .HasForeignKey(comment => comment.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}