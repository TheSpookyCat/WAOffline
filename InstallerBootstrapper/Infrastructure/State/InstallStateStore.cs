using System.IO;
using System.Text.Json;
using InstallerLauncher.Infrastructure.Logging;

namespace InstallerLauncher.Infrastructure.State;

public sealed class InstallStateStore
{
    private readonly string _stateFilePath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };

    public InstallStateStore()
    {
        Directory.CreateDirectory(LogPaths.GetStateDirectory());
        _stateFilePath = Path.Combine(LogPaths.GetStateDirectory(), "install_state.json");
    }

    public InstallState Load()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new InstallState();
        }

        var json = File.ReadAllText(_stateFilePath);
        var state = JsonSerializer.Deserialize<InstallState>(json, _serializerOptions);
        return state ?? new InstallState();
    }

    public void Save(InstallState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var json = JsonSerializer.Serialize(state with { LastUpdatedUtc = DateTimeOffset.UtcNow }, _serializerOptions);
        File.WriteAllText(_stateFilePath, json);
    }
}
