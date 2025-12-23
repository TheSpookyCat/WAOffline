using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Media;
using System.Windows.Input;
using InstallerLauncher.Infrastructure.Installation;
using InstallerLauncher.Infrastructure.State;

namespace InstallerBootstrapper;

public sealed class InstallerViewModel : INotifyPropertyChanged
{
    private const string DepotDownloaderApiUrl = "https://api.github.com/repos/SteamRE/DepotDownloader/releases/latest";
    private const string BepInExVersion = "5.4.23.4";
    private const string BepInExDownloadUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.4/BepInEx_win_x64_5.4.23.4.zip";

    private const int MaxStatusLines = 200;

    private readonly InstallStateStore _stateStore;
    private readonly InstallState _state;
    private Process? _webServerProcess;
    private Process? _gameServerProcess;
    private Process? _clientProcess;
    private double _progressPercent;
    private string _statusLine = "Initializing";
    private readonly ObservableCollection<string> _statusLog = new();
    private bool _canRetry;
    private string _installPath;
    private string _steamUsername = string.Empty;
    private string _payloadStatus = "Awaiting install";
    private string _currentStep = "Configure";
    private bool _isBusy;
    private bool _installReady;
    private bool _isClientRunning;
    private string _launchButtonText = "Launch";
    private Brush _launchButtonBackground;
    private Brush _currentStepBrush;

