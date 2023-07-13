using NAudio.Wave;

namespace rpvoicechat
{
    public class WaveFormatMono : WaveFormat
    {
        public WaveFormatMono() : base(AudioUtils.sampleRate, 16, 1)
        {
        }

        public override string ToString()
        {
            return $"{SampleRate} Hz, {BitsPerSample} bit, {Channels} channel PCM";
        }
    }
}
