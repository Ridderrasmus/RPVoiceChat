using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace RPVoiceChat.Server
{
    /// <summary>
    /// Spatial index for server-side voice listener lookup.
    /// Warm thanks to Hugo Cortell (a.k.a. YangWenLi) for contributing ideas and code
    /// from his mod Yang's High Performance Voice Chat to RPVoiceChat.
    /// </summary>
    internal sealed class Grid
    {
        public static readonly Grid Empty = new Grid(64, Array.Empty<GridPlayer>());

        private readonly GridPlayer[] players;
        private readonly Dictionary<string, int> playerIndexByUid;
        private readonly Dictionary<int, Dictionary<long, List<int>>> cellsByDimension;

        public int CellSize { get; }
        public int CellShift { get; }
        public IReadOnlyList<GridPlayer> Players => players;
        public bool IsEmpty => players.Length == 0;

        private Grid(int cellSizeBlocks, GridPlayer[] players)
        {
            CellSize = SanitizeCellSize(cellSizeBlocks);

            int shift = 0;
            int value = CellSize;
            while ((value >>= 1) != 0) shift++;
            CellShift = shift;

            this.players = players ?? Array.Empty<GridPlayer>();
            playerIndexByUid = new Dictionary<string, int>(this.players.Length);
            cellsByDimension = new Dictionary<int, Dictionary<long, List<int>>>();

            for (int index = 0; index < this.players.Length; index++)
            {
                var player = this.players[index];
                if (!string.IsNullOrEmpty(player.PlayerUID)) playerIndexByUid[player.PlayerUID] = index;

                AddToCell(index, player.Dimension, CoordToCell(player.X), CoordToCell(player.Z));
            }
        }

        public static Grid Build(ICoreServerAPI api, int cellSizeBlocks)
        {
            if (api?.World == null) return Empty;

            var snapshots = new List<GridPlayer>();
            foreach (var rawPlayer in api.World.AllOnlinePlayers)
            {
                if (rawPlayer is not IServerPlayer player) continue;
                if (player.Entity == null || player.Entity.Pos == null) continue;
                if (player.ConnectionState != EnumClientState.Playing) continue;

                EntityPos pos = player.Entity.Pos;
                snapshots.Add(new GridPlayer(
                    player,
                    pos.Dimension,
                    pos.X, pos.Y, pos.Z,
                    player.WorldData.CurrentGameMode == EnumGameMode.Spectator
                ));
            }

            return snapshots.Count == 0 ? Empty : new Grid(cellSizeBlocks, snapshots.ToArray());
        }

        public bool TryGetPlayer(string playerUid, out GridPlayer player)
        {
            if (!string.IsNullOrEmpty(playerUid) && playerIndexByUid.TryGetValue(playerUid, out int index) && index >= 0 && index < players.Length)
            {
                player = players[index];
                return true;
            }

            player = default;
            return false;
        }

        public int CollectNear(int dimension, double x, double z, int radiusBlocks, List<GridPlayer> into, bool clear = true)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            if (clear) into.Clear();
            if (radiusBlocks < 0) return into.Count;
            if (!cellsByDimension.TryGetValue(dimension, out var dimensionCells)) return into.Count;

            int centerCellX = CoordToCell(x);
            int centerCellZ = CoordToCell(z);
            int radiusCells = (radiusBlocks + (CellSize - 1)) >> CellShift;

            for (int cellZ = centerCellZ - radiusCells; cellZ <= centerCellZ + radiusCells; cellZ++)
            {
                for (int cellX = centerCellX - radiusCells; cellX <= centerCellX + radiusCells; cellX++)
                {
                    if (!dimensionCells.TryGetValue(CellKey(cellX, cellZ), out var bucket)) continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        int playerIndex = bucket[i];
                        if ((uint)playerIndex < (uint)players.Length) into.Add(players[playerIndex]);
                    }
                }
            }

            return into.Count;
        }

        public int CoordToCell(double coord) => BlockToCell((int)Math.Floor(coord));
        public int BlockToCell(int blockCoord) => blockCoord >> CellShift;

        public static long CellKey(int cellX, int cellZ) => ((long)cellX << 32) | (uint)cellZ;

        public static int SanitizeCellSize(int cellSizeBlocks)
        {
            if (cellSizeBlocks < 8) cellSizeBlocks = 8;
            if (cellSizeBlocks > 512) cellSizeBlocks = 512;

            int powerOfTwo = 1;
            while (powerOfTwo < cellSizeBlocks) powerOfTwo <<= 1;
            return powerOfTwo;
        }

        private void AddToCell(int playerIndex, int dimension, int cellX, int cellZ)
        {
            if (!cellsByDimension.TryGetValue(dimension, out var dimensionCells))
            {
                dimensionCells = new Dictionary<long, List<int>>(256);
                cellsByDimension[dimension] = dimensionCells;
            }

            long key = CellKey(cellX, cellZ);
            if (!dimensionCells.TryGetValue(key, out var bucket))
            {
                bucket = new List<int>(4);
                dimensionCells[key] = bucket;
            }

            bucket.Add(playerIndex);
        }
    }

    internal readonly struct GridPlayer
    {
        public readonly IServerPlayer Player;
        public readonly string PlayerUID;
        public readonly string PlayerName;

        public readonly int Dimension;
        public readonly double X, Y, Z;

        public readonly bool IsSpectator;

        public GridPlayer(IServerPlayer player, int dimension, double x, double y, double z, bool isSpectator)
        {
            Player = player;
            PlayerUID = player?.PlayerUID ?? "";
            PlayerName = player?.PlayerName ?? "";
            Dimension = dimension;
            X = x; Y = y; Z = z;
            IsSpectator = isSpectator;
        }
    }
}
