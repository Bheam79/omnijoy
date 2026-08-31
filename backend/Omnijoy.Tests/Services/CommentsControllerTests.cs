using System.Security.Claims;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Api.Hubs;
using Omnijoy.Api.RateLimiting;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Comments;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="CommentsController"/>.</summary>
public class CommentsControllerTests
{
    private static (CommentsController, Mock<ICommentService>, Mock<IHubContext<FeedHub>>) Build(Guid? userId)
    {
        var comments = new Mock<ICommentService>();
        var reactions = new Mock<ICommentReactionService>();
        var feedHub  = new Mock<IHubContext<FeedHub>>();

        // Wire up hub so SendAsync doesn't throw
        var clients  = new Mock<IHubClients>();
        var group    = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        feedHub.Setup(h => h.Clients).Returns(clients.Object);

        var controller = new CommentsController(comments.Object, reactions.Object, feedHub.Object);
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, comments, feedHub);
    }

    private sealed record ReactionFixture(
        CommentsController Controller,
        Mock<ICommentReactionService> Reactions,
        Mock<IHubClients> Clients,
        Mock<IClientProxy> Proxy);

    private static ReactionFixture BuildReactions(Guid? userId)
    {
        var comments = new Mock<ICommentService>();
        var reactions = new Mock<ICommentReactionService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var clients = new Mock<IHubClients>();
        var proxy = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        feedHub.Setup(h => h.Clients).Returns(clients.Object);
        proxy.Setup(p => p.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new CommentsController(
            comments.Object,
            reactions.Object,
            feedHub.Object);
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        return new ReactionFixture(controller, reactions, clients, proxy);
    }

    private static PostReactionsDto SampleReactions() => new(
        [new ReactionCountDto("Like", 2), new ReactionCountDto("Love", 1)],
        3,
        "Like");

    private static bool MatchesReactionEvent(
        object?[] args,
        Guid commentId,
        Guid postId,
        ReactionCountDto[] counts,
        int total)
    {
        var evt = args.SingleOrDefault() as CommentReactionCountsUpdatedEvent;
        return evt is not null &&
               evt.CommentId == commentId &&
               evt.PostId == postId &&
               ReferenceEquals(evt.Counts, counts) &&
               evt.Total == total;
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

    // ── Comment reactions ─────────────────────────────────────────────

    [Fact]
    public async Task GetReactions_ForwardsAuthenticatedCurrentUser_AndReturnsOk()
    {
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var fixture = BuildReactions(userId);
        var dto = SampleReactions();
        fixture.Reactions.Setup(r => r.GetReactionsAsync(commentId, userId)).ReturnsAsync(dto);

        var result = await fixture.Controller.GetReactions(commentId);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(dto);
        fixture.Reactions.Verify(r => r.GetReactionsAsync(commentId, userId), Times.Once);
    }

    [Fact]
    public async Task GetReactionWho_AllowsAnonymous_AndForwardsNullCurrentUser()
    {
        var commentId = Guid.NewGuid();
        var fixture = BuildReactions(null);
        var dto = new ReactionWhoDto([], 0);
        fixture.Reactions.Setup(r => r.GetReactionWhoAsync(commentId, null)).ReturnsAsync(dto);

        var result = await fixture.Controller.GetReactionWho(commentId);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(dto);
        fixture.Reactions.Verify(r => r.GetReactionWhoAsync(commentId, null), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReactionReads_ReturnNotFound_WhenCommentDoesNotExist(bool who)
    {
        var commentId = Guid.NewGuid();
        var fixture = BuildReactions(null);
        fixture.Reactions.Setup(r => r.GetReactionsAsync(commentId, null))
            .ThrowsAsync(new KeyNotFoundException("comment not found"));
        fixture.Reactions.Setup(r => r.GetReactionWhoAsync(commentId, null))
            .ThrowsAsync(new KeyNotFoundException("comment not found"));

        var result = who
            ? await fixture.Controller.GetReactionWho(commentId)
            : await fixture.Controller.GetReactions(commentId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddOrUpdateReaction_ReturnsCounts_AndPushesExactPostGroupEventAndPayload()
    {
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var fixture = BuildReactions(userId);
        var dto = SampleReactions();
        fixture.Reactions
            .Setup(r => r.AddOrUpdateReactionAsync(commentId, userId, "Love"))
            .ReturnsAsync(dto);
        fixture.Reactions.Setup(r => r.GetOwningPostIdAsync(commentId)).ReturnsAsync(postId);

        var result = await fixture.Controller.AddOrUpdateReaction(
            commentId,
            new AddOrUpdateReactionRequest("Love"));

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(dto);
        fixture.Clients.Verify(c => c.Group($"post:{postId}"), Times.Once);
        fixture.Clients.Verify(c => c.Group(It.Is<string>(group => group.StartsWith("user:"))), Times.Never);
        fixture.Proxy.Verify(p => p.SendCoreAsync(
            "CommentReactionCountsUpdated",
            It.Is<object?[]>(args => MatchesReactionEvent(
                args, commentId, postId, dto.Counts, dto.TotalCount)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveReaction_ReturnsCounts_AndPushesUpdate()
    {
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var fixture = BuildReactions(userId);
        var dto = SampleReactions();
        fixture.Reactions.Setup(r => r.RemoveReactionAsync(commentId, userId)).ReturnsAsync(dto);
        fixture.Reactions.Setup(r => r.GetOwningPostIdAsync(commentId)).ReturnsAsync(postId);

        var result = await fixture.Controller.RemoveReaction(commentId);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(dto);
        fixture.Proxy.Verify(p => p.SendCoreAsync(
            "CommentReactionCountsUpdated",
            It.Is<object?[]>(args => MatchesReactionEvent(
                args, commentId, postId, dto.Counts, dto.TotalCount)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveReaction_RepeatedDeleteReturnsNotFound_WithoutSecondPush()
    {
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var fixture = BuildReactions(userId);
        fixture.Reactions.SetupSequence(r => r.RemoveReactionAsync(commentId, userId))
            .ReturnsAsync(SampleReactions())
            .ThrowsAsync(new KeyNotFoundException("reaction not found"));
        fixture.Reactions.Setup(r => r.GetOwningPostIdAsync(commentId)).ReturnsAsync(Guid.NewGuid());

        (await fixture.Controller.RemoveReaction(commentId)).Should().BeOfType<OkObjectResult>();
        var repeated = await fixture.Controller.RemoveReaction(commentId);

        repeated.Should().BeOfType<NotFoundObjectResult>();
        fixture.Proxy.Verify(p => p.SendCoreAsync(
            "CommentReactionCountsUpdated",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Reactions.Verify(r => r.GetOwningPostIdAsync(commentId), Times.Once);
    }

    [Fact]
    public async Task AddOrUpdateReaction_InvalidTypeReturnsBadRequest_WithoutPush()
    {
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var fixture = BuildReactions(userId);
        fixture.Reactions
            .Setup(r => r.AddOrUpdateReactionAsync(commentId, userId, "Invalid"))
            .ThrowsAsync(new ArgumentException("invalid reaction"));

        var result = await fixture.Controller.AddOrUpdateReaction(
            commentId,
            new AddOrUpdateReactionRequest("Invalid"));

        result.Should().BeOfType<BadRequestObjectResult>();
        fixture.Proxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Reactions.Verify(r => r.GetOwningPostIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task AddOrUpdateReaction_MissingCommentReturnsNotFound_WithoutPush()
    {
        var userId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var fixture = BuildReactions(userId);
        fixture.Reactions
            .Setup(r => r.AddOrUpdateReactionAsync(commentId, userId, "Like"))
            .ThrowsAsync(new KeyNotFoundException("comment not found"));

        var result = await fixture.Controller.AddOrUpdateReaction(
            commentId,
            new AddOrUpdateReactionRequest("Like"));

        result.Should().BeOfType<NotFoundObjectResult>();
        fixture.Proxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Reactions.Verify(r => r.GetOwningPostIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData(nameof(CommentsController.AddOrUpdateReaction))]
    [InlineData(nameof(CommentsController.RemoveReaction))]
    public void ReactionWrites_UseInteractionRateLimit(string methodName)
    {
        var attribute = typeof(CommentsController)
            .GetMethod(methodName)!
            .GetCustomAttribute<EnableRateLimitingAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PolicyName.Should().Be(RateLimitConstants.InteractionPolicy);
    }

    [Theory]
    [InlineData(nameof(CommentsController.GetReactions))]
    [InlineData(nameof(CommentsController.GetReactionWho))]
    public void ReactionReads_AllowAnonymous(string methodName)
    {
        typeof(CommentsController)
            .GetMethod(methodName)!
            .GetCustomAttribute<AllowAnonymousAttribute>()
            .Should().NotBeNull();
    }

    [Fact]
    public async Task ReactionWrites_ReturnUnauthorized_WithoutCallingServiceOrPushing()
    {
        var fixture = BuildReactions(null);

        var add = await fixture.Controller.AddOrUpdateReaction(
            Guid.NewGuid(), new AddOrUpdateReactionRequest("Like"));
        var remove = await fixture.Controller.RemoveReaction(Guid.NewGuid());

        add.Should().BeOfType<UnauthorizedObjectResult>();
        remove.Should().BeOfType<UnauthorizedObjectResult>();
        fixture.Reactions.VerifyNoOtherCalls();
        fixture.Proxy.VerifyNoOtherCalls();
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
