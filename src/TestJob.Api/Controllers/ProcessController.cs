using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using TestJob.Api.Models;
using TestJob.Api.Services;

namespace TestJob.Api.Controllers;

[ApiController]
[Route("api/process")]
public sealed class ProcessController(
    IValidator<ProcessRequest> validator,
    ProcessingService service,
    ILogger<ProcessController> logger) : ControllerBase
{
    [HttpPost]
    [Produces("application/json")]
    [ProducesResponseType<ProcessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProcessResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProcessResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProcessResponse>> Process(
        [FromBody] ProcessRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var error = validation.Errors[0];
            return BadRequest(ProcessResponse.Error(error.ErrorCode, error.ErrorMessage));
        }

        try
        {
            return Ok(await service.ProcessAsync(request, cancellationToken));
        }
        catch (ProcessingException exception)
        {
            return BadRequest(ProcessResponse.Error(exception.Code, exception.Message));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Ошибка обработки запроса");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ProcessResponse.Error("INTERNAL_ERROR", exception.Message));
        }
    }
}
