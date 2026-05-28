using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class SearchServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly SearchService _sut;

    public SearchServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db  = new OmnijoyDbContext(options);
        _sut = new SearchService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(
        string displayName = "Alice",
        string? bio = null)
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = $"{Guid.NewGuid()}@test.com",
            DisplayName = displayName,
            Bio         = bio,
            Gender      = Gender.NotDisclosed,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Post> CreatePostAsync(
        User author,
        string content,
        PrivacyLevel privacy = PrivacyLevel.Everyone)
    {
        var post = new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Author       = author,
            Content      = content,
            PostType     = PostType.Text,
            Privacy      = privacy,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return post;
    }

    private async Task<Event> CreateEventAsync(
        User creator,
        string title,
        string? description = null,
        PrivacyLevel privacy = PrivacyLevel.Everyone)
    {
        var ev = new Event
        {
            Id            = Guid.NewGuid(),
            CreatorUserId = creator.Id,
            CreatorUser   = creator,
            Title         = title,
            Description   = description,
            StartAt       = DateTime.UtcNow.AddDays(7),
            Privacy       = privacy,
            CreatedAt     = DateTime.UtcNow,
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return ev;
    }

    private async Task<CompanyPage> CreateCompanyAsync(
        User creator,
        string name,
        string? description = null)
    {
        var page = new CompanyPage
        {
            Id              = Guid.NewGuid(),
            Name            = name,
            Description     = description,
            CreatedByUserId = creator.Id,
            CreatedByUser   = creator,
            CreatedAt       = DateTime.UtcNow,
        };
        _db.CompanyPages.Add(page);
        await _db.SaveChangesAsync();
        return page;
    }

    private async Task MakeFriendsAsync(Guid a, Guid b)
    {
        _db.Friends.Add(new Friend
        {
            Id          = Guid.NewGuid(),
            RequesterId = a,
            AddresseeId = b,
            Status      = FriendStatus.Accepted,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    // ── Empty / invalid query ─────────────────────────────────────────────────

    [Fact]
    public async Task Search_EmptyQuery_ReturnsEmptyResponse()
    {
        await CreateUserAsync("Alice");
        var result = await _sut.SearchAsync(null, "   ", "all", 1, 20);

        result.Users.Should().BeEmpty();
        result.Posts.Should().BeEmpty();
        result.Events.Should().BeEmpty();
        result.Companies.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
    }

    // ── User search ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_Users_MatchesDisplayName()
    {
        await CreateUserAsync("Alice Wonder");
        await CreateUserAsync("Bob Smith");

        var result = await _sut.SearchAsync(null, "Alice", "users", 1, 20);

        result.Users.Should().HaveCount(1);
        result.Users[0].DisplayName.Should().Be("Alice Wonder");
    }

    [Fact]
    public async Task Search_Users_MatchesBio()
    {
        await CreateUserAsync("Bob Smith", bio: "I love coding in C#");
        await CreateUserAsync("Carol Jones", bio: "Painter and musician");

        var result = await _sut.SearchAsync(null, "coding", "users", 1, 20);

        result.Users.Should().HaveCount(1);
        result.Users[0].DisplayName.Should().Be("Bob Smith");
    }

    [Fact]
    public async Task Search_Users_CaseInsensitive()
    {
        await CreateUserAsync("Alice Wonder");

        var result = await _sut.SearchAsync(null, "alice", "users", 1, 20);

        result.Users.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_Users_NoMatch_ReturnsEmpty()
    {
        await CreateUserAsync("Alice Wonder");

        var result = await _sut.SearchAsync(null, "Zork", "users", 1, 20);

        result.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_Users_ReturnsEmptyArraysForOtherTypes()
    {
        await CreateUserAsync("Alice Wonder");

        var result = await _sut.SearchAsync(null, "Alice", "users", 1, 20);

        result.Posts.Should().BeEmpty();
        result.Events.Should().BeEmpty();
        result.Companies.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_Users_Pagination_HasMoreTrue()
    {
        for (int i = 0; i < 5; i++)
            await CreateUserAsync($"Tester{i:D3}");

        var page1 = await _sut.SearchAsync(null, "Tester", "users", 1, 3);
        var page2 = await _sut.SearchAsync(null, "Tester", "users", 2, 3);

        page1.Users.Should().HaveCount(3);
        page1.HasMore.Should().BeTrue();

        page2.Users.Should().HaveCount(2);
        page2.HasMore.Should().BeFalse();
    }

    // ── Post search ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_Posts_GuestSeesOnlyEveryonePosts()
    {
        var author = await CreateUserAsync("Author");
        await CreatePostAsync(author, "Wonderful day outside", PrivacyLevel.Everyone);
        await CreatePostAsync(author, "Wonderful private thought", PrivacyLevel.Friends);
        await CreatePostAsync(author, "Wonderful secret note", PrivacyLevel.OnlyMe);

        var result = await _sut.SearchAsync(null, "Wonderful", "posts", 1, 20);

        result.Posts.Should().HaveCount(1);
        result.Posts[0].Content.Should().Contain("outside");
    }

    [Fact]
    public async Task Search_Posts_AuthenticatedUserSeesFriendsPosts()
    {
        var viewer = await CreateUserAsync("Viewer");
        var friend = await CreateUserAsync("Friend");
        await MakeFriendsAsync(viewer.Id, friend.Id);

        await CreatePostAsync(friend, "Friends post hello", PrivacyLevel.Friends);
        await CreatePostAsync(friend, "Everyone post hello", PrivacyLevel.Everyone);

        var result = await _sut.SearchAsync(viewer.Id, "hello", "posts", 1, 20);

        result.Posts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Search_Posts_AuthenticatedUserDoesNotSeeStrangersPrivatePosts()
    {
        var viewer  = await CreateUserAsync("Viewer");
        var stranger = await CreateUserAsync("Stranger");

        await CreatePostAsync(stranger, "Unique friends content xyz", PrivacyLevel.Friends);
        await CreatePostAsync(stranger, "Unique onlyme content xyz", PrivacyLevel.OnlyMe);

        var result = await _sut.SearchAsync(viewer.Id, "xyz", "posts", 1, 20);

        result.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_Posts_AuthenticatedUserSeesOwnOnlyMePosts()
    {
        var user = await CreateUserAsync("Owner");
        await CreatePostAsync(user, "Secret entry abc", PrivacyLevel.OnlyMe);

        var result = await _sut.SearchAsync(user.Id, "abc", "posts", 1, 20);

        result.Posts.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_Posts_DeletedPostsExcluded()
    {
        var author = await CreateUserAsync("Author");
        var post   = await CreatePostAsync(author, "deleted content special", PrivacyLevel.Everyone);

        post.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var result = await _sut.SearchAsync(null, "special", "posts", 1, 20);

        result.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_Posts_Pagination_HasMoreTrue()
    {
        var author = await CreateUserAsync("Author");
        for (int i = 0; i < 4; i++)
            await CreatePostAsync(author, $"searchterm content {i}", PrivacyLevel.Everyone);

        var page1 = await _sut.SearchAsync(null, "searchterm", "posts", 1, 2);
        var page2 = await _sut.SearchAsync(null, "searchterm", "posts", 2, 2);

        page1.Posts.Should().HaveCount(2);
        page1.HasMore.Should().BeTrue();
        page2.Posts.Should().HaveCount(2);
        page2.HasMore.Should().BeFalse();
    }

    // ── Event search ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_Events_MatchesTitle()
    {
        var creator = await CreateUserAsync("Creator");
        await CreateEventAsync(creator, "Summer Festival 2026");
        await CreateEventAsync(creator, "Winter Gala 2026");

        var result = await _sut.SearchAsync(null, "Summer", "events", 1, 20);

        result.Events.Should().HaveCount(1);
        result.Events[0].Title.Should().Be("Summer Festival 2026");
    }

    [Fact]
    public async Task Search_Events_MatchesDescription()
    {
        var creator = await CreateUserAsync("Creator");
        await CreateEventAsync(creator, "Untitled Event", description: "A wonderful outdoor picnic event");

        var result = await _sut.SearchAsync(null, "picnic", "events", 1, 20);

        result.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_Events_GuestSeesOnlyEveryoneEvents()
    {
        var creator = await CreateUserAsync("Creator");
        await CreateEventAsync(creator, "Public Party", privacy: PrivacyLevel.Everyone);
        await CreateEventAsync(creator, "Private Party", privacy: PrivacyLevel.Friends);

        var result = await _sut.SearchAsync(null, "Party", "events", 1, 20);

        result.Events.Should().HaveCount(1);
        result.Events[0].Title.Should().Be("Public Party");
    }

    [Fact]
    public async Task Search_Events_AuthenticatedUserSeesFriendsEvents()
    {
        var viewer  = await CreateUserAsync("Viewer");
        var creator = await CreateUserAsync("Creator");
        await MakeFriendsAsync(viewer.Id, creator.Id);

        await CreateEventAsync(creator, "Friend Party", privacy: PrivacyLevel.Friends);

        var result = await _sut.SearchAsync(viewer.Id, "Party", "events", 1, 20);

        result.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_Events_AuthenticatedUserDoesNotSeeStrangersFriendsEvents()
    {
        var viewer   = await CreateUserAsync("Viewer");
        var stranger = await CreateUserAsync("Stranger");

        await CreateEventAsync(stranger, "Secret Gathering", privacy: PrivacyLevel.Friends);

        var result = await _sut.SearchAsync(viewer.Id, "Gathering", "events", 1, 20);

        result.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_Events_AuthenticatedUserSeesOwnPrivateEvents()
    {
        var user = await CreateUserAsync("Owner");
        await CreateEventAsync(user, "My Private Retreat", privacy: PrivacyLevel.Friends);

        var result = await _sut.SearchAsync(user.Id, "Retreat", "events", 1, 20);

        result.Events.Should().HaveCount(1);
    }

    // ── Company page search ───────────────────────────────────────────────────

    [Fact]
    public async Task Search_Companies_MatchesName()
    {
        var creator = await CreateUserAsync("Creator");
        await CreateCompanyAsync(creator, "Acme Corp");
        await CreateCompanyAsync(creator, "Widget Inc");

        var result = await _sut.SearchAsync(null, "Acme", "companies", 1, 20);

        result.Companies.Should().HaveCount(1);
        result.Companies[0].Name.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task Search_Companies_MatchesDescription()
    {
        var creator = await CreateUserAsync("Creator");
        await CreateCompanyAsync(creator, "Unnamed Corp", description: "We build amazing rockets");

        var result = await _sut.SearchAsync(null, "rockets", "companies", 1, 20);

        result.Companies.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_Companies_ReturnsFollowerCount()
    {
        var creator  = await CreateUserAsync("Creator");
        var follower = await CreateUserAsync("Follower");
        var page     = await CreateCompanyAsync(creator, "Popular Co");

        _db.CompanyPageFollows.Add(new CompanyPageFollow
        {
            CompanyPageId = page.Id,
            UserId        = follower.Id,
            CreatedAt     = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var result = await _sut.SearchAsync(null, "Popular", "companies", 1, 20);

        result.Companies.Should().HaveCount(1);
        result.Companies[0].FollowerCount.Should().Be(1);
    }

    // ── "all" mode ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_All_ReturnsResultsFromAllCategories()
    {
        var user    = await CreateUserAsync("Omnijoy User");
        var creator = await CreateUserAsync("Event Creator");
        var comp    = await CreateUserAsync("Company Owner");

        await CreatePostAsync(user,    "Omnijoy is great platform", PrivacyLevel.Everyone);
        await CreateEventAsync(creator, "Omnijoy Meetup");
        await CreateCompanyAsync(comp,  "Omnijoy Solutions");

        var result = await _sut.SearchAsync(null, "Omnijoy", "all", 1, 20);

        result.Type.Should().Be("all");
        result.Users.Should().HaveCount(1);
        result.Posts.Should().HaveCount(1);
        result.Events.Should().HaveCount(1);
        result.Companies.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_All_HasMoreReflectsAnyOverflow()
    {
        var author = await CreateUserAsync("Author");
        for (int i = 0; i < 3; i++)
            await CreatePostAsync(author, $"overflow keyword post {i}", PrivacyLevel.Everyone);

        var result = await _sut.SearchAsync(null, "overflow", "all", 1, 2);

        result.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task Search_All_UnknownType_FallsBackToAll()
    {
        var author = await CreateUserAsync("Alice Keyword");
        await CreatePostAsync(author, "some keyword content", PrivacyLevel.Everyone);

        var result = await _sut.SearchAsync(null, "keyword", "bogus", 1, 20);

        result.Type.Should().Be("all");
    }

    // ── Misc ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_QueryIsTrimmed()
    {
        await CreateUserAsync("Alice Wonder");

        var result = await _sut.SearchAsync(null, "  Alice  ", "users", 1, 20);

        result.Users.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_PageSizeClampedTo50()
    {
        var author = await CreateUserAsync("Author");
        for (int i = 0; i < 5; i++)
            await CreatePostAsync(author, $"keyword stuff {i}", PrivacyLevel.Everyone);

        var result = await _sut.SearchAsync(null, "keyword", "posts", 1, 200);

        result.PageSize.Should().Be(50);
        result.Posts.Length.Should().BeLessThanOrEqualTo(50);
    }

    [Fact]
    public async Task Search_ResponseContainsQueryAndType()
    {
        var result = await _sut.SearchAsync(null, "hello", "users", 1, 10);

        result.Query.Should().Be("hello");
        result.Type.Should().Be("users");
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }
}
