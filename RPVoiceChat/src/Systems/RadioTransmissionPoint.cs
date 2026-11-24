using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Systems
{
    public readonly struct RadioTransmissionPoint
    {
        public RadioTransmissionPoint(Vec3d position, int rangeBlocks, string frequency, int dimension, bool isRepeaterRelay)
        {
            Position = position;
            RangeBlocks = rangeBlocks;
            Frequency = frequency;
            Dimension = dimension;
            IsRepeaterRelay = isRepeaterRelay;
        }

        public Vec3d Position { get; }
        public int RangeBlocks { get; }
        public string Frequency { get; }
        public int Dimension { get; }
        public bool IsRepeaterRelay { get; }
    }
}
