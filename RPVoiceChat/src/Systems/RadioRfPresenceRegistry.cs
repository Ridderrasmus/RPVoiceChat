using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RPVoiceChat.Systems
{
    /// <summary>
    /// Last-known RF / program state for emitters, repeaters, receivers, and mixing consoles
    /// that survives chunk unload. Live block entities refresh entries; unload keeps the snapshot; remove deletes it.
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RadioRfEmitterPresence
    {
        public BlockPos Pos;
        public int Dimension;
        public long NetworkId;
        public bool IsRepeater;
        public string Frequency = "";
        public int RangeBlocks;
        /// <summary>Wired-source TX, or repeater that could relay when last observed.</summary>
        public bool IsActive;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RadioRfAcousticPresence
    {
        public BlockPos Pos;
        public int Dimension;
        public int RangeBlocks;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RadioRfReceiverPresence
    {
        public BlockPos Pos;
        public int Dimension;
        public string TunedFrequency = "";
        public int PlaybackRangeBlocks;
        public bool IsEnabled;
        public List<RadioRfAcousticPresence> AcousticPoints = new List<RadioRfAcousticPresence>();
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RadioRfProgramPresence
    {
        public BlockPos Pos;
        public int Dimension;
        public long NetworkId;
        public bool IsOnAir;
        public string HlsStreamUrl = "";
        public string ActiveOperatorPlayerUid = "";
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RadioRfPresenceData
    {
        public List<RadioRfEmitterPresence> Emitters = new List<RadioRfEmitterPresence>();
        public List<RadioRfReceiverPresence> Receivers = new List<RadioRfReceiverPresence>();
        public List<RadioRfProgramPresence> Programs = new List<RadioRfProgramPresence>();
    }

    /// <summary>
    /// World-level RF / program presence index. Transmission, reception, and HLS program broadcast
    /// prefer this over loaded-only BE scans so stations, repeaters, receivers, and mixing streams
    /// keep working when their chunks are unloaded.
    /// </summary>
    public static class RadioRfPresenceRegistry
    {
        public const string PresenceDataKey = "rpvc:radio-rf-presence";

        private static readonly Dictionary<string, RadioRfEmitterPresence> emittersByKey = new();
        private static readonly Dictionary<string, RadioRfReceiverPresence> receiversByKey = new();
        private static readonly Dictionary<string, RadioRfProgramPresence> programsByKey = new();

        public static void Clear()
        {
            emittersByKey.Clear();
            receiversByKey.Clear();
            programsByKey.Clear();
        }

        public static void LoadFromSave(byte[] data)
        {
            Clear();
            if (data == null || data.Length == 0)
            {
                return;
            }

            var payload = SerializerUtil.Deserialize<RadioRfPresenceData>(data);
            if (payload == null)
            {
                return;
            }

            if (payload.Emitters != null)
            {
                foreach (var emitter in payload.Emitters)
                {
                    UpsertEmitter(emitter);
                }
            }

            if (payload.Receivers != null)
            {
                foreach (var receiver in payload.Receivers)
                {
                    UpsertReceiver(receiver);
                }
            }

            if (payload.Programs != null)
            {
                foreach (var program in payload.Programs)
                {
                    UpsertProgram(program);
                }
            }
        }

        public static byte[] ToSaveBytes()
        {
            var payload = new RadioRfPresenceData
            {
                Emitters = emittersByKey.Values
                    .Select(CloneEmitter)
                    .Where(entry => entry?.Pos != null)
                    .ToList(),
                Receivers = receiversByKey.Values
                    .Select(CloneReceiver)
                    .Where(entry => entry?.Pos != null)
                    .ToList(),
                Programs = programsByKey.Values
                    .Select(CloneProgram)
                    .Where(entry => entry?.Pos != null)
                    .ToList()
            };

            return SerializerUtil.Serialize(payload);
        }

        public static void UpsertEmitter(RadioRfEmitterPresence presence)
        {
            if (presence?.Pos == null)
            {
                return;
            }

            string key = PosKey(presence.Pos);
            emittersByKey[key] = CloneEmitter(presence);
        }

        public static void RemoveEmitter(BlockPos pos)
        {
            if (pos == null)
            {
                return;
            }

            emittersByKey.Remove(PosKey(pos));
        }

        public static void UpsertReceiver(RadioRfReceiverPresence presence)
        {
            if (presence?.Pos == null)
            {
                return;
            }

            string key = PosKey(presence.Pos);
            receiversByKey[key] = CloneReceiver(presence);
        }

        public static void RemoveReceiver(BlockPos pos)
        {
            if (pos == null)
            {
                return;
            }

            receiversByKey.Remove(PosKey(pos));
        }

        /// <summary>
        /// When a supervision console frequency changes, refresh wired-source snapshots on that network.
        /// </summary>
        public static void UpdateWiredSourceFrequency(long networkId, string frequency)
        {
            if (networkId == 0)
            {
                return;
            }

            string normalized = RadioFrequencyUtil.Normalize(frequency);
            foreach (var entry in emittersByKey.Values)
            {
                if (entry == null || entry.IsRepeater || entry.NetworkId != networkId)
                {
                    continue;
                }

                entry.Frequency = normalized;
                // Keep IsActive as last known; live emitters will republish shortly.
            }
        }

        public static IEnumerable<RadioRfEmitterPresence> GetEmitters()
        {
            return emittersByKey.Values.Where(entry => entry?.Pos != null);
        }

        public static IEnumerable<RadioRfReceiverPresence> GetReceivers()
        {
            return receiversByKey.Values.Where(entry => entry?.Pos != null);
        }

        public static IEnumerable<string> GetActiveFrequenciesForNetwork(long networkId)
        {
            if (networkId == 0)
            {
                yield break;
            }

            var seen = new HashSet<string>();
            foreach (var entry in GetEmitters())
            {
                if (entry.IsRepeater || !entry.IsActive || entry.NetworkId != networkId)
                {
                    continue;
                }

                string frequency = RadioFrequencyUtil.Normalize(entry.Frequency);
                if (frequency.Length == 0 || !seen.Add(frequency))
                {
                    continue;
                }

                yield return frequency;
            }
        }

        public static IEnumerable<string> EnumerateClaimedTransmitFrequencies(BlockPos excludePos)
        {
            foreach (var entry in GetEmitters())
            {
                if (excludePos != null && entry.Pos.Equals(excludePos))
                {
                    continue;
                }

                // Wired sources claim via console; repeaters claim their listen/TX frequency.
                if (!entry.IsRepeater)
                {
                    continue;
                }

                string frequency = RadioFrequencyUtil.Normalize(entry.Frequency);
                if (frequency.Length > 0)
                {
                    yield return frequency;
                }
            }
        }

        public static void UpsertProgram(RadioRfProgramPresence presence)
        {
            if (presence?.Pos == null)
            {
                return;
            }

            programsByKey[PosKey(presence.Pos)] = CloneProgram(presence);
        }

        public static void RemoveProgram(BlockPos pos)
        {
            if (pos == null)
            {
                return;
            }

            programsByKey.Remove(PosKey(pos));
        }

        public static IEnumerable<RadioRfProgramPresence> GetPrograms()
        {
            return programsByKey.Values.Where(entry => entry?.Pos != null);
        }

        private static string PosKey(BlockPos pos) => $"{pos.X},{pos.Y},{pos.Z},{pos.dimension}";

        private static RadioRfEmitterPresence CloneEmitter(RadioRfEmitterPresence source)
        {
            if (source == null)
            {
                return null;
            }

            return new RadioRfEmitterPresence
            {
                Pos = source.Pos?.Copy(),
                Dimension = source.Dimension,
                NetworkId = source.NetworkId,
                IsRepeater = source.IsRepeater,
                Frequency = source.Frequency ?? "",
                RangeBlocks = source.RangeBlocks,
                IsActive = source.IsActive
            };
        }

        private static RadioRfReceiverPresence CloneReceiver(RadioRfReceiverPresence source)
        {
            if (source == null)
            {
                return null;
            }

            return new RadioRfReceiverPresence
            {
                Pos = source.Pos?.Copy(),
                Dimension = source.Dimension,
                TunedFrequency = source.TunedFrequency ?? "",
                PlaybackRangeBlocks = source.PlaybackRangeBlocks,
                IsEnabled = source.IsEnabled,
                AcousticPoints = (source.AcousticPoints ?? new List<RadioRfAcousticPresence>())
                    .Where(point => point?.Pos != null)
                    .Select(point => new RadioRfAcousticPresence
                    {
                        Pos = point.Pos.Copy(),
                        Dimension = point.Dimension,
                        RangeBlocks = point.RangeBlocks
                    })
                    .ToList()
            };
        }

        private static RadioRfProgramPresence CloneProgram(RadioRfProgramPresence source)
        {
            if (source == null)
            {
                return null;
            }

            return new RadioRfProgramPresence
            {
                Pos = source.Pos?.Copy(),
                Dimension = source.Dimension,
                NetworkId = source.NetworkId,
                IsOnAir = source.IsOnAir,
                HlsStreamUrl = source.HlsStreamUrl ?? "",
                ActiveOperatorPlayerUid = source.ActiveOperatorPlayerUid ?? ""
            };
        }
    }
}
