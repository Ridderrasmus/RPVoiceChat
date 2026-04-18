using RPVoiceChat.GameContent.Systems;

using RPVoiceChat.Gui;

using RPVoiceChat.Networking.Packets;

using RPVoiceChat.Systems;

using RPVoiceChat.Util;

using System;

using System.Collections.Generic;

using System.Linq;

using System.Text;

using Vintagestory.API.Client;

using Vintagestory.API.Common;

using Vintagestory.API.Datastructures;

using Vintagestory.API.MathTools;

using Vintagestory.API.Server;

using Vintagestory.GameContent.Mechanics;



namespace RPVoiceChat.GameContent.BlockEntity

{

    public class BlockEntitySwitchboard : BEWireNode, IWireTypedNode, ISwitchboardNode

    {

        private GuiDialogSwitchboard dialog;



        /// <summary>

        /// Last known custom network name for this block (mirrors <see cref="WireNetwork.CustomName"/> when loaded).

        /// Persisted so the label survives chunk reload / server restart; the in-memory <see cref="WireNetwork"/> is not saved.

        /// </summary>

        private string persistedNetworkCustomName = "";



        private bool networkNameApplyPending;
        private bool usePowerRequirements = true;

        /// <summary>BellHammer-style: only <see cref="MarkDirty"/> when the displayed integer % changes, so clients receive updates via tree sync.</summary>
        private int _lastSyncedPowerPercent = -1;

        /// <summary>Detects crossing the configured min-power threshold when the integer % is unchanged.</summary>
        private bool _lastAboveMinPower;



        public WireNodeKind WireNodeKind => WireNodeKind.Switchboard;

        public float PowerPercent { get; private set; }
        public bool UsePowerRequirements => usePowerRequirements;



        protected override int MaxConnections => 4;



        public override void Initialize(ICoreAPI api)

        {

            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)

            {

                (api as ICoreServerAPI)?.Event.RegisterGameTickListener(OnServerSwitchboardTick, 100);

                TryDiscoverNetwork();

            }

            else

            {

                RegisterGameTickListener(_ => TryApplyPersistedNetworkName(), 1000);

            }

        }



        public void TryDiscoverNetwork()

        {

            if (Block?.Variant == null || !Block.Variant.TryGetValue("side", out string sideStr)) return;

            BlockFacing frontFace = BlockFacing.FromCode(sideStr);

            if (frontFace == null) return;

            BlockFacing connectorFace = frontFace.Opposite;



            var mechBase = GetBehavior<BEBehaviorMPBase>();

            mechBase?.CreateJoinAndDiscoverNetwork(connectorFace);

        }



        /// <summary>Same idea as <see cref="BlockEntityBellHammer.OnServerGameTick"/>: read TrueSpeed on the server and sync <see cref="PowerPercent"/> to clients with MarkDirty when the UI % or threshold state changes.</summary>
        private void OnServerSwitchboardTick(float dt)

        {

            TryApplyPersistedNetworkName();

            if (Api?.World?.BlockAccessor?.GetBlockEntity(Pos) != this)

            {

                return;

            }

            var consumer = GetBehavior<BEBehaviorMPConsumer>();

            float speed = consumer != null ? GameMath.Clamp(consumer.TrueSpeed, 0f, 1f) : 0f;

            PowerPercent = speed;

            WireNetworkKind currentKind = ResolveEffectiveNetworkKind();

            float minPower = WireNetworkTypeRules.GetRequirements(currentKind).MinPowerPercent;

            int percentDisplay = (int)(speed * 100f);

            bool aboveMin = speed >= minPower;

            bool intChanged = percentDisplay != _lastSyncedPowerPercent;

            bool thresholdChanged = aboveMin != _lastAboveMinPower;

            if (!intChanged && !thresholdChanged)

            {

                return;

            }

            _lastSyncedPowerPercent = percentDisplay;

            _lastAboveMinPower = aboveMin;

            MarkDirty();

            if (NetworkUID != 0)

            {

                WireNetworkHandler.RebuildNetworkState(NetworkUID);

            }

            if (IsDialogOpen())

            {

                dialog?.RefreshData();

            }

        }



        public override void ToTreeAttributes(ITreeAttribute tree)

