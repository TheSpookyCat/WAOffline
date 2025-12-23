using Bossa.Travellers.Player;
using WorldsAdriftRebornGameServer.DLLCommunication;
using WorldsAdriftRebornGameServer.Networking.Wrapper;

namespace WorldsAdriftRebornGameServer.Game.Components.Update.Handlers
{
    [RegisterComponentUpdateHandler]
    internal class LogoutState_Handler : IComponentUpdateHandler<LogoutState, LogoutState.Update, LogoutState.Data>
    {
        public LogoutState_Handler() { Init(1145); }
        protected override void Init( uint ComponentId )
        {
            this.ComponentId = ComponentId;
        }
        public override void HandleUpdate(
            ENetPeerHandle player,
            long entityId,
            LogoutState.Update clientComponentUpdate,
            LogoutState.Data serverComponentData)
        {
            clientComponentUpdate.ApplyTo(serverComponentData);

            SendOPHelper.SendComponentUpdateOp(player, entityId, new List<uint> { ComponentId },
                new List<object> { clientComponentUpdate });
        }
    }
}
