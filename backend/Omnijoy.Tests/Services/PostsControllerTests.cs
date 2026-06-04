using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="PostsController"/>.</summary>
public class PostsControllerTests
{
    private static PostsController Build(
        Mock<IPostService>         posts,
        Mock<IReactionService>     reactions,
        Mock<IHubContext<FeedHub>> feedHub,
        Mock<INotificationService> notifs,
        Mock<IShareService>        shares,
        Mock<IFeedCache>           feedCache,
        Guid? userId)
    {
        var clients = new Mock<IHubClients>();
        var proxy   = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        feedHub.Setup(h => h.Clients).Returns(clients.Object);

        var controller = new PostsController(
            posts.Object, reactions.Object, feedHub.Object,
            notifs.Object, shares.Object, feedCache.Object);

        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static PostDto SamplePost(Guid id, Guid authorId) => new(
        Id:                 id,
        Author:             new PostAuthorDto(authorId, "Alice", null),
        CompanyPageId:      null,
        Content:            "Hello world",
        BackgroundImageUrl: null,
        PostType:           "Text",
        Privacy:            "Friends",
        Media:              Array.Empty<PostMediaItemDto>(),
        LinkPreview:        null,
        CreatedAt:          DateTime.UtcNow,
        UpdatedAt:          DateTime.UtcNow
    );

    private static PostReactionsDto SampleReactions() => new(
        Counts:              Array.Empty<ReactionCountDto>(),
        TotalCount:          0,
        CurrentUserReaction: null
    );

    // ── GetFeed ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFeed_ReturnsOk_WithFeed()
    {
        var userId = Guid.NewGuid();
        var posts      = new Mock<IPostService>();
        var reactions  = new Mock<IReactionService>();
        var feedHub    = new Mock<IHubContext<FeedHub>>();
        var notifs     = new Mock<INotificationService>();
        var shares     = new Mock<IShareService>();
        var feedCache  = new Mock<IFeedCache>();
        posts.Setup(p => p.GetFeedAsync(userId, 1, 20)).ReturnsAsync(
            new PagedResult<FeedItemDto>(Array.Empty<FeedItemDto>(), 1, 20, false));
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.GetFeed();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetFeed_ReturnsUnauthorized_WhenNoUserId()
    {
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.GetFeed();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── GetTrending ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTrending_ReturnsOk_FromCache()
    {
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        feedCache.Setup(c => c.GetTrendingPostsAsync(default)).ReturnsAsync(Array.Empty<PostDto>());
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.GetTrending(default);

        result.Should().BeOfType<OkObjectResult>();
        posts.Verify(p => p.GetTrendingPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTrending_QueriesLive_OnCacheMiss()
    {
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        feedCache.Setup(c => c.GetTrendingPostsAsync(default)).ReturnsAsync((PostDto[]?)null);
        posts.Setup(p => p.GetTrendingPostsAsync(20, default)).ReturnsAsync(Array.Empty<PostDto>());
        feedCache.Setup(c => c.SetTrendingPostsAsync(It.IsAny<PostDto[]>(), default)).Returns(Task.CompletedTask);
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.GetTrending(default);

        result.Should().BeOfType<OkObjectResult>();
        posts.Verify(p => p.GetTrendingPostsAsync(20, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GetPost ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPost_ReturnsOk_WithDto()
    {
        var postId = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        posts.Setup(p => p.GetPostAsync(postId, null)).ReturnsAsync(SamplePost(postId, Guid.NewGuid()));
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.GetPost(postId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPost_ReturnsNotFound_WhenPostMissing()
    {
        var postId = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        posts.Setup(p => p.GetPostAsync(postId, null)).ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.GetPost(postId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetPost_Returns403_WhenPrivate()
    {
        var postId = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        posts.Setup(p => p.GetPostAsync(postId, It.IsAny<Guid?>()))
             .ThrowsAsync(new UnauthorizedAccessException("private"));
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.GetPost(postId);

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    // ── UpdatePost ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePost_ReturnsOk_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        var request   = new UpdatePostRequest("Updated content", "Friends");
        posts.Setup(p => p.UpdatePostAsync(postId, userId, request)).ReturnsAsync(SamplePost(postId, userId));
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.UpdatePost(postId, request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdatePost_ReturnsUnauthorized_WhenNoUserId()
    {
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.UpdatePost(Guid.NewGuid(), new UpdatePostRequest(null, null));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UpdatePost_ReturnsNotFound_WhenPostMissing()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        posts.Setup(p => p.UpdatePostAsync(postId, userId, It.IsAny<UpdatePostRequest>()))
             .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.UpdatePost(postId, new UpdatePostRequest(null, null));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── DeletePost ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePost_ReturnsNoContent_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        posts.Setup(p => p.DeletePostAsync(postId, userId)).Returns(Task.CompletedTask);
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.DeletePost(postId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeletePost_ReturnsUnauthorized_WhenNoUserId()
    {
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.DeletePost(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── GetReactions ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReactions_ReturnsOk_WithReactionsDto()
    {
        var postId    = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        reactions.Setup(r => r.GetReactionsAsync(postId, null)).ReturnsAsync(SampleReactions());
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.GetReactions(postId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetReactions_ReturnsNotFound_WhenPostMissing()
    {
        var postId    = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        reactions.Setup(r => r.GetReactionsAsync(postId, null)).ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.GetReactions(postId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── AddOrUpdateReaction ──────────────────────────────────────────────────

    [Fact]
    public async Task AddOrUpdateReaction_ReturnsOk_OnSuccess()
    {
        var userId    = Guid.NewGuid();
        var postId    = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        reactions.Setup(r => r.AddOrUpdateReactionAsync(postId, userId, "Like")).ReturnsAsync(SampleReactions());
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.AddOrUpdateReaction(postId, new AddOrUpdateReactionRequest("Like"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddOrUpdateReaction_ReturnsUnauthorized_WhenNoUserId()
    {
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.AddOrUpdateReaction(Guid.NewGuid(), new AddOrUpdateReactionRequest("Like"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── RemoveReaction ───────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveReaction_ReturnsOk_OnSuccess()
    {
        var userId    = Guid.NewGuid();
        var postId    = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        reactions.Setup(r => r.RemoveReactionAsync(postId, userId)).ReturnsAsync(SampleReactions());
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.RemoveReaction(postId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RemoveReaction_ReturnsUnauthorized_WhenNoUserId()
    {
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.RemoveReaction(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
