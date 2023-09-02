using RPVoiceChat.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Vintagestory.API.Server;

namespace RPVoiceChat.Server
{
    public class GameServer : IDisposable
    {
        private ICoreServerAPI api;
        private INetworkServer networkServer;
        private INetworkServer reserveServer;
        private IServerNetworkChannel handshakeChannel;
        private Dictionary<string, INetworkServer> serverByTransport;

        public GameServer(ICoreServerAPI sapi, INetworkServer server, INetworkServer _reserveServer = null)
        {
            api = sapi;
            networkServer = server;
            reserveServer = _reserveServer;
            handshakeChannel = sapi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionInfo>(FinalizeHandshake);

            if (reserveServer is IExtendedNetworkServer)
                throw new NotSupportedException("Reserve server requiring handshake is not supported");
        }

        public void Launch()
        {
            serverByTransport = new Dictionary<string, INetworkServer>();
            try
            {
                var extendedServer = networkServer as IExtendedNetworkServer;
                extendedServer?.Launch();
                api.Event.PlayerNowPlaying += PlayerJoined;
                api.Event.PlayerDisconnect += PlayerLeft;
                networkServer.OnReceivedPacket += SendAudioToAllClientsInRange;
                serverByTransport.Add(networkServer.GetTransportID(), networkServer);
                if (reserveServer == null) return;
                reserveServer.OnReceivedPacket += SendAudioToAllClientsInRange;
                serverByTransport.Add(reserveServer.GetTransportID(), reserveServer);
                return;
            }
            catch (Exception e)
            {
                var unsupportedTransport = networkServer.GetTransportID();
                api.Logger.Error($"[RPVoiceChat] Failed to launch {unsupportedTransport} server:\n{e}");
            }

            if (reserveServer == null)
                throw new Exception("Failed to launch any server");

            api.Logger.Notification($"[RPVoiceChat] Using {reserveServer.GetTransportID()} server from now on");
            SwapActiveServer(reserveServer);
            reserveServer = null;
            Launch();
        }

        public void PlayerJoined(IServerPlayer player)
        {
            InitHandshake(player);
        }

        public void PlayerLeft(IServerPlayer player)
        {
            var extendedServer = networkServer as IExtendedNetworkServer;
            extendedServer?.PlayerDisconnected(player.PlayerUID);
        }

        public void SendAudioToAllClientsInRange(AudioPacket packet)
        {
            var player = api.World.PlayerByUid(packet.PlayerId);
            int distance = WorldConfig.GetVoiceDistance(api, packet.VoiceLevel);
            int squareDistance = distance * distance;

            foreach (var closePlayer in api.World.AllOnlinePlayers)
            {
                if (closePlayer == player ||
                    closePlayer.Entity == null ||
                    player.Entity.Pos.SquareDistanceTo(closePlayer.Entity.Pos.XYZ) > squareDistance)
                    continue;

                SendPacket(packet, closePlayer.PlayerUID);
            }
        }

        private void SwapActiveServer(INetworkServer newTransport)
        {
            networkServer = newTransport;
        }

        private void InitHandshake(IServerPlayer player)
        {
            var serverConnection = networkServer.GetConnection();
            serverConnection.SupportedTransports = serverByTransport.Keys.ToArray();
            handshakeChannel.SendPacket(serverConnection, player);
        }

        private void FinalizeHandshake(IServerPlayer player, ConnectionInfo playerConnection)
        {
            var playerTransport = playerConnection.SupportedTransports.FirstOrDefault();
            if (!serverByTransport.ContainsKey(playerTransport)) return;

            var extendedServer = serverByTransport[playerTransport] as IExtendedNetworkServer;
            playerConnection.Address = IPAddress.Parse(player.IpAddress).MapToIPv4().ToString();
            extendedServer?.PlayerConnected(player.PlayerUID, playerConnection);
        }

        private void SendPacket(INetworkPacket packet, string playerId)
        {
            try
            {
                networkServer.SendPacket(packet, playerId);
                return;
            }
            catch (Exception e)
            {
                api.Logger.VerboseDebug($"[RPVoiceChat] Couldn't use main server to deliver a packet to {playerId}: {e.Message}");
            }

            try
            {
                reserveServer?.SendPacket(packet, playerId);
            }
            catch (Exception e)
            {
                api.Logger.VerboseDebug($"[RPVoiceChat] Couldn't use backup server to deliver a packet to {playerId}: {e.Message}");
            }
        }

        public void Dispose()
        {
            var disposableServer = networkServer as IDisposable;
            var disposableReserveServer = reserveServer as IDisposable;
            disposableServer?.Dispose();
            disposableReserveServer?.Dispose();
        }
    }
}
