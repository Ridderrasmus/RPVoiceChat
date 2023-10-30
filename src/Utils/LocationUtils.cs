using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Utils
{
    using BlockEntry = Tuple<BlockPos, Block>;
    using RayTraceResults = Tuple<List<Tuple<BlockPos, Block>>, List<Entity>>;

    public static class LocationUtils
    {
        private static int maxRayTraceDistance = 100;
        private static string[] transparentBlocks = new string[]
        {
            "BlockGroundStorage",
            "BlockPie",
            "BlockSupportBeam"
        };

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
            return GetRelativeSpeakerLocation(speakerPos.XYZFloat, listenerPos);
        }

        /// <summary>
        /// Returns speaker's position from listener's point of view
        /// </summary>
        /// <param name="speakerPos">Speaker's position</param>
        /// <param name="listenerPos">Listener's position object</param>
        public static Vec3f GetRelativeSpeakerLocation(Vec3f speakerPos, EntityPos listenerPos)
        {
            var relativeSpeakerCoords = speakerPos - listenerPos.XYZFloat;
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

        /// <summary>
        /// Calculates obstruction level between two given players
        /// </summary>
        /// <returns>Combined volume of obstructing blocks</returns>
        public static float GetWallThickness(ICoreClientAPI capi, IPlayer source, IPlayer target)
        {
            var origin = GetLocationOfPlayer(source);
            var destination = GetLocationOfPlayer(target);

            var obstructingBlocks = RayTraceThrough(capi, origin, destination).Item1;
            float thickness = 0;
            foreach (BlockEntry blockEntry in obstructingBlocks)
            {
                var blockPos = blockEntry.Item1;
                var block = blockEntry.Item2;
                if (transparentBlocks.Contains(block?.Class)) continue;

                var collisionBoxes = new Cuboidf[0];
                try
                {
                    collisionBoxes = block?.GetCollisionBoxes(capi.World.BlockAccessor, blockPos) ?? collisionBoxes;
                }
                catch (Exception e)
                {
                    Logger.client.Warning($"Couldn't retrieve collision boxes for {block.Class} at {blockPos}:\n{e}");
                }

                foreach (Cuboidf box in collisionBoxes)
                    thickness += box.Length * box.Height * box.Width;
            }

            return thickness;
        }

        /// <summary>
        /// Casts a ray between two given positions <br />
        /// <b>May be inaccurate as game's API is bugged</b>
        /// </summary>
        /// <returns>Two lists containing blocks and entities intersected by the ray</returns>
        public static RayTraceResults RayTraceThrough(ICoreClientAPI capi, Vec3d from, Vec3d to)
        {
            var visitedBlocks = new List<BlockEntry>();
            var visitedEntities = new List<Entity>();
            EntityFilter entityFilter = entity =>
            {
                return !(visitedEntities.Contains(entity) || entity.Class == "EntityPlayer");
            };
            BlockFilter blockFilter = (pos, block) =>
            {
                var blockEntry = new BlockEntry(pos.Copy(), block);
                return !visitedBlocks.Contains(blockEntry);
            };

            try
            {
                for (var i = 0; i < maxRayTraceDistance; i++)
                {
                    BlockSelection blockSelection = new BlockSelection();
                    EntitySelection entitySelection = new EntitySelection();
                    capi.World.RayTraceForSelection(from, to, ref blockSelection, ref entitySelection, blockFilter, entityFilter);

                    if (blockSelection == null && entitySelection == null) break;
                    if (entitySelection?.Entity != null) visitedEntities.Add(entitySelection.Entity);
                    if (blockSelection?.Block != null && blockSelection?.Position is BlockPos)
                    {
                        var blockEntry = new BlockEntry(blockSelection.Position.Copy(), blockSelection.Block);
                        visitedBlocks.Add(blockEntry);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.client.Error($"Ray trace API threw an exception:\n{e}");
            }

            return new RayTraceResults(visitedBlocks, visitedEntities);
        }

        private static Vec3d GetSpeakerLocation(EntityPos pos)
        {
            return new Vec3d(pos.X, pos.Y + 1.5, pos.Z);
        }
    }
}
