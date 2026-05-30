using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ShareController"/>.
/// Uses in-memory EF so the full HTML-generation path is exercised without
/// touching a real database. Because there is no built <c>index.html</c>
/// in the test environment, the controller falls back to
/// <c>BuildStaticShell</c> — the entire response-building path is still covered.
/// </summary>
public class ShareControllerTests : IDisposable
{
    private readonly OmnijoyDbContext _db;

    public ShareControllerTests()
    {
        var opts = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new OmnijoyDbContext(opts);
    }

    public void Dispose() => _db.Dispose();

    private ShareController Build()
    {
        var env = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        // Point to a non-existent directory so index.html is not found → BuildStaticShell is used.
        env.Setup(e => e.WebRootPath).Returns("/nonexistent-path");

        var controller = new ShareController(_db, env.Object);

        var http = new DefaultHttpContext();
        // Set up Request.Scheme and Request.Host so BuildCanonical works.
        http.Request.Scheme = "https";
        http.Request.Host   = new HostString("omnijoy.test");

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private async Task<User> CreateUserAsync(
        string displayName = "Alice",
        PrivacyLevel profilePrivacy = PrivacyLevel.Everyone)
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = $"{Guid.NewGuid()}@test.com",
            DisplayName = displayName,
            Gender      = Gender.NotDisclosed,
            CreatedAt   = DateTime.UtcNow,
            PrivacySettings = new UserPrivacySettings
            {
                WhoCanSeeProfile = profilePrivacy,
                WhoCanSeePosts   = PrivacyLevel.Everyone,
                WhoCanSeeFriendList = PrivacyLevel.Everyone,
                WhoCanSendMessages  = PrivacyLevel.Friends,
                WhoCanSeeEvents     = PrivacyLevel.Everyone,
                WhoCanTagInPosts    = PrivacyLevel.Friends,
            }
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Post> CreatePostAsync(User author, PrivacyLevel privacy = PrivacyLevel.Everyone)
    {
        var post = new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Content      = "Hello world",
            Privacy      = privacy,
            PostType     = PostType.Text,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return post;
    }

    private async Task<Event> CreateEventAsync(User creator, PrivacyLevel privacy = PrivacyLevel.Everyone)
    {
        var ev = new Event
        {
            Id            = Guid.NewGuid(),
            CreatorUserId = creator.Id,
            Title         = "Test Event",
            StartAt       = DateTime.UtcNow.AddDays(7),
            Privacy       = privacy,
            CreatedAt     = DateTime.UtcNow,
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return ev;
    }

    // ── SharePost ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SharePost_ReturnsHtml_ForPublicPost()
    {
        var author = await CreateUserAsync();
        var post   = await CreatePostAsync(author, PrivacyLevel.Everyone);
        var controller = Build();

        var result = await controller.SharePost(post.Id);

        result.Should().BeOfType<ContentResult>();
        var content = (ContentResult)result;
        content.ContentType.Should().Contain("text/html");
        content.Content.Should().Contain("Hello world");
    }

    [Fact]
    public async Task SharePost_ReturnsHtml_WithPrivateMessage_WhenPrivate()
    {
        var author = await CreateUserAsync();
        var post   = await CreatePostAsync(author, PrivacyLevel.Friends);
        var controller = Build();

        var result = await controller.SharePost(post.Id);

        result.Should().BeOfType<ContentResult>();
        var content = (ContentResult)result;
        content.Content.Should().Contain("Private");
    }

    [Fact]
    public async Task SharePost_ReturnsHtml_WithNotFoundMessage_WhenPostMissing()
    {
        var controller = Build();

        var result = await controller.SharePost(Guid.NewGuid());

        result.Should().BeOfType<ContentResult>();
        var content = (ContentResult)result;
        content.Content.Should().Contain("not found");
    }

    [Fact]
    public async Task SharePost_ReturnsHtml_ForPublicPost_WithNoContent()
    {
        var author = await CreateUserAsync();
        // Post with empty content
        var post = new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Content      = "",
            Privacy      = PrivacyLevel.Everyone,
            PostType     = PostType.Image,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        var controller = Build();

        var result = await controller.SharePost(post.Id);

        result.Should().BeOfType<ContentResult>();
    }

    // ── ShareUser ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShareUser_ReturnsHtml_ForPublicProfile()
    {
        var user = await CreateUserAsync("Bob", PrivacyLevel.Everyone);
        var controller = Build();

        var result = await controller.ShareUser(user.Id);

        result.Should().BeOfType<ContentResult>();
        var content = (ContentResult)result;
        content.Content.Should().Contain("Bob");
    }

    [Fact]
    public async Task ShareUser_ReturnsHtml_WithPrivateMessage_WhenProfileRestricted()
    {
        var user = await CreateUserAsync("Charlie", PrivacyLevel.Friends);
        var controller = Build();

        var result = await controller.ShareUser(user.Id);

        result.Should().BeOfType<ContentResult>();
        var content = (ContentResult)result;
        content.Content.Should().Contain("Private");
    }

    [Fact]
    public async Task ShareUser_ReturnsHtml_WithNotFoundMessage_WhenUserMissing()
    {
        var controller = Build();

        var result = await controller.ShareUser(Guid.NewGuid());

        result.Should().BeOfType<ContentResult>();
        var content = (ContentResult)result;
        content.Content.Should().Contain("not found");
    }

    [Fact]
    public async Task ShareUser_ReturnsHtml_WithBio_WhenPublicAndHasBio()
    {
        var user = await CreateUserAsync("Diana", PrivacyLevel.Everyone);
        user.Bio = "Hello, I am Diana!";
        await _db.SaveChangesAsync();
        var controller = Build();

        var result = await controller.ShareUser(user.Id);

        result.Should().BeOfType<ContentResult>();
        ((ContentResult)result).Content.Should().Contain("Diana");
    }

    // ── ShareEvent ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShareEvent_ReturnsHtml_ForPublicEvent()
    {
        var creator = await CreateUserAsync("Eve");
        var ev      = await CreateEventAsync(creator, PrivacyLevel.Everyone);
        var controller = Build();

        var result = await controller.ShareEvent(ev.Id);

        result.Should().BeOfType<ContentResult>();
        var content = (ContentResult)result;
        content.Content.Should().Contain("Test Event");
    }

    [Fact]
    public async Task ShareEvent_ReturnsHtml_WithPrivateMessage_WhenRestricted()
    {
        var creator = await CreateUserAsync("Frank");
        var ev      = await CreateEventAsync(creator, PrivacyLevel.Friends);
        var controller = Build();

        var result = await controller.ShareEvent(ev.Id);

        result.Should().BeOfType<ContentResult>();
        var content = (ContentResult)result;
        content.Content.Should().Contain("Private");
    }

    [Fact]
    public async Task ShareEvent_ReturnsHtml_WithNotFoundMessage_WhenMissing()
    {
        var controller = Build();

        var result = await controller.ShareEvent(Guid.NewGuid());

        result.Should().BeOfType<ContentResult>();
        ((ContentResult)result).Content.Should().Contain("not found");
    }

    [Fact]
    public async Task ShareEvent_ReturnsHtml_WithLocationAndDescription_WhenPublic()
    {
        var creator = await CreateUserAsync("Grace");
        var ev = new Event
        {
            Id            = Guid.NewGuid(),
            CreatorUserId = creator.Id,
            Title         = "Community Meetup",
            Description   = "A fun community meetup event",
            Location      = "Paris, France",
            StartAt       = DateTime.UtcNow.AddDays(14),
            Privacy       = PrivacyLevel.Everyone,
            CreatedAt     = DateTime.UtcNow,
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        var controller = Build();

        var result = await controller.ShareEvent(ev.Id);

        result.Should().BeOfType<ContentResult>();
        var content = (ContentResult)result;
        content.Content.Should().Contain("Community Meetup");
        content.Content.Should().Contain("Paris");
    }
}
