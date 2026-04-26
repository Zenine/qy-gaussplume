namespace GnnSimulation.Core.Atmosphere;

// 高斯烟羽扩散模型。基于稳态高斯烟羽方程计算浓度分布。
// 严格对齐 Python 原版 backend/core/gaussian_plume.py 行为。
public sealed class GaussianPlumeModel
{
    // 纬度每度约 111 km（球面近似），经度随纬度缩放
    public const double MetersPerLatDegree = 111_000.0;
    public const double VonKarman = 0.4;
    public const double Gravity = 9.81;

    public double WindSpeed { get; }
    public double WindDirection { get; }
    public string StabilityClass { get; }
    public double Temperature { get; }
    public double BoundaryLayerHeight { get; }
    public double Humidity { get; }
    public double CloudCover { get; }
    public double Precipitation { get; }

    private readonly PasquillGiffordParams _sigmaParams;

    public GaussianPlumeModel(
        double windSpeed,
        double windDirection,
        string stabilityClass = "D",
        double temperature = 293.15,
        double boundaryLayerHeight = 1000.0,
        double humidity = 50.0,
        double cloudCover = 0.0,
        double precipitation = 0.0)
    {
        WindSpeed = Math.Max(windSpeed, 0.1);
        WindDirection = windDirection;
        StabilityClass = stabilityClass.ToUpperInvariant();
        Temperature = temperature;
        BoundaryLayerHeight = boundaryLayerHeight;
        Humidity = humidity;
        CloudCover = cloudCover;
        Precipitation = precipitation;

        _sigmaParams = PasquillGifford.Get(StabilityClass);
    }

    // ================== 衰减模型 ==================

    public double CalculateGravitationalSettlingVelocity(string pollutant = "PM2.5")
        => PollutantProperties.GetGravitationalSettling(pollutant);

    public double CalculateEffectiveMixingHeight()
    {
        var factor = PasquillGifford.MixingFactors.TryGetValue(StabilityClass, out var f) ? f : 0.6;
        return BoundaryLayerHeight * factor;
    }

    public double CalculateDryDepositionVelocity(string pollutant = "PM2.5")
    {
        var stabFactor = PasquillGifford.AerodynamicResistanceFactors.TryGetValue(StabilityClass, out var f) ? f : 1.5;
        var ra = stabFactor / (VonKarman * WindSpeed + 1e-6);

        var (rb, rc) = PollutantProperties.GetDryResistance(pollutant);
        var vdTurbulent = 1.0 / (ra + rb + rc);
        var vg = CalculateGravitationalSettlingVelocity(pollutant);

        var vd = vdTurbulent + vg;
        vd *= 1 + 0.2 * Humidity / 100;

        if (PollutantProperties.TempCorrectedPollutants.Contains(pollutant))
        {
            var tempFactor = 1 + 0.01 * (Temperature - 293.15);
            vd *= tempFactor;
        }
        return vd;
    }

    public double CalculateWetScavengingCoefficient(string pollutant = "PM2.5")
    {
        var (a, b) = PollutantProperties.GetWetScavenging(pollutant);
        var lambda = Precipitation > 0 ? a * Math.Pow(Precipitation, b) : 0.0;
        const double background = 1e-5;
        lambda += background;
        var cloudFactor = 1 + 0.1 * CloudCover;
        lambda *= cloudFactor;
        return lambda;
    }

    public double CalculateDepositionCoefficient(double distance, string pollutant = "PM2.5")
    {
        var vd = CalculateDryDepositionVelocity(pollutant);
        var kDry = BoundaryLayerHeight > 0 ? vd / BoundaryLayerHeight : 0.0;
        var lambda = CalculateWetScavengingCoefficient(pollutant);
        var kTotal = kDry + lambda;
        return Math.Exp(-kTotal * distance / WindSpeed);
    }

    public double CalculateChemicalDecay(double distance, string pollutant = "PM2.5")
    {
        var kBase = PollutantProperties.GetChemicalRate(pollutant);
        var tempFactor = Math.Exp((Temperature - 298) / 20);
        var humidityFactor = 1 + (Humidity - 50) / 200;
        var cloudFactor = 1 + CloudCover / 50;

        if (PollutantProperties.ChemicalEnhancedPollutants.Contains(pollutant))
        {
            tempFactor *= 1.5;
            humidityFactor *= 1.3;
        }

        var kEffective = kBase * tempFactor * humidityFactor * cloudFactor;
        var travelTime = distance / WindSpeed;
        return Math.Exp(-kEffective * travelTime);
    }

