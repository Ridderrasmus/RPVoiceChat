using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using OpenTK.Audio.OpenAL;
using RPVoiceChat;
using RPVoiceChat.Audio;
using RPVoiceChat.Audio.Input;
using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Networking;
using RPVoiceChat.Server;
using RPVoiceChat.Util;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RPVoiceChat.Systems
{
    public class RadioProgramBroadcastSystem : ModSystem
    {
        private readonly ConcurrentDictionary<string, ProgramSession> sessions = new();
        private readonly ConcurrentDictionary<string, string> micOperatorToRouteKey = new();
        private readonly ConcurrentQueue<AudioPacket> pendingPackets = new();

        private ICoreServerAPI sapi;
        private RadioVoiceRoutingSystem routing;
        private GameServer gameServer;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            routing = api.ModLoader.GetModSystem<RadioVoiceRoutingSystem>();
            api.Event.RegisterGameTickListener(OnServerTick, 100);
            FfmpegLocator.BeginEnsureAvailable();
        }

        public void BindGameServer(GameServer server)
        {
            gameServer = server;
            server.SetProgramMicAudioSink(TryConsumeMicAudio);
        }

        public bool TryConsumeMicAudio(AudioPacket packet)
        {
            if (packet == null
                || string.IsNullOrWhiteSpace(packet.PlayerId)
                || packet.AudioData == null
                || packet.AudioData.Length == 0)
            {
                return false;
            }

            if (!micOperatorToRouteKey.TryGetValue(packet.PlayerId, out string routeKey)
                || !sessions.TryGetValue(routeKey, out ProgramSession session))
            {
                return false;
            }

            short[] micSamples = DecodeMicPacket(session, packet);
            if (micSamples.Length == 0)
            {
                return true;
            }

            session.MicBuffer.EnqueueSamples(micSamples);

            if (!session.HasHlsCapture)
            {
                EmitMicOnlyFrames(session, micSamples);
            }

            return true;
        }

        private void OnServerTick(float dt)
        {
            if (sapi == null || routing == null)
            {
                return;
            }

            // Loaded consoles refresh world-level presence (HLS keeps running after unload).
            foreach (BlockEntityRadioMixingConsole mixingConsole in RadioBlockIndex.GetLoadedMixingConsoles(sapi.World).ToList())
            {
                mixingConsole.PublishProgramPresence();
            }

            RefreshMicOperatorBindings();

            var activeRouteKeys = new HashSet<string>();
            foreach (RadioRfProgramPresence program in RadioRfPresenceRegistry.GetPrograms().ToList())
            {
                string routeKey = RadioProgramRouteKey.ForMixingConsole(program.Pos);
                if (string.IsNullOrWhiteSpace(routeKey))
                {
                    continue;
                }

                if (!program.IsOnAir || program.NetworkId == 0)
                {
                    StopSession(routeKey);
                    routing.ClearProgramRoute(routeKey);
                    continue;
                }

                IEnumerable<BlockEntitySpeaker> speakers = null;
                BlockEntityRadioMixingConsole loadedConsole = ResolveLoadedConsole(program.Pos);
                if (loadedConsole != null)
                {
                    speakers = RadioWireNetworkHelper.FindSpeakers(loadedConsole);
                }

                var routes = RadioWiredRouteBuilder.BuildRoutesForNetwork(
                    sapi,
                    program.NetworkId,
                    speakers,
                    program.Dimension);

                if (routes.Count == 0)
                {
                    StopSession(routeKey);
                    routing.ClearProgramRoute(routeKey);
                    continue;
                }

                routing.SetProgramRoutes(routeKey, routes);
                activeRouteKeys.Add(routeKey);

                if (!sessions.ContainsKey(routeKey))
                {
                    TryStartSession(program, routeKey, loadedConsole);
                }
            }

            foreach (string routeKey in sessions.Keys.ToArray())
            {
                if (!activeRouteKeys.Contains(routeKey))
                {
                    StopSession(routeKey);
                    routing.ClearProgramRoute(routeKey);
                }
            }

            FlushPendingPackets();
        }

        private BlockEntityRadioMixingConsole ResolveLoadedConsole(BlockPos pos)
        {
            if (pos == null || sapi?.World == null)
            {
                return null;
            }

            return sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityRadioMixingConsole;
        }

        private void RefreshMicOperatorBindings()
        {
            var nextBindings = new Dictionary<string, string>();
            foreach (RadioRfProgramPresence program in RadioRfPresenceRegistry.GetPrograms())
            {
                if (!program.IsOnAir)
                {
                    continue;
                }

                string routeKey = RadioProgramRouteKey.ForMixingConsole(program.Pos);
                BlockEntityRadioMixingConsole mixingConsole = ResolveLoadedConsole(program.Pos);
                if (mixingConsole == null)
                {
                    continue;
                }

                foreach (BlockEntityRadioMicrophone microphone in RadioWireNetworkHelper.FindMicrophones(mixingConsole))
                {
                    if (!microphone.IsTransmitting || string.IsNullOrWhiteSpace(microphone.ActiveOperatorPlayerUid))
                    {
                        continue;
                    }

                    nextBindings[microphone.ActiveOperatorPlayerUid] = routeKey;
                }
            }

            foreach (string playerUid in micOperatorToRouteKey.Keys.ToArray())
            {
                if (!nextBindings.ContainsKey(playerUid))
                {
                    micOperatorToRouteKey.TryRemove(playerUid, out _);
                }
            }

            foreach (var entry in nextBindings)
            {
                micOperatorToRouteKey[entry.Key] = entry.Value;
            }
        }

        private void TryStartSession(
            RadioRfProgramPresence program,
            string routeKey,
            BlockEntityRadioMixingConsole loadedConsole)
        {
            var session = new ProgramSession
            {
                RouteKey = routeKey,
                EncoderCodec = new OpusCodec(RadioHlsStreamCapture.SampleRate, RadioHlsStreamCapture.Channels),
                MicDecoder = new OpusCodec(RadioHlsStreamCapture.SampleRate, RadioHlsStreamCapture.Channels),
                // Monotonic across restarts so clients don't treat new frames as "late".
                SequenceNumber = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100
            };

            string hlsUrl = program.HlsStreamUrl ?? "";
            if (!string.IsNullOrWhiteSpace(hlsUrl))
            {
                var capture = new RadioHlsStreamCapture();
                session.Capture = capture;
                session.FrameHandler = musicFrame => OnMusicFrame(routeKey, musicFrame);
                capture.OnPcmFrame += session.FrameHandler;

                if (!capture.TryStart(hlsUrl))
                {
                    string error = capture.LastError;
                    capture.Dispose();
                    Logger.server.Warning($"[RadioProgram] Failed to start HLS for {routeKey}: {error}");

                    if (!HasActiveMicOnGraph(loadedConsole))
                    {
                        NotifyOperator(program, "Radio.MixingConsole.Error.FfmpegMissing");
                        return;
                    }

                    session.Capture = null;
                    session.FrameHandler = null;
                    Logger.server.Notification($"[RadioProgram] Mic-only program bus for {routeKey} (HLS unavailable).");
                }
                else
                {
                    Logger.server.Notification($"[RadioProgram] Mixed program started for {routeKey} (HLS + mic bus).");
                }
            }
            else if (HasActiveMicOnGraph(loadedConsole))
            {
                Logger.server.Notification($"[RadioProgram] Mic-only program bus started for {routeKey}.");
            }
            else
            {
                return;
            }

            sessions[routeKey] = session;
        }

        private static bool HasActiveMicOnGraph(BlockEntityRadioMixingConsole mixingConsole)
        {
            if (mixingConsole == null)
            {
                return false;
            }

            return RadioWireNetworkHelper.FindMicrophones(mixingConsole)
                .Any(microphone => microphone.IsTransmitting);
        }

        private void NotifyOperator(RadioRfProgramPresence program, string langKey)
        {
            if (sapi == null
                || program == null
                || string.IsNullOrWhiteSpace(program.ActiveOperatorPlayerUid)
                || string.IsNullOrWhiteSpace(langKey))
            {
                return;
            }

            if (sapi.World.PlayerByUid(program.ActiveOperatorPlayerUid) is IServerPlayer operatorPlayer)
            {
                RPVoiceChatMod.SendRadioClientNotification(operatorPlayer, langKey);
            }
        }

        private void StopSession(string routeKey)
        {
            if (!sessions.TryRemove(routeKey, out ProgramSession session))
            {
                return;
            }

            if (session.FrameHandler != null && session.Capture != null)
            {
                session.Capture.OnPcmFrame -= session.FrameHandler;
            }

            session.Capture?.Dispose();
            session.MicBuffer.Clear();
            while (pendingPackets.TryDequeue(out _))
            {
            }

            Logger.server.Notification($"[RadioProgram] Program bus stopped for {routeKey}");
        }

        private void OnMusicFrame(string routeKey, short[] musicFrame)
        {
            if (!sessions.TryGetValue(routeKey, out ProgramSession session)
                || musicFrame == null
                || musicFrame.Length == 0)
            {
                return;
            }

            var micFrame = new short[musicFrame.Length];
            session.MicBuffer.ReadFrame(musicFrame.Length, micFrame);

            var mixed = new short[musicFrame.Length];
            RadioProgramMixer.MixFrames(musicFrame, micFrame, mixed);
            EnqueueMixedFrame(session, mixed);
        }

        private void EmitMicOnlyFrames(ProgramSession session, short[] micSamples)
        {
            int frameSamples = RadioHlsStreamCapture.FrameSamples;
            for (int offset = 0; offset + frameSamples <= micSamples.Length; offset += frameSamples)
            {
                var micFrame = new short[frameSamples];
                Array.Copy(micSamples, offset, micFrame, 0, frameSamples);

                var silentMusic = new short[frameSamples];
                var mixed = new short[frameSamples];
                RadioProgramMixer.MixFrames(silentMusic, micFrame, mixed);
                EnqueueMixedFrame(session, mixed);
            }
        }

        private void EnqueueMixedFrame(ProgramSession session, short[] mixed)
        {
            byte[] encoded = session.EncoderCodec.EncodeForProgramStream(mixed);
            if (encoded == null || encoded.Length == 0)
            {
                return;
            }

            session.SequenceNumber++;
            pendingPackets.Enqueue(new AudioPacket
            {
                PlayerId = session.RouteKey,
                AudioData = encoded,
                Length = encoded.Length,
                VoiceLevel = VoiceLevel.Shouting,
                Frequency = RadioHlsStreamCapture.SampleRate,
                Format = ALFormat.Mono16,
                SequenceNumber = session.SequenceNumber,
                Codec = OpusCodec._Name
                // Locational like speakers: distance/walls apply via SourcePos + TransmissionRangeBlocks.
            });
        }

        private static short[] DecodeMicPacket(ProgramSession session, AudioPacket packet)
        {
            if (packet.Codec == OpusCodec._Name)
            {
                return AudioUtils.BytesToShorts(session.MicDecoder.Decode(packet.AudioData));
            }

            if (packet.Codec == DummyCodec._Name)
            {
                return AudioUtils.BytesToShorts(packet.AudioData);
            }

            return Array.Empty<short>();
        }

        private void FlushPendingPackets()
        {
            if (gameServer == null)
            {
                while (pendingPackets.TryDequeue(out _))
                {
                }

                return;
            }

            // Realtime capture emits ~10 frames / 100ms tick. Flush with slack, then drop
            // only if we are still building a large backlog (server lag / slow clients).
            const int maxPacketsPerTick = 20;
            const int maxBacklog = 60;
            while (pendingPackets.Count > maxBacklog && pendingPackets.TryDequeue(out _))
            {
            }

            for (int i = 0; i < maxPacketsPerTick && pendingPackets.TryDequeue(out AudioPacket packet); i++)
            {
                gameServer.SendAudioToAllClientsInRange(packet);
            }
        }

        public override void Dispose()
        {
            foreach (string routeKey in sessions.Keys.ToArray())
            {
                StopSession(routeKey);
            }

            base.Dispose();
        }

        private sealed class ProgramSession
        {
            public string RouteKey;
            public RadioHlsStreamCapture Capture;
            public OpusCodec EncoderCodec;
            public OpusCodec MicDecoder;
            public RadioProgramMicBuffer MicBuffer = new();
            public Action<short[]> FrameHandler;
            public long SequenceNumber;
            public bool HasHlsCapture => Capture?.IsRunning == true;
        }
    }
}
