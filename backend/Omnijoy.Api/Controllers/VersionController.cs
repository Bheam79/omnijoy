using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VersionController : ControllerBase
{
    /// <summary>
    /// Returns the assembly's InformationalVersion string.
    /// In production Docker images this is the git commit hash injected at
    /// build time via <c>dotnet publish /p:InformationalVersion=&lt;hash&gt;</c>.
    /// Clients poll this endpoint to detect deploys and prompt users to reload.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get() =>
        Ok(new
        {
            version = typeof(VersionController).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?? "unknown",
        });
}
