using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Core.DTOs.Notifications;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class CommentReactionServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<INotificationService> _notifications = new();
    private readonly CommentReactionService _sut;

    public CommentReactionServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new OmnijoyDbContext(options);
        _sut = new CommentReactionService(_db, _notifications.Object);
    }

    public void Dispose() => _db.Dispose();

    private async Task<User> CreateUserAsync(string name = "User")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.com",
            DisplayName = name,
            Gender = Gender.NotDisclosed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Comment> CreateCommentAsync(User author, bool deleted = false)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Content = "Post",
            PostType = PostType.Text,
            Privacy = PrivacyLevel.Everyone,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = post.Id,
            AuthorId = author.Id,
            Content = "Comment",
            IsDeleted = deleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();
        return comment;
    }

    [Fact]
    public async Task AddReaction_IsCaseInsensitive_PersistsAndNotifiesAuthor()
    {
        var author = await CreateUserAsync("Author");
        var actor = await CreateUserAsync("Actor");
        var comment = await CreateCommentAsync(author);

        var result = await _sut.AddOrUpdateReactionAsync(comment.Id, actor.Id, "love");

        result.TotalCount.Should().Be(1);
        result.CurrentUserReaction.Should().Be("Love");
        result.Counts.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            ReactionType = "Love",
            Count = 1,
        });
        (await _db.CommentReactions.SingleAsync()).ReactionType.Should().Be(ReactionType.Love);
        _notifications.Verify(n => n.CreateAsync(
            author.Id,
            NotificationType.CommentLike,
            comment.Id.ToString(),
            actor.Id), Times.Once);
    }

    [Fact]
    public async Task UpdateReaction_ChangesExistingRowAndNotifiesAgain()
    {
        var author = await CreateUserAsync("Author");
        var actor = await CreateUserAsync("Actor");
        var comment = await CreateCommentAsync(author);

        await _sut.AddOrUpdateReactionAsync(comment.Id, actor.Id, "Like");
        var createdAt = (await _db.CommentReactions.SingleAsync()).CreatedAt;
        var result = await _sut.AddOrUpdateReactionAsync(comment.Id, actor.Id, "Angry");

        (await _db.CommentReactions.CountAsync()).Should().Be(1);
        (await _db.CommentReactions.SingleAsync()).CreatedAt.Should().Be(createdAt);
        result.TotalCount.Should().Be(1);
        result.CurrentUserReaction.Should().Be("Angry");
        result.Counts.Should().ContainSingle(c => c.ReactionType == "Angry" && c.Count == 1);
        _notifications.Verify(n => n.CreateAsync(
            author.Id,
            NotificationType.CommentLike,
            comment.Id.ToString(),
            actor.Id), Times.Exactly(2));
    }

    [Fact]
    public async Task RemoveReaction_RemovesIt_AndRepeatedRemovalThrowsNotFound()
    {
        var author = await CreateUserAsync();
        var comment = await CreateCommentAsync(author);
        await _sut.AddOrUpdateReactionAsync(comment.Id, author.Id, "Like");

        var result = await _sut.RemoveReactionAsync(comment.Id, author.Id);

        result.TotalCount.Should().Be(0);
        result.Counts.Should().BeEmpty();
        result.CurrentUserReaction.Should().BeNull();
        await _sut.Invoking(s => s.RemoveReactionAsync(comment.Id, author.Id))
            .Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Reaction*not found*");
    }

    [Fact]
    public async Task RemoveReaction_MissingComment_ThrowsNotFound()
    {
        var actor = await CreateUserAsync();

        await _sut.Invoking(s => s.RemoveReactionAsync(Guid.NewGuid(), actor.Id))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("Thumbsup")]
    [InlineData("0")]
    public async Task AddReaction_InvalidType_ReportsSameValidValuesAsPostReactions(string reactionType)
    {
        var author = await CreateUserAsync();
        var comment = await CreateCommentAsync(author);

        await _sut.Invoking(s => s.AddOrUpdateReactionAsync(comment.Id, author.Id, reactionType))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage($"Invalid ReactionType: '{reactionType}'. Valid values are: Like, Love, Haha, Wow, Sad, Angry.");
        _notifications.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("get")]
    [InlineData("who")]
    [InlineData("add")]
    [InlineData("remove")]
    public async Task Operations_DeletedComment_ThrowAndPreserveHistoricalRows(string operation)
    {
        var author = await CreateUserAsync();
        var comment = await CreateCommentAsync(author, deleted: true);
        _db.CommentReactions.Add(new CommentReaction
        {
            Id = Guid.NewGuid(),
            CommentId = comment.Id,
            UserId = author.Id,
            ReactionType = ReactionType.Wow,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        Func<Task> act = operation switch
        {
            "get" => () => _sut.GetReactionsAsync(comment.Id, author.Id),
            "who" => () => _sut.GetReactionWhoAsync(comment.Id, author.Id),
            "add" => () => _sut.AddOrUpdateReactionAsync(comment.Id, author.Id, "Love"),
            _ => () => _sut.RemoveReactionAsync(comment.Id, author.Id),
        };

        await act.Should().ThrowAsync<KeyNotFoundException>();
        (await _db.CommentReactions.SingleAsync()).ReactionType.Should().Be(ReactionType.Wow);
    }

    [Fact]
    public async Task GetReactions_ReturnsCountsAndCurrentUserState()
    {
        var author = await CreateUserAsync("Author");
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var comment = await CreateCommentAsync(author);

        await _sut.AddOrUpdateReactionAsync(comment.Id, author.Id, "Like");
        await _sut.AddOrUpdateReactionAsync(comment.Id, alice.Id, "Like");
        await _sut.AddOrUpdateReactionAsync(comment.Id, bob.Id, "Sad");

        var mine = await _sut.GetReactionsAsync(comment.Id, bob.Id);
        var anonymous = await _sut.GetReactionsAsync(comment.Id, null);

        mine.TotalCount.Should().Be(3);
        mine.Counts.Should().BeEquivalentTo([
            new ReactionCountDto("Like", 2),
            new ReactionCountDto("Sad", 1),
        ]);
        mine.CurrentUserReaction.Should().Be("Sad");
        anonymous.CurrentUserReaction.Should().BeNull();
    }

    [Fact]
    public async Task GetOwningPostId_ReturnsPostId_AndRejectsMissingOrDeletedComment()
    {
        var author = await CreateUserAsync();
        var active = await CreateCommentAsync(author);
        var deleted = await CreateCommentAsync(author, deleted: true);

        (await _sut.GetOwningPostIdAsync(active.Id)).Should().Be(active.PostId);
        await _sut.Invoking(s => s.GetOwningPostIdAsync(deleted.Id))
            .Should().ThrowAsync<KeyNotFoundException>();
        await _sut.Invoking(s => s.GetOwningPostIdAsync(Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetReactionWho_PrioritizesAcceptedFriends_LimitsFiveAndReturnsRemainder()
    {
        var viewer = await CreateUserAsync("Viewer");
        var author = await CreateUserAsync("Author");
        var reactors = new List<User>();
        for (var i = 0; i < 7; i++)
            reactors.Add(await CreateUserAsync($"Reactor {i}"));
        var comment = await CreateCommentAsync(author);
        var timestamp = DateTime.UtcNow;

        _db.Friends.AddRange(
            new Friend { Id = Guid.NewGuid(), RequesterId = viewer.Id, AddresseeId = reactors[5].Id, Status = FriendStatus.Accepted, CreatedAt = timestamp, UpdatedAt = timestamp },
            new Friend { Id = Guid.NewGuid(), RequesterId = reactors[6].Id, AddresseeId = viewer.Id, Status = FriendStatus.Accepted, CreatedAt = timestamp, UpdatedAt = timestamp },
            new Friend { Id = Guid.NewGuid(), RequesterId = viewer.Id, AddresseeId = reactors[4].Id, Status = FriendStatus.Pending, CreatedAt = timestamp, UpdatedAt = timestamp });
        for (var i = 0; i < reactors.Count; i++)
        {
            _db.CommentReactions.Add(new CommentReaction
            {
                Id = Guid.NewGuid(),
                CommentId = comment.Id,
                UserId = reactors[i].Id,
                ReactionType = i == 6 ? ReactionType.Love : ReactionType.Like,
                CreatedAt = timestamp.AddSeconds(i),
            });
        }
        await _db.SaveChangesAsync();

        var result = await _sut.GetReactionWhoAsync(comment.Id, viewer.Id);

        result.People.Should().HaveCount(5);
        result.Remaining.Should().Be(2);
        result.People.Take(2).Select(person => person.Id)
            .Should().Equal(reactors[5].Id, reactors[6].Id);
        result.People.Take(2).Should().OnlyContain(person => person.IsFriend);
        result.People.Skip(2).Should().OnlyContain(person => !person.IsFriend);
        result.People[1].ReactionType.Should().Be("Love");
    }

    [Fact]
    public async Task GetReactionWho_NullViewerUsesChronologicalOrder_AndEmptyIsSupported()
    {
        var author = await CreateUserAsync("Author");
        var first = await CreateUserAsync("First");
        var second = await CreateUserAsync("Second");
        var comment = await CreateCommentAsync(author);

        (await _sut.GetReactionWhoAsync(comment.Id, null)).People.Should().BeEmpty();
        await _sut.AddOrUpdateReactionAsync(comment.Id, first.Id, "Like");
        await Task.Delay(2);
        await _sut.AddOrUpdateReactionAsync(comment.Id, second.Id, "Wow");

        var result = await _sut.GetReactionWhoAsync(comment.Id, null);

        result.People.Select(person => person.Id).Should().Equal(first.Id, second.Id);
        result.People.Should().OnlyContain(person => !person.IsFriend);
        result.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task NotificationService_PersistsAuthorNotification_ButSuppressesSelfReaction()
    {
        var author = await CreateUserAsync("Author");
        var actor = await CreateUserAsync("Actor");
        var comment = await CreateCommentAsync(author);
        var hub = new Mock<IHubContextDispatcher>();
        var notificationService = new NotificationService(_db, hub.Object);
        var sut = new CommentReactionService(_db, notificationService);

        await sut.AddOrUpdateReactionAsync(comment.Id, actor.Id, "Like");
        await sut.AddOrUpdateReactionAsync(comment.Id, author.Id, "Love");

        var notification = await _db.Notifications.SingleAsync();
        notification.UserId.Should().Be(author.Id);
        notification.Type.Should().Be(NotificationType.CommentLike);
        notification.ReferenceId.Should().Be(comment.Id.ToString());
        hub.Verify(h => h.SendToUserAsync(
            author.Id,
            "NotificationReceived",
            It.IsAny<NotificationDto>()), Times.Once);
    }
}