    public InstallerViewModel(InstallStateStore stateStore)
    {
        _stateStore = stateStore;
        _state = _stateStore.Load();
        _installPath = string.IsNullOrWhiteSpace(_state.InstallPath)
            ? GetDefaultInstallPath()
            : _state.InstallPath!;

        _launchButtonBackground = GetBrushFromResources("AccentBrush");
        _currentStepBrush = GetBrushFromResources("AccentBrush");

        RetryCommand = new RelayCommand(async _ => await BeginInstallAsync(), _ => CanRetry && !IsBusy);
        ExportDiagnosticsCommand = new RelayCommand(_ => ExportDiagnostics(), _ => !IsBusy);
        LaunchAfterInstallCommand = new RelayCommand(async _ => await LaunchAfterInstallAsync(), _ => (InstallReady && !IsBusy) || IsClientRunning);
        BrowseInstallPathCommand = new RelayCommand(_ => BrowseForInstallPath(), _ => !IsBusy);
        StartInstallCommand = new RelayCommand(async _ => await BeginInstallAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(SteamUsername));

        AppendStatusLine("Choose an install directory to get started.");
        ResumeIfAlreadyInstalled();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RetryCommand { get; }

    public ICommand ExportDiagnosticsCommand { get; }

    public ICommand LaunchAfterInstallCommand { get; }

    public ICommand BrowseInstallPathCommand { get; }

    public ICommand StartInstallCommand { get; }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            if (Math.Abs(_progressPercent - value) > 0.01)
            {
                _progressPercent = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusLine
    {
        get => _statusLine;
        private set
        {
            if (_statusLine != value)
            {
                _statusLine = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<string> StatusLog => _statusLog;

    private void AppendStatusLine(string message)
    {
        var trimmed = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        StatusLine = trimmed;
        _statusLog.Add(trimmed);

        while (_statusLog.Count > MaxStatusLines)
        {
            _statusLog.RemoveAt(0);
        }
    }

    private void ResumeIfAlreadyInstalled()
    {
        if (!_state.ContentDownloadComplete)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_state.InstallPath))
        {
            return;
        }

        InstallPath = Path.GetFullPath(_state.InstallPath);

        if (!Directory.Exists(InstallPath))
        {
            return;
        }

        if (!VerifyGameFiles())
        {
            return;
        }

        var webServerPath = Path.Combine(InstallPath, "WebServer", "WorldsAdriftServer.exe");
        var gameServerPath = Path.Combine(InstallPath, "GameServer", "WorldsAdriftRebornGameServer.exe");

        if (!File.Exists(webServerPath) || !File.Exists(gameServerPath))
        {
            return;
        }

        InstallReady = true;
        ProgressPercent = 100;
        PayloadStatus = $"Game rooted at {InstallPath}";
        AppendStatusLine("Ready to launch");
        CurrentStep = "Complete";
    }

    public bool CanRetry
    {
        get => _canRetry;
        private set
        {
            if (_canRetry != value)
            {
                _canRetry = value;
                OnPropertyChanged();
                RefreshCommandStates();
            }
        }
    }

    public string InstallPath
    {
        get => _installPath;
        set
        {
            if (_installPath != value)
            {
                _installPath = value;
                OnPropertyChanged();
            }
        }
    }

    public string SteamUsername
    {
        get => _steamUsername;
        set
        {
            if (_steamUsername != value)
            {
                _steamUsername = value;
                OnPropertyChanged();
                RefreshCommandStates();
            }
        }
    }

    public string PayloadStatus
    {
        get => _payloadStatus;
        private set
        {
            if (_payloadStatus != value)
            {
                _payloadStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
                RefreshCommandStates();
            }
        }
    }

    public bool InstallReady
    {
        get => _installReady;
        private set
        {
            if (_installReady != value)
            {
                _installReady = value;
                OnPropertyChanged();
                RefreshCommandStates();
            }
        }
    }

    public bool IsClientRunning
    {
        get => _isClientRunning;
        private set
        {
            if (_isClientRunning != value)
            {
                _isClientRunning = value;
                OnPropertyChanged();
                RefreshCommandStates();
            }
        }
    }

    public string LaunchButtonText
    {
        get => _launchButtonText;
        private set
        {
            if (_launchButtonText != value)
            {
                _launchButtonText = value;
                OnPropertyChanged();
            }
        }
    }

    public Brush LaunchButtonBackground
    {
        get => _launchButtonBackground;
        private set
        {
            if (_launchButtonBackground != value)
            {
                _launchButtonBackground = value;
                OnPropertyChanged();
            }
        }
    }

    public Brush CurrentStepBrush
    {
        get => _currentStepBrush;
        private set
        {
            if (_currentStepBrush != value)
            {
                _currentStepBrush = value;
                OnPropertyChanged();
            }
        }
    }

    public string CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (_currentStep != value)
            {
                _currentStep = value;
                OnPropertyChanged();
                UpdateCurrentStepBrush(value);
            }
        }
    }

    private async Task BeginInstallAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        InstallReady = false;
        CanRetry = false;

        CurrentStep = "DepotDownloader";

        try
        {
            Directory.CreateDirectory(InstallPath);
            AppendStatusLine("Contacting Steam...");
            ProgressPercent = 5;

            var downloadVerified = VerifyExistingDownload();
            if (!downloadVerified)
            {
                await RunDepotDownloaderAsync();

                if (!VerifyGameFiles())
                {
                    CurrentStep = "Failed";
                    CanRetry = true;
                    return;
                }
            }

            AppendStatusLine("Applying bundled servers");
            await DeployServersAsync();
            ProgressPercent = 82;
            
            SyncServerSdkDllsFromClient();

            CurrentStep = "BepInEx";
            AppendStatusLine($"Installing BepInEx {BepInExVersion}");
            await EnsureBepInExInstalledAsync();
            ProgressPercent = Math.Max(ProgressPercent, 88);

            AppendStatusLine("Copying client plugin");
            await DeployClientPluginAsync();
            ProgressPercent = 94;

            PersistState();
            AppendStatusLine("Ready to launch");
            PayloadStatus = $"Game rooted at {InstallPath}";
            ProgressPercent = 100;
            InstallReady = true;
            CurrentStep = "Complete";
        }
        catch (Exception ex)
        {
            AppendStatusLine($"Install failed: {ex.Message}");
            CurrentStep = "Failed";
            CanRetry = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunDepotDownloaderAsync()
    {
        await EnsureSteamIsClosedAsync();
        await EnsureDepotDownloaderAsync();

        AppendStatusLine("Running DepotDownloader");
        ProgressPercent = Math.Max(ProgressPercent, 15);

        var startInfo = new ProcessStartInfo
        {
            FileName = GetDepotExePath(),
            Arguments = $"-app 322780 -depot 322783 -manifest 4624240741051053915 -dir \"{InstallPath}\" -validate -username \"{SteamUsername}\"",

            UseShellExecute = true,
            CreateNoWindow = false,
            WorkingDirectory = GetDepotToolDir()
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) => HandleDepotOutput(args.Data);
        process.ErrorDataReceived += (_, args) => HandleDepotOutput(args.Data);

        process.Start();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"DepotDownloader exited with code {process.ExitCode}");
        }

        AppendStatusLine("Depot download complete");
        ProgressPercent = Math.Max(ProgressPercent, 75);
    }

