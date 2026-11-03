using CosmicStoreAPI.Error;
using Microsoft.AspNetCore.Mvc;

namespace TrendHealthAPI.Controllers
{
    /// <summary>
    /// Shared API base: every derived controller is served at <c>api/{ControllerName}</c> unless it
    /// declares its own <c>[Route]</c>, which replaces this one. All responses are JSON, and errors use
    /// the <see cref="ApiErrorResponse"/> shape.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class BaseController : ControllerBase
    {
        /// <summary>404 with an <see cref="ApiErrorResponse"/> body.</summary>
        protected NotFoundObjectResult NotFoundError(string message) =>
            NotFound(new ApiErrorResponse(StatusCodes.Status404NotFound, message));

        /// <summary>409 with an <see cref="ApiErrorResponse"/> body.</summary>
        protected ConflictObjectResult ConflictError(string message) =>
            Conflict(new ApiErrorResponse(StatusCodes.Status409Conflict, message));
    }
}
