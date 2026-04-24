using System.Text;
using RPVoiceChat.Config;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Items
{
    public class ItemTelegraphWire : Item
    {
        private int MaxConnectionDistance => ServerConfigManager.TelegraphMaxConnectionDistance;
        private BlockPos firstNodePos;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null)
            {
                // Let base behaviors execute (like GroundStorable)
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            var node = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as IWireConnectable;
            if (node == null)
            {
                // No node detected, let base behaviors execute
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            handling = EnumHandHandling.PreventDefault;

            // First click
            if (firstNodePos == null)
            {
                if (node is BEWireNode firstWireNode && !firstWireNode.CanAcceptMoreConnections())
                {
                    (byEntity.World.Api as ICoreClientAPI)?.TriggerIngameError(
                        this,
                        "wire-max-connections",
                        UIUtils.I18n("Wire.ConnectionDenied.MaxConnections", firstWireNode.GetMaxConnections())
                    );
                    return;
                }

                firstNodePos = blockSel.Position.Copy();
                (byEntity.World.Api as ICoreClientAPI)?.TriggerChatMessage(UIUtils.I18n("Wire.StartConnection"));
                return;
            }

            // Second click
            var firstNode = byEntity.World.BlockAccessor.GetBlockEntity(firstNodePos) as IWireConnectable;
            if (firstNode == null)
            {
                firstNodePos = null;
                return;
            }

            if (firstNode != null && firstNode != node)
            {
                double dist = firstNodePos.DistanceTo(node.Position);
                if (dist > MaxConnectionDistance)
                {
                    (byEntity.Api as ICoreClientAPI)?.TriggerChatMessage(UIUtils.I18n("Wire.ConnectionTooFar", MaxConnectionDistance));
                }
                else
                {
                    WireConnection connection = new WireConnection(firstNode, node);

                    // Keep item logic generic (IWireConnectable) while still using BEWireNode network logic.
                    bool connected = false;
                    string failureLangKey = null;
                    object[] failureArgs = System.Array.Empty<object>();

                    if (firstNode is BEWireNode firstWireNode)
                    {
                        connected = firstWireNode.Connect(connection, out failureLangKey, out failureArgs); // Connect() handles network creation/merge
                    }
                    else if (node is BEWireNode secondWireNode)
                    {
                        connected = secondWireNode.Connect(connection, out failureLangKey, out failureArgs);
                    }
                    else
                    {
                        firstNodePos = null;
                        return;
                    }

                    if (!connected)
                    {
                        if (!string.IsNullOrWhiteSpace(failureLangKey))
                        {
                            var capi = byEntity.Api as ICoreClientAPI;
                            string message = UIUtils.I18n(failureLangKey, failureArgs);
                            bool isConnectionDenied = failureLangKey.StartsWith("Wire.ConnectionDenied.", System.StringComparison.Ordinal);
                            if (isConnectionDenied)
                            {
                                capi?.TriggerIngameError(this, "wire-connection-denied", message);
                            }
                            else
                            {
                                capi?.TriggerChatMessage(message);
                            }
                        }
                        else
                        {
                            (byEntity.Api as ICoreClientAPI)?.TriggerChatMessage(UIUtils.I18n("Wire.ConnectionFailed"));
                        }
                        firstNodePos = null;
                        return;
                    }

                    (byEntity.Api as ICoreClientAPI)?.TriggerChatMessage(UIUtils.I18n("Wire.ConnectionSuccess"));

                    slot.TakeOut(1);
                    slot.MarkDirty();
                }
            }

            firstNodePos = null; // Reset
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            base.OnHeldIdle(slot, byEntity);
            if (firstNodePos != null && byEntity?.Api is ICoreClientAPI capi && byEntity == capi.World.Player.Entity)
            {
                capi.TriggerIngameError(this, "wire-pending", UIUtils.I18n("Wire.Pending", firstNodePos));
            }
        }
    }
}