    private bool VerifyExistingDownload()
    {
        AppendStatusLine("Validating existing game files...");

        if (!VerifyGameFiles())
        {
            AppendStatusLine("Existing install incomplete. Running DepotDownloader.");
            return false;
        }

        AppendStatusLine("Game already downloaded. Skipping DepotDownloader.");
        ProgressPercent = Math.Max(ProgressPercent, 75);
        return true;
    }

    private async Task EnsureSteamIsClosedAsync()
    {
        while (IsSteamRunning())
        {
            var result = MessageBox.Show(
                "Steam is currently running. We can't install the correct game version while Steam is open.\n\n" +
                "Press Yes to let the installer close Steam automatically, No if you've already closed it, or Cancel to abort.",
                "Steam must be closed",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Yes);

            switch (result)
            {
                case MessageBoxResult.Cancel:
                    throw new OperationCanceledException("Installation cancelled because Steam is running.");
                case MessageBoxResult.Yes:
                    await RequestSteamShutdownAsync();
                    break;
                case MessageBoxResult.No:
                    await Task.Delay(1000);
                    break;
            }

            if (IsSteamRunning())
            {
                AppendStatusLine("Waiting for Steam to close");
                await WaitForSteamExitAsync(TimeSpan.FromSeconds(10));
            }
        }
    }

    private void HandleDepotOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        AppendStatusLine(line);

