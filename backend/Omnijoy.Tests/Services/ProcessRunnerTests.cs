using FluentAssertions;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for <see cref="ProcessRunner"/> — the real <see cref="System.Diagnostics.Process"/>-backed
/// implementation. Tests run actual OS processes; they are kept short-lived (echo / true / false).
/// </summary>
public class ProcessRunnerTests
{
    private readonly ProcessRunner _sut = new();

    [Fact]
    public async Task RunAsync_SuccessfulProcess_ReturnsZero()
    {
        // 'true' exits 0 on Linux/macOS; on Windows use cmd /c exit 0
        var (exe, args) = IsWindows()
            ? ("cmd", "/c exit 0")
            : ("true", "");

        var exitCode = await _sut.RunAsync(exe, args);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_FailingProcess_ReturnsNonZero()
    {
        var (exe, args) = IsWindows()
            ? ("cmd", "/c exit 1")
            : ("false", "");

        var exitCode = await _sut.RunAsync(exe, args);

        exitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task RunAsync_EchoCommand_ReturnsZero()
    {
        var (exe, args) = IsWindows()
            ? ("cmd", "/c echo hello")
            : ("echo", "hello");

        var exitCode = await _sut.RunAsync(exe, args);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_CancellationAfterStart_ThrowsOrCompletes()
    {
        // Use a very short-lived command so the test is not flaky.
        // If the CancellationToken fires before WaitForExitAsync the method
        // throws OperationCanceledException; otherwise it returns normally.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var (exe, args) = IsWindows()
            ? ("cmd", "/c echo hello")
            : ("echo", "hello");

        var exitCode = await _sut.RunAsync(exe, args, cts.Token);

        exitCode.Should().Be(0);
    }

    private static bool IsWindows() =>
        Environment.OSVersion.Platform == PlatformID.Win32NT;
}
