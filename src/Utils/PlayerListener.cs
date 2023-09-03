using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RPVoiceChat
{
    public class PlayerListener
    {
        private Vec3d listenerPos;
        private float facing;

        public PlayerListener(Vec3d listenerPos, float facing)
        {
            this.listenerPos = listenerPos;
            this.facing = facing;
        }

        public PlayerListener(EntityPos pos)
        {
            this.listenerPos.Set(pos.X, pos.Y, pos.Z);
            this.facing = pos.Yaw + pos.HeadYaw;
        }

        public void UpdateListener(Vec3d listenerPos, float facing)
        {
            this.listenerPos.Set(listenerPos);
            this.facing = facing;
        }

        public void UpdateListener(EntityPos pos)
        {
            this.listenerPos.Set(pos.X, pos.Y, pos.Z);
            this.facing = pos.Yaw + pos.HeadYaw;
        }

        public Vec3d GetListenerPos()
        {
            return listenerPos;
        }

        public float GetFacing()
        {
            return facing;
        }
    }
}
