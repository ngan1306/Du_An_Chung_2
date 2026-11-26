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
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<ConnectionRequest> ConnectionRequests => Set<ConnectionRequest>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
<<<<<<< HEAD
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();
    public DbSet<RoomMessage> RoomMessages => Set<RoomMessage>();
    public DbSet<Report> Reports => Set<Report>();
=======
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<HiddenChatMessage> HiddenChatMessages => Set<HiddenChatMessage>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<DateInvitation> DateInvitations => Set<DateInvitation>();
    public DbSet<DateReview> DateReviews => Set<DateReview>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<CommunityEvent> CommunityEvents => Set<CommunityEvent>();
    public DbSet<EventAttendance> EventAttendances => Set<EventAttendance>();
>>>>>>> ad6278e33dcb2b25f88d5effa2fee3acc8ed6106

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
            .Property(user => user.LastSeenAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<User>()
            .Property(user => user.EmailVerifiedAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<User>()
            .Property(user => user.Name)
            .HasMaxLength(80);
        modelBuilder.Entity<User>()
            .Property(user => user.Email)
            .HasMaxLength(256);
        modelBuilder.Entity<User>()
            .Property(user => user.NormalizedEmail)
            .HasMaxLength(256);
        modelBuilder.Entity<User>()
            .Property(user => user.PasswordHash)
            .HasMaxLength(512);
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
        modelBuilder.Entity<User>()
            .Property(user => user.Role)
            .HasMaxLength(24)
            .HasDefaultValue("User");

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
            .Property(match => match.EndedAt)
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
            .HasFilter("[IsAvatar] = 1");
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
            .OnDelete(DeleteBehavior.NoAction);

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
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<JourneyComment>()
            .HasOne(comment => comment.ParentComment)
            .WithMany(comment => comment.Replies)
            .HasForeignKey(comment => comment.ParentCommentId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Story>().Property(story => story.Content).HasMaxLength(500);
        modelBuilder.Entity<Story>().Property(story => story.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<Story>().Property(story => story.ExpiresAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<Story>().HasIndex(story => new { story.ExpiresAt, story.UserId });
        modelBuilder.Entity<Story>().HasOne(story => story.User).WithMany().HasForeignKey(story => story.UserId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .Property(message => message.Content)
            .HasMaxLength(2000);
        modelBuilder.Entity<ChatMessage>()
            .Property(message => message.MediaFileName)
            .HasMaxLength(80);
        modelBuilder.Entity<ChatMessage>()
            .Property(message => message.MediaType)
            .HasMaxLength(32);
        modelBuilder.Entity<ChatMessage>()
            .Property(message => message.SentAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<ChatMessage>().Property(message => message.ReadAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<ChatMessage>().Property(message => message.RevokedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<ChatMessage>()
            .HasIndex(message => new { message.SenderId, message.RecipientId, message.SentAt });
        modelBuilder.Entity<ChatMessage>()
            .HasIndex(message => message.RecipientId);
        modelBuilder.Entity<ChatMessage>()
            .HasOne(message => message.Sender)
            .WithMany()
            .HasForeignKey(message => message.SenderId)
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<ChatMessage>()
            .HasOne(message => message.Recipient)
            .WithMany()
            .HasForeignKey(message => message.RecipientId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PasswordResetToken>()
            .Property(reset => reset.TokenHash)
            .HasMaxLength(64);
        modelBuilder.Entity<PasswordResetToken>()
            .Property(reset => reset.CreatedAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<PasswordResetToken>()
            .Property(reset => reset.ExpiresAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(reset => reset.TokenHash)
            .IsUnique();
        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(reset => reset.UserId);
        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(reset => reset.User)
            .WithMany()
            .HasForeignKey(reset => reset.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ConnectionRequest>()
            .HasIndex(connection => new { connection.FirstUserId, connection.SecondUserId })
            .IsUnique();
        modelBuilder.Entity<ConnectionRequest>().Property(connection => connection.Status).HasMaxLength(16);
        modelBuilder.Entity<ConnectionRequest>().Property(connection => connection.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<ConnectionRequest>().HasIndex(connection => new { connection.RequestedByUserId, connection.Status });
<<<<<<< HEAD
        modelBuilder.Entity<ConnectionRequest>().HasOne<User>().WithMany().HasForeignKey(connection => connection.FirstUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ConnectionRequest>().HasOne<User>().WithMany().HasForeignKey(connection => connection.SecondUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ConnectionRequest>().HasOne<User>().WithMany().HasForeignKey(connection => connection.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
=======
        modelBuilder.Entity<ConnectionRequest>().HasOne<User>().WithMany().HasForeignKey(connection => connection.FirstUserId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<ConnectionRequest>().HasOne<User>().WithMany().HasForeignKey(connection => connection.SecondUserId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<ConnectionRequest>().HasOne<User>().WithMany().HasForeignKey(connection => connection.RequestedByUserId).OnDelete(DeleteBehavior.NoAction);
>>>>>>> ad6278e33dcb2b25f88d5effa2fee3acc8ed6106

        modelBuilder.Entity<Conversation>()
            .HasIndex(conversation => new { conversation.FirstUserId, conversation.SecondUserId })
            .IsUnique();
        modelBuilder.Entity<Conversation>().Property(conversation => conversation.FirstUserReadAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<Conversation>().Property(conversation => conversation.SecondUserReadAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<Conversation>().Property(conversation => conversation.UpdatedAt).HasConversion(UtcDateTimeOffsetConverter);
<<<<<<< HEAD
        modelBuilder.Entity<Conversation>().HasOne<User>().WithMany().HasForeignKey(conversation => conversation.FirstUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Conversation>().HasOne<User>().WithMany().HasForeignKey(conversation => conversation.SecondUserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Room>()
            .Property(room => room.CreatedAt)
            .HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<Room>().Property(room => room.Name).HasMaxLength(120);
        modelBuilder.Entity<Room>().Property(room => room.Topic).HasMaxLength(80);
        modelBuilder.Entity<Room>().Property(room => room.Description).HasMaxLength(1000);
        modelBuilder.Entity<Room>().Property(room => room.AvatarUrl).HasMaxLength(500);
        modelBuilder.Entity<Room>().Property(room => room.InviteCode).HasMaxLength(64);
        modelBuilder.Entity<Room>().HasIndex(room => room.InviteCode).IsUnique().HasFilter("[InviteCode] IS NOT NULL");
        modelBuilder.Entity<Room>().HasIndex(room => new { room.IsPrivate, room.CreatedAt });
        modelBuilder.Entity<Room>().HasOne<User>().WithMany().HasForeignKey(room => room.OwnerId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RoomMember>().HasKey(member => new { member.RoomId, member.UserId });
        modelBuilder.Entity<RoomMember>().Property(member => member.Role).HasMaxLength(16);
        modelBuilder.Entity<RoomMember>().Property(member => member.JoinedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<RoomMember>().HasOne<Room>().WithMany().HasForeignKey(member => member.RoomId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RoomMember>().HasOne<User>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RoomMessage>().Property(message => message.Content).HasMaxLength(1000);
        modelBuilder.Entity<RoomMessage>().Property(message => message.MediaUrl).HasMaxLength(500);
        modelBuilder.Entity<RoomMessage>().Property(message => message.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<RoomMessage>().HasIndex(message => new { message.RoomId, message.CreatedAt });
        modelBuilder.Entity<RoomMessage>().HasOne<Room>().WithMany().HasForeignKey(message => message.RoomId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RoomMessage>().HasOne<User>().WithMany().HasForeignKey(message => message.SenderId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Report>().Property(report => report.Reason).HasMaxLength(500);
        modelBuilder.Entity<Report>().Property(report => report.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<Report>().HasIndex(report => new { report.RoomId, report.CreatedAt });
        modelBuilder.Entity<Report>().HasOne<User>().WithMany().HasForeignKey(report => report.ReporterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Report>().HasOne<User>().WithMany().HasForeignKey(report => report.TargetUserId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Report>().HasOne<Room>().WithMany().HasForeignKey(report => report.RoomId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Report>().HasOne<RoomMessage>().WithMany().HasForeignKey(report => report.MessageId).OnDelete(DeleteBehavior.NoAction);
=======
        modelBuilder.Entity<Conversation>().HasOne<User>().WithMany().HasForeignKey(conversation => conversation.FirstUserId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Conversation>().HasOne<User>().WithMany().HasForeignKey(conversation => conversation.SecondUserId).OnDelete(DeleteBehavior.NoAction);
>>>>>>> ad6278e33dcb2b25f88d5effa2fee3acc8ed6106

        modelBuilder.Entity<Message>().Property(message => message.Content).HasMaxLength(2000);
        modelBuilder.Entity<Message>().Property(message => message.SentAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<Message>().HasIndex(message => new { message.ConversationId, message.SentAt, message.Id });
        modelBuilder.Entity<Message>().HasOne(message => message.Conversation).WithMany(conversation => conversation.Messages).HasForeignKey(message => message.ConversationId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Message>().HasOne<User>().WithMany().HasForeignKey(message => message.SenderId).OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserSession>().Property(session => session.DeviceName).HasMaxLength(160);
        modelBuilder.Entity<UserSession>().Property(session => session.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<UserSession>().Property(session => session.LastSeenAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<UserSession>().Property(session => session.RevokedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<UserSession>().HasIndex(session => new { session.UserId, session.RevokedAt });
        modelBuilder.Entity<UserSession>().HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HiddenChatMessage>().Property(item => item.HiddenAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<HiddenChatMessage>().HasIndex(item => new { item.MessageId, item.UserId }).IsUnique();
        modelBuilder.Entity<HiddenChatMessage>().HasOne<ChatMessage>().WithMany().HasForeignKey(item => item.MessageId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<HiddenChatMessage>().HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<EmailVerificationToken>().Property(token => token.CodeHash).HasMaxLength(64);
        modelBuilder.Entity<EmailVerificationToken>().Property(token => token.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<EmailVerificationToken>().Property(token => token.ExpiresAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<EmailVerificationToken>().Property(token => token.ConsumedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<EmailVerificationToken>().HasIndex(token => token.CodeHash).IsUnique();
        modelBuilder.Entity<EmailVerificationToken>().HasIndex(token => new { token.UserId, token.ExpiresAt });
        modelBuilder.Entity<EmailVerificationToken>().HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserNotification>().Property(item => item.Type).HasMaxLength(32);
        modelBuilder.Entity<UserNotification>().Property(item => item.Title).HasMaxLength(120);
        modelBuilder.Entity<UserNotification>().Property(item => item.Body).HasMaxLength(500);
        modelBuilder.Entity<UserNotification>().Property(item => item.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<UserNotification>().Property(item => item.ReadAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<UserNotification>().HasIndex(item => new { item.UserId, item.CreatedAt });
        modelBuilder.Entity<UserNotification>().HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DateInvitation>().Property(item => item.VenueName).HasMaxLength(120);
        modelBuilder.Entity<DateInvitation>().Property(item => item.VenueAddress).HasMaxLength(300);
        modelBuilder.Entity<DateInvitation>().Property(item => item.VenueUrl).HasMaxLength(500);
        modelBuilder.Entity<DateInvitation>().Property(item => item.Message).HasMaxLength(500);
        modelBuilder.Entity<DateInvitation>().Property(item => item.Status).HasMaxLength(16);
        modelBuilder.Entity<DateInvitation>().Property(item => item.StartsAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<DateInvitation>().Property(item => item.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<DateInvitation>().Property(item => item.UpdatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<DateInvitation>().Property(item => item.ReminderSentAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<DateInvitation>().HasIndex(item => new { item.RecipientUserId, item.Status });
        modelBuilder.Entity<DateInvitation>().HasIndex(item => new { item.StartsAt, item.Status });
        modelBuilder.Entity<DateInvitation>().HasOne(item => item.Organizer).WithMany().HasForeignKey(item => item.OrganizerUserId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<DateInvitation>().HasOne(item => item.Recipient).WithMany().HasForeignKey(item => item.RecipientUserId).OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<DateReview>().Property(item => item.Comment).HasMaxLength(500);
        modelBuilder.Entity<DateReview>().Property(item => item.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<DateReview>().HasIndex(item => new { item.InvitationId, item.ReviewerUserId }).IsUnique();
        modelBuilder.Entity<DateReview>().HasOne(item => item.Invitation).WithMany(invitation => invitation.Reviews).HasForeignKey(item => item.InvitationId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DateReview>().HasOne(item => item.Reviewer).WithMany().HasForeignKey(item => item.ReviewerUserId).OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<UserBlock>().Property(item => item.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<UserBlock>().HasIndex(item => new { item.BlockerUserId, item.BlockedUserId }).IsUnique();
        modelBuilder.Entity<UserBlock>().HasOne<User>().WithMany().HasForeignKey(item => item.BlockerUserId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<UserBlock>().HasOne<User>().WithMany().HasForeignKey(item => item.BlockedUserId).OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ContentReport>().Property(item => item.TargetType).HasMaxLength(24);
        modelBuilder.Entity<ContentReport>().Property(item => item.Reason).HasMaxLength(500);
        modelBuilder.Entity<ContentReport>().Property(item => item.Status).HasMaxLength(16);
        modelBuilder.Entity<ContentReport>().Property(item => item.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<ContentReport>().Property(item => item.ReviewedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<ContentReport>().HasIndex(item => new { item.Status, item.CreatedAt });
        modelBuilder.Entity<ContentReport>().HasOne<User>().WithMany().HasForeignKey(item => item.ReporterUserId).OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<AdminAuditLog>().Property(item => item.Action).HasMaxLength(32);
        modelBuilder.Entity<AdminAuditLog>().Property(item => item.TargetType).HasMaxLength(32);
        modelBuilder.Entity<AdminAuditLog>().Property(item => item.Details).HasMaxLength(500);
        modelBuilder.Entity<AdminAuditLog>().Property(item => item.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<AdminAuditLog>().HasIndex(item => item.CreatedAt);

        modelBuilder.Entity<CommunityEvent>().Property(item => item.Name).HasMaxLength(120);
        modelBuilder.Entity<CommunityEvent>().Property(item => item.Description).HasMaxLength(1000);
        modelBuilder.Entity<CommunityEvent>().Property(item => item.VenueName).HasMaxLength(160);
        modelBuilder.Entity<CommunityEvent>().Property(item => item.StartsAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<CommunityEvent>().Property(item => item.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<CommunityEvent>().HasIndex(item => item.StartsAt);
        modelBuilder.Entity<CommunityEvent>().HasOne(item => item.Creator).WithMany().HasForeignKey(item => item.CreatorUserId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<EventAttendance>().HasKey(item => new { item.EventId, item.UserId });
        modelBuilder.Entity<EventAttendance>().Property(item => item.JoinedAt).HasConversion(UtcDateTimeOffsetConverter);
        modelBuilder.Entity<EventAttendance>().HasOne(item => item.Event).WithMany(item => item.Attendees).HasForeignKey(item => item.EventId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<EventAttendance>().HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
