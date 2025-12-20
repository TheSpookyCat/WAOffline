using System.ComponentModel;
using Bossa.Travellers.Inventory;
using Bossa.Travellers.Player;
using Bossa.Travellers.Weather;
using Improbable.Collections;
using Improbable.Corelibrary.Math;
using Improbable.Corelibrary.Transforms;
using Improbable.Math;
using Improbable.Worker.Internal;
using WorldsAdriftRebornGameServer.DLLCommunication;
using WorldsAdriftRebornGameServer.Game.Items;
using WorldsAdriftRebornGameServer.Networking.Wrapper;

namespace WorldsAdriftRebornGameServer.Game.Components.Update.Handlers
{
    public readonly struct DistanceReplicatedEntity
    {
        public readonly long EntityId;
        public readonly Vector3f Position;
        public readonly string Prefab;
        public readonly Dictionary<uint, object> InitialDataInstances;

        public DistanceReplicatedEntity(long entityId, Vector3f position, string prefab, Dictionary<uint, object> datas)
        {
            EntityId = entityId;
            Position = position;
            Prefab = prefab;
            InitialDataInstances = datas;
        }
    }
    
    public static class DistanceReplicationRegistry
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
        }
    }

    [RegisterComponentUpdateHandler]
    internal class TransformState_Handler : IComponentUpdateHandler<TransformState, TransformState.Update, TransformState.Data>
    {
        public TransformState_Handler() { Init(190602); }

        public static Dictionary<long, (Vector3f position, string prefab, Vector3f rotation)> ReplicatedByDistance =
            new Dictionary<long, (Vector3f position, string prefab, Vector3f rotation)>();

        private const float MaxDistance = 6000f;
        private const float MaxDistanceSqr = MaxDistance * MaxDistance;
        private static readonly Dictionary<long, HashSet<long>> SpawnedForPlayer = new();
        private static readonly Dictionary<long, DateTime> LastCheckTime = new();
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
        
        private static readonly double fixedPointToDoubleFactor = 1 << 12;
        public static FixedPointVector3 CreateFixedVector(float x, float y, float z)
        {
            return new FixedPointVector3(new Improbable.Collections.List<long>(3)
            {
                (long)(x * fixedPointToDoubleFactor),
                (long)(y * fixedPointToDoubleFactor),
                (long)(z * fixedPointToDoubleFactor),
            });
        }
        
        public static Vector3f CreateVector3f(FixedPointVector3 v)
        {
            return new Vector3f(
                (float)(v.fixedPointValues[0] / fixedPointToDoubleFactor),
                (float)(v.fixedPointValues[1] / fixedPointToDoubleFactor),
                (float)(v.fixedPointValues[2] / fixedPointToDoubleFactor)
            );
        }
        
        private static readonly Dictionary<long, CancellationTokenSource> ReplicationTokens = new();
        public static async Task SpawnNearbyEntitiesAsync(
            ENetPeerHandle peer,
            long playerEntityId,
            Vector3f playerPos,
            int delayMs = 500)
        {
            delayMs = Math.Clamp(delayMs, 200, 5000);

            if (ReplicationTokens.TryGetValue(playerEntityId, out var oldCts))
            {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            ReplicationTokens[playerEntityId] = cts;
            var token = cts.Token;

            if (!SpawnedForPlayer.TryGetValue(playerEntityId, out var spawned))
            {
                spawned = new HashSet<long>();
                SpawnedForPlayer[playerEntityId] = spawned;
            }

            try
            {
                foreach (var candidate in DistanceReplicationRegistry.AllEntities)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(1, token);

                    if (spawned.Contains(candidate.EntityId))
                        continue;

                    var delta = candidate.Position - playerPos;
                    var distSqr =
                        delta.X * delta.X +
                        delta.Y * delta.Y +
                        delta.Z * delta.Z;

                    if (distSqr > MaxDistanceSqr)
                        continue;

                    foreach (var data in candidate.InitialDataInstances)
                    {
                        WorldsAdriftRebornGameServer.AddComponent(
                            candidate.EntityId,
                            data.Key,
                            data.Value);
                    }

                    if (SendOPHelper.SendAddEntityOP(
                            peer,
                            candidate.EntityId,
                            candidate.Prefab,
                            "notNeeded?"))
                    {
                        spawned.Add(candidate.EntityId);
                        Console.WriteLine($"[replication] spawned {candidate.Prefab}");
                    }

                    await Task.Delay(delayMs, token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[replication] spawn cancelled (resync)");
            }
        }
        
        protected override void Init( uint ComponentId )
        {
            this.ComponentId = ComponentId;
        }
        public override void HandleUpdate(
            ENetPeerHandle player,
            long entityId,
            TransformState.Update clientComponentUpdate,
            TransformState.Data serverComponentData)
        {
            ComponentId = 190602;
            if (clientComponentUpdate.localPosition.HasValue)
            {
                var now = DateTime.UtcNow;

                if (!LastCheckTime.TryGetValue(entityId, out var last)
                    || now - last > CheckInterval)
                {
                    LastCheckTime[entityId] = now;

                    var pos = clientComponentUpdate.localPosition.Value;
                    var playerPos = CreateVector3f(pos);
                    Console.WriteLine($"Starting replication sync at {playerPos.ToString()}");
                    _ = SpawnNearbyEntitiesAsync(player, entityId, playerPos, 1500);
                }
            }

            clientComponentUpdate.ApplyTo(serverComponentData);

            // SendOPHelper.SendComponentUpdateOp(
            //     player,
            //     entityId,
            //     new System.Collections.Generic.List<uint> { ComponentId },
            //     new System.Collections.Generic.List<object> { clientComponentUpdate });
        }
    }
}
