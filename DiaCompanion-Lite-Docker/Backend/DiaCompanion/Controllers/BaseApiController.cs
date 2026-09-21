using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Controllers;

/// <summary>
/// Base HTTP controller. Business rules and data access belong to services
/// and repositories, not to the controller layer.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
}
