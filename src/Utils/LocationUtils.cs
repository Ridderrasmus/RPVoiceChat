using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using static OpenTK.Graphics.OpenGL.GL;

namespace rpvoicechat.src.Utils
{
    public class LocationUtils
    {
        private static readonly LocationUtils _instance = new LocationUtils();
        public static LocationUtils Instance { get { return _instance; } }

        static LocationUtils()
        {
        }

        private LocationUtils()
        {
        }

        public static Vec3d GetLocationOfPlayer(ICoreClientAPI api)
        {
            if (api == null)
                throw new Exception("api is null");

            if (api.World.Player.Entity.Swimming)
                return (api.World.Player.Entity.Pos.XYZ + new Vec3d(0, 0.5, 0));

            return GetSpeakerLocation(api.World.Player.Entity.Pos);
        }

        public static Vec3d GetLocationOfPlayer(IPlayer player)
        {
            if (player == null)
                throw new Exception("player is null");

            if (player.Entity.Swimming)
                return (player.Entity.Pos.XYZ + new Vec3d(0, 0.5, 0));

            return GetSpeakerLocation(player.Entity.Pos);
        }

        public static Vec3d GetLocationOfPlayer(EntityPos pos)
        {
            if (pos == null)
                throw new Exception("entity is null");

            return GetSpeakerLocation(pos);
        }

        private static Vec3d GetSpeakerLocation(EntityPos pos)
        {
            return new Vec3d(pos.X, pos.Y + 1.5, pos.Z);
        }
    }
}