        var percent = ExtractPercent(line);
        if (percent.HasValue)
        {
            ProgressPercent = Math.Max(ProgressPercent, percent.Value);
        }
    }

    private static double? ExtractPercent(string line)
    {
        var match = Regex.Match(line, "(\\d{1,3})%");
        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(match.Groups[1].Value, out var value))
        {
            return null;
        }

        return Math.Clamp(value, 0, 100);
    }
    private static JsonElement SelectWindowsAsset(JsonElement assets)
    {
        string[] preferred =
        {
            "depotdownloader-windows-x64.zip",
            "depotdownloader-windows-arm64.zip"
        };

        foreach (var want in preferred)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString()!.ToLowerInvariant();
                if (name == want)
                    return asset;
            }
        }

        throw new InvalidOperationException(
            "No compatible Windows DepotDownloader asset found in GitHub release.");
    }

    private async Task EnsureDepotDownloaderAsync()
    {
        var toolDir = GetDepotToolDir();
        var exePath = GetDepotExePath();

        if (File.Exists(exePath))
            return;

        Directory.CreateDirectory(toolDir);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WAInstaller/1.0");

        var json = await http.GetStringAsync(
            "https://api.github.com/repos/SteamRE/DepotDownloader/releases/latest");

        using var doc = JsonDocument.Parse(json);

        var asset = SelectWindowsAsset(
            doc.RootElement.GetProperty("assets"));

        var url = asset.GetProperty("browser_download_url").GetString()!;
        var zipPath = Path.Combine(toolDir, "DepotDownloader.zip");

        await using (var net = await http.GetStreamAsync(url))
        await using (var file = File.Create(zipPath))
            await net.CopyToAsync(file);

        ZipFile.ExtractToDirectory(zipPath, toolDir, overwriteFiles: true);
        File.Delete(zipPath);

        if (!File.Exists(exePath))
            throw new InvalidOperationException(
                "DepotDownloader.exe missing after extraction (invalid release asset).");
    }


    private async Task<string> GetWindowsZipAssetUrlAsync(HttpClient httpClient)
    {
        using var response = await httpClient.GetAsync(DepotDownloaderApiUrl);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        if (!document.RootElement.TryGetProperty("assets", out var assets))
        {
            throw new InvalidOperationException("DepotDownloader release payload missing assets.");
        }

        string? fallbackUrl = null;

        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var nameProperty) || !asset.TryGetProperty("browser_download_url", out var urlProperty))
            {
                continue;
            }

            var name = nameProperty.GetString();
            var url = urlProperty.GetString();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                fallbackUrl ??= url;
            }

            if (name.Contains("windows", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackUrl))
        {
            return fallbackUrl;
        }

        throw new InvalidOperationException("Unable to locate DepotDownloader Windows asset.");
    }

    private string GetToolsRoot() => Path.Combine(InstallPath, ".tools");

    private async Task EnsureBepInExInstalledAsync()
    {
        if (IsBepInExInstalled())
        {
            AppendStatusLine("BepInEx already installed");
            return;
        }

        var toolsRoot = GetToolsRoot();
        var zipPath = Path.Combine(toolsRoot, $"BepInEx_{BepInExVersion}.zip");
        Directory.CreateDirectory(toolsRoot);

        using (var http = new HttpClient())
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WAInstaller/1.0");

            await using var downloadStream = await http.GetStreamAsync(BepInExDownloadUrl);
            await using var zipFile = File.Create(zipPath);
            await downloadStream.CopyToAsync(zipFile);
        }

        AppendStatusLine("Extracting BepInEx");
        ZipFile.ExtractToDirectory(zipPath, InstallPath, overwriteFiles: true);

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        if (!IsBepInExInstalled())
        {
            throw new InvalidOperationException("BepInEx installation incomplete after extraction.");
        }
        
        var appIdPath = Path.Combine(InstallPath, "steam_appid.txt");
        if (!File.Exists(appIdPath))
        {
            await File.WriteAllTextAsync(appIdPath, "322780");
        }

        AppendStatusLine($"BepInEx {BepInExVersion} installed.");
    }

    private bool IsBepInExInstalled()
    {
        var coreDll = Path.Combine(InstallPath, "BepInEx", "core", "BepInEx.dll");
        return File.Exists(coreDll);
    }

    private static async Task DownloadToFileAsync(HttpClient httpClient, string url, string destinationPath)
    {
        await using var downloadStream = await httpClient.GetStreamAsync(url);
        await using var fileStream = File.Create(destinationPath);
        await downloadStream.CopyToAsync(fileStream);
    }

    private void NormalizeDepotDownloaderLayout()
    {
        var targetExe = GetDepotExePath();
        if (File.Exists(targetExe))
        {
            return;
        }

        var candidate = Directory.EnumerateFiles(GetDepotToolDir(), "DepotDownloader.exe", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (candidate is null)
        {
            return;
        }

        var candidateRoot = Path.GetDirectoryName(candidate)!;

        foreach (var filePath in Directory.EnumerateFiles(candidateRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(candidateRoot, filePath);
            var destination = Path.Combine(GetDepotToolDir(), relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(filePath, destination, overwrite: true);
        }
    }

    private static bool IsSteamRunning()
    {
        return Process.GetProcessesByName("steam").Any(p => !p.HasExited);
    }

    private static async Task RequestSteamShutdownAsync()
    {
        foreach (var process in Process.GetProcessesByName("steam"))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                }
            }
            catch
            {
            }
        }

        await WaitForSteamExitAsync(TimeSpan.FromSeconds(20));
    }

    private static async Task WaitForSteamExitAsync(TimeSpan timeout)
    {
        var start = DateTime.UtcNow;

        while (IsSteamRunning() && DateTime.UtcNow - start < timeout)
        {
            await Task.Delay(500);
        }
    }

    private string GetDepotToolDir() => Path.Combine(InstallPath, ".tools", "DepotDownloader");

    private string GetDepotExePath() => Path.Combine(GetDepotToolDir(), "DepotDownloader.exe");

    private bool VerifyGameFiles()
    {
        var clientExe = Path.Combine(InstallPath, "UnityClient@Windows.exe");
        if (!File.Exists(clientExe))
        {
            AppendStatusLine("UnityClient@Windows.exe not found. Wait for DepotDownloader to finish.");
            PayloadStatus = InstallPath;
            return false;
        }

        PayloadStatus = "Game files validated";
        return true;
    }

    private void SyncServerSdkDllsFromClient()
    {
        var clientManaged = Path.Combine(InstallPath, "UnityClient@Windows_Data", "Managed");
        var serverDir = Path.Combine(InstallPath, "GameServer");

        string[] dlls =
        {
            "Assembly-CSharp.dll",
            "UnityEngine.dll",
            "Newtonsoft.Json.dll",
            "Improbable.WorkerSdkCsharp.dll",
            "Improbable.WorkerSdkCsharp.Framework.dll",
            "Generated.Code.dll",
            "protobuf-net.dll",

            "Microsoft.Win32.SystemEvents.dll",
            "Mono.Messaging.dll",
            "Mono.Security.dll",
            "System.Configuration.ConfigurationManager.dll",
            "System.Configuration.Install.dll",
            "System.Drawing.Common.dll",
            "System.EnterpriseServices.dll",
            "System.IdentityModel.dll",
            "System.IdentityModel.Selectors.dll",
            "System.Messaging.dll",
            "System.Security.Cryptography.ProtectedData.dll",
            "System.Security.Permissions.dll",
            "System.ServiceModel.dll",
            "System.Web.Services.dll",
            "System.Windows.Extensions.dll"
        };

        foreach (var dll in dlls)
        {
            var src = Path.Combine(clientManaged, dll);
            var dst = Path.Combine(serverDir, dll);

            if (!File.Exists(src))
            {
                // AppendStatusLine($"Skipped: {src}");
                continue;
            }
            AppendStatusLine($"Copied: {src}");

            File.Copy(src, dst, overwrite: true);
        }

        AppendStatusLine("Synchronized GameServer DLLs");
    }

    private async Task DeployServersAsync()
    {
        var payloadRoot = await PayloadLayout.EnsurePayloadExtractedAsync();
        if (!Directory.Exists(payloadRoot))
        {
            throw new DirectoryNotFoundException($"Missing payload folder at {payloadRoot}");
        }

        await Task.Run(() =>
        {
            CopyPayloadFolder(PayloadLayout.GetPayloadDirectory("WebServer"), Path.Combine(InstallPath, "WebServer"));
            CopyPayloadFolder(PayloadLayout.GetPayloadDirectory("GameServer"), Path.Combine(InstallPath, "GameServer"));
        });
    }

    private async Task DeployClientPluginAsync()
    {
        await PayloadLayout.EnsurePayloadExtractedAsync();

        var destination = Path.Combine(InstallPath, "BepInEx", "plugins", "WorldsAdriftReborn");
        var clientPayload = PayloadLayout.GetPayloadDirectory("Client");

        await Task.Run(() =>
        {
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }

            Directory.CreateDirectory(destination);
            CopyPayloadFolder(clientPayload, destination);
        });
    }

    private static void CopyPayloadFolder(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Missing payload: {source}");
        }

        Directory.CreateDirectory(destination);
        foreach (var filePath in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, filePath);
            var targetPath = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(filePath, targetPath, overwrite: true);
        }
    }

    private void ExportDiagnostics()
    {
        var saveFileDialog = new SaveFileDialog
        {
            Title = "Export logs",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = "installer-logs.txt",
            DefaultExt = "txt",
            AddExtension = true
        };

        var result = saveFileDialog.ShowDialog();
        if (result != true)
        {
            return;
        }

        try
        {
            File.WriteAllLines(saveFileDialog.FileName, StatusLog);
            AppendStatusLine($"Logs exported to {saveFileDialog.FileName}");
        }
        catch (Exception ex)
        {
            AppendStatusLine($"Failed to export logs: {ex.Message}");
        }
    }

    private async Task LaunchAfterInstallAsync()
    {
        if (IsClientRunning)
        {
            await ShutdownLaunchedProcessesAsync();
            return;
        }

        var webServerPath = Path.Combine(InstallPath, "WebServer", "WorldsAdriftServer.exe");
        var gameServerPath = Path.Combine(InstallPath, "GameServer", "WorldsAdriftRebornGameServer.exe");
        var clientPath = Path.Combine(InstallPath, "UnityClient@Windows.exe");

        if (!File.Exists(webServerPath) || !File.Exists(gameServerPath) || !File.Exists(clientPath))
        {
            AppendStatusLine("Launch failed: install is missing required executables.");
            return;
        }

        try
        {
            AppendStatusLine("Starting WebServer...");
            _webServerProcess = StartProcessOrThrow(webServerPath);

            AppendStatusLine("Starting GameServer...");
            _gameServerProcess = StartProcessOrThrow(gameServerPath);

            AppendStatusLine("Starting client...");
            _clientProcess = StartProcessOrThrow(clientPath);
            _clientProcess.EnableRaisingEvents = true;
            _clientProcess.Exited += async (_, _) => await Application.Current.Dispatcher.InvokeAsync(async () => await ShutdownLaunchedProcessesAsync());

            IsClientRunning = true;
            UpdateLaunchButtonVisuals();
            AppendStatusLine("Client running");
        }
        catch (Exception ex)
        {
            AppendStatusLine($"Launch failed: {ex.Message}");
            await ShutdownLaunchedProcessesAsync();
        }
    }

    private Process StartProcessOrThrow(string fileName)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = Path.GetDirectoryName(fileName)!,
                UseShellExecute = false
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Unable to start {Path.GetFileName(fileName)}");
        }

        return process;
    }

    private async Task ShutdownLaunchedProcessesAsync()
    {
        await Task.Run(() =>
        {
            TryStopProcess(_clientProcess);
            TryStopProcess(_gameServerProcess);
            TryStopProcess(_webServerProcess);
        });

        _clientProcess = null;
        _gameServerProcess = null;
        _webServerProcess = null;

        IsClientRunning = false;
        UpdateLaunchButtonVisuals();
        AppendStatusLine("Launch closed");
    }

    private static void TryStopProcess(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(2000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
        catch
        {
        }
        finally
        {
            process.Dispose();
        }
    }

    private void UpdateLaunchButtonVisuals()
    {
        LaunchButtonText = IsClientRunning ? "Close" : "Launch";
        LaunchButtonBackground = IsClientRunning
            ? GetBrushFromResources("DangerBrush")
            : GetBrushFromResources("AccentBrush");
    }

    private void UpdateCurrentStepBrush(string step)
    {
        CurrentStepBrush = step switch
        {
            "Failed" => GetBrushFromResources("DangerBrush"),
            "Complete" => GetBrushFromResources("SuccessBrush"),
            _ => GetBrushFromResources("AccentBrush")
        };
    }

    private Brush GetBrushFromResources(string resourceKey)
    {
        return Application.Current.TryFindResource(resourceKey) as Brush
            ?? Brushes.Gray;
    }

    private void BrowseForInstallPath()
    {
        var picker = new FolderPicker();
        var folder = picker.PickFolder(InstallPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            InstallPath = folder;
        }
    }

    private void PersistState()
    {
        var nextState = _state with
        {
            ContentDownloadComplete = true,
            InstallPath = InstallPath,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        _stateStore.Save(nextState);
    }

    private string GetDefaultInstallPath()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return Path.Combine(programFiles, "WorldsAdriftReborn");
    }

    private void RefreshCommandStates()
    {
        (StartInstallCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RetryCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportDiagnosticsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LaunchAfterInstallCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (BrowseInstallPathCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
