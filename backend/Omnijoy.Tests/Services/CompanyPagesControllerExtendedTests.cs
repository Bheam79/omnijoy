using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs.CompanyPages;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Extended tests for <see cref="CompanyPagesController"/> form-upload methods.
/// </summary>
public class CompanyPagesControllerExtendedTests
{
    private static CompanyPagesController Build(
        Mock<ICompanyPageService> pages,
        Mock<ISlugService>        slugs,
        Guid? userId)
    {
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        var http = new DefaultHttpContext();
        // Provide an empty form so Request.Form.Files doesn't throw on bracket-fallback.
        http.Request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
            new FormFileCollection());
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static CompanyPageDto SamplePage(Guid id, Guid creatorId) => new(
        Id:            id,
        Name:          "ACME Corp",
        Description:   "Test",
        LogoUrl:       null,
        CoverUrl:      null,
        CreatedBy:     new CompanyPageCreatorDto(creatorId, "Alice", null),
        FollowerCount: 0,
        IsFollowing:   false,
        MyRole:        "Owner",
        CreatedAt:     DateTime.UtcNow
    );

    // ── CreatePage ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePage_ReturnsCreated_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.CreatePageAsync(userId, It.IsAny<CreateCompanyPageRequest>(), null, null))
             .ReturnsAsync(SamplePage(pageId, userId));
        var controller = Build(pages, slugs, userId);

        var result = await controller.CreatePage(new CreatePageFormInput
        {
            Name        = "ACME Corp",
            Description = "A test company",
        });

        result.Should().BeOfType<CreatedResult>();
        ((CreatedResult)result).Location.Should().Contain(pageId.ToString());
    }

    [Fact]
    public async Task CreatePage_ReturnsUnauthorized_WhenNoUserId()
    {
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        var controller = Build(pages, slugs, null);

        var result = await controller.CreatePage(new CreatePageFormInput { Name = "ACME" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task CreatePage_ReturnsBadRequest_WhenArgumentException()
    {
        var userId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.CreatePageAsync(userId, It.IsAny<CreateCompanyPageRequest>(), null, null))
             .ThrowsAsync(new ArgumentException("name required"));
        var controller = Build(pages, slugs, userId);

        var result = await controller.CreatePage(new CreatePageFormInput { Name = "" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UpdatePage ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePage_ReturnsOk_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.UpdatePageAsync(pageId, userId, It.IsAny<UpdateCompanyPageRequest>(), null, null))
             .ReturnsAsync(SamplePage(pageId, userId));
        var controller = Build(pages, slugs, userId);

        var result = await controller.UpdatePage(pageId, new UpdatePageFormInput { Name = "New Name" });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdatePage_ReturnsUnauthorized_WhenNoUserId()
    {
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        var controller = Build(pages, slugs, null);

        var result = await controller.UpdatePage(Guid.NewGuid(), new UpdatePageFormInput());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UpdatePage_ReturnsNotFound_WhenPageMissing()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.UpdatePageAsync(pageId, userId, It.IsAny<UpdateCompanyPageRequest>(), null, null))
             .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(pages, slugs, userId);

        var result = await controller.UpdatePage(pageId, new UpdatePageFormInput());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdatePage_Returns403_WhenNotAdmin()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.UpdatePageAsync(pageId, userId, It.IsAny<UpdateCompanyPageRequest>(), null, null))
             .ThrowsAsync(new UnauthorizedAccessException("not admin"));
        var controller = Build(pages, slugs, userId);

        var result = await controller.UpdatePage(pageId, new UpdatePageFormInput());

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }
}