    public double CalculateTotalDecay(double distance, string pollutant = "PM2.5")
        => CalculateDepositionCoefficient(distance, pollutant) * CalculateChemicalDecay(distance, pollutant);

    public double CalculateMaxDiffusionDistance()
    {
        var factor = PasquillGifford.MaxDistanceFactors.TryGetValue(StabilityClass, out var f) ? f : 1.0;
        var uFactor = WindSpeed > 0 ? Math.Clamp(4.0 / WindSpeed, 0.7, 1.5) : 1.0;
        var baseDist = BoundaryLayerHeight * 10;
        return factor * uFactor * baseDist;
    }

    // ================== 扩散参数 ==================

    public (double SigmaY, double SigmaZ) CalculateSigma(double distance)
    {
        var sigmaY = _sigmaParams.Ay * Math.Pow(distance, _sigmaParams.By);
        var sigmaZ = _sigmaParams.Az * Math.Pow(distance, _sigmaParams.Bz);

        sigmaY = Math.Max(sigmaY, 1.0);
        sigmaZ = Math.Max(sigmaZ, 1.0);

        if (BoundaryLayerHeight > 0)
            sigmaZ /= Math.Sqrt(1 + (sigmaZ / BoundaryLayerHeight) * (sigmaZ / BoundaryLayerHeight));

        return (sigmaY, sigmaZ);
    }

    public double CalculateEffectiveHeight(
        double stackHeight, double emissionRate, double stackTemperature, double velocity, double diameter)
    {
        var deltaT = stackTemperature - Temperature;
        var buoyancyFlux = Gravity * velocity * (diameter * diameter) / 4 * deltaT / Temperature;

        double deltaH = buoyancyFlux > 0
            ? 1.6 * Math.Pow(buoyancyFlux, 1.0 / 3.0) / WindSpeed * Math.Pow(stackHeight, 2.0 / 3.0)
            : 0.0;

        var momentumRise = 3 * diameter * velocity / WindSpeed;
        deltaH = Math.Max(deltaH, momentumRise);

        return stackHeight + deltaH;
    }

    // ================== 单点浓度 ==================

    // 高斯烟羽方程：
    //   C = Q/(2π·u·σy·σz) · exp(-y²/(2σy²)) · (exp(-(z-H)²/(2σz²)) + exp(-(z+H)²/(2σz²)))
    public double CalculateConcentration(
        double x, double y, double z,
        double sourceHeight, double emissionRate,
        double? effectiveHeight = null, string pollutant = "PM2.5")
    {
        if (x <= 0) return 0.0;
        var maxDist = CalculateMaxDiffusionDistance();
        if (x > maxDist) return 0.0;

        var (sigmaY, sigmaZ) = CalculateSigma(x);
        var h = effectiveHeight ?? sourceHeight;
        var emissionRateUg = emissionRate * 1e6;

        var term1 = emissionRateUg / (2 * Math.PI * WindSpeed * sigmaY * sigmaZ);
        var term2 = Math.Exp(-y * y / (2 * sigmaY * sigmaY));
        var term3 = Math.Exp(-(z - h) * (z - h) / (2 * sigmaZ * sigmaZ))
                  + Math.Exp(-(z + h) * (z + h) / (2 * sigmaZ * sigmaZ));

        var concentration = term1 * term2 * term3;
        concentration *= CalculateTotalDecay(x, pollutant);
        return concentration;
    }

    public double CalculateReceptorConcentration(
        double sourceLat, double sourceLon, double sourceHeight, double emissionRate,
        double receptorLat, double receptorLon,
        double receptorHeight = 0.0,
        double stackTemperature = 400.0, double velocity = 10.0, double diameter = 1.0,
        string pollutant = "PM2.5")
    {
        var effectiveHeight = CalculateEffectiveHeight(sourceHeight, emissionRate, stackTemperature, velocity, diameter);

        var (xRotated, yRotated) = RotateToWindFrame(
            sourceLat, sourceLon, receptorLat, receptorLon, referenceLat: sourceLat);

        if (xRotated <= 0) return 0.0;

        return CalculateConcentration(
            xRotated, yRotated, receptorHeight,
            sourceHeight, emissionRate, effectiveHeight, pollutant);
    }

