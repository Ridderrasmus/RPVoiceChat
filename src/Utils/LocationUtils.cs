using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace rpvoicechat.Utils
{
    public static class LocationUtils
    {
        public static Vec3d GetLocationOfPlayer(ICoreClientAPI api)
        {
            if (api == null)
                throw new Exception("api is null");

            return GetLocationOfPlayer(api.World.Player);
        }

        public static Vec3d GetLocationOfPlayer(IPlayer player)
        {
            if (player == null)
                throw new Exception("player is null");

            if (player.Entity.Swimming || (player.Entity.CurrentControls & EnumEntityActivity.FloorSitting) != 0)
                return (player.Entity.Pos.XYZ + new Vec3d(0, 0.5, 0));

            return GetSpeakerLocation(player.Entity.Pos);
        }

        public static Vec3d GetLocationOfPlayer(EntityPos pos)
        {
            if (pos == null)
                throw new Exception("entity is null");

            return GetSpeakerLocation(pos);
        }

        /// <summary>
        /// Returns speaker's position from listener's point of view
        /// </summary>
        /// <param name="speakerPos">Speaker's position object</param>
        /// <param name="listenerPos">Listener's position object</param>
        public static Vec3f GetRelativeSpeakerLocation(EntityPos speakerPos, EntityPos listenerPos)
        {
            var relativeSpeakerCoords = speakerPos.XYZFloat - listenerPos.XYZFloat;
            var listenerHeadAngle = listenerPos.Yaw + listenerPos.HeadYaw - Math.PI / 2;
            var cs = Math.Cos(listenerHeadAngle);
            var sn = Math.Sin(listenerHeadAngle);
            var rotatedVector = new Vec3d(
                relativeSpeakerCoords.X * cs - relativeSpeakerCoords.Z * sn,
                relativeSpeakerCoords.Y,
                relativeSpeakerCoords.X * sn + relativeSpeakerCoords.Z * cs
            );

            return rotatedVector.ToVec3f();
        }

        private static Vec3d GetSpeakerLocation(EntityPos pos)
        {
            return new Vec3d(pos.X, pos.Y + 1.5, pos.Z);
        }
    }
}
