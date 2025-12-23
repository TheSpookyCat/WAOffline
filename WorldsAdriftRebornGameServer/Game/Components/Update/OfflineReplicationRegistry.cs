using Bossa.Travellers.Weather;
using Improbable.Collections;
using Improbable.Corelibrary.Math;
using Improbable.Corelibrary.Transforms;
using Improbable.Math;
using Improbable.Worker.Internal;
using WorldsAdriftRebornGameServer.Game.Components.Update.Handlers;

namespace WorldsAdriftRebornGameServer.Game.Components.Update
{
    public static class OfflineReplicationRegistry
    {
        public static readonly System.Collections.Generic.List<DistanceReplicatedEntity> AllEntities = new();
        private static bool registered = false;

        public static void RegisterEntities()
        {
            if (registered) return;
            registered = true;
            foreach (var island in WorldMapData.Instance.Islands)
            {
                var entityId = WorldsAdriftRebornGameServer.NextEntityId;
                var prefab = island.Island.Replace(".json", "") + "@Island";

                var pos = new Vector3f(island.x, island.y, island.z);

                AllEntities.Add(new DistanceReplicatedEntity(
                    entityId,
                    pos,
                    prefab,
                    new Dictionary<uint, object>
                    {
                        [ComponentDatabase.MetaclassToId<TransformState>()] = new TransformState.Data(
                            TransformState_Handler.CreateFixedVector(pos.X, pos.Y, pos.Z),
                            new Quaternion32(1023),
                            new Option<Parent>(),
                            Vector3d.ZERO,
                            Vector3f.ZERO,
                            Vector3f.ZERO,
                            false,
                            1000
                        )
                    }
                ));
            }

            var wallId = 0;
            foreach (var wall in WorldMapData.Instance.Walls)
            {
                float x1 = wall.x1;
                float z1 = wall.z1;
                float x2 = wall.x2;
                float z2 = wall.z2;

                float dx = x2 - x1;
                float dz = z2 - z1;

                float totalLength = MathF.Sqrt(dx * dx + dz * dz);
                if (totalLength <= 0.001f)
                    continue;

                Vector3d forward = new Vector3d(dx, 0, dz);
                var mag = Math.Sqrt(forward.X * forward.X + forward.Y * forward.Y + forward.Z * forward.Z);
                var rot = forward / mag;

                const float segmentMaxSize = 800;
                int fullSegments = (int)(totalLength / segmentMaxSize);
                float remainder = totalLength - (fullSegments * segmentMaxSize);
                int totalSegments = remainder > 0 ? fullSegments + 1 : fullSegments;

                Vector3d start = new Vector3d(x1, 0, z1);
                float covered = 0f;

                ++wallId;
                for (int i = 0; i < totalSegments; i++)
                {
                    float segLength =
                        (i == totalSegments - 1 && remainder > 0)
                            ? remainder
                            : segmentMaxSize;

                    float half = segLength * 0.5f;
                    Vector3d pos = start + rot * (covered + half);
                    covered += segLength;

                    var wallPos = new Vector3f((float)pos.X, 0f, (float)pos.Z);
                    AllEntities.Add(new DistanceReplicatedEntity(
                        WorldsAdriftRebornGameServer.NextEntityId,
                        wallPos,
                        "WallSegment",
                        new Dictionary<uint, object>
                        {
                            [ComponentDatabase.MetaclassToId<WallSegmentState>()] = new WallSegmentState.Data(
                                wall.Type,
                                wallId,
                                rot,
                                half
                            ),
                            [ComponentDatabase.MetaclassToId<TransformState>()] = new TransformState.Data(
                                TransformState_Handler.CreateFixedVector(wallPos.X, wallPos.Y, wallPos.Z),
                                new Quaternion32(1023),
                                new Option<Parent>(),
                                Vector3d.ZERO,
                                Vector3f.ZERO,
                                Vector3f.ZERO,
                                false,
                                1000
                            )
                        }
                    ));
                }
            }

            foreach (var celldata in GlobalWeather.GetAllCells())
            {
                var entityId = WorldsAdriftRebornGameServer.NextEntityId;
                AllEntities.Add(new DistanceReplicatedEntity(entityId, new Vector3f(celldata.x, 0f, celldata.z), "WeatherCell", new Dictionary<uint, object>
                {
                    [ComponentDatabase.MetaclassToId<TransformState>()] = new TransformState.Data(
                        TransformState_Handler.CreateFixedVector(celldata.x, 0f, celldata.z),
                        new Quaternion32(1023),
                        null,
                        Vector3d.ZERO,
                        Vector3f.ZERO,
                        Vector3f.ZERO,
                        false,
                        0
                    ),
                    [ComponentDatabase.MetaclassToId<WeatherCellState>()] = new WeatherCellState.Data(celldata.cell.Pressure, celldata.cell.Wind)
                } ,10000f));
            }
        }
    }
}
