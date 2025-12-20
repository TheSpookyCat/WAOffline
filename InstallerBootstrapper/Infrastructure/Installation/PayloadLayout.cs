using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace InstallerLauncher.Infrastructure.Installation;

public static class PayloadLayout
{
    public const string PayloadFolderName = "InstallerPayloads";

    public static string GetPayloadRoot() => Path.Combine(AppContext.BaseDirectory, PayloadFolderName);

    public static async Task<string> EnsurePayloadExtractedAsync()
    {
        var payloadRoot = GetPayloadRoot();

        if (Directory.Exists(payloadRoot) && Directory.EnumerateFileSystemEntries(payloadRoot).Any())
        {
            return payloadRoot;
        }

        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("PayloadData.zip", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException("Embedded payload archive 'PayloadData.zip' not found.");
        }

        await using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Failed to open embedded payload archive.");

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"InstallerPayload_{Guid.NewGuid():N}.zip");
        await using (var file = File.Create(tempZipPath))
        {
            await resourceStream.CopyToAsync(file);
        }

        var payloadHash = await ComputeSha256Async(tempZipPath);
        var payloadHashPath = Path.Combine(payloadRoot, ".payloadhash");

        if (Directory.Exists(payloadRoot) && Directory.EnumerateFileSystemEntries(payloadRoot).Any())
        {
            if (File.Exists(payloadHashPath))
            {
                var existingHash = await File.ReadAllTextAsync(payloadHashPath);
                if (string.Equals(existingHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tempZipPath);
                    return payloadRoot;
                }
            }
        }

        var tempExtractDir = Path.Combine(Path.GetTempPath(), $"InstallerPayload_{Guid.NewGuid():N}");
        ZipFile.ExtractToDirectory(tempZipPath, tempExtractDir, overwriteFiles: true);
        File.Delete(tempZipPath);

        var extractedRoot = Path.Combine(tempExtractDir, PayloadFolderName);
        var contentRoot = Directory.Exists(extractedRoot) ? extractedRoot : tempExtractDir;

        if (Directory.Exists(payloadRoot))
        {
            Directory.Delete(payloadRoot, recursive: true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(payloadRoot)!);

        if (string.Equals(Path.GetPathRoot(contentRoot), Path.GetPathRoot(payloadRoot), StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(contentRoot, payloadRoot);
        }
        else
        {
            Directory.CreateDirectory(payloadRoot);
            CopyDirectory(contentRoot, payloadRoot);
        }

        await File.WriteAllTextAsync(payloadHashPath, payloadHash);

        if (Directory.Exists(tempExtractDir))
        {
            Directory.Delete(tempExtractDir, recursive: true);
        }

        return payloadRoot;
    }

    public static string GetPayloadDirectory(string payloadName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadName);
        return Path.Combine(GetPayloadRoot(), payloadName);
    }

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, directory);
            var targetDir = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(targetDir);
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var targetFile = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite: true);
        }
    }
}
