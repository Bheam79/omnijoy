using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs.Reports;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="ReportsController"/>.</summary>
public class ReportsControllerTests
{
    private static ReportsController Build(Mock<IReportService> reports, Guid? userId)
    {
        var controller = new ReportsController(reports.Object);
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static ReportDto SampleReport(Guid id) => new(
        Id:           id,
        ReporterId:   Guid.NewGuid(),
        ReporterName: "Reporter",
        TargetType:   "Post",
        TargetId:     Guid.NewGuid(),
        Reason:       "Spam",
        Notes:        null,
        Status:       "Pending",
        CreatedAt:    DateTime.UtcNow
    );

    // ── SubmitReport ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitReport_ReturnsCreated_OnSuccess()
    {
        var userId   = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var request  = new SubmitReportRequest("Post", Guid.NewGuid(), "Spam", null);
        var reports  = new Mock<IReportService>();
        reports.Setup(r => r.SubmitReportAsync(userId, request)).ReturnsAsync(SampleReport(reportId));
        var controller = Build(reports, userId);

        var result = await controller.SubmitReport(request);

        result.Should().BeOfType<CreatedResult>();
        var created = (CreatedResult)result;
        created.Location.Should().Contain(reportId.ToString());
    }

    [Fact]
    public async Task SubmitReport_ReturnsUnauthorized_WhenNoUserId()
    {
        var reports    = new Mock<IReportService>();
        var controller = Build(reports, null);

        var result = await controller.SubmitReport(new SubmitReportRequest("Post", Guid.NewGuid(), "Spam", null));

        result.Should().BeOfType<UnauthorizedObjectResult>();
        reports.Verify(r => r.SubmitReportAsync(It.IsAny<Guid>(), It.IsAny<SubmitReportRequest>()), Times.Never);
    }

    [Fact]
    public async Task SubmitReport_ReturnsBadRequest_WhenArgumentException()
    {
        var userId  = Guid.NewGuid();
        var reports = new Mock<IReportService>();
        reports.Setup(r => r.SubmitReportAsync(userId, It.IsAny<SubmitReportRequest>()))
               .ThrowsAsync(new ArgumentException("bad target type"));
        var controller = Build(reports, userId);

        var result = await controller.SubmitReport(new SubmitReportRequest("BadType", Guid.NewGuid(), "Spam", null));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitReport_ReturnsNotFound_WhenTargetMissing()
    {
        var userId  = Guid.NewGuid();
        var reports = new Mock<IReportService>();
        reports.Setup(r => r.SubmitReportAsync(userId, It.IsAny<SubmitReportRequest>()))
               .ThrowsAsync(new KeyNotFoundException("target missing"));
        var controller = Build(reports, userId);

        var result = await controller.SubmitReport(new SubmitReportRequest("Post", Guid.NewGuid(), "Spam", null));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SubmitReport_ReturnsConflict_WhenAlreadyReported()
    {
        var userId  = Guid.NewGuid();
        var reports = new Mock<IReportService>();
        reports.Setup(r => r.SubmitReportAsync(userId, It.IsAny<SubmitReportRequest>()))
               .ThrowsAsync(new InvalidOperationException("already reported"));
        var controller = Build(reports, userId);

        var result = await controller.SubmitReport(new SubmitReportRequest("Post", Guid.NewGuid(), "Spam", null));

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // ── ListReports ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ListReports_ReturnsOk_WithPaginatedResult()
    {
        var reports = new Mock<IReportService>();
        var listResult = new ReportListResult(
            Array.Empty<ReportDto>(), 1, 20, HasMore: false);
        reports.Setup(r => r.ListReportsAsync(null, null, 1, 20)).ReturnsAsync(listResult);
        var controller = Build(reports, Guid.NewGuid());

        var result = await controller.ListReports(null, null, 1, 20);

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().Be(listResult);
    }

    // ── UpdateReportStatus ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateReportStatus_ReturnsOk_OnSuccess()
    {
        var userId   = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var request  = new UpdateReportStatusRequest("Reviewed");
        var reports  = new Mock<IReportService>();
        reports.Setup(r => r.UpdateStatusAsync(userId, reportId, request))
               .ReturnsAsync(SampleReport(reportId));
        var controller = Build(reports, userId);

        var result = await controller.UpdateReportStatus(reportId, request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateReportStatus_ReturnsUnauthorized_WhenNoUserId()
    {
        var reports    = new Mock<IReportService>();
        var controller = Build(reports, null);

        var result = await controller.UpdateReportStatus(Guid.NewGuid(), new UpdateReportStatusRequest("Reviewed"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UpdateReportStatus_ReturnsBadRequest_WhenArgumentException()
    {
        var userId  = Guid.NewGuid();
        var reports = new Mock<IReportService>();
        reports.Setup(r => r.UpdateStatusAsync(userId, It.IsAny<Guid>(), It.IsAny<UpdateReportStatusRequest>()))
               .ThrowsAsync(new ArgumentException("bad status"));
        var controller = Build(reports, userId);

        var result = await controller.UpdateReportStatus(Guid.NewGuid(), new UpdateReportStatusRequest("Bad"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateReportStatus_ReturnsNotFound_WhenReportMissing()
    {
        var userId  = Guid.NewGuid();
        var reports = new Mock<IReportService>();
        reports.Setup(r => r.UpdateStatusAsync(userId, It.IsAny<Guid>(), It.IsAny<UpdateReportStatusRequest>()))
               .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(reports, userId);

        var result = await controller.UpdateReportStatus(Guid.NewGuid(), new UpdateReportStatusRequest("Reviewed"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
