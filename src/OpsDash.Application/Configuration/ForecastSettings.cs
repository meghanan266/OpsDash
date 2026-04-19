namespace OpsDash.Application.Configuration;

public sealed class ForecastSettings
{
    public int DefaultDataPoints { get; set; } = 30;

    public int ForecastHorizon { get; set; } = 7;

    public string DefaultMethod { get; set; } = "WMA";
}
