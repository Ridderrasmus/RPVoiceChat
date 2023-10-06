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
                networkServer.OnReceivedPacket += SendAudioToAllClientsInRange;
                serverByTransport.Add(networkServer.GetTransportID(), networkServer);
                Logger.server.Notification($"{networkServer.GetTransportID()} server started");

                if (reserveServer == null) return;
                Logger.server.Notification($"Launching {reserveServer.GetTransportID()} server");
                reserveServer.OnReceivedPacket += SendAudioToAllClientsInRange;
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

            Logger.server.Notification($"Using {reserveServer.GetTransportID()} server from now on");
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
            int distance = WorldConfig.GetInt(packet.VoiceLevel);
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

        private void SendPacket(INetworkPacket packet, string playerId)
        {
            try
            {
                networkServer.SendPacket(packet, playerId);
                return;
            }
            catch (Exception e)
            {
                Logger.server.VerboseDebug($"Couldn't use main server to deliver a packet to {playerId}: {e.Message}");
            }

            try
            {
                reserveServer?.SendPacket(packet, playerId);
            }
            catch (Exception e)
            {
                Logger.server.VerboseDebug($"Couldn't use backup server to deliver a packet to {playerId}: {e.Message}");
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
