using Bossa.Travellers.Controls;
using Improbable;
using Improbable.Collections;
using Improbable.Corelibrary.Math;
using Improbable.Corelibrary.Transforms;
using Improbable.Worker.Internal;
using WorldsAdriftRebornGameServer.DLLCommunication;
using WorldsAdriftRebornGameServer.Networking.Wrapper;

namespace WorldsAdriftRebornGameServer.Game.Components.Update.Handlers
{
    [RegisterComponentUpdateHandler]
    internal class CharacterControlsData_Handler : IComponentUpdateHandler<CharacterControlsData, CharacterControlsData.Update, CharacterControlsData.Data>
    {
        public CharacterControlsData_Handler() { Init(1072); }
        protected override void Init( uint ComponentId )
        {
            this.ComponentId = ComponentId;
        }
        public override void HandleUpdate(
            ENetPeerHandle player,
            long entityId,
            CharacterControlsData.Update clientComponentUpdate,
            CharacterControlsData.Data serverComponentData)
        {
            foreach (var x in clientComponentUpdate.respawn)
            {
                Console.WriteLine($"INFO - Player sent respawn request!");

                if (!TransformState_Handler.SpawnedForPlayer.TryGetValue(entityId, out var spawned))
                {
                    Console.WriteLine("Can't find an island to teleport player to");
                    continue;
                }

                DistanceReplicatedEntity nearest;
                try
                {
                    nearest = OfflineReplicationRegistry.AllEntities.First(x =>
                        x.Prefab.Contains("Island") && spawned.Contains(x.EntityId));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error finding nearest island: {ex.Message}\n{ex.StackTrace}");
                    continue;
                }

                SendOPHelper.SendAuthorityChangeOp(player, entityId,
                    new System.Collections.Generic.List<uint> { 190602 }, false);
                // parenting the player SHOULD force them to 0,0,0 of their parent, but the player won't be able to move if they stay attached
                // not currently working
                SendOPHelper.SendComponentUpdateOp(player, entityId, new System.Collections.Generic.List<uint>
                {
                    ComponentDatabase.MetaclassToId<TransformState>(),
                }, new System.Collections.Generic.List<object>
                {
                    new TransformState.Update().SetLocalPosition(new FixedPointVector3(new Improbable.Collections.List<long>{0,0,0})).SetParent(new Option<Parent>(new Parent(new EntityId(nearest.EntityId), "Island")))
                });
                // SendOPHelper.SendComponentUpdateOp(player, entityId, new System.Collections.Generic.List<uint>
                // {
                //     ComponentDatabase.MetaclassToId<TransformState>(),
                // }, new System.Collections.Generic.List<object>
                // {
                //     new TransformState.Update().SetParent(null)
                // });
                Console.WriteLine($"INFO - Finished respawn teleport! ({nearest.EntityId} {nearest.Prefab})");
            }

            clientComponentUpdate.ApplyTo(serverComponentData);
        }
    }
}
