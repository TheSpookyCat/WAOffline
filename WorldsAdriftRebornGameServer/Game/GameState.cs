using System.Text.Json;
using WorldsAdriftRebornGameServer.DLLCommunication;

namespace WorldsAdriftRebornGameServer.Game
{
    public sealed class WorldMapData
    {
        public WorldInfoData? WorldInfo { get; set; }
        public HavenData? Haven { get; set; }
        public List<IslandData>? Islands { get; set; }
        public List<BiomeData>? Biomes { get; set; }
        public List<WallData>? Walls { get; set; }

        private static readonly string Path = System.IO.Path.Combine(AppContext.BaseDirectory, "Game", "shard_sunset.json");
        public static WorldMapData Instance => instance ?? Load();
        private static WorldMapData? instance;

        private static WorldMapData Load()
        {
            try
            {
                var json = File.ReadAllText(Path);

                var result = JsonSerializer.Deserialize<WorldMapData>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result != null) return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR - Failed to load file {Path}: {ex.Message}\n{ex.StackTrace}");
                return new WorldMapData();
            }

            Console.WriteLine($"ERROR - No world data found at {Path}!");
            return new WorldMapData();
        }
    }

    public sealed class WorldInfoData
    {
        public string GSIMConfig { get; set; }
        public int WorldEdgeLength { get; set; }
    }

    public sealed class HavenData
    {
        public float xOfVerticalSeparator { get; set; }
    }

    public sealed class IslandData
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public string Island { get; set; }
    }

    public sealed class BiomeData
    {
        public float x { get; set; }
        public float z { get; set; }
        public int Type { get; set; }
        public int Civ { get; set; }
        public string District { get; set; }
    }

    public sealed class WallData
    {
        public float x1 { get; set; }
        public float z1 { get; set; }
        public float x2 { get; set; }
        public float z2 { get; set; }
        public int Type { get; set; }
    }
    
    internal class SyncStep
    {
        public GameState.NextStateRequirement NextStateRequirement { get; set; }
        public Action<object> Step { get; set; } = new Action<object>((object o) => { });
        public SyncStep(GameState.NextStateRequirement req, Action<object> action)
        {
            NextStateRequirement = req;
            Step = action;
        }
    }
    internal class GameState
    {
        public enum NextStateRequirement
        {
            NOTHING,
            ASSET_LOADED_RESPONSE,
            ADDED_ENTITY_RESPONSE
        }

        private static GameState instance { get; set; }
        public static GameState Instance
        {
            get
            {
                return instance ?? (instance = new GameState());
            }
        }
        // each world chunk has a list of actions that needs to be perfomred for every client to sync up to the current state.
        public Dictionary<int, List<SyncStep>> WorldState { get; set; }
        // we need to store mappings for components for each player to handle updates of them
        // this crappy dictionary nesting needs to be replaced, we need to make use of the games own structures here, this is only temporary
        public Dictionary<ENetPeerHandle, Dictionary<long, Dictionary<uint, ulong>>> ComponentMap = new Dictionary<ENetPeerHandle, Dictionary<long, Dictionary<uint, ulong>>>();

        private GameState()
        {
            WorldState = new Dictionary<int, List<SyncStep>>
            {
                { 0, new List<SyncStep>() }
            };
        }
    }
}
