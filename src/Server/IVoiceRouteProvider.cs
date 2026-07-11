using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Server
{
    public readonly struct VoiceRoute
    {
        public VoiceRoute(Vec3d emissionPos, int rangeBlocks) : this(emissionPos, rangeBlocks, 0) { }

        public VoiceRoute(Vec3d emissionPos, int rangeBlocks, int dimension)
        {
            EmissionPos = emissionPos;
            RangeBlocks = rangeBlocks;
            Dimension = dimension;
        }

        public Vec3d EmissionPos { get; }
        public int RangeBlocks { get; }
        public int Dimension { get; }
    }

    public interface IVoiceRouteProvider
    {
        bool TryGetRoute(string playerUid, out Vec3d emissionPos, out int rangeBlocks);
    }

    public interface IVoiceMultiRouteProvider
    {
        bool TryGetRoutes(string playerUid, out IReadOnlyList<VoiceRoute> routes);
    }
}
