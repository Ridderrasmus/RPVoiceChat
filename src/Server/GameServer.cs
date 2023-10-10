using Open.Nat;
using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
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
                Logger.server.Notification($"Launching {networkServer.GetTransportID()} server");
                var extendedServer = networkServer as IExtendedNetworkServer;
                extendedServer?.Launch();
                api.Event.PlayerNowPlaying += PlayerJoined;
                api.Event.PlayerDisconnect += PlayerLeft;
                networkServer.AudioPacketReceived += SendAudioToAllClientsInRange;
                serverByTransport.Add(networkServer.GetTransportID(), networkServer);
                Logger.server.Notification($"{networkServer.GetTransportID()} server started");

                if (reserveServer == null) return;
                Logger.server.Notification($"Launching {reserveServer.GetTransportID()} server");
                reserveServer.AudioPacketReceived += SendAudioToAllClientsInRange;
                serverByTransport.Add(reserveServer.GetTransportID(), reserveServer);
                Logger.server.Notification($"{reserveServer.GetTransportID()} server started");
                return;
            }
            catch (NatDeviceNotFoundException)
            {
                Logger.server.Error($"Failed to launch {networkServer.GetTransportID()} server: Unable to port forward with UPnP. " +
                    $"Make sure your IP is public and UPnP is enabled if you want to use {networkServer.GetTransportID()} server.");
            }
            catch (Exception e)
            {
                Logger.server.Error($"Failed to launch {networkServer.GetTransportID()} server:\n{e}");
            }

            if (reserveServer == null)
                throw new Exception("Failed to launch any server");

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
            var transmittingPlayer = api.World.PlayerByUid(packet.PlayerId);
            int distance = WorldConfig.GetInt(packet.VoiceLevel);
            int squareDistance = distance * distance;

            foreach (IServerPlayer player in api.World.AllOnlinePlayers)
            {
                if (player == transmittingPlayer ||
                    player.Entity == null ||
                    player.ConnectionState != EnumClientState.Playing ||
                    transmittingPlayer.Entity.Pos.SquareDistanceTo(player.Entity.Pos.XYZ) > squareDistance)
                    continue;

                SendPacket(packet, player.PlayerUID);
            }
        }

        private void SwapActiveServer(INetworkServer newTransport)
        {
            Logger.server.Notification($"Using {newTransport.GetTransportID()} server from now on");
            networkServer.Dispose();
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
            if (extendedServer == null) return;
            try
            {
                playerConnection.Address = NetworkUtils.ParseIP(player.IpAddress).MapToIPv4().ToString();
                extendedServer?.PlayerConnected(player.PlayerUID, playerConnection);
            }
            catch (Exception e)
            {
                Logger.server.Warning($"Server failed to establish connection with {player.PlayerUID}({player.PlayerName}) over " +
                    $"requested transport: {playerTransport}.\nServer will attempt to use other available transports to deliver " +
                    "packets to this client. Mismatch between server and client transports can result in unstable behavior!\n" +
                    $"Player address: {player.IpAddress}, Reason: {e}");
            }
        }

        private void SendPacket(NetworkPacket packet, string playerId)
        {
            try
            {
                bool success = networkServer.SendPacket(packet, playerId);
                if (success) return;
            }
            catch (Exception e)
            {
                Logger.server.VerboseDebug($"Couldn't use main server to deliver a packet to {playerId}: {e.Message}");
            }

            try
            {
                bool success = reserveServer?.SendPacket(packet, playerId) ?? false;
                if (success) return;
            }
            catch (Exception e)
            {
                Logger.server.VerboseDebug($"Couldn't use backup server to deliver a packet to {playerId}: {e.Message}");
            }

            Logger.server.Error($"Failed to deliver a packet to {playerId}: All servers refused to serve the client");
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
