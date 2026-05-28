using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Reports;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

public class ReportServiceTests : IDisposable
{
    private readonly OmnijoyDbContext _db;
    private readonly ReportService _sut;

    public ReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new OmnijoyDbContext(options);
        _sut = new ReportService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string displayName = "Alice")
    {
        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = $"{Guid.NewGuid()}@test.com",
            DisplayName = displayName,
            Gender      = Gender.NotDisclosed,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Post> CreatePostAsync(User author)
    {
        var post = new Post
        {
            Id           = Guid.NewGuid(),
            AuthorUserId = author.Id,
            Author       = author,
            Content      = "Test post",
            PostType     = PostType.Text,
            Privacy      = PrivacyLevel.Everyone,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return post;
    }

    private async Task<Comment> CreateCommentAsync(User author, Post post)
    {
        var comment = new Comment
        {
            Id        = Guid.NewGuid(),
            PostId    = post.Id,
            AuthorId  = author.Id,
            Content   = "Test comment",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();
        return comment;
    }

    // ── SubmitReportAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitReport_PostTarget_ReturnsDto()
    {
        var reporter = await CreateUserAsync("Reporter");
        var author   = await CreateUserAsync("Author");
        var post     = await CreatePostAsync(author);

        var request = new SubmitReportRequest("Post", post.Id, "Spam", null);
        var result  = await _sut.SubmitReportAsync(reporter.Id, request);

        result.ReporterId.Should().Be(reporter.Id);
        result.ReporterName.Should().Be("Reporter");
        result.TargetType.Should().Be("Post");
        result.TargetId.Should().Be(post.Id);
        result.Reason.Should().Be("Spam");
        result.Notes.Should().BeNull();
        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task SubmitReport_UserTarget_ReturnsDto()
    {
        var reporter = await CreateUserAsync("Reporter");
        var target   = await CreateUserAsync("TargetUser");

        var request = new SubmitReportRequest("User", target.Id, "Harassment", "some notes");
        var result  = await _sut.SubmitReportAsync(reporter.Id, request);

        result.TargetType.Should().Be("User");
        result.TargetId.Should().Be(target.Id);
        result.Reason.Should().Be("Harassment");
        result.Notes.Should().Be("some notes");
    }

    [Fact]
    public async Task SubmitReport_CommentTarget_ReturnsDto()
    {
        var reporter = await CreateUserAsync("Reporter");
        var author   = await CreateUserAsync("Author");
        var post     = await CreatePostAsync(author);
        var comment  = await CreateCommentAsync(author, post);

        var request = new SubmitReportRequest("Comment", comment.Id, "Violence", null);
        var result  = await _sut.SubmitReportAsync(reporter.Id, request);

        result.TargetType.Should().Be("Comment");
        result.TargetId.Should().Be(comment.Id);
        result.Reason.Should().Be("Violence");
    }

    [Fact]
    public async Task SubmitReport_InvalidTargetType_ThrowsArgumentException()
    {
        var reporter = await CreateUserAsync();
        var request  = new SubmitReportRequest("BadType", Guid.NewGuid(), "Spam", null);

        await _sut.Invoking(s => s.SubmitReportAsync(reporter.Id, request))
                  .Should().ThrowAsync<ArgumentException>()
                  .WithMessage("*TargetType*");
    }

    [Fact]
    public async Task SubmitReport_InvalidReason_ThrowsArgumentException()
    {
        var reporter = await CreateUserAsync();
        var author   = await CreateUserAsync("Author");
        var post     = await CreatePostAsync(author);

        var request  = new SubmitReportRequest("Post", post.Id, "BadReason", null);

        await _sut.Invoking(s => s.SubmitReportAsync(reporter.Id, request))
                  .Should().ThrowAsync<ArgumentException>()
                  .WithMessage("*Reason*");
    }

    [Fact]
    public async Task SubmitReport_NonExistentPost_ThrowsKeyNotFoundException()
    {
        var reporter = await CreateUserAsync();
        var request  = new SubmitReportRequest("Post", Guid.NewGuid(), "Spam", null);

        await _sut.Invoking(s => s.SubmitReportAsync(reporter.Id, request))
                  .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SubmitReport_NonExistentUser_ThrowsKeyNotFoundException()
    {
        var reporter = await CreateUserAsync();
        var request  = new SubmitReportRequest("User", Guid.NewGuid(), "Spam", null);

        await _sut.Invoking(s => s.SubmitReportAsync(reporter.Id, request))
                  .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SubmitReport_DeletedPost_ThrowsKeyNotFoundException()
    {
        var reporter = await CreateUserAsync();
        var author   = await CreateUserAsync("Author");
        var post     = await CreatePostAsync(author);

        // Soft-delete the post
        post.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var request = new SubmitReportRequest("Post", post.Id, "Spam", null);

        await _sut.Invoking(s => s.SubmitReportAsync(reporter.Id, request))
                  .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SubmitReport_DuplicateReport_ThrowsInvalidOperationException()
    {
        var reporter = await CreateUserAsync("Reporter");
        var author   = await CreateUserAsync("Author");
        var post     = await CreatePostAsync(author);

        var request = new SubmitReportRequest("Post", post.Id, "Spam", null);
        await _sut.SubmitReportAsync(reporter.Id, request);

        await _sut.Invoking(s => s.SubmitReportAsync(reporter.Id, request))
                  .Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("*already reported*");
    }

    [Fact]
    public async Task SubmitReport_TwoReportersSameTarget_BothSucceed()
    {
        var reporter1 = await CreateUserAsync("Reporter1");
        var reporter2 = await CreateUserAsync("Reporter2");
        var author    = await CreateUserAsync("Author");
        var post      = await CreatePostAsync(author);

        var request = new SubmitReportRequest("Post", post.Id, "Spam", null);
        var r1 = await _sut.SubmitReportAsync(reporter1.Id, request);
        var r2 = await _sut.SubmitReportAsync(reporter2.Id, request);

        r1.Id.Should().NotBe(r2.Id);
    }

    // ── ListReportsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListReports_NoFilter_ReturnsAllReports()
    {
        var reporter = await CreateUserAsync("Reporter");
        var author   = await CreateUserAsync("Author");
        var post     = await CreatePostAsync(author);

        var req = new SubmitReportRequest("Post", post.Id, "Spam", null);
        await _sut.SubmitReportAsync(reporter.Id, req);

        var result = await _sut.ListReportsAsync(null, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task ListReports_FilterByStatus_ReturnsOnlyMatchingReports()
    {
        var reporter = await CreateUserAsync("Reporter");
        var author   = await CreateUserAsync("Author");
        var post     = await CreatePostAsync(author);
        var target   = await CreateUserAsync("Target");

        // Create two reports; update one to Reviewed
        var req1 = new SubmitReportRequest("Post", post.Id, "Spam", null);
        var report1 = await _sut.SubmitReportAsync(reporter.Id, req1);
        var req2 = new SubmitReportRequest("User", target.Id, "Harassment", null);
        await _sut.SubmitReportAsync(reporter.Id, req2);

        await _sut.UpdateStatusAsync(report1.Id, new UpdateReportStatusRequest("Reviewed"));

        var pending  = await _sut.ListReportsAsync("Pending",  null, 1, 20);
        var reviewed = await _sut.ListReportsAsync("Reviewed", null, 1, 20);

        pending.Items.Should().HaveCount(1);
        reviewed.Items.Should().HaveCount(1);
        reviewed.Items[0].Status.Should().Be("Reviewed");
    }

    [Fact]
    public async Task ListReports_FilterByTargetType_ReturnsOnlyMatchingReports()
    {
        var reporter = await CreateUserAsync("Reporter");
        var author   = await CreateUserAsync("Author");
        var post     = await CreatePostAsync(author);
        var userTarget = await CreateUserAsync("Target");

        await _sut.SubmitReportAsync(reporter.Id, new SubmitReportRequest("Post", post.Id, "Spam", null));
        await _sut.SubmitReportAsync(reporter.Id, new SubmitReportRequest("User", userTarget.Id, "Spam", null));

        var postReports = await _sut.ListReportsAsync(null, "Post", 1, 20);
        var userReports = await _sut.ListReportsAsync(null, "User", 1, 20);

        postReports.Items.Should().HaveCount(1);
        postReports.Items[0].TargetType.Should().Be("Post");
        userReports.Items.Should().HaveCount(1);
        userReports.Items[0].TargetType.Should().Be("User");
    }

    [Fact]
    public async Task ListReports_Pagination_HasMoreIsCorrect()
    {
        var reporter = await CreateUserAsync("Reporter");

        // Create 3 different user targets (each reporter can report each user once)
        for (int i = 0; i < 3; i++)
        {
            var target = await CreateUserAsync($"Target{i}");
            await _sut.SubmitReportAsync(reporter.Id,
                new SubmitReportRequest("User", target.Id, "Spam", null));
        }

        var page1 = await _sut.ListReportsAsync(null, null, 1, 2);
        var page2 = await _sut.ListReportsAsync(null, null, 2, 2);

        page1.Items.Should().HaveCount(2);
        page1.HasMore.Should().BeTrue();

        page2.Items.Should().HaveCount(1);
        page2.HasMore.Should().BeFalse();
    }

    // ── UpdateStatusAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_ValidStatus_UpdatesReport()
    {
        var reporter = await CreateUserAsync("Reporter");
        var author   = await CreateUserAsync("Author");
        var post     = await CreatePostAsync(author);

        var submitted = await _sut.SubmitReportAsync(reporter.Id,
            new SubmitReportRequest("Post", post.Id, "Spam", null));

        var updated = await _sut.UpdateStatusAsync(submitted.Id,
            new UpdateReportStatusRequest("Reviewed"));

        updated.Status.Should().Be("Reviewed");
        updated.Id.Should().Be(submitted.Id);
    }

    [Fact]
    public async Task UpdateStatus_Dismissed_UpdatesReport()
    {
        var reporter = await CreateUserAsync("Reporter");
        var author   = await CreateUserAsync("Author");
        var post     = await CreatePostAsync(author);

        var submitted = await _sut.SubmitReportAsync(reporter.Id,
            new SubmitReportRequest("Post", post.Id, "Spam", null));

        var updated = await _sut.UpdateStatusAsync(submitted.Id,
            new UpdateReportStatusRequest("Dismissed"));

        updated.Status.Should().Be("Dismissed");
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_ThrowsArgumentException()
    {
        var reporter = await CreateUserAsync("Reporter");
        var author   = await CreateUserAsync("Author");
        var post     = await CreatePostAsync(author);

        var submitted = await _sut.SubmitReportAsync(reporter.Id,
            new SubmitReportRequest("Post", post.Id, "Spam", null));

        await _sut.Invoking(s => s.UpdateStatusAsync(submitted.Id,
                new UpdateReportStatusRequest("BadStatus")))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Status*");
    }

    [Fact]
    public async Task UpdateStatus_NonExistentReport_ThrowsKeyNotFoundException()
    {
        await _sut.Invoking(s => s.UpdateStatusAsync(Guid.NewGuid(),
                new UpdateReportStatusRequest("Reviewed")))
            .Should().ThrowAsync<KeyNotFoundException>();
    }
}
