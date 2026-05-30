using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Extended tests for <see cref="PostsController"/> focusing on the higher-complexity
/// form-upload / share paths.
/// </summary>
public class PostsControllerExtendedTests
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
        // We deliberately do not set up form files in the Build() method;
        // tests that need to exercise the service call should ensure
        // input.Media has at least one file (Count > 0) so the fallback
        // Request.Form path is not reached.
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static PostDto SamplePost(Guid id, Guid authorId, string privacy = "Friends") => new(
        Id:                 id,
        Author:             new PostAuthorDto(authorId, "Alice", null),
        CompanyPageId:      null,
        Content:            "Hello world",
        BackgroundImageUrl: null,
        PostType:           "Text",
        Privacy:            privacy,
        Media:              Array.Empty<PostMediaItemDto>(),
        LinkPreview:        null,
        CreatedAt:          DateTime.UtcNow,
        UpdatedAt:          DateTime.UtcNow
    );

    private static SharedPostFeedItemDto SampleSharedPost(Guid originalPostId, Guid sharerId) => new(
        Id:           Guid.NewGuid(),
        Sharer:       new PostAuthorDto(sharerId, "Bob", null),
        Message:      "Check this out!",
        TargetType:   "OwnWall",
        TargetId:     null,
        OriginalPost: SamplePost(originalPostId, Guid.NewGuid()),
        CreatedAt:    DateTime.UtcNow
    );

    // ── CreatePost — no-content guard ─────────────────────────────────────────

    [Fact]
    public async Task CreatePost_ReturnsBadRequest_WhenTextPostHasNoContent()
    {
        var userId   = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.CreatePost(new CreatePostFormInput
        {
            Content  = "   ", // whitespace only
            PostType = "Text",
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        posts.Verify(p => p.CreatePostAsync(It.IsAny<Guid>(), It.IsAny<CreatePostRequest>(), It.IsAny<List<MediaUploadItem>?>()), Times.Never);
    }

    [Fact]
    public async Task CreatePost_ReturnsBadRequest_WhenImagePostHasNoContent()
    {
        var userId    = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.CreatePost(new CreatePostFormInput
        {
            Content  = null,
            PostType = "Image",
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreatePost_ReturnsUnauthorized_WhenNoUserId()
    {
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.CreatePost(new CreatePostFormInput
        {
            Content  = "Hello",
            PostType = "Text",
        });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task CreatePost_ReturnsCreated_OnSuccess_TextPost()
    {
        var userId  = Guid.NewGuid();
        var postId  = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();

        // Supply a mock IFormFile so the fallback Request.Form path is not triggered.
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(100);
        mockFile.Setup(f => f.FileName).Returns("photo.jpg");
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        var files = new FormFileCollection { mockFile.Object };

        posts.Setup(p => p.CreatePostAsync(userId, It.IsAny<CreatePostRequest>(), It.IsAny<List<MediaUploadItem>?>()))
             .ReturnsAsync(SamplePost(postId, userId, "Friends"));
        posts.Setup(p => p.GetFriendIdsAsync(userId)).ReturnsAsync(new List<Guid>());
        feedCache.Setup(c => c.InvalidateUserFeedsAsync(It.IsAny<IEnumerable<Guid>>()))
                 .Returns(Task.CompletedTask);

        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.CreatePost(new CreatePostFormInput
        {
            Content  = "Hello world!",
            PostType = "Text",
            Privacy  = "Friends",
            Media    = files,
        });

        result.Should().BeOfType<CreatedResult>();
        ((CreatedResult)result).Location.Should().Contain(postId.ToString());
    }

    [Fact]
    public async Task CreatePost_NotifiesFriends_WhenPostIsNotOnlyMe()
    {
        var userId   = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var postId   = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();

        // Supply a mock file to avoid Request.Form fallback
        var mockFile2 = new Mock<IFormFile>();
        mockFile2.Setup(f => f.Length).Returns(50);
        mockFile2.Setup(f => f.FileName).Returns("img.png");
        mockFile2.Setup(f => f.ContentType).Returns("image/png");
        mockFile2.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        posts.Setup(p => p.CreatePostAsync(userId, It.IsAny<CreatePostRequest>(), It.IsAny<List<MediaUploadItem>?>()))
             .ReturnsAsync(SamplePost(postId, userId, "Everyone"));
        posts.Setup(p => p.GetFriendIdsAsync(userId)).ReturnsAsync(new List<Guid> { friendId });
        feedCache.Setup(c => c.InvalidateUserFeedsAsync(It.IsAny<IEnumerable<Guid>>()))
                 .Returns(Task.CompletedTask);
        notifs.Setup(n => n.CreateForManyAsync(
            It.IsAny<IEnumerable<Guid>>(), NotificationType.NewPostFromFriend,
            It.IsAny<string?>(), userId)).Returns(Task.CompletedTask);

        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.CreatePost(new CreatePostFormInput
        {
            Content  = "Hello world!",
            PostType = "Text",
            Privacy  = "Everyone",
            Media    = new FormFileCollection { mockFile2.Object },
        });

        result.Should().BeOfType<CreatedResult>();
        notifs.Verify(n => n.CreateForManyAsync(
            It.IsAny<IEnumerable<Guid>>(), NotificationType.NewPostFromFriend,
            It.IsAny<string?>(), userId), Times.Once);
    }

    [Fact]
    public async Task CreatePost_DoesNotNotifyFriends_WhenPostIsOnlyMe()
    {
        var userId   = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var postId   = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();

        var mockFile3 = new Mock<IFormFile>();
        mockFile3.Setup(f => f.Length).Returns(50);
        mockFile3.Setup(f => f.FileName).Returns("photo.jpg");
        mockFile3.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile3.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        posts.Setup(p => p.CreatePostAsync(userId, It.IsAny<CreatePostRequest>(), It.IsAny<List<MediaUploadItem>?>()))
             .ReturnsAsync(SamplePost(postId, userId, "OnlyMe"));
        posts.Setup(p => p.GetFriendIdsAsync(userId)).ReturnsAsync(new List<Guid> { friendId });
        feedCache.Setup(c => c.InvalidateUserFeedsAsync(It.IsAny<IEnumerable<Guid>>()))
                 .Returns(Task.CompletedTask);

        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.CreatePost(new CreatePostFormInput
        {
            Content  = "Private note",
            PostType = "Text",
            Privacy  = "OnlyMe",
            Media    = new FormFileCollection { mockFile3.Object },
        });

        result.Should().BeOfType<CreatedResult>();
        notifs.Verify(n => n.CreateForManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task CreatePost_ReturnsBadRequest_WhenArgumentException()
    {
        var userId = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        var mockFile4 = new Mock<IFormFile>();
        mockFile4.Setup(f => f.Length).Returns(50);
        mockFile4.Setup(f => f.FileName).Returns("x.jpg");
        mockFile4.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile4.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        posts.Setup(p => p.CreatePostAsync(userId, It.IsAny<CreatePostRequest>(), It.IsAny<List<MediaUploadItem>?>()))
             .ThrowsAsync(new ArgumentException("invalid post type"));

        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.CreatePost(new CreatePostFormInput
        {
            Content  = "Hello",
            PostType = "Invalid",
            Media    = new FormFileCollection { mockFile4.Object },
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── SharePost ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SharePost_ReturnsCreated_OnSuccess()
    {
        var userId        = Guid.NewGuid();
        var originalPostId = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();

        var sharedPost = SampleSharedPost(originalPostId, userId);
        var shareRequest = new CreateShareRequest("OwnWall", null, "Check this!");
        shares.Setup(s => s.CreateShareAsync(userId, originalPostId, shareRequest)).ReturnsAsync(sharedPost);
        shares.Setup(s => s.GetShareRecipientIdsAsync(userId, sharedPost)).ReturnsAsync(new List<Guid> { userId });
        notifs.Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
              .ReturnsAsync(new Omnijoy.Core.DTOs.Notifications.NotificationDto(
                  Guid.NewGuid(), "PostShare", null, false, DateTime.UtcNow, null, null, null));

        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.SharePost(originalPostId, shareRequest);

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task SharePost_ReturnsUnauthorized_WhenNoUserId()
    {
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, null);

        var result = await controller.SharePost(Guid.NewGuid(), new CreateShareRequest("OwnWall", null, null));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task SharePost_ReturnsNotFound_WhenOriginalPostMissing()
    {
        var userId        = Guid.NewGuid();
        var originalPostId = Guid.NewGuid();
        var posts     = new Mock<IPostService>();
        var reactions = new Mock<IReactionService>();
        var feedHub   = new Mock<IHubContext<FeedHub>>();
        var notifs    = new Mock<INotificationService>();
        var shares    = new Mock<IShareService>();
        var feedCache = new Mock<IFeedCache>();
        shares.Setup(s => s.CreateShareAsync(userId, originalPostId, It.IsAny<CreateShareRequest>()))
              .ThrowsAsync(new KeyNotFoundException("not found"));

        var controller = Build(posts, reactions, feedHub, notifs, shares, feedCache, userId);

        var result = await controller.SharePost(originalPostId, new CreateShareRequest("OwnWall", null, null));

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
