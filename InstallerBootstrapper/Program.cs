using System.IO;

namespace InstallerBootstrapper
{
    internal static class Program
    {
        [STAThread]
        public static void Main()
        {
            var extractDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "InstallerBootstrapper",
                "runtime"
            );
        
            Directory.CreateDirectory(extractDir);
        
            Environment.SetEnvironmentVariable(
                "DOTNET_BUNDLE_EXTRACT_BASE_DIR",
                extractDir
            );

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
