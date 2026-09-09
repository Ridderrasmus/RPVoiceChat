using System.Collections.Generic;
using ProtoBuf;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.GameContent.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace RPVoiceChat.Systems
{
    /// <summary>
    /// Serializable node identity for world-level wire network persistence.
    /// Survives chunk unload/reload (pattern inspired by VintageEngineering <c>ElectricNetwork.allNodes</c>).
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WireNodeRef
    {
        public BlockPos Pos;
        public WireNodeKind Kind;

        public WireNodeRef() { }

        public WireNodeRef(BlockPos pos, WireNodeKind kind)
        {
            Pos = pos?.Copy();
            Kind = kind;
        }

        public bool Matches(BEWireNode node)
        {
            return node != null && Pos != null && Pos.Equals(node.Pos);
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WireNetworkSaveData
    {
        public long NetworkId;
        public string CustomName = "";
        public List<WireNodeRef> Nodes = new List<WireNodeRef>();
    }

    /// <summary>
    /// Server-side world save/load for wire communication networks.
    /// </summary>
    public class WireNetworkPersistence : ModSystem
    {
        public const string NetworksDataKey = "rpvc:wire-networks";
        public const string NextNetworkIdDataKey = "rpvc:wire-network-nextid";

        private ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.SaveGameLoaded += OnSaveGameLoaded;
            api.Event.GameWorldSave += OnGameWorldSave;
        }

        private void OnSaveGameLoaded()
        {
            byte[] topologyBytes = sapi.WorldManager.SaveGame.GetData(WireTopologyRegistry.TopologyDataKey);
            WireTopologyRegistry.LoadFromSave(topologyBytes);

            byte[] wirelessTopologyBytes = sapi.WorldManager.SaveGame.GetData(WirelessTopologyRegistry.TopologyDataKey);
            WirelessTopologyRegistry.LoadFromSave(wirelessTopologyBytes);

            byte[] rfPresenceBytes = sapi.WorldManager.SaveGame.GetData(RadioRfPresenceRegistry.PresenceDataKey);
            RadioRfPresenceRegistry.LoadFromSave(rfPresenceBytes);

            byte[] networkBytes = sapi.WorldManager.SaveGame.GetData(NetworksDataKey);
            if (networkBytes == null)
            {
                return;
            }

            byte[] nextIdBytes = sapi.WorldManager.SaveGame.GetData(NextNetworkIdDataKey);
            WireNetworkHandler.InitializeFromSave(networkBytes, nextIdBytes);
            WireNetworkHandler.RejoinLoadedNodes(sapi);
        }

        private void OnGameWorldSave()
        {
            WireNetworkHandler.PersistToSaveGame(sapi.WorldManager.SaveGame);
            sapi.WorldManager.SaveGame.StoreData(WireTopologyRegistry.TopologyDataKey, WireTopologyRegistry.ToSaveBytes());
            sapi.WorldManager.SaveGame.StoreData(WirelessTopologyRegistry.TopologyDataKey, WirelessTopologyRegistry.ToSaveBytes());
            sapi.WorldManager.SaveGame.StoreData(RadioRfPresenceRegistry.PresenceDataKey, RadioRfPresenceRegistry.ToSaveBytes());
        }
    }
}
