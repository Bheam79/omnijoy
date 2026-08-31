using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Models;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class MentionResolverTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly MentionResolver _sut;

    public MentionResolverTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new OmnijoyDbContext(options);
        _sut = new MentionResolver(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ResolveUsersAsync_BulkResolvesKnownSlugsAndSnapshotsNormalizedMatch()
    {
        var alice = User("alice");
        var bob = User("bob-user");
        _db.Users.AddRange(alice, bob);
        await _db.SaveChangesAsync();

        var result = await _sut.ResolveUsersAsync(["ALICE", "bob-user", "alice"]);

        result.Should().BeEquivalentTo(
            [new { UserId = alice.Id, MatchedSlug = "alice" },
             new { UserId = bob.Id, MatchedSlug = "bob-user" }]);
    }

    [Fact]
    public async Task ResolveUsersAsync_OmitsUnknownAndUsersWithoutUrlSlug()
    {
        _db.Users.AddRange(User("known"), User(null));
        await _db.SaveChangesAsync();

        var result = await _sut.ResolveUsersAsync(["known", "unknown"]);

        result.Should().ContainSingle().Which.MatchedSlug.Should().Be("known");
    }

    [Fact]
    public async Task ResolveUsersAsync_EmptyOrInvalidSet_DoesNotResolveUsers()
    {
        _db.Users.Add(User("alice"));
        await _db.SaveChangesAsync();

        var result = await _sut.ResolveUsersAsync(["", "ab", "admin", "alice--bad"]);

        result.Should().BeEmpty();
    }

    private static User User(string? slug) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@test.invalid",
        DisplayName = "Test User",
        UrlSlug = slug,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
