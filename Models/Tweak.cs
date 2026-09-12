namespace GasbynOptimizer.Models;

public sealed class Tweak
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public Tier Tier { get; init; } = Tier.Free;
    public required Func<TweakContext, TweakResult> Apply { get; init; }
    public required Func<TweakContext, TweakResult> Restore { get; init; }
    public required Func<TweakContext, TweakStatus> GetStatus { get; init; }
}

public sealed class TweakContext
{
    public string BackupDirectory { get; init; } = string.Empty;
}

public enum TweakStatus
{
    Unknown,
    Enabled,
    Disabled,
    NotConfigured
}

public sealed record TweakResult(bool Success, string Message)
{
    public static TweakResult Ok(string message) => new(true, message);
    public static TweakResult Fail(string message) => new(false, message);
}
