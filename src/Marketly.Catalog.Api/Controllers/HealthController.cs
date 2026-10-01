using Microsoft.AspNetCore.Mvc;

namespace Marketly.Catalog.Api.Controllers;

/// <summary>Simple liveness endpoint for the Catalog API container.</summary>
[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Returns a healthy response when ASP.NET Core can serve requests.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "healthy", service = "catalog" });
}
