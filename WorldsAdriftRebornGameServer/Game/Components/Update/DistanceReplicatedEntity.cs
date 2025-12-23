using Improbable.Math;

namespace WorldsAdriftRebornGameServer.Game.Components.Update
{
    public readonly struct DistanceReplicatedEntity
    {
        public readonly long EntityId;
        public readonly Vector3f Position;
        public readonly string Prefab;
        public readonly Dictionary<uint, object> InitialDataInstances;
        public readonly float ReplicationDistance = 5000f;

        public DistanceReplicatedEntity(long entityId, Vector3f position, string prefab, Dictionary<uint, object> datas, float replicationDistance = 5000f)
        {
            EntityId = entityId;
            Position = position;
            Prefab = prefab;
            InitialDataInstances = datas;
            ReplicationDistance = replicationDistance;
        }
    }
}
