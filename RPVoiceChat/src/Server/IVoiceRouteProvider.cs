using System.Collections.Generic;
using RPVoiceChat.Networking;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Server
{
    public readonly struct VoiceRoute
    {
        public VoiceRoute(Vec3d emissionPos, int rangeBlocks) : this(emissionPos, rangeBlocks, 0, null, true) { }

        public VoiceRoute(Vec3d emissionPos, int rangeBlocks, int dimension) : this(emissionPos, rangeBlocks, dimension, null, true) { }

        public VoiceRoute(Vec3d emissionPos, int rangeBlocks, int dimension, string radioFrequency)
            : this(emissionPos, rangeBlocks, dimension, radioFrequency, acousticEmission: true)
        {
        }

        public VoiceRoute(Vec3d emissionPos, int rangeBlocks, int dimension, string radioFrequency, bool acousticEmission)
        {
            EmissionPos = emissionPos;
            RangeBlocks = rangeBlocks;
            Dimension = dimension;
            RadioFrequency = radioFrequency;
            IsAcousticEmission = acousticEmission;
        }

        public Vec3d EmissionPos { get; }
        public int RangeBlocks { get; }
        public int Dimension { get; }
        /// <summary>When set, talkie/receiver listeners must match this RF channel.</summary>
        public string RadioFrequency { get; }
        /// <summary>
        /// When false, this route is RF coverage only (e.g. transmitter/antenna) and must not
        /// place world audio at <see cref="EmissionPos"/>.
        /// </summary>
        public bool IsAcousticEmission { get; }
    }

    public interface IVoiceRouteProvider
    {
        bool TryGetRoute(string playerUid, out Vec3d emissionPos, out int rangeBlocks);
    }

    public interface IVoiceMultiRouteProvider
    {
        bool TryGetRoutes(string playerUid, out IReadOnlyList<VoiceRoute> routes);
    }

    public readonly struct RoutedVoiceRecipient
    {
        public readonly string PlayerUID;
        public readonly VoiceRoute Route;
        public readonly double DistanceSq;

        public RoutedVoiceRecipient(string playerUid, VoiceRoute route, double distanceSq)
        {
            PlayerUID = playerUid;
            Route = route;
            DistanceSq = distanceSq;
        }
    }

    public interface IVoiceRecipientExpander
    {
        void ExpandRoutedRecipients(
            AudioPacket packet,
            IReadOnlyList<VoiceRoute> routes,
            Dictionary<string, RoutedVoiceRecipient> recipients);
    }
}
