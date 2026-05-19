using GnnSimulation.Api.Dtos;
using GnnSimulation.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GnnSimulation.Api.Controllers;

[ApiController]
[Route("api/simulation")]
public class SimulationController : ControllerBase
{
    private readonly SimulationService _service;
    private readonly ParallelSimulationService _parallel;

    public SimulationController(SimulationService service, ParallelSimulationService parallel)
    {
        _service = service;
        _parallel = parallel;
    }

    [HttpPost("run")]
    public async Task<ActionResult<SimulationResultDto>> Run(
        [FromBody] SimulationRequestDto request,
        CancellationToken ct)
    {
        try
        {
            return await _service.RunAsync(request, ct);
        }
        catch (SimulationNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (SimulationBadRequestException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost("run_parallel")]
    public async Task<ActionResult<ParallelSimulationResultDto>> RunParallel(
        [FromBody] ParallelSimulationRequestDto request,
        CancellationToken ct)
    {
        try
        {
            return await _parallel.RunAsync(request, ct);
        }
        catch (SimulationNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (SimulationBadRequestException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}