    // 气象风向（风来自的方向）→ 数学角度；以源为原点，x 轴指向下风方向
    private (double X, double Y) RotateToWindFrame(
        double originLat, double originLon,
        double targetLat, double targetLon,
        double referenceLat)
    {
        var windAngle = (270 - WindDirection) * Math.PI / 180.0;
        var lonToM = MetersPerLatDegree * Math.Cos(referenceLat * Math.PI / 180.0);

        var dyLat = (targetLat - originLat) * MetersPerLatDegree;
        var dxLon = (targetLon - originLon) * lonToM;

        var cos = Math.Cos(windAngle);
        var sin = Math.Sin(windAngle);
        return (dxLon * cos + dyLat * sin, -dxLon * sin + dyLat * cos);
    }

    // ================== 浓度场（点源向量化） ==================

    public double[,] CalculateConcentrationField(
        double sourceLat, double sourceLon,
        double sourceHeight, double emissionRate,
        double[] gridLat, double[] gridLon,
        double stackTemperature = 400.0,
        double velocity = 10.0,
        double diameter = 1.0,
        double receptorHeight = 0.0,
        string pollutant = "PM2.5")
    {
        var nLat = gridLat.Length;
        var nLon = gridLon.Length;
        var field = new double[nLat, nLon];

        var effectiveHeight = CalculateEffectiveHeight(
            sourceHeight, emissionRate, stackTemperature, velocity, diameter);

        var windAngle = (270 - WindDirection) * Math.PI / 180.0;
        var cos = Math.Cos(windAngle);
        var sin = Math.Sin(windAngle);
        var lonToM = MetersPerLatDegree * Math.Cos(sourceLat * Math.PI / 180.0);

        var maxDist = CalculateMaxDiffusionDistance();
        var emissionRateUg = emissionRate * 1e6;
        var h = effectiveHeight;

        // 预计算沉降相关项（在整个网格上共享）
        // 注意：Python 向量化路径 **只使用** deposition（干+湿），不叠加 chemical decay。
        // 这与非向量化的 CalculateConcentration 不同，这里刻意保留该差异以对齐。
        var vd = CalculateDryDepositionVelocity(pollutant);
        var lambda = CalculateWetScavengingCoefficient(pollutant);
        var kDry = BoundaryLayerHeight > 0 ? vd / BoundaryLayerHeight : 0.0;
        var kTotal = kDry + lambda;

        for (var i = 0; i < nLat; i++)
        {
            var dyLat = (gridLat[i] - sourceLat) * MetersPerLatDegree;
            for (var j = 0; j < nLon; j++)
            {
                var dxLon = (gridLon[j] - sourceLon) * lonToM;
                var xRot = dxLon * cos + dyLat * sin;
                if (xRot <= 0 || xRot > maxDist) continue;

                var yRot = -dxLon * sin + dyLat * cos;

                var sigmaY = _sigmaParams.Ay * Math.Pow(xRot, _sigmaParams.By);
                var sigmaZ = _sigmaParams.Az * Math.Pow(xRot, _sigmaParams.Bz);
                sigmaY = Math.Max(sigmaY, 1.0);
                sigmaZ = Math.Max(sigmaZ, 1.0);
                if (BoundaryLayerHeight > 0)
                    sigmaZ /= Math.Sqrt(1 + (sigmaZ / BoundaryLayerHeight) * (sigmaZ / BoundaryLayerHeight));

                var t1 = emissionRateUg / (2 * Math.PI * WindSpeed * sigmaY * sigmaZ);
                var t2 = Math.Exp(-yRot * yRot / (2 * sigmaY * sigmaY));
                var zm = receptorHeight - h;
                var zp = receptorHeight + h;
                var t3 = Math.Exp(-zm * zm / (2 * sigmaZ * sigmaZ))
                       + Math.Exp(-zp * zp / (2 * sigmaZ * sigmaZ));

                var conc = t1 * t2 * t3;
                var decay = Math.Exp(-kTotal * xRot / WindSpeed);
                field[i, j] = conc * decay;
            }
        }

        return field;
    }

    // ================== 面源（含等效面源） ==================

