using Bossa.Travellers.Weather;
using Improbable.Corelibrary.Transforms;
using Improbable.Math;
using WorldsAdriftRebornGameServer.Game.Components.Update.Handlers;

namespace WorldsAdriftRebornGameServer.Game
{
    public static class GlobalWeather
    {
        public const float CellSpacing = 500f;

        public static readonly float WorldEdgeLength =
            WorldMapData.Instance.WorldInfo.WorldEdgeLength;

        public static readonly float HalfWorld =
            WorldEdgeLength * 0.5f;

        public static readonly int CellsPerAxis =
            (int)Math.Round(WorldEdgeLength / CellSpacing);

        private static readonly float GridStart =
            -(CellsPerAxis * CellSpacing * 0.5f) + CellSpacing;

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
            for (int x = 0; x < CellsPerAxis; x++)
            {
                for (int z = 0; z < CellsPerAxis; z++)
                {
                    _cells[(x, z)] = GenerateCell(x, z);
                }
            }
        }

        private static WeatherCell GenerateCell(int cellX, int cellZ)
        {
            return new WeatherCell
            {
                Pressure = 0.5f,
                Wind = new Vector3f(
                    1,
                    0f,
                    -2f
                )
            };
        }

        private static int ToCellCoord(float v)
        {
            if (v >= HalfWorld) v = HalfWorld - 0.0001f;
            if (v < -HalfWorld) v = -HalfWorld;
            return (int)Math.Floor(v / CellSpacing);
        }

        private static (int x, int z) WorldToGrid(Vector3f pos)
        {
            int gx = ToCellCoord(pos.X);
            int gz = ToCellCoord(pos.Z);

            int ix = gx + (CellsPerAxis / 2) - 1;
            int iz = gz + (CellsPerAxis / 2) - 1;

            return (ix, iz);
        }

        public static IEnumerable<(float x, float z, WeatherCell cell)> GetAllCells()
        {
            foreach (var kv in _cells)
            {
                yield return (
                    GridStart + kv.Key.x * CellSpacing,
                    GridStart + kv.Key.z * CellSpacing,
                    kv.Value
                );
            }
        }

        public static WeatherCell GetWeatherAt(Vector3f worldPos)
        {
            var (ix, iz) = WorldToGrid(worldPos);
            return _cells.GetValueOrDefault((ix, iz));
        }

        public static WeatherCellState.Data? GetWeatherFor(long entityId)
        {
            if (!WorldsAdriftRebornGameServer.ComponentOverrideMap.TryGetValue(entityId, out var comps))
                return null;

            if (comps.TryGetValue(1139, out var windData)) return (WeatherCellState.Data) windData;

            if (!comps.TryGetValue(190602, out var obj) || obj is not TransformState.Data transform)
                return null;

            var pos = TransformState_Handler.CreateVector3f(transform.Get().Value.localPosition);
            var cell = GetWeatherAt(pos);

            var data = new WeatherCellState.Data(cell.Pressure, cell.Wind);

            comps[1139] = data;
            return data;
        }
    }
}
