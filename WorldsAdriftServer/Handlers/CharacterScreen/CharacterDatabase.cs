using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using WorldsAdriftServer.Objects.CharacterSelection;

namespace WorldsAdriftServer.Handlers.CharacterScreen
{
    public static class CharacterDatabase
    {
        private static readonly string DbPath =
            Path.Combine(
                AppDataPaths.Directory("webserverdata"),
                "characters.db"
            );

        private static string ConnectionString =>
            $"Data Source={DbPath};Mode=ReadWriteCreate";

        public static void Setup()
        {
            var dir = Path.GetDirectoryName(DbPath)!;
            Directory.CreateDirectory(dir);

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                PRAGMA journal_mode=WAL;

                CREATE TABLE IF NOT EXISTS characters (
                    id INTEGER PRIMARY KEY,
                    characterUid TEXT NOT NULL UNIQUE,
                    name TEXT NOT NULL,
                    server TEXT NOT NULL,
                    serverIdentifier TEXT NOT NULL,

                    cosmetics TEXT NOT NULL,
                    universalColors TEXT NOT NULL,

                    isMale INTEGER NOT NULL,
                    seenIntro INTEGER NOT NULL,
                    skippedTutorial INTEGER NOT NULL,

                    logged_in INTEGER NOT NULL DEFAULT 0
                );
            ";

            command.ExecuteNonQuery();
        }

        internal static void Store(
            CharacterCreationData data,
            string cosmeticsJson,
            string universalColorsJson
        )
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO characters (
                    id,
                    characterUid,
                    name,
                    server,
                    serverIdentifier,
                    cosmetics,
                    universalColors,
                    isMale,
                    seenIntro,
                    skippedTutorial,
                    logged_in
                )
                VALUES (
                    $id,
                    $uid,
                    $name,
                    $server,
                    $serverId,
                    $cosmetics,
                    $colors,
                    $isMale,
                    $seenIntro,
                    $skippedTutorial,
                    $loggedIn
                )
                ON CONFLICT(characterUid) DO UPDATE SET
                    name = excluded.name,
                    server = excluded.server,
                    serverIdentifier = excluded.serverIdentifier,
                    cosmetics = excluded.cosmetics,
                    universalColors = excluded.universalColors,
                    isMale = excluded.isMale,
                    seenIntro = excluded.seenIntro,
                    skippedTutorial = excluded.skippedTutorial,
                    logged_in = excluded.logged_in;
            ";

            command.Parameters.AddWithValue("$id", data.Id);
            command.Parameters.AddWithValue("$uid", data.characterUid);
            command.Parameters.AddWithValue("$name", data.Name);
            command.Parameters.AddWithValue("$server", data.Server);
            command.Parameters.AddWithValue("$serverId", data.serverIdentifier);
            command.Parameters.AddWithValue("$cosmetics", cosmeticsJson);
            command.Parameters.AddWithValue("$colors", universalColorsJson);
            command.Parameters.AddWithValue("$isMale", data.isMale ? 1 : 0);
            command.Parameters.AddWithValue("$seenIntro", data.seenIntro ? 1 : 0);
            command.Parameters.AddWithValue("$skippedTutorial", data.skippedTutorial ? 1 : 0);
            command.Parameters.AddWithValue("$loggedIn", 0);

            command.ExecuteNonQuery();
        }

        internal static bool IsCharacterNameTaken(string characterName)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 1
                FROM characters
                WHERE name = $name
                LIMIT 1;
            ";

            command.Parameters.AddWithValue("$name", characterName);

            using var reader = command.ExecuteReader();
            return reader.Read();
        }

        internal static CharacterCreationData GetByCharacterUid(string characterUid)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    id,
                    characterUid,
                    name,
                    server,
                    serverIdentifier,
                    cosmetics,
                    universalColors,
                    isMale,
                    seenIntro,
                    skippedTutorial
                FROM characters
                WHERE characterUid = $uid;
            ";

            command.Parameters.AddWithValue("$uid", characterUid);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return null;

            return new CharacterCreationData(
                id: reader.GetInt32(0),
                characterUid: reader.GetString(1),
                name: reader.GetString(2),
                server: reader.GetString(3),
                serverIdentifier: reader.GetString(4),
                cosmetics: JsonConvert.DeserializeObject<Dictionary<CharacterSlotType, ItemData>>(reader.GetString(5)),
                universalColors: JsonConvert.DeserializeObject<CharacterUniversalColors>(reader.GetString(6)),
                isMale: reader.GetInt32(7) == 1,
                seenIntro: reader.GetInt32(8) == 1,
                skippedTutorial: reader.GetInt32(9) == 1
            );
        }

        internal static List<CharacterCreationData> GetAllCharacters()
        {
            var results = new List<CharacterCreationData>();

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    id,
                    characterUid,
                    name,
                    server,
                    serverIdentifier,
                    cosmetics,
                    universalColors,
                    isMale,
                    seenIntro,
                    skippedTutorial
                FROM characters
                ORDER BY id;
            ";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new CharacterCreationData(
                    id: reader.GetInt32(0),
                    characterUid: reader.GetString(1),
                    name: reader.GetString(2),
                    server: reader.GetString(3),
                    serverIdentifier: reader.GetString(4),
                    cosmetics: JsonConvert.DeserializeObject<Dictionary<CharacterSlotType, ItemData>>(reader.GetString(5)),
                    universalColors: JsonConvert.DeserializeObject<CharacterUniversalColors>(reader.GetString(6)),
                    isMale: reader.GetInt32(7) == 1,
                    seenIntro: reader.GetInt32(8) == 1,
                    skippedTutorial: reader.GetInt32(9) == 1
                ));
            }

            return results;
        }

        public static void DeleteByCharacterUid(string characterUid)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM characters
                WHERE characterUid = $uid;
            ";

            command.Parameters.AddWithValue("$uid", characterUid);
            command.ExecuteNonQuery();
        }

        internal static void SetLoggedInCharacter(string characterUid)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            using (var clearCmd = connection.CreateCommand())
            {
                clearCmd.Transaction = transaction;
                clearCmd.CommandText = @"
                    UPDATE characters
                    SET logged_in = 0
                    WHERE logged_in = 1;
                ";
                clearCmd.ExecuteNonQuery();
            }

            using (var setCmd = connection.CreateCommand())
            {
                setCmd.Transaction = transaction;
                setCmd.CommandText = @"
                    UPDATE characters
                    SET logged_in = 1
                    WHERE characterUid = $uid;
                ";
                setCmd.Parameters.AddWithValue("$uid", characterUid);
                setCmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }
}