        {

            if (Api?.Side == EnumAppSide.Server && NetworkUID != 0)

            {

                var n = WireNetworkHandler.GetNetwork(NetworkUID);

                if (n != null)

                {

                    persistedNetworkCustomName = n.CustomName ?? "";
                    WireNetworkHandler.SetPersistedNetworkName(NetworkUID, persistedNetworkCustomName);

                }

            }



            base.ToTreeAttributes(tree);

            tree.SetFloat("switchboardPowerPercent", PowerPercent);
            tree.SetBool("switchboardUsePowerRequirements", usePowerRequirements);

            tree.SetString("savedNetworkCustomName", persistedNetworkCustomName ?? "");

        }



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)

        {

            persistedNetworkCustomName = tree.GetString("savedNetworkCustomName", "");

            networkNameApplyPending = true;



            base.FromTreeAttributes(tree, worldForResolving);

            PowerPercent = tree.GetFloat("switchboardPowerPercent", 0f);
            usePowerRequirements = tree.GetBool("switchboardUsePowerRequirements", true);

            if (NetworkUID != 0)

            {

                WireNetworkHandler.SetPersistedNetworkName(NetworkUID, persistedNetworkCustomName);
                WireNetworkHandler.RebuildNetworkState(NetworkUID);

            }

            if (IsDialogOpen())

            {

                dialog?.RefreshData();

            }

        }



        /// <summary>

        /// Restores <see cref="CommunicationNetworkBase.CustomName"/> from chunk data once the in-memory network exists (server and client).

        /// </summary>

        private void TryApplyPersistedNetworkName()

        {

            if (!networkNameApplyPending)

            {

                return;

            }



            if (NetworkUID == 0)

            {

                return;

            }



            var net = WireNetworkHandler.GetNetwork(NetworkUID);

            if (net == null)

            {

                return;

            }



            WireNetworkHandler.SetPersistedNetworkName(NetworkUID, persistedNetworkCustomName);

            networkNameApplyPending = false;

        }



        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)

        {

            string displayName = GetNetworkDisplayName();

            if (!string.IsNullOrWhiteSpace(displayName))

            {

                dsc.AppendLine(UIUtils.I18n("Switchboard.NetworkName", displayName));

            }

        }



        public bool HasSufficientPowerFor(WireNetworkKind networkKind)

        {
            WireNetworkRequirements requirements = WireNetworkTypeRules.GetRequirements(networkKind);

            // Below mechanical threshold: toggle has no effect — network never counts as "powered" for advanced features.
            if (PowerPercent < requirements.MinPowerPercent)
            {
                return false;
            }

            // At or above threshold: advanced telegraph options are enabled only when the toggle is on.
            return usePowerRequirements;

        }



        public bool OnInteract()

        {

            if (Api?.Side != EnumAppSide.Client)

            {

                return true;

            }



            if (Api is not ICoreClientAPI capi)

            {

                return true;

            }



            if (dialog?.IsOpened() == true)

            {

                return true;

            }



            dialog = new GuiDialogSwitchboard(capi, this);

            dialog.TryOpen();

            return true;

        }



        public string GetNetworkDisplayName()

        {

            if (NetworkUID == 0)

            {

                return "";

            }



            return WireNetworkHandler.GetDisplayName(NetworkUID);

        }



        /// <summary>Custom network label only (empty if unset). Never the numeric network id â€” for editable fields.</summary>

        public string GetNetworkCustomNameForEditor()

        {

            if (NetworkUID == 0)

            {

                return "";

            }



            var network = WireNetworkHandler.GetNetwork(NetworkUID);
            if (network != null)
            {
                return network.CustomName ?? "";
            }

            return WireNetworkHandler.GetPersistedNetworkName(NetworkUID);

        }



        public bool RenameNetwork(string name, out string failureLangKey)

        {

            if (NetworkUID == 0)

            {

                failureLangKey = "Network.NoNetwork";

                return false;

            }



            bool success = WireNetworkHandler.TryRenameNetwork(NetworkUID, name, out failureLangKey);

            if (success)

            {

                var net = WireNetworkHandler.GetNetwork(NetworkUID);

                persistedNetworkCustomName = net?.CustomName ?? "";
                WireNetworkHandler.SetPersistedNetworkName(NetworkUID, persistedNetworkCustomName);

                networkNameApplyPending = false;

                MarkDirty(true);

            }

            return success;

        }



        public void RequestRenameNetwork(string desiredName)

        {

            if (Api?.Side != EnumAppSide.Client)

            {

                return;

            }



            RPVoiceChatMod.SwitchboardClientChannel?.SendPacket(new SwitchboardRenameNetworkPacket

            {

                SwitchboardPos = Pos,

                NetworkName = desiredName ?? ""

            });

        }

        public void SetUsePowerRequirements(bool enabled)

        {

            if (usePowerRequirements == enabled)

            {

                return;

            }



            usePowerRequirements = enabled;

            MarkDirty(true);

            if (NetworkUID != 0)

            {

                WireNetworkHandler.RebuildNetworkState(NetworkUID);

            }

            if (IsDialogOpen())

            {

                dialog?.RefreshData();

            }

        }



        public void RequestSetUsePowerRequirements(bool enabled)

        {

            if (Api?.Side != EnumAppSide.Client)

            {

                return;

            }



            RPVoiceChatMod.SwitchboardClientChannel?.SendPacket(new SwitchboardPowerModePacket

            {

                SwitchboardPos = Pos,

                UsePowerRequirements = enabled

            });

        }



        public string[] GetConnectedLogicalNodeNames()

        {

            var network = WireNetworkHandler.GetNetwork(NetworkUID);

            if (network == null)

            {

                return Array.Empty<string>();

            }



            var entries = new List<string>();

            foreach (var node in network.Nodes.ToArray())

            {

                if (node == null || node is not IWireTypedNode typedNode)

                {

                    continue;

                }



                if (typedNode.WireNodeKind == WireNodeKind.Infrastructure || typedNode.WireNodeKind == WireNodeKind.Switchboard)

                {

                    continue;

                }



                string typeLabel = typedNode.WireNodeKind.ToString();

                string nodeLabel = node.Pos?.ToString() ?? "Unknown";

                if (node is ITelegraphEndpoint telegraphEndpoint && !string.IsNullOrWhiteSpace(telegraphEndpoint.CustomEndpointName))

                {

                    nodeLabel = telegraphEndpoint.CustomEndpointName;

                }



                entries.Add($"{typeLabel}: {nodeLabel}");

            }



            entries.Sort(StringComparer.OrdinalIgnoreCase);

            return entries.ToArray();

        }



        private bool IsDialogOpen()

        {

            return dialog != null && dialog.IsOpened();

        }



        private WireNetworkKind ResolveEffectiveNetworkKind()

        {

            var network = WireNetworkHandler.GetNetwork(NetworkUID);

            if (network == null || network.CurrentType == WireNetworkKind.None)

            {

                return WireNetworkKind.Telegraph;

            }



            return network.CurrentType;

        }

    }

}


