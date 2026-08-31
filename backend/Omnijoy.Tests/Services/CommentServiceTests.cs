using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Core.DTOs.Comments;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class CommentServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<INotificationService> _notificationsMock;
    private readonly CommentService _sut;

    public CommentServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _notificationsMock = new Mock<INotificationService>();
        _sut = new CommentService(
            _db,
            new MentionResolver(_db),
            new PrivacyService(_db),
            _notificationsMock.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string displayName = "Alice", string? urlSlug = null)
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = $"{Guid.NewGuid()}@test.com",
            DisplayName = displayName,
            UrlSlug     = urlSlug,
            Gender      = Gender.NotDisclosed,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task BlockAsync(Guid blockerId, Guid blockedId)
    {
        _db.Friends.Add(new Friend
        {
            Id = Guid.NewGuid(),
            RequesterId = blockerId,
            AddresseeId = blockedId,
            Status = FriendStatus.Blocked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    private async Task<Post> CreatePostAsync(User author)
    {
        var post = new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Author       = author,
            Content      = "Test post",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Everyone,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return post;
    }

    private async Task<Comment> CreateTopLevelCommentAsync(Post post, User author, string content = "Top-level comment")
    {
        var comment = new Comment
        {
            Id        = Guid.NewGuid(),
            PostId    = post.Id,
            Post      = post,
            AuthorId  = author.Id,
            Author    = author,
            Content   = content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();
        return comment;
    }

    // ── CreateCommentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateComment_TopLevel_ReturnsDto()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user);
        var request = new CreateCommentRequest("Hello world");

        var dto = await _sut.CreateCommentAsync(post.Id, user.Id, request);

        dto.Should().NotBeNull();
        dto.Content.Should().Be("Hello world");
        dto.PostId.Should().Be(post.Id);
        dto.Author.Id.Should().Be(user.Id);
        dto.ParentCommentId.Should().BeNull();
        dto.ReplyCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateComment_Reply_ReturnsDto()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user);
        var parent = await CreateTopLevelCommentAsync(post, user);

        var request = new CreateCommentRequest("A reply", parent.Id);

        var dto = await _sut.CreateCommentAsync(post.Id, user.Id, request);

        dto.ParentCommentId.Should().Be(parent.Id);
        dto.Content.Should().Be("A reply");
    }

    [Fact]
    public async Task CreateComment_EmptyContent_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user);
        var request = new CreateCommentRequest("   ");

        await _sut.Invoking(s => s.CreateCommentAsync(post.Id, user.Id, request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public async Task CreateComment_PostNotFound_ThrowsKeyNotFoundException()
    {
        var user = await CreateUserAsync();
        var request = new CreateCommentRequest("Comment");

        await _sut.Invoking(s => s.CreateCommentAsync(Guid.NewGuid(), user.Id, request))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreateComment_ParentNotFound_ThrowsKeyNotFoundException()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user);
        var request = new CreateCommentRequest("Reply", Guid.NewGuid());

        await _sut.Invoking(s => s.CreateCommentAsync(post.Id, user.Id, request))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreateComment_ReplyToReply_ThrowsInvalidOperation()
    {
        // Max depth is 2 levels; replying to a reply should be rejected.
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user);
        var parent = await CreateTopLevelCommentAsync(post, user);

        // Create first reply (level 2)
        var reply = new Comment
        {
            Id              = Guid.NewGuid(),
            PostId          = post.Id,
            Post            = post,
            AuthorId        = user.Id,
            Author          = user,
            ParentCommentId = parent.Id,
            Content         = "Level 2 reply",
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow,
        };
        _db.Comments.Add(reply);
        await _db.SaveChangesAsync();

        // Try to reply to the reply (level 3) — must fail
        var request = new CreateCommentRequest("Level 3 reply", reply.Id);

        await _sut.Invoking(s => s.CreateCommentAsync(post.Id, user.Id, request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*depth*");
    }

    [Fact]
    public async Task CreateComment_ParentBelongsToDifferentPost_ThrowsInvalidOperation()
    {
        var user   = await CreateUserAsync();
        var post1  = await CreatePostAsync(user);
        var post2  = await CreatePostAsync(user);
        var parent = await CreateTopLevelCommentAsync(post1, user);

        var request = new CreateCommentRequest("Cross-post reply", parent.Id);

        await _sut.Invoking(s => s.CreateCommentAsync(post2.Id, user.Id, request))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateComment_DuplicateMention_PersistsOnceAndNotifiesWithCommentReference()
    {
        var author = await CreateUserAsync("Alice", "alice");
        var mentioned = await CreateUserAsync("Bob", "bob-user");
        var post = await CreatePostAsync(author);

        var dto = await _sut.CreateCommentAsync(
            post.Id,
            author.Id,
            new CreateCommentRequest("Hi @BOB-user and @bob-user"));

        var mention = await _db.CommentMentions.SingleAsync();
        mention.CommentId.Should().Be(dto.Id);
        mention.MentionedUserId.Should().Be(mentioned.Id);
        mention.MatchedSlug.Should().Be("bob-user");
        dto.Mentions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            MatchedSlug = "bob-user",
            UserId = mentioned.Id,
            DisplayName = "Bob",
            UrlSlug = "bob-user",
        });
        _notificationsMock.Verify(n => n.CreateAsync(
            mentioned.Id,
            NotificationType.MentionInComment,
            dto.Id.ToString(),
            author.Id), Times.Once);
    }

    [Fact]
    public async Task CreateComment_SelfMention_IsPersistedButNotNotified()
    {
        var author = await CreateUserAsync("Alice", "alice");
        var post = await CreatePostAsync(author);

        var dto = await _sut.CreateCommentAsync(
            post.Id,
            author.Id,
            new CreateCommentRequest("Self @alice"));

        var mention = await _db.CommentMentions.SingleAsync();
        mention.CommentId.Should().Be(dto.Id);
        mention.MentionedUserId.Should().Be(author.Id);
        _notificationsMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateComment_BlockedMention_LeavesTextButDoesNotPersistOrNotify()
    {
        var author = await CreateUserAsync("Alice", "alice");
        var blocked = await CreateUserAsync("Bob", "blocked-bob");
        var post = await CreatePostAsync(author);
        await BlockAsync(author.Id, blocked.Id);

        var dto = await _sut.CreateCommentAsync(
            post.Id,
            author.Id,
            new CreateCommentRequest("Hello @blocked-bob"));

        dto.Content.Should().Be("Hello @blocked-bob");
        dto.Mentions.Should().BeEmpty();
        (await _db.CommentMentions.CountAsync()).Should().Be(0);
        _notificationsMock.Verify(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateComment_OverMentionLimit_IsRejectedAtomically()
    {
        var author = await CreateUserAsync();
        var post = await CreatePostAsync(author);
        var initialCommentCount = await _db.Comments.CountAsync();
        var content = string.Join(' ', Enumerable.Range(0, 11).Select(i => $"@user{i}"));

        await _sut.Invoking(s => s.CreateCommentAsync(
                post.Id,
                author.Id,
                new CreateCommentRequest(content)))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*10 distinct users*");

        (await _db.Comments.CountAsync()).Should().Be(initialCommentCount);
        (await _db.CommentMentions.CountAsync()).Should().Be(0);
        _notificationsMock.VerifyNoOtherCalls();
    }

    // ── GetCommentsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetComments_ReturnsTopLevelOnly()
    {
        var user    = await CreateUserAsync();
        var post    = await CreatePostAsync(user);
        var parent  = await CreateTopLevelCommentAsync(post, user, "Top-level");

        // Add a reply (should NOT appear in top-level listing)
        _db.Comments.Add(new Comment
        {
            Id              = Guid.NewGuid(),
            PostId          = post.Id,
            Post            = post,
            AuthorId        = user.Id,
            Author          = user,
            ParentCommentId = parent.Id,
            Content         = "Reply",
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetCommentsAsync(post.Id, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Items[0].Content.Should().Be("Top-level");
        result.Items[0].ReplyCount.Should().Be(1);
    }

    [Fact]
    public async Task GetComments_Pagination_Works()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user);

        for (int i = 0; i < 15; i++)
        {
            _db.Comments.Add(new Comment
            {
                Id        = Guid.NewGuid(),
                PostId    = post.Id,
                Post      = post,
                AuthorId  = user.Id,
                Author    = user,
                Content   = $"Comment {i}",
                CreatedAt = DateTime.UtcNow.AddSeconds(i),
                UpdatedAt = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync();

        var page1 = await _sut.GetCommentsAsync(post.Id, 1, 10);
        var page2 = await _sut.GetCommentsAsync(post.Id, 2, 10);

        page1.Items.Should().HaveCount(10);
        page1.HasMore.Should().BeTrue();
        page2.Items.Should().HaveCount(5);
        page2.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetComments_PostNotFound_ThrowsKeyNotFoundException()
    {
        await _sut.Invoking(s => s.GetCommentsAsync(Guid.NewGuid(), 1, 20))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetComments_HydratesReactionSummariesForWholePage_AndHidesDeletedState()
    {
        var author = await CreateUserAsync("Author");
        var viewer = await CreateUserAsync("Viewer");
        var user2 = await CreateUserAsync("User 2");
        var user3 = await CreateUserAsync("User 3");
        var user4 = await CreateUserAsync("User 4");
        var user5 = await CreateUserAsync("User 5");
        var user6 = await CreateUserAsync("User 6");
        var post = await CreatePostAsync(author);
        var first = await CreateTopLevelCommentAsync(post, author, "First");
        var second = await CreateTopLevelCommentAsync(post, author, "Second");
        var deleted = await CreateTopLevelCommentAsync(post, author, "Deleted");
        deleted.IsDeleted = true;

        var now = DateTime.UtcNow;
        _db.CommentReactions.AddRange(
            new CommentReaction { Id = Guid.NewGuid(), CommentId = first.Id, UserId = viewer.Id, ReactionType = ReactionType.Like, CreatedAt = now },
            new CommentReaction { Id = Guid.NewGuid(), CommentId = first.Id, UserId = user2.Id, ReactionType = ReactionType.Like, CreatedAt = now },
            new CommentReaction { Id = Guid.NewGuid(), CommentId = first.Id, UserId = user3.Id, ReactionType = ReactionType.Love, CreatedAt = now },
            new CommentReaction { Id = Guid.NewGuid(), CommentId = first.Id, UserId = user4.Id, ReactionType = ReactionType.Love, CreatedAt = now },
            new CommentReaction { Id = Guid.NewGuid(), CommentId = first.Id, UserId = user5.Id, ReactionType = ReactionType.Haha, CreatedAt = now },
            new CommentReaction { Id = Guid.NewGuid(), CommentId = first.Id, UserId = user6.Id, ReactionType = ReactionType.Haha, CreatedAt = now },
            new CommentReaction { Id = Guid.NewGuid(), CommentId = first.Id, UserId = author.Id, ReactionType = ReactionType.Wow, CreatedAt = now },
            new CommentReaction { Id = Guid.NewGuid(), CommentId = second.Id, UserId = user2.Id, ReactionType = ReactionType.Sad, CreatedAt = now },
            new CommentReaction { Id = Guid.NewGuid(), CommentId = deleted.Id, UserId = viewer.Id, ReactionType = ReactionType.Angry, CreatedAt = now });
        await _db.SaveChangesAsync();

        var result = await _sut.GetCommentsAsync(post.Id, 1, 20, viewer.Id);

        var firstDto = result.Items.Single(item => item.Id == first.Id);
        firstDto.ReactionsCount.Should().Be(7);
        firstDto.MyReaction.Should().Be("Like");
        firstDto.TopReactions.Should().Equal(
            new ReactionCountDto("Like", 2),
            new ReactionCountDto("Love", 2),
            new ReactionCountDto("Haha", 2));

        var secondDto = result.Items.Single(item => item.Id == second.Id);
        secondDto.ReactionsCount.Should().Be(1);
        secondDto.TopReactions.Should().ContainSingle()
            .Which.Should().Be(new ReactionCountDto("Sad", 1));
        secondDto.MyReaction.Should().BeNull();

        var deletedDto = result.Items.Single(item => item.Id == deleted.Id);
        deletedDto.ReactionsCount.Should().Be(0);
        deletedDto.TopReactions.Should().BeEmpty();
        deletedDto.MyReaction.Should().BeNull();
        (await _db.CommentReactions.CountAsync(r => r.CommentId == deleted.Id)).Should().Be(1);
    }

    [Fact]
    public async Task CommentDtoQueries_ReturnMatchedSlugWithCurrentMentionedUserProfile()
    {
        var author = await CreateUserAsync("Alice", "alice");
        var mentioned = await CreateUserAsync("Bob Before", "bob-old");
        var post = await CreatePostAsync(author);
        var parent = await _sut.CreateCommentAsync(
            post.Id,
            author.Id,
            new CreateCommentRequest("Parent mentions @bob-old"));
        var reply = await _sut.CreateCommentAsync(
            post.Id,
            author.Id,
            new CreateCommentRequest("Reply mentions @BOB-OLD!", parent.Id));

        mentioned.DisplayName = "Bob After";
        mentioned.UrlSlug = "bob-current";
        await _db.SaveChangesAsync();

        var comments = await _sut.GetCommentsAsync(post.Id, 1, 20);
        var replies = await _sut.GetRepliesAsync(parent.Id);

        comments.Items.Single().Mentions.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            MatchedSlug = "bob-old",
            UserId = mentioned.Id,
            DisplayName = "Bob After",
            UrlSlug = "bob-current",
        });
        replies.Single(item => item.Id == reply.Id).Mentions
            .Should().BeEquivalentTo(comments.Items.Single().Mentions);

        var updated = await _sut.UpdateCommentAsync(
            parent.Id,
            author.Id,
            new UpdateCommentRequest("Now mentions @bob-current"));
        updated.Mentions.Should().ContainSingle().Which.MatchedSlug.Should().Be("bob-current");
        updated.Mentions![0].UrlSlug.Should().Be("bob-current");
    }

    // ── GetRepliesAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetReplies_ReturnsRepliesOrderedByCreatedAt()
    {
        var user   = await CreateUserAsync();
        var post   = await CreatePostAsync(user);
        var parent = await CreateTopLevelCommentAsync(post, user);

        for (int i = 0; i < 3; i++)
        {
            _db.Comments.Add(new Comment
            {
                Id              = Guid.NewGuid(),
                PostId          = post.Id,
                Post            = post,
                AuthorId        = user.Id,
                Author          = user,
                ParentCommentId = parent.Id,
                Content         = $"Reply {i}",
                CreatedAt       = DateTime.UtcNow.AddSeconds(i),
                UpdatedAt       = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync();

        var replies = await _sut.GetRepliesAsync(parent.Id);

        replies.Should().HaveCount(3);
        // Oldest first
        replies[0].Content.Should().Be("Reply 0");
        replies[2].Content.Should().Be("Reply 2");
    }

    [Fact]
    public async Task GetReplies_DoesNotReturnDeletedReplies()
    {
        var user   = await CreateUserAsync();
        var post   = await CreatePostAsync(user);
        var parent = await CreateTopLevelCommentAsync(post, user);

        _db.Comments.Add(new Comment
        {
            Id              = Guid.NewGuid(),
            PostId          = post.Id,
            Post            = post,
            AuthorId        = user.Id,
            Author          = user,
            ParentCommentId = parent.Id,
            Content         = "Deleted reply",
            IsDeleted       = true,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var replies = await _sut.GetRepliesAsync(parent.Id);

        replies.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReplies_CommentNotFound_ThrowsKeyNotFoundException()
    {
        await _sut.Invoking(s => s.GetRepliesAsync(Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── UpdateCommentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateComment_Owner_UpdatesContent()
    {
        var user    = await CreateUserAsync();
        var post    = await CreatePostAsync(user);
        var comment = await CreateTopLevelCommentAsync(post, user, "Old content");

        var dto = await _sut.UpdateCommentAsync(comment.Id, user.Id, new UpdateCommentRequest("New content"));

        dto.Content.Should().Be("New content");
    }

    [Fact]
    public async Task UpdateComment_NonOwner_ThrowsUnauthorized()
    {
        var alice   = await CreateUserAsync("Alice");
        var bob     = await CreateUserAsync("Bob");
        var post    = await CreatePostAsync(alice);
        var comment = await CreateTopLevelCommentAsync(post, alice, "Alice's comment");

        await _sut.Invoking(s => s.UpdateCommentAsync(comment.Id, bob.Id, new UpdateCommentRequest("Hacked")))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UpdateComment_EmptyContent_ThrowsArgumentException()
    {
        var user    = await CreateUserAsync();
        var post    = await CreatePostAsync(user);
        var comment = await CreateTopLevelCommentAsync(post, user);

        await _sut.Invoking(s => s.UpdateCommentAsync(comment.Id, user.Id, new UpdateCommentRequest("  ")))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateComment_NotFound_ThrowsKeyNotFoundException()
    {
        var user = await CreateUserAsync();

        await _sut.Invoking(s => s.UpdateCommentAsync(Guid.NewGuid(), user.Id, new UpdateCommentRequest("x")))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateComment_DiffsMentions_AndOnlyNotifiesNewRecipients()
    {
        var author = await CreateUserAsync("Alice", "alice");
        var bob = await CreateUserAsync("Bob", "bob-user");
        var carol = await CreateUserAsync("Carol", "carol-user");
        var post = await CreatePostAsync(author);
        var comment = await _sut.CreateCommentAsync(
            post.Id,
            author.Id,
            new CreateCommentRequest("Hi @bob-user"));
        _notificationsMock.Invocations.Clear();

        await _sut.UpdateCommentAsync(
            comment.Id,
            author.Id,
            new UpdateCommentRequest("Still @bob-user, welcome @carol-user"));

        (await _db.CommentMentions.Where(m => m.CommentId == comment.Id).Select(m => m.MentionedUserId).ToListAsync())
            .Should().BeEquivalentTo([bob.Id, carol.Id]);
        _notificationsMock.Verify(n => n.CreateAsync(
            carol.Id, NotificationType.MentionInComment, comment.Id.ToString(), author.Id), Times.Once);
        _notificationsMock.Verify(n => n.CreateAsync(
            bob.Id, NotificationType.MentionInComment, It.IsAny<string>(), author.Id), Times.Never);

        _notificationsMock.Invocations.Clear();
        await _sut.UpdateCommentAsync(
            comment.Id,
            author.Id,
            new UpdateCommentRequest("Only unchanged @bob-user remains"));

        (await _db.CommentMentions.Where(m => m.CommentId == comment.Id).Select(m => m.MentionedUserId).ToListAsync())
            .Should().Equal(bob.Id);
        _notificationsMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateComment_OverMentionLimit_LeavesContentAndMentionsUnchanged()
    {
        var author = await CreateUserAsync("Alice", "alice");
        var bob = await CreateUserAsync("Bob", "bob-user");
        var post = await CreatePostAsync(author);
        var comment = await _sut.CreateCommentAsync(
            post.Id,
            author.Id,
            new CreateCommentRequest("Original @bob-user"));
        _notificationsMock.Invocations.Clear();
        var overLimit = string.Join(' ', Enumerable.Range(0, 11).Select(i => $"@user{i}"));

        await _sut.Invoking(s => s.UpdateCommentAsync(
                comment.Id,
                author.Id,
                new UpdateCommentRequest(overLimit)))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*10 distinct users*");

        (await _db.Comments.FindAsync(comment.Id))!.Content.Should().Be("Original @bob-user");
        (await _db.CommentMentions.SingleAsync(m => m.CommentId == comment.Id)).MentionedUserId.Should().Be(bob.Id);
        _notificationsMock.VerifyNoOtherCalls();
    }

    // ── DeleteCommentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteComment_Owner_SoftDeletes()
    {
        var user    = await CreateUserAsync();
        var post    = await CreatePostAsync(user);
        var comment = await CreateTopLevelCommentAsync(post, user);

        await _sut.DeleteCommentAsync(comment.Id, user.Id);

        var stored = await _db.Comments.FindAsync(comment.Id);
        stored!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteComment_NonOwner_ThrowsUnauthorized()
    {
        var alice   = await CreateUserAsync("Alice");
        var bob     = await CreateUserAsync("Bob");
        var post    = await CreatePostAsync(alice);
        var comment = await CreateTopLevelCommentAsync(post, alice);

        await _sut.Invoking(s => s.DeleteCommentAsync(comment.Id, bob.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteComment_NotFound_ThrowsKeyNotFoundException()
    {
        var user = await CreateUserAsync();

        await _sut.Invoking(s => s.DeleteCommentAsync(Guid.NewGuid(), user.Id))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeletedComment_ContentMasked_InGetComments()
    {
        // Soft-deleted top-level comments should return "[deleted]" for content.
        var user    = await CreateUserAsync();
        var post    = await CreatePostAsync(user);
        var comment = new Comment
        {
            Id        = Guid.NewGuid(),
            PostId    = post.Id,
            Post      = post,
            AuthorId  = user.Id,
            Author    = user,
            Content   = "Secret content",
            IsDeleted = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        // GetComments returns all (including soft-deleted) with masked content
        // so clients know the comment existed (for reply threading context).
        // The IsDeleted flag is also exposed so the UI can render the tombstone.
        var result = await _sut.GetCommentsAsync(post.Id, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Items[0].IsDeleted.Should().BeTrue();
        result.Items[0].Content.Should().Be("[deleted]");
    }
}