    // 矩形面源使用虚拟点源法：σ_eff = sqrt(σ² + σ₀²)，其中 σ₀ 由面源尺寸确定。
    public double[,] CalculateAreaSourceConcentrationField(
        double centerLat, double centerLon,
        double areaLength, double areaWidth, double areaHeight,
        double emissionRate,
        double[] gridLat, double[] gridLon,
        double? sigmaZ0 = null,
        double receptorHeight = 0.0,
        double? maxConcentration = null,
        bool isEquivalent = false,
        string pollutant = "PM2.5")
    {
        var nLat = gridLat.Length;
        var nLon = gridLon.Length;
        var field = new double[nLat, nLon];

        var sigmaY0 = areaWidth / 4.3;
        var sZ0 = sigmaZ0 ?? (areaHeight > 0 ? areaHeight / 2.15 : 1.0);

        var xVirtualY = _sigmaParams.Ay > 0
            ? Math.Pow(sigmaY0 / _sigmaParams.Ay, 1.0 / _sigmaParams.By) : 0;
        var xVirtualZ = _sigmaParams.Az > 0
            ? Math.Pow(sZ0 / _sigmaParams.Az, 1.0 / _sigmaParams.Bz) : 0;
        var xVirtual = Math.Max(xVirtualY, xVirtualZ);

        var windAngle = (270 - WindDirection) * Math.PI / 180.0;
        var cos = Math.Cos(windAngle);
        var sin = Math.Sin(windAngle);
        var lonToM = MetersPerLatDegree * Math.Cos(centerLat * Math.PI / 180.0);

        var maxDist = CalculateMaxDiffusionDistance();
        var halfLen = areaLength / 2;
        var halfWid = areaWidth / 2;

        var emissionRateUg = emissionRate * 1e6;
        var h = areaHeight;

        for (var i = 0; i < nLat; i++)
        {
            var dyLat = (gridLat[i] - centerLat) * MetersPerLatDegree;
            for (var j = 0; j < nLon; j++)
            {
                var dxLon = (gridLon[j] - centerLon) * lonToM;
                var xRot = dxLon * cos + dyLat * sin;
                var xEff = xRot + xVirtual;
                if (xEff <= 0 || xEff > maxDist) continue;

                var yRot = -dxLon * sin + dyLat * cos;

                var sigmaYRaw = Math.Max(_sigmaParams.Ay * Math.Pow(xEff, _sigmaParams.By), 1.0);
                var sigmaZRaw = Math.Max(_sigmaParams.Az * Math.Pow(xEff, _sigmaParams.Bz), 1.0);
                if (BoundaryLayerHeight > 0)
                    sigmaZRaw /= Math.Sqrt(1 + (sigmaZRaw / BoundaryLayerHeight) * (sigmaZRaw / BoundaryLayerHeight));

                var sigmaYEffSq = sigmaYRaw * sigmaYRaw + sigmaY0 * sigmaY0;
                var sigmaZEffSq = sigmaZRaw * sigmaZRaw + sZ0 * sZ0;
                var sigmaYEff = Math.Sqrt(sigmaYEffSq);

                // 烟羽横向截断
                if (Math.Abs(yRot) >= 4 * sigmaYEff) continue;

                var inv2SyEffSq = 1.0 / (2 * sigmaYEffSq);
                var inv2SzEffSq = 1.0 / (2 * sigmaZEffSq);

                var t1 = emissionRateUg / (2 * Math.PI * WindSpeed * Math.Sqrt(sigmaYEffSq) * Math.Sqrt(sigmaZEffSq));
                var t2 = Math.Exp(-yRot * yRot * inv2SyEffSq);
                var zm = receptorHeight - h;
                var zp = receptorHeight + h;
                var t3 = Math.Exp(-zm * zm * inv2SzEffSq) + Math.Exp(-zp * zp * inv2SzEffSq);

                var conc = t1 * t2 * t3 * CalculateTotalDecay(xEff, pollutant);

                // 等效面源内部浓度夹紧到实测值
                if (isEquivalent && maxConcentration.HasValue
                    && Math.Abs(dxLon) <= halfLen && Math.Abs(dyLat) <= halfWid)
                {
                    conc = Math.Min(conc, maxConcentration.Value);
                }

                field[i, j] = conc;
            }
        }

        return field;
    }

