namespace WorldsAdriftServer.Handlers.CharacterScreen
{
    public static class AppDataPaths
    {
        private const string Vendor = "WorldsReborn";
        private const string Product = "WorldsAdriftOffline";

        public static readonly string Base =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Vendor, Product);

        public static string Directory( string subfolder )
        {
            return Path.Combine(Base, subfolder);
        }
    }
}
