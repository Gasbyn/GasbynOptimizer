namespace GasbynOptimizer.Models;

public sealed class InstalledApplication
{
    public required string Name { get; init; }
    public string Version { get; init; } = "Unknown";
    public string Publisher { get; init; } = "Unknown";
    public string InstallLocation { get; init; } = string.Empty;
    public string UninstallCommand { get; init; } = string.Empty;
    public string QuietUninstallCommand { get; init; } = string.Empty;
    public string RegistryKey { get; init; } = string.Empty;
    public long EstimatedSizeKb { get; init; }
    public bool IsSystemComponent { get; init; }

    public string EstimatedSizeText => EstimatedSizeKb switch
    {
        <= 0 => "Velikost neznámá",
        < 1024 => $"{EstimatedSizeKb} KB",
        < 1024 * 1024 => $"{EstimatedSizeKb / 1024d:0.0} MB",
        _ => $"{EstimatedSizeKb / 1024d / 1024d:0.0} GB"
    };
}
