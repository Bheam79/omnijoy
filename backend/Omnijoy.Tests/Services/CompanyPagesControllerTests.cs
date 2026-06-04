using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.CompanyPages;
using Omnijoy.Core.DTOs.Users;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="CompanyPagesController"/>.</summary>
public class CompanyPagesControllerTests
{
    private static CompanyPagesController Build(
        Mock<ICompanyPageService> pages,
        Mock<ISlugService>        slugs,
        Guid? userId)
    {
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        var http = new DefaultHttpContext();
        // Provide a form context so Request.Form.Files doesn't throw
        http.Request.ContentType = "multipart/form-data; boundary=test";
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
        Description:   "Test company",
        LogoUrl:       null,
        CoverUrl:      null,
        CreatedBy:     new CompanyPageCreatorDto(creatorId, "Alice", null),
        FollowerCount: 0,
        IsFollowing:   false,
        MyRole:        "Owner",
        CreatedAt:     DateTime.UtcNow
    );

    // ── GetPages ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPages_ReturnsOk_WithPaginatedList()
    {
        var userId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.GetPagesAsync(userId, false, 1, 20))
             .ReturnsAsync(new PagedResult<CompanyPageDto>(Array.Empty<CompanyPageDto>(), 1, 20, false));
        var controller = Build(pages, slugs, userId);

        var result = await controller.GetPages();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPages_ReturnsUnauthorized_WhenNoUserId()
    {
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        var controller = Build(pages, slugs, null);

        var result = await controller.GetPages();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── GetMyPages ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyPages_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.GetMyAdminPagesAsync(userId)).ReturnsAsync(Array.Empty<CompanyPageDto>());
        var controller = Build(pages, slugs, userId);

        var result = await controller.GetMyPages();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetPage ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPage_ReturnsOk_WithDto()
    {
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.GetPageAsync(pageId, null)).ReturnsAsync(SamplePage(pageId, Guid.NewGuid()));
        var controller = Build(pages, slugs, null);

        var result = await controller.GetPage(pageId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPage_ReturnsNotFound_WhenMissing()
    {
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.GetPageAsync(pageId, It.IsAny<Guid?>())).ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(pages, slugs, null);

        var result = await controller.GetPage(pageId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GetAdmins ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAdmins_ReturnsOk_WithList()
    {
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.GetAdminsAsync(pageId, null)).ReturnsAsync(new AdminsResult(Array.Empty<CompanyPageAdminDto>()));
        var controller = Build(pages, slugs, null);

        var result = await controller.GetAdmins(pageId);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── AddAdmin ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAdmin_ReturnsOk_OnSuccess()
    {
        var userId   = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var pageId   = Guid.NewGuid();
        var pages    = new Mock<ICompanyPageService>();
        var slugs    = new Mock<ISlugService>();
        var request  = new AddAdminRequest(targetId, "Editor");
        pages.Setup(p => p.AddOrUpdateAdminAsync(pageId, userId, request))
             .ReturnsAsync(new AdminsResult(new[] { new CompanyPageAdminDto(targetId, "Bob", null, "Editor") }));
        var controller = Build(pages, slugs, userId);

        var result = await controller.AddAdmin(pageId, request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddAdmin_ReturnsUnauthorized_WhenNoUserId()
    {
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        var controller = Build(pages, slugs, null);

        var result = await controller.AddAdmin(Guid.NewGuid(), new AddAdminRequest(Guid.NewGuid(), "Editor"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task AddAdmin_Returns403_WhenNotOwner()
    {
        var userId  = Guid.NewGuid();
        var pageId  = Guid.NewGuid();
        var pages   = new Mock<ICompanyPageService>();
        var slugs   = new Mock<ISlugService>();
        pages.Setup(p => p.AddOrUpdateAdminAsync(pageId, userId, It.IsAny<AddAdminRequest>()))
             .ThrowsAsync(new UnauthorizedAccessException("not owner"));
        var controller = Build(pages, slugs, userId);

        var result = await controller.AddAdmin(pageId, new AddAdminRequest(Guid.NewGuid(), "Editor"));

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    // ── RemoveAdmin ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAdmin_ReturnsOk_OnSuccess()
    {
        var userId   = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var pageId   = Guid.NewGuid();
        var pages    = new Mock<ICompanyPageService>();
        var slugs    = new Mock<ISlugService>();
        pages.Setup(p => p.RemoveAdminAsync(pageId, userId, targetId))
             .ReturnsAsync(new AdminsResult(Array.Empty<CompanyPageAdminDto>()));
        var controller = Build(pages, slugs, userId);

        var result = await controller.RemoveAdmin(pageId, targetId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RemoveAdmin_ReturnsUnauthorized_WhenNoUserId()
    {
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        var controller = Build(pages, slugs, null);

        var result = await controller.RemoveAdmin(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── Follow / Unfollow ────────────────────────────────────────────────────

    [Fact]
    public async Task Follow_ReturnsOk_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.FollowAsync(pageId, userId)).ReturnsAsync(SamplePage(pageId, Guid.NewGuid()));
        var controller = Build(pages, slugs, userId);

        var result = await controller.Follow(pageId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Follow_ReturnsUnauthorized_WhenNoUserId()
    {
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        var controller = Build(pages, slugs, null);

        var result = await controller.Follow(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Unfollow_ReturnsOk_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.UnfollowAsync(pageId, userId)).ReturnsAsync(SamplePage(pageId, Guid.NewGuid()));
        var controller = Build(pages, slugs, userId);

        var result = await controller.Unfollow(pageId);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── SetSlug ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetSlug_ReturnsOk_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        slugs.Setup(s => s.SetCompanyPageSlugAsync(pageId, userId, "acme", default)).ReturnsAsync("acme");
        var controller = Build(pages, slugs, userId);

        var result = await controller.SetSlug(pageId, new SetSlugRequest("acme"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SetSlug_ReturnsUnauthorized_WhenNoUserId()
    {
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        var controller = Build(pages, slugs, null);

        var result = await controller.SetSlug(Guid.NewGuid(), new SetSlugRequest("acme"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task SetSlug_Returns403_WhenNotAuthorized()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        slugs.Setup(s => s.SetCompanyPageSlugAsync(pageId, userId, It.IsAny<string?>(), default))
             .ThrowsAsync(new UnauthorizedAccessException("not admin"));
        var controller = Build(pages, slugs, userId);

        var result = await controller.SetSlug(pageId, new SetSlugRequest("acme"));

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }
}
