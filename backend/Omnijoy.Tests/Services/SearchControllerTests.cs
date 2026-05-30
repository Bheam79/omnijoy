using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs.Search;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="SearchController"/>.</summary>
public class SearchControllerTests
{
    private static SearchController Build(Mock<ISearchService> search, Guid? userId = null)
    {
        var controller = new SearchController(search.Object);
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static SearchResponse SampleSearchResult() => new(
        Query:     "test",
        Type:      "all",
        Users:     Array.Empty<UserSearchResult>(),
        Posts:     Array.Empty<PostSearchResult>(),
        Events:    Array.Empty<EventSearchResult>(),
        Companies: Array.Empty<CompanySearchResult>(),
        Page:      1,
        PageSize:  20,
        HasMore:   false
    );

    // ── Search ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ReturnsOk_WithResults()
    {
        var search = new Mock<ISearchService>();
        search.Setup(s => s.SearchAsync(null, "hello", "all", 1, 20))
              .ReturnsAsync(SampleSearchResult());
        var controller = Build(search);

        var result = await controller.Search("hello");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Search_ReturnsBadRequest_WhenQueryIsEmpty()
    {
        var search     = new Mock<ISearchService>();
        var controller = Build(search);

        var result = await controller.Search(null);

        result.Should().BeOfType<BadRequestObjectResult>();
        search.Verify(s => s.SearchAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Search_ReturnsBadRequest_WhenQueryIsWhitespace()
    {
        var search     = new Mock<ISearchService>();
        var controller = Build(search);

        var result = await controller.Search("   ");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Search_PassesUserId_WhenAuthenticated()
    {
        var userId = Guid.NewGuid();
        var search = new Mock<ISearchService>();
        search.Setup(s => s.SearchAsync(userId, "query", "users", 1, 20))
              .ReturnsAsync(SampleSearchResult());
        var controller = Build(search, userId);

        var result = await controller.Search("query", type: "users");

        result.Should().BeOfType<OkObjectResult>();
        search.Verify(s => s.SearchAsync(userId, "query", "users", 1, 20), Times.Once);
    }

    [Fact]
    public async Task Search_UsesDefaultParameters()
    {
        var search = new Mock<ISearchService>();
        search.Setup(s => s.SearchAsync(null, "test", "all", 1, 20))
              .ReturnsAsync(SampleSearchResult());
        var controller = Build(search);

        var result = await controller.Search("test");

        search.Verify(s => s.SearchAsync(null, "test", "all", 1, 20), Times.Once);
    }

    // ── Suggest ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Suggest_ReturnsOk_WithResults()
    {
        var search = new Mock<ISearchService>();
        search.Setup(s => s.SearchAsync(null, "alice", "all", 1, 5))
              .ReturnsAsync(SampleSearchResult());
        var controller = Build(search);

        var result = await controller.Suggest("alice");

        result.Should().BeOfType<OkObjectResult>();
        search.Verify(s => s.SearchAsync(null, "alice", "all", 1, 5), Times.Once);
    }

    [Fact]
    public async Task Suggest_ReturnsBadRequest_WhenQueryIsEmpty()
    {
        var search     = new Mock<ISearchService>();
        var controller = Build(search);

        var result = await controller.Suggest(null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Suggest_ReturnsBadRequest_WhenQueryIsWhitespace()
    {
        var search     = new Mock<ISearchService>();
        var controller = Build(search);

        var result = await controller.Suggest("  ");

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
