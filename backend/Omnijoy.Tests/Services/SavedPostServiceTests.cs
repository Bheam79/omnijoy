using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class SavedPostServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly Mock<IPrivacyService> _privacy = new();
    private readonly SavedPostService _sut;

    public SavedPostServiceTests()
    {
        _db = CreateContext();
        _privacy.Setup(p => p.CanViewPostsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>()))
            .ReturnsAsync(true);
        _privacy.Setup(p => p.AreFriendsAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);
        _sut = new SavedPostService(_db, _privacy.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SaveAsync_IsIdempotent()
    {
        var user = await AddUserAsync(_db, "Owner");
        var post = await AddPostAsync(_db, user, privacy: PrivacyLevel.OnlyMe);

        (await _sut.SaveAsync(user.Id, post.Id)).Should().BeTrue();
        (await _sut.SaveAsync(user.Id, post.Id)).Should().BeFalse();

        (await _db.SavedPosts.CountAsync()).Should().Be(1);
        (await _sut.IsSavedAsync(user.Id, post.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ConcurrentUniqueViolation_ReturnsFalseAndDetachesEntry()
    {
        await using var db = new ThrowingDbContext(
            new DbContextOptionsBuilder<OmnijoyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var user = await AddUserAsync(db, "Owner");
        var post = await AddPostAsync(db, user);
        db.ExceptionToThrow = new DbUpdateException(
            "write failed", new InvalidOperationException("1062 Duplicate entry"));
        var service = new SavedPostService(db, _privacy.Object);

        (await service.SaveAsync(user.Id, post.Id)).Should().BeFalse();
        db.ChangeTracker.Entries<SavedPost>().Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_NonUniqueDatabaseFailure_Propagates()
    {
        await using var db = new ThrowingDbContext(
            new DbContextOptionsBuilder<OmnijoyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var user = await AddUserAsync(db, "Owner");
        var post = await AddPostAsync(db, user);
        db.ExceptionToThrow = new DbUpdateException("connection lost");
        var service = new SavedPostService(db, _privacy.Object);

        await service.Invoking(s => s.SaveAsync(user.Id, post.Id))
            .Should().ThrowAsync<DbUpdateException>()
            .WithMessage("connection lost");
    }

    [Fact]
    public async Task SaveAsync_MissingPost_Throws()
    {
        var user = await AddUserAsync(_db, "Saver");

        await _sut.Invoking(s => s.SaveAsync(user.Id, Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SaveAsync_WhenGlobalPrivacyDenies_ThrowsWithoutSaving()
    {
        var saver = await AddUserAsync(_db, "Saver");
        var author = await AddUserAsync(_db, "Author");
        var post = await AddPostAsync(_db, author);
        _privacy.Setup(p => p.CanViewPostsAsync(author.Id, (Guid?)saver.Id))
            .ReturnsAsync(false);

        await _sut.Invoking(s => s.SaveAsync(saver.Id, post.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        (await _db.SavedPosts.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WhenPostPrivacyDenies_Throws()
    {
        var saver = await AddUserAsync(_db, "Saver");
        var author = await AddUserAsync(_db, "Author");
        var post = await AddPostAsync(_db, author, privacy: PrivacyLevel.OnlyMe);

        await _sut.Invoking(s => s.SaveAsync(saver.Id, post.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SaveAsync_WhenAuthorInactive_Throws()
    {
        var saver = await AddUserAsync(_db, "Saver");
        var author = await AddUserAsync(_db, "Author", isActive: false);
        var post = await AddPostAsync(_db, author);

        await _sut.Invoking(s => s.SaveAsync(saver.Id, post.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SaveAsync_FriendsPost_UsesFriendGate()
    {
        var saver = await AddUserAsync(_db, "Saver");
        var author = await AddUserAsync(_db, "Author");
        var post = await AddPostAsync(_db, author, privacy: PrivacyLevel.Friends);

        (await _sut.SaveAsync(saver.Id, post.Id)).Should().BeTrue();
        _privacy.Verify(p => p.AreFriendsAsync(author.Id, saver.Id), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_PublicPost_AllowsAuthorizedViewer()
    {
        var saver = await AddUserAsync(_db, "Saver");
        var author = await AddUserAsync(_db, "Author");
        var post = await AddPostAsync(_db, author, privacy: PrivacyLevel.Everyone);

        (await _sut.SaveAsync(saver.Id, post.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_FollowerPost_AllowsPageFollower()
    {
        var saver = await AddUserAsync(_db, "Saver");
        var author = await AddUserAsync(_db, "Author");
        var page = await AddCompanyPageAsync(_db, author);
        var post = await AddPostAsync(_db, author, PrivacyLevel.Followers, page.Id);
        _db.CompanyPageFollows.Add(new CompanyPageFollow
        {
            CompanyPageId = page.Id,
            UserId = saver.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        (await _sut.SaveAsync(saver.Id, post.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_CollectionMustBelongToSaver()
    {
        var saver = await AddUserAsync(_db, "Saver");
        var owner = await AddUserAsync(_db, "Owner");
        var post = await AddPostAsync(_db, saver);
        var collection = new SavedPostCollection
        {
            Id = Guid.NewGuid(), UserId = owner.Id, Name = "Other", CreatedAt = DateTime.UtcNow,
        };
        _db.SavedPostCollections.Add(collection);
        await _db.SaveChangesAsync();

        await _sut.Invoking(s => s.SaveAsync(saver.Id, post.Id, collection.Id))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UnsaveAsync_IsIdempotent()
    {
        var user = await AddUserAsync(_db, "Owner");
        var post = await AddPostAsync(_db, user);
        await _sut.SaveAsync(user.Id, post.Id);

        (await _sut.UnsaveAsync(user.Id, post.Id)).Should().BeTrue();
        (await _sut.UnsaveAsync(user.Id, post.Id)).Should().BeFalse();
        (await _sut.IsSavedAsync(user.Id, post.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task GetSavedPostIdsAsync_UsesRequestedBatch()
    {
        var user = await AddUserAsync(_db, "Owner");
        var posts = new[]
        {
            await AddPostAsync(_db, user),
            await AddPostAsync(_db, user),
            await AddPostAsync(_db, user),
        };
        await _sut.SaveAsync(user.Id, posts[0].Id);
        await _sut.SaveAsync(user.Id, posts[1].Id);

        var ids = await _sut.GetSavedPostIdsAsync(
            user.Id, new[] { posts[0].Id, posts[0].Id, posts[2].Id });

        ids.Should().BeEquivalentTo([posts[0].Id]);
        (await _sut.GetSavedPostIdsAsync(user.Id, Array.Empty<Guid>())).Should().BeEmpty();
    }

    [Fact]
    public async Task GetSavedAsync_IsDeterministicAndPaginatesAfterVisibilityFilter()
    {
        var user = await AddUserAsync(_db, "Owner");
        var timestamp = new DateTime(2026, 8, 31, 10, 0, 0, DateTimeKind.Utc);
        var saves = new List<SavedPost>();
        for (var i = 0; i < 5; i++)
        {
            var post = await AddPostAsync(_db, user, content: $"post-{i}");
            saves.Add(new SavedPost
            {
                Id = Guid.NewGuid(), UserId = user.Id, PostId = post.Id, CreatedAt = timestamp,
            });
        }
        _db.SavedPosts.AddRange(saves);
        await _db.SaveChangesAsync();
        var expected = saves.OrderByDescending(s => s.Id).Select(s => s.PostId).ToArray();

        var first = await _sut.GetSavedAsync(user.Id, 1, 2);
        var second = await _sut.GetSavedAsync(user.Id, 2, 2);
        var third = await _sut.GetSavedAsync(user.Id, 3, 2);

        first.Items.Select(i => i.Post.Id).Should().Equal(expected[..2]);
        second.Items.Select(i => i.Post.Id).Should().Equal(expected[2..4]);
        third.Items.Select(i => i.Post.Id).Should().Equal(expected[4..]);
        first.HasMore.Should().BeTrue();
        second.HasMore.Should().BeTrue();
        third.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetSavedAsync_SilentlyOmitsDeletedAndNewlyInvisiblePosts()
    {
        var saver = await AddUserAsync(_db, "Saver");
        var visibleAuthor = await AddUserAsync(_db, "Visible");
        var hiddenAuthor = await AddUserAsync(_db, "Hidden");
        var deletedAuthor = await AddUserAsync(_db, "Deleted");
        AddEveryoneSettings(_db, visibleAuthor, hiddenAuthor, deletedAuthor);
        var visible = await AddPostAsync(_db, visibleAuthor);
        var hidden = await AddPostAsync(_db, hiddenAuthor);
        var deleted = await AddPostAsync(_db, deletedAuthor);
        AddSaved(_db, saver, visible, hidden, deleted);
        await _db.SaveChangesAsync();

        hiddenAuthor.PrivacySettings!.WhoCanSeePosts = PrivacyLevel.OnlyMe;
        deleted.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var result = await _sut.GetSavedAsync(saver.Id, 1, 20);

        result.Items.Should().ContainSingle().Which.Post.Id.Should().Be(visible.Id);
    }

    [Fact]
    public async Task GetSavedAsync_MapsCollectionAndNormalizesInvalidPaging()
    {
        var user = await AddUserAsync(_db, "Owner");
        var post = await AddPostAsync(_db, user, content: "collected");
        var collection = new SavedPostCollection
        {
            Id = Guid.NewGuid(), UserId = user.Id, Name = "Read later", CreatedAt = DateTime.UtcNow,
        };
        _db.SavedPostCollections.Add(collection);
        await _db.SaveChangesAsync();
        await _sut.SaveAsync(user.Id, post.Id, collection.Id);

        var result = await _sut.GetSavedAsync(user.Id, 0, 100);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items.Should().ContainSingle();
        result.Items[0].Post.Content.Should().Be("collected");
        result.Items[0].Collection.Should().BeEquivalentTo(new { collection.Id, collection.Name });
    }

    [Fact]
    public void Model_HasUniqueBookmarkIndexAndSafeDeleteBehaviors()
    {
        var entity = _db.Model.FindEntityType(typeof(SavedPost))!;
        entity.GetIndexes().Should().Contain(index =>
            index.IsUnique && index.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { nameof(SavedPost.UserId), nameof(SavedPost.PostId) }));

        entity.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(User))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        entity.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(Post))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        entity.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(SavedPostCollection))
            .DeleteBehavior.Should().Be(DeleteBehavior.SetNull);
    }

    private static OmnijoyDbContext CreateContext()
        => new(new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<User> AddUserAsync(
        OmnijoyDbContext db,
        string name,
        bool isActive = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.local",
            DisplayName = name,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Post> AddPostAsync(
        OmnijoyDbContext db,
        User author,
        PrivacyLevel privacy = PrivacyLevel.Everyone,
        Guid? companyPageId = null,
        string content = "post")
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Author = author,
            CompanyPageId = companyPageId,
            Content = content,
            PostType = PostType.Text,
            Privacy = privacy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    private static async Task<CompanyPage> AddCompanyPageAsync(OmnijoyDbContext db, User owner)
    {
        var page = new CompanyPage
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = owner.Id,
            Name = "Page",
            CreatedAt = DateTime.UtcNow,
        };
        db.CompanyPages.Add(page);
        await db.SaveChangesAsync();
        return page;
    }

    private static void AddEveryoneSettings(OmnijoyDbContext db, params User[] users)
    {
        foreach (var user in users)
        {
            var settings = new UserPrivacySettings
            {
                UserId = user.Id,
                User = user,
                WhoCanSeePosts = PrivacyLevel.Everyone,
            };
            user.PrivacySettings = settings;
            db.UserPrivacySettings.Add(settings);
        }
    }

    private static void AddSaved(OmnijoyDbContext db, User user, params Post[] posts)
    {
        db.SavedPosts.AddRange(posts.Select(post => new SavedPost
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PostId = post.Id,
            CreatedAt = DateTime.UtcNow,
        }));
    }

    private sealed class ThrowingDbContext(DbContextOptions<OmnijoyDbContext> options)
        : OmnijoyDbContext(options)
    {
        public DbUpdateException? ExceptionToThrow { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                var exception = ExceptionToThrow;
                ExceptionToThrow = null;
                return Task.FromException<int>(exception);
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
