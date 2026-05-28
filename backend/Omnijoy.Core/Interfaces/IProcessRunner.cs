namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Abstraction over an OS process invocation.
/// Allows external-process calls (e.g. FFmpeg) to be mocked in unit tests.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Starts <paramref name="executable"/> with the given <paramref name="arguments"/>,
    /// waits for it to exit, and returns its exit code.
    /// </summary>
    Task<int> RunAsync(string executable, string arguments, CancellationToken cancellationToken = default);
}
