namespace HarmonyDB.Shelf.Model.App;

public record HealthResponse
{
    public required HealthStatus Status { get; init; }
}

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
}
