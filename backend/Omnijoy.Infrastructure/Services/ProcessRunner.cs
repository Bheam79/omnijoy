using System.Diagnostics;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IProcessRunner"/> implementation backed by
/// <see cref="System.Diagnostics.Process"/>.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public async Task<int> RunAsync(
        string executable,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = executable,
                Arguments              = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            },
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
