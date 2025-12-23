using Microsoft.Data.Sqlite;

namespace WorldsAdriftRebornGameServer.Game
{
    public static class ActivePlayerHelper
    {
        private static readonly string dbPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WorldsReborn",
                "WorldsAdriftOffline",
                "webserverdata",
                "characters.db"
            );

        private static string ConnectionString =>
            $"Data Source={dbPath};Mode=ReadWriteCreate";

        // cache it first time we need it incase someone decides to boot two offline instances xd
        private static (string Name, string CosmeticsJson, string UniversalColorsJson, string CharacterUid)? cachedData;

        public static (string Name, string CosmeticsJson, string UniversalColorsJson, string CharacterUid)? GetLoggedInData()
        {
            if (cachedData != null) return cachedData;
            
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    name,
                    cosmetics,
                    universalColors,
                    characterUid
                FROM characters
                WHERE logged_in = 1
                LIMIT 1;
            ";

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return null;

            cachedData = (
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)
            );
            Console.WriteLine($"INFO - Successfully got logged in data {cachedData.Value.Name}");
            return cachedData;
        }
    }
}
