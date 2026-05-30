using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="SlugsController"/>.</summary>
public class SlugsControllerTests
{
    private static SlugsController Build(Mock<ISlugService> slugs)
    {
        var controller = new SlugsController(slugs.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        return controller;
    }

    // ── Check ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_ReturnsOk_WithAvailableTrue_WhenFree()
    {
        var slugs = new Mock<ISlugService>();
        slugs.Setup(s => s.CheckAsync("alice", default))
             .ReturnsAsync(new SlugAvailability(true, null));
        var controller = Build(slugs);

        var result = await controller.Check("alice");

        result.Should().BeOfType<OkObjectResult>();
        var body = (Omnijoy.Core.DTOs.Users.SlugAvailabilityDto)((OkObjectResult)result).Value!;
        body.Available.Should().BeTrue();
        body.Reason.Should().BeNull();
    }

    [Fact]
    public async Task Check_ReturnsOk_WithAvailableFalse_WhenTaken()
    {
        var slugs = new Mock<ISlugService>();
        slugs.Setup(s => s.CheckAsync("alice", default))
             .ReturnsAsync(new SlugAvailability(false, SlugUnavailableReason.Taken));
        var controller = Build(slugs);

        var result = await controller.Check("alice");

        result.Should().BeOfType<OkObjectResult>();
        var body = (Omnijoy.Core.DTOs.Users.SlugAvailabilityDto)((OkObjectResult)result).Value!;
        body.Available.Should().BeFalse();
        body.Reason.Should().Be("taken");
    }

    [Fact]
    public async Task Check_ReturnsOk_WithReasonInvalid_WhenFormatBad()
    {
        var slugs = new Mock<ISlugService>();
        slugs.Setup(s => s.CheckAsync("INVALID!", default))
             .ReturnsAsync(new SlugAvailability(false, SlugUnavailableReason.Invalid));
        var controller = Build(slugs);

        var result = await controller.Check("INVALID!");

        var body = (Omnijoy.Core.DTOs.Users.SlugAvailabilityDto)((OkObjectResult)result).Value!;
        body.Reason.Should().Be("invalid");
    }

    [Fact]
    public async Task Check_ReturnsOk_WithReasonReserved_WhenReservedWord()
    {
        var slugs = new Mock<ISlugService>();
        slugs.Setup(s => s.CheckAsync("admin", default))
             .ReturnsAsync(new SlugAvailability(false, SlugUnavailableReason.Reserved));
        var controller = Build(slugs);

        var result = await controller.Check("admin");

        var body = (Omnijoy.Core.DTOs.Users.SlugAvailabilityDto)((OkObjectResult)result).Value!;
        body.Reason.Should().Be("reserved");
    }

    // ── Resolve ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_ReturnsOk_WithOwnerData_WhenFound()
    {
        var ownerId = Guid.NewGuid();
        var slugs   = new Mock<ISlugService>();
        slugs.Setup(s => s.ResolveAsync("alice", default))
             .ReturnsAsync(new SlugOwner(SlugOwnerType.User, ownerId));
        var controller = Build(slugs);

        var result = await controller.Resolve("alice");

        result.Should().BeOfType<OkObjectResult>();
        var body = (Omnijoy.Core.DTOs.Users.SlugResolutionDto)((OkObjectResult)result).Value!;
        body.Type.Should().Be("user");
        body.Id.Should().Be(ownerId);
    }

    [Fact]
    public async Task Resolve_ReturnsNotFound_WhenSlugNotAssigned()
    {
        var slugs = new Mock<ISlugService>();
        slugs.Setup(s => s.ResolveAsync("unknown", default)).ReturnsAsync((SlugOwner?)null);
        var controller = Build(slugs);

        var result = await controller.Resolve("unknown");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Resolve_ReturnsCompanyType_ForCompanyPageSlug()
    {
        var pageId = Guid.NewGuid();
        var slugs  = new Mock<ISlugService>();
        slugs.Setup(s => s.ResolveAsync("acme", default))
             .ReturnsAsync(new SlugOwner(SlugOwnerType.Company, pageId));
        var controller = Build(slugs);

        var result = await controller.Resolve("acme");

        var body = (Omnijoy.Core.DTOs.Users.SlugResolutionDto)((OkObjectResult)result).Value!;
        body.Type.Should().Be("company");
        body.Id.Should().Be(pageId);
    }
}
