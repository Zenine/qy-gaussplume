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
            GaussianPlumeFormula = "C = Q / (2πuσyσz) · exp(-y²/(2σy²)) · [exp(-(z-H)²/(2σz²)) + exp(-(z+H)²/(2σz²))]；Q(g/s) 在计算中换算为 μg/s，H 为 Briggs 抬升后的有效源高。",
            DecayFormula = "沉降衰减 D_dep = exp(-((v_d/BLH)+Λ)·x/u)，v_d = v_g + 1/(R_a+R_b+R_c)，Λ = (a·P^b + background_scavenging) × cloud_factor；化学衰减 D_chem = exp(-k_eff·x/u)。受体贡献、面源、线源和等效面源使用 C_final = C_plume × D_dep × D_chem；点源网格场为保持历史批量场口径仅使用 D_dep。",
            WindAggregationFormula = "C_aggregate = Σ(C_direction_i × normalized_weight_i)。失败风向剔除后，只对成功风向的原始权重重新归一化；各污染物分场和受体贡献按同一归一化权重分别聚合。",
            Pollutants = pollutants,
            SourceTypes = new List<SourceFormulaInfoDto>
            {
                new()
                {
                    Type = "point",
                    Name = "点源",
                    Formula = "C_point = C_gaussian(Q, H_eff, σy(x), σz(x))；H_eff = stack_height + max(浮力抬升, 动量抬升)",
                    Notes = "点源受体贡献使用完整沉降+化学衰减；点源网格浓度场沿用历史批量场口径，只叠加沉降/湿清除衰减。",
                },
                new()
                {
                    Type = "area",
                    Name = "面源",
                    Formula = "虚拟点源法：σ_eff = sqrt(σ² + σ0²)，σy0 = areaWidth/4.3，σz0 = sigmaZ0Area 或 areaHeight/2.15",
                    Notes = "按面源中心、长度、宽度、高度和初始垂直扩散参数计算；面源网格和受体路径均使用完整沉降+化学衰减。",
                },
                new()
                {
                    Type = "line",
                    Name = "线源",
                    Formula = "C_line = Σ C_area(segment_i)，segmentEmission = Q_total / numSegments",
                    Notes = "按起终点、lineWidth、lineHeight、segmentLength 和 sigmaZ0Line 拆成若干短面源 segment 后累加。",
                },
                new()
                {
                    Type = "equivalent_area",
                    Name = "等效面源",
                    Formula = "Q_equiv = concentration/1e6 × windSpeed × areaHeight × sqrt(areaLength × areaWidth)",
                    Notes = "concentration 单位为 μg/m³，反算得到 g/s；计算浓度时复用面源虚拟点源公式，并在源区内部以实测浓度作为最大值约束。",
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