    public double CalculateAreaSourceReceptorConcentration(
        double centerLat, double centerLon,
        double areaLength, double areaWidth, double areaHeight,
        double emissionRate,
        double receptorLat, double receptorLon,
        double? sigmaZ0 = null,
        double receptorHeight = 0.0,
        double? concentration = null,
        bool isEquivalent = false,
        string pollutant = "PM2.5")
    {
        var lonToM = MetersPerLatDegree * Math.Cos(centerLat * Math.PI / 180.0);
        var dy = (receptorLat - centerLat) * MetersPerLatDegree;
        var dx = (receptorLon - centerLon) * lonToM;

        var halfLen = areaLength / 2;
        var halfWid = areaWidth / 2;

        var sigmaY0 = areaWidth / 4.3;
        var sZ0 = sigmaZ0 ?? (areaHeight > 0 ? areaHeight / 2.15 : 1.0);

        var xVirtualY = _sigmaParams.Ay > 0 ? Math.Pow(sigmaY0 / _sigmaParams.Ay, 1.0 / _sigmaParams.By) : 0;
        var xVirtualZ = _sigmaParams.Az > 0 ? Math.Pow(sZ0 / _sigmaParams.Az, 1.0 / _sigmaParams.Bz) : 0;
        var xVirtual = Math.Max(xVirtualY, xVirtualZ);

        var windAngle = (270 - WindDirection) * Math.PI / 180.0;
        var xRotated = dx * Math.Cos(windAngle) + dy * Math.Sin(windAngle);
        var yRotated = -dx * Math.Sin(windAngle) + dy * Math.Cos(windAngle);
        var xEff = xRotated + xVirtual;

        if (xEff <= 0) return 0.0;

        var inSource = Math.Abs(dx) <= halfLen && Math.Abs(dy) <= halfWid;

        var (sigmaY, sigmaZ) = CalculateSigma(xEff);
        var sigmaYEff = Math.Sqrt(sigmaY * sigmaY + sigmaY0 * sigmaY0);
        var sigmaZEff = Math.Sqrt(sigmaZ * sigmaZ + sZ0 * sZ0);

        var emissionRateUg = emissionRate * 1e6;
        var h = areaHeight;

        var t1 = emissionRateUg / (2 * Math.PI * WindSpeed * sigmaYEff * sigmaZEff);
        var t2 = Math.Exp(-yRotated * yRotated / (2 * sigmaYEff * sigmaYEff));
        var t3 = Math.Exp(-(receptorHeight - h) * (receptorHeight - h) / (2 * sigmaZEff * sigmaZEff))
               + Math.Exp(-(receptorHeight + h) * (receptorHeight + h) / (2 * sigmaZEff * sigmaZEff));

        var result = t1 * t2 * t3 * CalculateTotalDecay(xEff, pollutant);

        if (isEquivalent && concentration.HasValue && inSource)
            result = Math.Min(result, concentration.Value);

        return result;
    }

    // ================== 线源（分段点源法） ==================

    public double[,] CalculateLineSourceConcentrationField(
        double startLat, double startLon,
        double endLat, double endLon,
        double lineWidth, double lineHeight,
        double emissionRate,
        double[] gridLat, double[] gridLon,
        double segmentLength = 10.0,
        double? sigmaZ0 = null,
        double receptorHeight = 0.0,
        string pollutant = "PM2.5")
    {
        var lonToM = MetersPerLatDegree * Math.Cos((startLat + endLat) / 2 * Math.PI / 180.0);
        var dx = (endLon - startLon) * lonToM;
        var dy = (endLat - startLat) * MetersPerLatDegree;
        var lineLength = Math.Sqrt(dx * dx + dy * dy);

        var numSegments = Math.Max(1, (int)(lineLength / segmentLength));
        var segmentEmission = emissionRate / numSegments;

        var sZ0 = sigmaZ0 ?? (lineHeight > 0 ? lineHeight / 2.15 : 2.0);

        var nLat = gridLat.Length;
        var nLon = gridLon.Length;
        var field = new double[nLat, nLon];

        for (var seg = 0; seg < numSegments; seg++)
        {
            var t = (seg + 0.5) / numSegments;
            var segLat = startLat + t * (endLat - startLat);
            var segLon = startLon + t * (endLon - startLon);

            var segField = CalculateAreaSourceConcentrationField(
                centerLat: segLat,
                centerLon: segLon,
                areaLength: segmentLength,
                areaWidth: lineWidth,
                areaHeight: lineHeight,
                emissionRate: segmentEmission,
                gridLat: gridLat,
                gridLon: gridLon,
                sigmaZ0: sZ0,
                receptorHeight: receptorHeight,
                pollutant: pollutant);

            for (var i = 0; i < nLat; i++)
                for (var j = 0; j < nLon; j++)
                    field[i, j] += segField[i, j];
        }

        return field;
    }

