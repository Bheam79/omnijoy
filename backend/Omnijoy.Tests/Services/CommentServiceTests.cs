using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Comments;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class CommentServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly CommentService _sut;

    public CommentServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _sut = new CommentService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string displayName = "Alice")
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = $"{Guid.NewGuid()}@test.com",
            DisplayName = displayName,
            Gender      = Gender.NotDisclosed,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
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
