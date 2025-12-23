using System.ComponentModel;
using Bossa.Travellers.Inventory;
using Bossa.Travellers.Player;
using Improbable.Corelibrary.Math;
using Improbable.Corelibrary.Transforms;
using Improbable.Math;
using WorldsAdriftRebornGameServer.DLLCommunication;
using WorldsAdriftRebornGameServer.Game.Items;
using WorldsAdriftRebornGameServer.Networking.Wrapper;

namespace WorldsAdriftRebornGameServer.Game.Components.Update.Handlers
{
    [RegisterComponentUpdateHandler]
    internal class TransformState_Handler : IComponentUpdateHandler<TransformState, TransformState.Update, TransformState.Data>
    {
        public TransformState_Handler() { Init(190602); }

        public static Dictionary<long, (Vector3f position, string prefab, Vector3f rotation)> ReplicatedByDistance =
            new Dictionary<long, (Vector3f position, string prefab, Vector3f rotation)>();

        public static readonly Dictionary<long, HashSet<long>> SpawnedForPlayer = new();
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
                var toAdd = new System.Collections.Generic.List<(DistanceReplicatedEntity, float)>();
                foreach (var candidate in OfflineReplicationRegistry.AllEntities)
                {
                    token.ThrowIfCancellationRequested();

                    if (spawned.Contains(candidate.EntityId))
                        continue;

                    var dx = candidate.Position.X - playerPos.X;
                    var dz = candidate.Position.Z - playerPos.Z;
                    var distSqr = dx * dx + dz * dz;

                    if (distSqr > candidate.ReplicationDistance * candidate.ReplicationDistance)
                        continue;

                    toAdd.Add((candidate, distSqr));
                }
                toAdd.Sort((x, y) => x.Item2.CompareTo(y.Item2));
                
                foreach (var tuple in toAdd)
                {
                    token.ThrowIfCancellationRequested();
                    var candidate = tuple.Item1;
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

                if (!LastCheckTime.TryGetValue(entityId, out var last))
                {
                    last = now + CheckInterval;
                    LastCheckTime[entityId] = last;
                }
                if (now - last > CheckInterval)
                {
                    LastCheckTime[entityId] = now;

                    var pos = clientComponentUpdate.localPosition.Value;
                    var playerPos = CreateVector3f(pos);
                    Console.WriteLine($"Starting replication sync at {playerPos.ToString()}");
                    _ = SpawnNearbyEntitiesAsync(player, entityId, playerPos, 2500);
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
