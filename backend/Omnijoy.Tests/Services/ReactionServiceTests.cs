using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class ReactionServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly ReactionService _sut;

    public ReactionServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _sut = new ReactionService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string name = "Alice")
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

    private async Task<Post> CreatePostAsync(Guid authorId)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorUserId = authorId,
            Content = "Test post",
            PostType = PostType.Text,
            Privacy = PrivacyLevel.Everyone,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return post;
    }

    // ── GetReactionsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetReactions_PostWithNoReactions_ReturnsEmptyCounts()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user.Id);

        var dto = await _sut.GetReactionsAsync(post.Id, user.Id);

        dto.Counts.Should().BeEmpty();
        dto.TotalCount.Should().Be(0);
        dto.CurrentUserReaction.Should().BeNull();
    }

    [Fact]
    public async Task GetReactions_WithExistingReactions_ReturnsCorrectCounts()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");
        var carol = await CreateUserAsync("Carol");
        var post  = await CreatePostAsync(alice.Id);

        _db.PostReactions.AddRange(
            new PostReaction { Id = Guid.NewGuid(), PostId = post.Id, UserId = alice.Id, ReactionType = ReactionType.Like,  CreatedAt = DateTime.UtcNow },
            new PostReaction { Id = Guid.NewGuid(), PostId = post.Id, UserId = bob.Id,   ReactionType = ReactionType.Like,  CreatedAt = DateTime.UtcNow },
            new PostReaction { Id = Guid.NewGuid(), PostId = post.Id, UserId = carol.Id, ReactionType = ReactionType.Love,  CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var dto = await _sut.GetReactionsAsync(post.Id, alice.Id);

        dto.TotalCount.Should().Be(3);
        dto.Counts.Should().Contain(c => c.ReactionType == "Like" && c.Count == 2);
        dto.Counts.Should().Contain(c => c.ReactionType == "Love" && c.Count == 1);
        dto.CurrentUserReaction.Should().Be("Like");
    }

    [Fact]
    public async Task GetReactions_AnonymousViewer_ReturnsNullCurrentUserReaction()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user.Id);

        _db.PostReactions.Add(new PostReaction
        {
            Id = Guid.NewGuid(),
            PostId = post.Id,
            UserId = user.Id,
            ReactionType = ReactionType.Haha,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var dto = await _sut.GetReactionsAsync(post.Id, null);

        dto.TotalCount.Should().Be(1);
        dto.CurrentUserReaction.Should().BeNull();
    }

    [Fact]
    public async Task GetReactions_PostNotFound_ThrowsKeyNotFoundException()
    {
        var act = async () => await _sut.GetReactionsAsync(Guid.NewGuid(), null);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetReactions_DeletedPost_ThrowsKeyNotFoundException()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user.Id);
        post.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var act = async () => await _sut.GetReactionsAsync(post.Id, user.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── AddOrUpdateReactionAsync ──────────────────────────────────────────────

    [Fact]
    public async Task AddReaction_NewReaction_PersistsAndReturnsCounts()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user.Id);

        var dto = await _sut.AddOrUpdateReactionAsync(post.Id, user.Id, "Love");

        dto.TotalCount.Should().Be(1);
        dto.Counts.Should().ContainSingle(c => c.ReactionType == "Love" && c.Count == 1);
        dto.CurrentUserReaction.Should().Be("Love");

        var stored = await _db.PostReactions.SingleAsync();
        stored.ReactionType.Should().Be(ReactionType.Love);
    }

    [Fact]
    public async Task AddReaction_CaseInsensitive_AcceptsLowerCase()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user.Id);

        var dto = await _sut.AddOrUpdateReactionAsync(post.Id, user.Id, "haha");

        dto.CurrentUserReaction.Should().Be("Haha");
    }

    [Fact]
    public async Task UpdateReaction_ExistingReaction_ChangesType()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user.Id);

        await _sut.AddOrUpdateReactionAsync(post.Id, user.Id, "Like");
        var dto = await _sut.AddOrUpdateReactionAsync(post.Id, user.Id, "Angry");

        dto.TotalCount.Should().Be(1); // still just one reaction
        dto.CurrentUserReaction.Should().Be("Angry");
        dto.Counts.Should().NotContain(c => c.ReactionType == "Like");
        dto.Counts.Should().Contain(c => c.ReactionType == "Angry" && c.Count == 1);

        // DB should have only one row
        (await _db.PostReactions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AddReaction_MultipleUsers_EachStored()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");
        var post  = await CreatePostAsync(alice.Id);

        await _sut.AddOrUpdateReactionAsync(post.Id, alice.Id, "Like");
        await _sut.AddOrUpdateReactionAsync(post.Id, bob.Id,   "Wow");

        var dto = await _sut.GetReactionsAsync(post.Id, alice.Id);
        dto.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task AddReaction_InvalidType_ThrowsArgumentException()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user.Id);

        var act = async () => await _sut.AddOrUpdateReactionAsync(post.Id, user.Id, "Thumbsup");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Thumbsup*");
    }

    [Fact]
    public async Task AddReaction_PostNotFound_ThrowsKeyNotFoundException()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.AddOrUpdateReactionAsync(Guid.NewGuid(), user.Id, "Like");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── RemoveReactionAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RemoveReaction_ExistingReaction_RemovesIt()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user.Id);

        await _sut.AddOrUpdateReactionAsync(post.Id, user.Id, "Like");
        var dto = await _sut.RemoveReactionAsync(post.Id, user.Id);

        dto.TotalCount.Should().Be(0);
        dto.Counts.Should().BeEmpty();
        dto.CurrentUserReaction.Should().BeNull();

        (await _db.PostReactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RemoveReaction_NoExistingReaction_IsNoOp()
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user.Id);

        // Should not throw
        var dto = await _sut.RemoveReactionAsync(post.Id, user.Id);

        dto.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task RemoveReaction_OnlyRemovesCallerReaction_LeavesOthersIntact()
    {
        var alice = await CreateUserAsync("Alice");
        var bob   = await CreateUserAsync("Bob");
        var post  = await CreatePostAsync(alice.Id);

        await _sut.AddOrUpdateReactionAsync(post.Id, alice.Id, "Like");
        await _sut.AddOrUpdateReactionAsync(post.Id, bob.Id,   "Love");

        await _sut.RemoveReactionAsync(post.Id, alice.Id);

        var dto = await _sut.GetReactionsAsync(post.Id, bob.Id);
        dto.TotalCount.Should().Be(1);
        dto.Counts.Should().ContainSingle(c => c.ReactionType == "Love" && c.Count == 1);
        dto.CurrentUserReaction.Should().Be("Love");
    }

    [Fact]
    public async Task RemoveReaction_PostNotFound_ThrowsKeyNotFoundException()
    {
        var user = await CreateUserAsync();

        var act = async () => await _sut.RemoveReactionAsync(Guid.NewGuid(), user.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── All six reaction types ────────────────────────────────────────────────

    [Theory]
    [InlineData("Like")]
    [InlineData("Love")]
    [InlineData("Haha")]
    [InlineData("Wow")]
    [InlineData("Sad")]
    [InlineData("Angry")]
    public async Task AddReaction_AllValidTypes_AreAccepted(string reactionType)
    {
        var user = await CreateUserAsync();
        var post = await CreatePostAsync(user.Id);

        var dto = await _sut.AddOrUpdateReactionAsync(post.Id, user.Id, reactionType);

        dto.CurrentUserReaction.Should().Be(reactionType);
        dto.TotalCount.Should().Be(1);
    }
}
