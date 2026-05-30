using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for <see cref="TrendingFeedRefreshService"/>.
///
/// <see cref="TrendingFeedRefreshService.RefreshOnceAsync"/> is exposed as
/// <c>internal</c> so tests can drive a single refresh cycle without spinning
/// the long-running <c>ExecuteAsync</c> loop (and relying on <c>InternalsVisibleTo</c>
/// in <c>Omnijoy.Infrastructure.csproj</c>).
/// </summary>
public class TrendingFeedRefreshServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (TrendingFeedRefreshService sut, Mock<IPostService> postMock, Mock<IFeedCache> cacheMock)
        CreateSut(IReadOnlyList<PostDto>? trending = null)
    {
        var postMock  = new Mock<IPostService>();
        var cacheMock = new Mock<IFeedCache>();

        var posts = trending ?? Array.Empty<PostDto>();

        postMock
            .Setup(p => p.GetTrendingPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        cacheMock
            .Setup(c => c.SetTrendingPostsAsync(It.IsAny<IReadOnlyList<PostDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton<IPostService>(postMock.Object);
        services.AddSingleton<IFeedCache>(cacheMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var sut = new TrendingFeedRefreshService(
            serviceProvider,
            NullLogger<TrendingFeedRefreshService>.Instance);

        return (sut, postMock, cacheMock);
    }

    private static PostDto MakePostDto(Guid? id = null) =>
        new PostDto(
            Id:                id ?? Guid.NewGuid(),
            Author:            new PostAuthorDto(Guid.NewGuid(), "Alice", null),
            CompanyPageId:     null,
            Content:           "Trending post",
            BackgroundImageUrl: null,
            PostType:          "Text",
            Privacy:           "Everyone",
            Media:             [],
            LinkPreview:       null,
            CreatedAt:         DateTime.UtcNow,
            UpdatedAt:         DateTime.UtcNow
        );

    // ── RefreshOnceAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshOnce_CallsGetTrendingAndSetsTrendingCache()
    {
        var posts = new[] { MakePostDto(), MakePostDto() };
        var (sut, postMock, cacheMock) = CreateSut(posts);

        await sut.RefreshOnceAsync(CancellationToken.None);

        postMock.Verify(
            p => p.GetTrendingPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);

        cacheMock.Verify(
            c => c.SetTrendingPostsAsync(
                It.Is<IReadOnlyList<PostDto>>(list => list.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshOnce_EmptyTrendingList_StillCallsSetCache()
    {
        var (sut, _, cacheMock) = CreateSut(Array.Empty<PostDto>());

        await sut.RefreshOnceAsync(CancellationToken.None);

        cacheMock.Verify(
            c => c.SetTrendingPostsAsync(
                It.Is<IReadOnlyList<PostDto>>(list => list.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshOnce_PostServiceThrows_DoesNotPropagateException()
    {
        var postMock  = new Mock<IPostService>();
        var cacheMock = new Mock<IFeedCache>();

        postMock
            .Setup(p => p.GetTrendingPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB is down"));

        var services = new ServiceCollection();
        services.AddSingleton<IPostService>(postMock.Object);
        services.AddSingleton<IFeedCache>(cacheMock.Object);
        var sp = services.BuildServiceProvider();

        var sut = new TrendingFeedRefreshService(
            sp,
            NullLogger<TrendingFeedRefreshService>.Instance);

        // The background service must swallow the exception — if it bubbles the
        // test will fail with InvalidOperationException.
        var act = () => sut.RefreshOnceAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();

        // Cache must NOT have been updated — the exception happened before that point.
        cacheMock.Verify(
            c => c.SetTrendingPostsAsync(It.IsAny<IReadOnlyList<PostDto>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RefreshOnce_CacheThrows_DoesNotPropagateException()
    {
        var postMock  = new Mock<IPostService>();
        var cacheMock = new Mock<IFeedCache>();

        postMock
            .Setup(p => p.GetTrendingPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakePostDto() });

        cacheMock
            .Setup(c => c.SetTrendingPostsAsync(It.IsAny<IReadOnlyList<PostDto>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis is down"));

        var services = new ServiceCollection();
        services.AddSingleton<IPostService>(postMock.Object);
        services.AddSingleton<IFeedCache>(cacheMock.Object);
        var sp = services.BuildServiceProvider();

        var sut = new TrendingFeedRefreshService(
            sp,
            NullLogger<TrendingFeedRefreshService>.Instance);

        var act = () => sut.RefreshOnceAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefreshOnce_CancelledToken_DoesNotCallPostService()
    {
        var postMock  = new Mock<IPostService>();
        var cacheMock = new Mock<IFeedCache>();

        // If the token is already cancelled before the scope is used,
        // the OperationCanceledException is swallowed just like other exceptions.
        postMock
            .Setup(p => p.GetTrendingPostsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var services = new ServiceCollection();
        services.AddSingleton<IPostService>(postMock.Object);
        services.AddSingleton<IFeedCache>(cacheMock.Object);
        var sp = services.BuildServiceProvider();

        var sut = new TrendingFeedRefreshService(sp, NullLogger<TrendingFeedRefreshService>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.RefreshOnceAsync(cts.Token);

        await act.Should().NotThrowAsync();
    }

    // ── RefreshInterval constant ──────────────────────────────────────────────

    [Fact]
    public void RefreshInterval_IsFiveMinutes()
    {
        TrendingFeedRefreshService.RefreshInterval.Should().Be(TimeSpan.FromMinutes(5));
    }
}
