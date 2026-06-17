using GnnSimulation.Api.Dtos;
using GnnSimulation.Api.Services;
using GnnSimulation.Core.Atmosphere;
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

    [HttpGet("formulas")]
    public ActionResult<SimulationFormulaInfoDto> Formulas()
    {
        var pollutantNames = new Dictionary<string, string>
        {
            ["PM2.5"] = "细颗粒物",
            ["PM10"] = "可吸入颗粒物",
            ["TSP"] = "总悬浮颗粒物",
            ["VOCs"] = "挥发性有机物",
            ["NOx"] = "氮氧化物",
            ["SO2"] = "二氧化硫",
            ["CO"] = "一氧化碳",
            ["O3"] = "臭氧",
        };

        var pollutants = PollutantProperties.GravitationalSettlingVelocity.Keys
            .OrderBy(x => x)
            .Select(type =>
            {
                var resistance = PollutantProperties.GetDryResistance(type);
                var scavenging = PollutantProperties.GetWetScavenging(type);
                return new PollutantFormulaParameterDto
                {
                    Type = type,
                    Name = pollutantNames.GetValueOrDefault(type, type),
                    GravitationalSettlingVelocity = PollutantProperties.GetGravitationalSettling(type),
                    DryResistanceRb = resistance.Rb,
                    DryResistanceRc = resistance.Rc,
                    WetScavengingA = scavenging.A,
                    WetScavengingB = scavenging.B,
                    ChemicalRate = PollutantProperties.GetChemicalRate(type),
                    ChemicalEnhanced = PollutantProperties.ChemicalEnhancedPollutants.Contains(type),
                    TemperatureCorrected = PollutantProperties.TempCorrectedPollutants.Contains(type),
                };
            })
            .ToList();

        return new SimulationFormulaInfoDto
        {
            GaussianPlumeFormula = "C = Q / (2πuσyσz) · exp(-y²/(2σy²)) · [exp(-(z-H)²/(2σz²)) + exp(-(z+H)²/(2σz²))]",
            DecayFormula = "C_final = C_plume · exp(-v_d·x/u) · exp(-Λ·x/u) · exp(-k·x/u)，其中 Λ = a·P^b + background_scavenging",
            WindAggregationFormula = "C_aggregate = Σ(C_direction_i × normalized_weight_i)。失败风向剔除后，只对成功风向的原始权重重新归一化。",
            Pollutants = pollutants,
            SourceTypes = new List<SourceFormulaInfoDto>
            {
                new()
                {
                    Type = "point",
                    Name = "点源",
                    Formula = "Gaussian plume with Briggs plume rise",
                    Notes = "使用烟囱高度、烟气温度、出口速度和直径计算有效源高，再进入高斯烟羽公式。",
                },
                new()
                {
                    Type = "area",
                    Name = "面源",
                    Formula = "Area source integration over equivalent sub-sources",
                    Notes = "按面源长度、宽度、高度和 sigmaZ0Area 计算受体或网格浓度。",
                },
                new()
                {
                    Type = "line",
                    Name = "线源",
                    Formula = "Line source segmented into finite source elements",
                    Notes = "按起终点、线宽、线高、分段长度和 sigmaZ0Line 拆分计算后累加。",
                },
                new()
                {
                    Type = "equivalent_area",
                    Name = "等效面源",
                    Formula = "Q_equiv = f(concentration, areaLength, areaWidth, areaHeight)",
                    Notes = "由实测 concentration 反算等效排放速率，并使用实测浓度作为源区最大浓度约束。",
                },
            },
        };
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
