using Bossa.Travellers.Player;
using Bossa.Travellers.Rope;
using WorldsAdriftRebornGameServer.DLLCommunication;
using WorldsAdriftRebornGameServer.Networking.Wrapper;

namespace WorldsAdriftRebornGameServer.Game.Components.Update.Handlers
{
    [RegisterComponentUpdateHandler]
    internal class RopeControlPoints_Handler : IComponentUpdateHandler<RopeControlPoints, RopeControlPoints.Update, RopeControlPoints.Data>
    {
        public RopeControlPoints_Handler() { Init(1098); }
        protected override void Init( uint ComponentId )
        {
            this.ComponentId = ComponentId;
        }
        public override void HandleUpdate(
            ENetPeerHandle player,
            long entityId,
            RopeControlPoints.Update clientComponentUpdate,
            RopeControlPoints.Data serverComponentData)
        {
            
            clientComponentUpdate.ApplyTo(serverComponentData);

            SendOPHelper.SendComponentUpdateOp(player, entityId, new List<uint> { ComponentId },
                new List<object> { clientComponentUpdate });
        }
    }
}
