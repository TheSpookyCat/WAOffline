using System.IO;
using System.Runtime.Versioning;

namespace InstallerBootstrapper.Infrastructure.Logging;

[SupportedOSPlatform("windows")]
public static class LogPaths
{
    private const string Vendor = "WorldsReborn";
    private const string Product = "WorldsAdriftOffline";

    public static string GetLogDirectory()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, Vendor, Product, "logs");
    }

    public static string GetStateDirectory()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, Vendor, Product, "state");
    }
}
