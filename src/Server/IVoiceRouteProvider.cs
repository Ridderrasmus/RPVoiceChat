using Vintagestory.API.MathTools;

namespace RPVoiceChat.Server
{
    public interface IVoiceRouteProvider
    {
        bool TryGetRoute(string playerUid, out Vec3d emissionPos, out int rangeBlocks);
    }
}
