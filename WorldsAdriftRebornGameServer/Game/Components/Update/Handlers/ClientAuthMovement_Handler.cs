using Bossa.Travellers.Player;
using WorldsAdriftRebornGameServer.DLLCommunication;
using WorldsAdriftRebornGameServer.Networking.Wrapper;

namespace WorldsAdriftRebornGameServer.Game.Components.Update.Handlers
{
    [RegisterComponentUpdateHandler]
    internal class ClientAuthMovement_Handler : IComponentUpdateHandler<ClientAuthoritativePlayerState, ClientAuthoritativePlayerState.Update, ClientAuthoritativePlayerState.Data>
    {
        public ClientAuthMovement_Handler() { Init(1073); }
        protected override void Init( uint ComponentId )
        {
            this.ComponentId = ComponentId;
        }
        public override void HandleUpdate(
            ENetPeerHandle player,
            long entityId,
            ClientAuthoritativePlayerState.Update clientComponentUpdate,
            ClientAuthoritativePlayerState.Data serverComponentData)
        {
            clientComponentUpdate.ApplyTo(serverComponentData);

            SendOPHelper.SendComponentUpdateOp(player, entityId, new List<uint> { ComponentId },
                new List<object> { clientComponentUpdate });
        }
    }
}
