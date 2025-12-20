using System.Text.Json.Serialization;

namespace InstallerLauncher.Infrastructure.State;

public sealed record InstallState
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "0.1.0";

    [JsonPropertyName("contentDownloadComplete")]
    public bool ContentDownloadComplete { get; init; }

    [JsonPropertyName("depotManifestId")]
    public string? DepotManifestId { get; init; }

    [JsonPropertyName("lastUpdatedUtc")]
    public DateTimeOffset LastUpdatedUtc { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("installPath")]
    public string? InstallPath { get; init; }
}
