using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace rpvoicechat
{
    public class WaveFormatStereo : WaveFormat
    {
        public WaveFormatStereo() : base(AudioUtils.sampleRate, 16, 2)
        {
        }

        public override string ToString()
        {
            return $"{SampleRate} Hz, {BitsPerSample} bit, {Channels} channel PCM";
        }
    }
}
