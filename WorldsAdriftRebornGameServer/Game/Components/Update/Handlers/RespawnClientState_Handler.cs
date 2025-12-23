using Bossa.Travellers.Player;
using WorldsAdriftRebornGameServer.DLLCommunication;

namespace WorldsAdriftRebornGameServer.Game.Components.Update.Handlers
{
    [RegisterComponentUpdateHandler]
    internal class RespawnClientState_Handler : IComponentUpdateHandler<RespawnClientState, RespawnClientState.Update, RespawnClientState.Data>
    {
        public RespawnClientState_Handler() { Init(1093); }
        protected override void Init( uint ComponentId )
        {
            this.ComponentId = ComponentId;
        }
        public override void HandleUpdate(
            ENetPeerHandle player,
            long entityId, 
            RespawnClientState.Update clientComponentUpdate,
            RespawnClientState.Data serverComponentData)
        {
            foreach (var x in clientComponentUpdate.respawnDone)
            {
                Console.WriteLine($"INFO - Player sent RespawnDone for entity {x.targetEntityId}");
            }

            foreach (var x in clientComponentUpdate.getReviverShipInfoRequest)
            {
                Console.WriteLine($"INFO - Player sent getReviverShipInfoRequest");
            }

            foreach (var x in clientComponentUpdate.respawnAtNearestAncientRespawner)
            {
                Console.WriteLine($"INFO - Player sent nearest respawn request, Last Valid Biome {x.lastValidBiomeId}");
            }

            foreach (var x in clientComponentUpdate.respawnAtRandomAncientRespawner)
            {
                Console.WriteLine($"INFO - Player sent random respawn request, Last Valid Biome: {x.lastValidBiomeId}");
            }

            foreach (var x in clientComponentUpdate.respawnAtPersonalReviver)
            {
                Console.WriteLine($"INFO - Player requested respawn at personal reviver. Last Valid Biome: {x.lastValidBiomeId}");
            }
            
            

            clientComponentUpdate.ApplyTo(serverComponentData);
        }
    }
}
