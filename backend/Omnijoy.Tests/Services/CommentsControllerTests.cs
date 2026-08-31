using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Comments;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="CommentsController"/>.</summary>
public class CommentsControllerTests
{
    private static (CommentsController, Mock<ICommentService>, Mock<IHubContext<FeedHub>>) Build(Guid? userId)
    {
        var comments = new Mock<ICommentService>();
        var feedHub  = new Mock<IHubContext<FeedHub>>();

        // Wire up hub so SendAsync doesn't throw
        var clients  = new Mock<IHubClients>();
        var group    = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        feedHub.Setup(h => h.Clients).Returns(clients.Object);

        var controller = new CommentsController(comments.Object, feedHub.Object);
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, comments, feedHub);
    }

    private static CommentDto SampleComment(Guid id, Guid postId) => new(
        Id:              id,
        PostId:          postId,
        Author:          new CommentAuthorDto(Guid.NewGuid(), "Author", null),
        ParentCommentId: null,
        Content:         "Hello world",
        ReplyCount:      0,
        CreatedAt:       DateTime.UtcNow,
        UpdatedAt:       DateTime.UtcNow,
        IsDeleted:       false,
        ReactionsCount:  0,
        TopReactions:    [],
        MyReaction:      null
    );

    // ── CreateComment ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateComment_ReturnsCreated_OnSuccess()
    {
        var userId    = Guid.NewGuid();
        var postId    = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var (controller, comments, _) = Build(userId);
        var request   = new CreateCommentRequest("Hello");
        comments.Setup(c => c.CreateCommentAsync(postId, userId, request))
                .ReturnsAsync(SampleComment(commentId, postId));

        var result = await controller.CreateComment(postId, request);

        result.Should().BeOfType<CreatedResult>();
        ((CreatedResult)result).Location.Should().Contain(commentId.ToString());
    }

    [Fact]
    public async Task CreateComment_ReturnsUnauthorized_WhenNoUserId()
    {
        var postId               = Guid.NewGuid();
        var (controller, _, _)   = Build(null);

        var result = await controller.CreateComment(postId, new CreateCommentRequest("Hi"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task CreateComment_ReturnsNotFound_WhenPostMissing()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var (controller, comments, _) = Build(userId);
        comments.Setup(c => c.CreateCommentAsync(postId, userId, It.IsAny<CreateCommentRequest>()))
                .ThrowsAsync(new KeyNotFoundException("post not found"));

        var result = await controller.CreateComment(postId, new CreateCommentRequest("Hi"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateComment_ReturnsBadRequest_WhenArgumentException()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var (controller, comments, _) = Build(userId);
        comments.Setup(c => c.CreateCommentAsync(postId, userId, It.IsAny<CreateCommentRequest>()))
                .ThrowsAsync(new ArgumentException("too long"));

        var result = await controller.CreateComment(postId, new CreateCommentRequest(""));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateComment_ReturnsBadRequest_WhenInvalidOperationException()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var (controller, comments, _) = Build(userId);
        comments.Setup(c => c.CreateCommentAsync(postId, userId, It.IsAny<CreateCommentRequest>()))
                .ThrowsAsync(new InvalidOperationException("max depth exceeded"));

        var result = await controller.CreateComment(postId, new CreateCommentRequest("reply"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetComments ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetComments_ReturnsOk_WithPaginatedResult()
    {
        var postId   = Guid.NewGuid();
        var (controller, comments, _) = Build(null); // anonymous
        var pageResult = new PagedResult<CommentDto>(Array.Empty<CommentDto>(), 1, 20, false);
        comments.Setup(c => c.GetCommentsAsync(postId, 1, 20)).ReturnsAsync(pageResult);

        var result = await controller.GetComments(postId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetComments_ReturnsNotFound_WhenPostMissing()
    {
        var postId   = Guid.NewGuid();
        var (controller, comments, _) = Build(null);
        comments.Setup(c => c.GetCommentsAsync(postId, 1, 20)).ThrowsAsync(new KeyNotFoundException("post"));

        var result = await controller.GetComments(postId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GetReplies ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReplies_ReturnsOk_WithList()
    {
        var commentId = Guid.NewGuid();
        var (controller, comments, _) = Build(null);
        comments.Setup(c => c.GetRepliesAsync(commentId)).ReturnsAsync(Array.Empty<CommentDto>());

        var result = await controller.GetReplies(commentId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetReplies_ReturnsNotFound_WhenCommentMissing()
    {
        var commentId = Guid.NewGuid();
        var (controller, comments, _) = Build(null);
        comments.Setup(c => c.GetRepliesAsync(commentId)).ThrowsAsync(new KeyNotFoundException("comment"));

        var result = await controller.GetReplies(commentId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── UpdateComment ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateComment_ReturnsOk_OnSuccess()
    {
        var userId    = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var (controller, comments, _) = Build(userId);
        var request   = new UpdateCommentRequest("updated");
        comments.Setup(c => c.UpdateCommentAsync(commentId, userId, request))
                .ReturnsAsync(SampleComment(commentId, Guid.NewGuid()));

        var result = await controller.UpdateComment(commentId, request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateComment_ReturnsUnauthorized_WhenNoUserId()
    {
        var (controller, _, _) = Build(null);

        var result = await controller.UpdateComment(Guid.NewGuid(), new UpdateCommentRequest("x"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UpdateComment_ReturnsNotFound_WhenCommentMissing()
    {
        var userId    = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var (controller, comments, _) = Build(userId);
        comments.Setup(c => c.UpdateCommentAsync(commentId, userId, It.IsAny<UpdateCommentRequest>()))
                .ThrowsAsync(new KeyNotFoundException("not found"));

        var result = await controller.UpdateComment(commentId, new UpdateCommentRequest("x"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateComment_Returns403_WhenNotOwner()
    {
        var userId    = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var (controller, comments, _) = Build(userId);
        comments.Setup(c => c.UpdateCommentAsync(commentId, userId, It.IsAny<UpdateCommentRequest>()))
                .ThrowsAsync(new UnauthorizedAccessException("not owner"));

        var result = await controller.UpdateComment(commentId, new UpdateCommentRequest("x"));

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    // ── DeleteComment ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteComment_ReturnsNoContent_OnSuccess()
    {
        var userId    = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var (controller, comments, _) = Build(userId);
        comments.Setup(c => c.DeleteCommentAsync(commentId, userId)).Returns(Task.CompletedTask);

        var result = await controller.DeleteComment(commentId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteComment_ReturnsUnauthorized_WhenNoUserId()
    {
        var (controller, _, _) = Build(null);

        var result = await controller.DeleteComment(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task DeleteComment_Returns403_WhenNotOwner()
    {
        var userId    = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var (controller, comments, _) = Build(userId);
        comments.Setup(c => c.DeleteCommentAsync(commentId, userId))
                .ThrowsAsync(new UnauthorizedAccessException("not owner"));

        var result = await controller.DeleteComment(commentId);

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }
}
