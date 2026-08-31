using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Api.RateLimiting;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

public class SavedPostsControllerTests
{
    private static SavedPostsController Build(Mock<ISavedPostService> savedPosts, Guid? userId)
    {
        var controller = new SavedPostsController(savedPosts.Object);
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static PostDto SamplePost(Guid id, Guid authorId) => new(
        Id: id,
        Author: new PostAuthorDto(authorId, "Alice", null),
        CompanyPageId: null,
        Content: "Saved post",
        BackgroundImageUrl: null,
        PostType: "Text",
        Privacy: "Friends",
        Media: [],
        LinkPreview: null,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: DateTime.UtcNow,
        IsSavedByMe: true);

    [Fact]
    public void Controller_RequiresAuthorization()
    {
        typeof(SavedPostsController).GetCustomAttribute<AuthorizeAttribute>()
            .Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(SavedPostsController.SavePost))]
    [InlineData(nameof(SavedPostsController.UnsavePost))]
    public void WriteActions_UseInteractionRateLimit(string actionName)
    {
        var attribute = typeof(SavedPostsController)
            .GetMethod(actionName)!
            .GetCustomAttribute<EnableRateLimitingAttribute>();

        attribute.Should().NotBeNull();
        attribute!.PolicyName.Should().Be(RateLimitConstants.InteractionPolicy);
    }

    [Fact]
    public async Task SavePost_ReturnsChangedContract_AndScopesToAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var service = new Mock<ISavedPostService>();
        service.Setup(s => s.SaveAsync(userId, postId, null)).ReturnsAsync(true);
        var controller = Build(service, userId);

        var result = await controller.SavePost(postId);

        var body = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<SavedPostStateDto>().Subject;
        body.Should().Be(new SavedPostStateDto(IsSaved: true, Changed: true));
        service.Verify(s => s.SaveAsync(userId, postId, null), Times.Once);
    }

    [Fact]
    public async Task SavePost_ReturnsUnchangedContract_WhenAlreadySaved()
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var service = new Mock<ISavedPostService>();
        service.Setup(s => s.SaveAsync(userId, postId, null)).ReturnsAsync(false);
        var controller = Build(service, userId);

        var result = await controller.SavePost(postId);

        var body = ((OkObjectResult)result).Value.Should().BeOfType<SavedPostStateDto>().Subject;
        body.Should().Be(new SavedPostStateDto(IsSaved: true, Changed: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavePost_MapsMissingAndInvisiblePostToSameNotFoundResponse(bool invisible)
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var service = new Mock<ISavedPostService>();
        Exception error = invisible
            ? new UnauthorizedAccessException("private post details")
            : new KeyNotFoundException("missing post details");
        service.Setup(s => s.SaveAsync(userId, postId, null)).ThrowsAsync(error);
        var controller = Build(service, userId);

        var result = await controller.SavePost(postId);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value.Should().BeEquivalentTo(new { error = "Post not found." });
    }

    [Fact]
    public async Task SavePost_ReturnsUnauthorized_WithoutAuthenticatedUser()
    {
        var service = new Mock<ISavedPostService>();
        var controller = Build(service, null);

        var result = await controller.SavePost(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedObjectResult>();
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnsavePost_ReturnsIdempotentContract(bool changed)
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var service = new Mock<ISavedPostService>();
        service.Setup(s => s.UnsaveAsync(userId, postId)).ReturnsAsync(changed);
        var controller = Build(service, userId);

        var result = await controller.UnsavePost(postId);

        var body = ((OkObjectResult)result).Value.Should().BeOfType<SavedPostStateDto>().Subject;
        body.Should().Be(new SavedPostStateDto(IsSaved: false, Changed: changed));
        service.Verify(s => s.UnsaveAsync(userId, postId), Times.Once);
    }

    [Fact]
    public async Task UnsavePost_ReturnsUnauthorized_WithoutAuthenticatedUser()
    {
        var service = new Mock<ISavedPostService>();
        var controller = Build(service, null);

        var result = await controller.UnsavePost(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedObjectResult>();
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnsavePost_MapsMissingAndInvisiblePostToSameNotFoundResponse(bool invisible)
    {
        var userId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var service = new Mock<ISavedPostService>();
        Exception error = invisible
            ? new UnauthorizedAccessException("private post details")
            : new KeyNotFoundException("missing post details");
        service.Setup(s => s.UnsaveAsync(userId, postId)).ThrowsAsync(error);
        var controller = Build(service, userId);

        var result = await controller.UnsavePost(postId);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value.Should().BeEquivalentTo(new { error = "Post not found." });
    }

    [Fact]
    public async Task GetSavedPosts_ReturnsNormalPostDtos_WithServicePagination()
    {
        var userId = Guid.NewGuid();
        var post = SamplePost(Guid.NewGuid(), Guid.NewGuid());
        var saved = new SavedPostDto(Guid.NewGuid(), post, null, DateTime.UtcNow);
        var service = new Mock<ISavedPostService>();
        service.Setup(s => s.GetSavedAsync(userId, 3, 10)).ReturnsAsync(
            new PagedResult<SavedPostDto>([saved], 3, 10, true));
        var controller = Build(service, userId);

        var result = await controller.GetSavedPosts(page: 3, pageSize: 10);

        var body = ((OkObjectResult)result).Value.Should().BeOfType<PagedResult<PostDto>>().Subject;
        body.Should().BeEquivalentTo(new PagedResult<PostDto>([post], 3, 10, true));
        service.Verify(s => s.GetSavedAsync(userId, 3, 10), Times.Once);
    }

    [Theory]
    [InlineData(0, 20, 1, 20)]
    [InlineData(-3, 0, 1, 20)]
    [InlineData(2, 51, 2, 20)]
    public async Task GetSavedPosts_ClampsPagination(
        int requestedPage,
        int requestedPageSize,
        int expectedPage,
        int expectedPageSize)
    {
        var userId = Guid.NewGuid();
        var service = new Mock<ISavedPostService>();
        service.Setup(s => s.GetSavedAsync(userId, expectedPage, expectedPageSize)).ReturnsAsync(
            new PagedResult<SavedPostDto>([], expectedPage, expectedPageSize, false));
        var controller = Build(service, userId);

        var result = await controller.GetSavedPosts(requestedPage, requestedPageSize);

        var body = ((OkObjectResult)result).Value.Should().BeOfType<PagedResult<PostDto>>().Subject;
        body.Page.Should().Be(expectedPage);
        body.PageSize.Should().Be(expectedPageSize);
        service.Verify(s => s.GetSavedAsync(userId, expectedPage, expectedPageSize), Times.Once);
    }

    [Fact]
    public async Task GetSavedPosts_ReturnsUnauthorized_WithoutAuthenticatedUser()
    {
        var service = new Mock<ISavedPostService>();
        var controller = Build(service, null);

        var result = await controller.GetSavedPosts();

        result.Should().BeOfType<UnauthorizedObjectResult>();
        service.VerifyNoOtherCalls();
    }
}
