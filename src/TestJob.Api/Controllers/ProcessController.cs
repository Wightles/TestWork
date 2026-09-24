using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TestJob.Api.Models;

namespace TestJob.Api.Controllers;

[ApiController]
[Route("api/process")]
public sealed class ProcessController(IValidator<ProcessRequest> validator) : ControllerBase
{
    [HttpPost]
    [Produces("application/json")]
    [ProducesResponseType<ProcessResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProcessResponse>(StatusCodes.Status501NotImplemented)]
    public async Task<ActionResult<ProcessResponse>> Process(
        [FromBody] ProcessRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var error = validation.Errors[0];
            return BadRequest(ProcessResponse.Error(error.ErrorCode, error.ErrorMessage));
        }

        // Бизнес-логика будет подключена на следующем этапе.
        return StatusCode(StatusCodes.Status501NotImplemented,
            ProcessResponse.Error("NOT_IMPLEMENTED", "Обработка данных пока не реализована."));
    }
}
