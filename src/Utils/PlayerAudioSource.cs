using OpenTK.Audio.OpenAL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;

namespace rpvoicechat
{
    public class PlayerAudioSource
    {
        public Vec3d AudioPos { get; set; }
        public bool IsMuffled { get; set; }
        public int SourceNum { get; set; }
        public int BufferNum { get; set; }
        public Queue<byte[]> AudioQueue { get; set; }
        public float Volume { get; internal set; }

        public PlayerAudioSource(Vec3d audioPos, bool isMuffled)
        {
            AudioPos = audioPos;
            IsMuffled = isMuffled;
            SourceNum = AL.GenSource();
            BufferNum = AL.GenBuffer();
            AudioQueue = new Queue<byte[]>();
        }
    }
}
