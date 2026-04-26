namespace GnnSimulation.Api.Services;

public class SimulationNotFoundException : Exception
{
    public SimulationNotFoundException(string message) : base(message) { }
}

public class SimulationBadRequestException : Exception
{
    public SimulationBadRequestException(string message) : base(message) { }
}
