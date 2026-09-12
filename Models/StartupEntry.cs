namespace GasbynOptimizer.Models;

public sealed class StartupEntry
{
    public string Name { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public bool CanToggle { get; init; }
    public string Status => IsEnabled ? "ENABLED" : "DISABLED";
}
