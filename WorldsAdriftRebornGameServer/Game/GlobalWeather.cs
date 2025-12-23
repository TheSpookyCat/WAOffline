using Bossa.Travellers.Weather;
using Improbable.Corelibrary.Transforms;
using Improbable.Math;
using WorldsAdriftRebornGameServer.Game.Components.Update.Handlers;

namespace WorldsAdriftRebornGameServer.Game
{
    public static class GlobalWeather
    {
        public const float CellSpacing = 500f;
        public static readonly float WorldEdgeLength = WorldMapData.Instance.WorldInfo.WorldEdgeLength;
        public static readonly float HalfWorld = WorldEdgeLength / 2;
        public static readonly int CellsPerAxis = (int) Math.Floor(WorldEdgeLength / CellSpacing);

        public struct WeatherCell
        {
            public float Pressure;
            public Vector3f Wind;
        }

        private static readonly Dictionary<(int x, int z), WeatherCell> _cells;
        private static readonly Random _rng;

        static GlobalWeather()
        {
            _rng = new Random();
            _cells = new Dictionary<(int, int), WeatherCell>(CellsPerAxis * CellsPerAxis);
            GenerateAllCells();
        }

        private static void GenerateAllCells()
        {
            int half = CellsPerAxis / 2;
            for (int x = -half; x < half; x++)
            {
                for (int z = -half; z < half; z++)
                {
                    _cells[(x, z)] = GenerateCell();
                }
            }
        }

        private static WeatherCell GenerateCell()
        {
            float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);
            float magnitude = 5f + (float)_rng.NextDouble() * 20f;

            return new WeatherCell
            {
                Pressure = (float)_rng.NextDouble(),
                Wind = new Vector3f(
                    (float)Math.Cos(angle) * magnitude,
                    0f,
                    (float)Math.Sin(angle) * magnitude
                )
            };
        }

        private static int ToCellCoord(float v)
        {
            if (v >= HalfWorld) v = HalfWorld - 0.0001f;
            if (v < -HalfWorld) v = -HalfWorld;
            return (int)Math.Floor(v / CellSpacing);
        }
        
        public static IEnumerable<(float x, float z, WeatherCell cell)> GetAllCells()
        {
            foreach (var kv in _cells)
            {
                yield return (
                    kv.Key.x * CellSpacing,
                    kv.Key.z * CellSpacing,
                    kv.Value
                );
            }
        }

        public static WeatherCellState.Data? GetWeatherFor( long entityId )
        {
            if (!WorldsAdriftRebornGameServer.ComponentOverrideMap.TryGetValue(entityId, out var comps)) return null;
            if (!comps.TryGetValue(190602, out var obj) || obj is not TransformState.Data tansform) return null;
            
            var cell = GetWeatherAt(TransformState_Handler.CreateVector3f(tansform.Get().Value.localPosition));
            return new WeatherCellState.Data(cell.Pressure, cell.Wind);
        }

        public static WeatherCell GetWeatherAt(Vector3f worldPos)
        {
            var key = (ToCellCoord(worldPos.X), ToCellCoord(worldPos.Z));
            return _cells.GetValueOrDefault(key);
        }
    }
}