    public double CalculateLineSourceReceptorConcentration(
        double startLat, double startLon,
        double endLat, double endLon,
        double lineWidth, double lineHeight,
        double emissionRate,
        double receptorLat, double receptorLon,
        double segmentLength = 10.0,
        double? sigmaZ0 = null,
        double receptorHeight = 0.0,
        string pollutant = "PM2.5")
    {
        var lonToM = MetersPerLatDegree * Math.Cos((startLat + endLat) / 2 * Math.PI / 180.0);
        var dx = (endLon - startLon) * lonToM;
        var dy = (endLat - startLat) * MetersPerLatDegree;
        var lineLength = Math.Sqrt(dx * dx + dy * dy);

        var numSegments = Math.Max(1, (int)(lineLength / segmentLength));
        var segmentEmission = emissionRate / numSegments;
        var sZ0 = sigmaZ0 ?? (lineHeight > 0 ? lineHeight / 2.15 : 2.0);

        var total = 0.0;
        for (var seg = 0; seg < numSegments; seg++)
        {
            var t = (seg + 0.5) / numSegments;
            var segLat = startLat + t * (endLat - startLat);
            var segLon = startLon + t * (endLon - startLon);

            total += CalculateAreaSourceReceptorConcentration(
                centerLat: segLat,
                centerLon: segLon,
                areaLength: segmentLength,
                areaWidth: lineWidth,
                areaHeight: lineHeight,
                emissionRate: segmentEmission,
                receptorLat: receptorLat,
                receptorLon: receptorLon,
                sigmaZ0: sZ0,
                receptorHeight: receptorHeight,
                pollutant: pollutant);
        }
        return total;
    }

    // ================== 反推排放速率 ==================

    public double CalculateEmissionRateFromConcentration(
        double x, double y, double z,
        double sourceHeight, double concentration,
        double? effectiveHeight = null)
    {
        if (x <= 0) throw new ArgumentException("下风向距离必须大于 0", nameof(x));
        if (concentration <= 0) return 0.0;

        var (sigmaY, sigmaZ) = CalculateSigma(x);
        var h = effectiveHeight ?? sourceHeight;

        var term2 = Math.Exp(-y * y / (2 * sigmaY * sigmaY));
        var term3 = Math.Exp(-(z - h) * (z - h) / (2 * sigmaZ * sigmaZ))
                  + Math.Exp(-(z + h) * (z + h) / (2 * sigmaZ * sigmaZ));

        if (term2 * term3 < 1e-30)
            throw new InvalidOperationException("计算点位置过于偏离烟羽中心，无法准确反推排放速率");

        var emissionRateUg = concentration * 2 * Math.PI * WindSpeed * sigmaY * sigmaZ / (term2 * term3);
        return emissionRateUg / 1e6;
    }

    public double CalculateEquivalentEmissionRate(
        double concentration, double areaLength, double areaWidth, double areaHeight)
    {
        var concentrationG = concentration / 1e6;
        return concentrationG * WindSpeed * areaHeight * Math.Sqrt(areaLength * areaWidth);
    }

    public double CalculateEmissionRateFromReceptor(
        double sourceLat, double sourceLon, double sourceHeight,
        double receptorLat, double receptorLon, double concentration,
        double receptorHeight = 0.0,
        double stackTemperature = 400.0, double velocity = 10.0, double diameter = 1.0)
    {
        var effectiveHeight = CalculateEffectiveHeight(sourceHeight, 1.0, stackTemperature, velocity, diameter);

        var (xRotated, yRotated) = RotateToWindFrame(
            sourceLat, sourceLon, receptorLat, receptorLon, referenceLat: sourceLat);

        if (xRotated <= 0)
        {
            xRotated = 100.0;
            yRotated = 0.0;
        }

        return CalculateEmissionRateFromConcentration(
            xRotated, yRotated, receptorHeight, sourceHeight, concentration, effectiveHeight);
    }
}
