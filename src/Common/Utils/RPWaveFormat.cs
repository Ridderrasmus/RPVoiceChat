

using NAudio.Wave;

namespace rpvoicechat
{

    //public class PlayerWaveOut : WaveOut
    //{
    //    public PlayerWaveOut(BufferedWaveProvider bufferedWaveProvider) : base()
    //    {
    //        bufferedWaveProvider.DiscardOnBufferOverflow = true;
    //        this.Init(bufferedWaveProvider);
    //        this.Play();
    //    }
    //}
    

    public class WaveFormatMono : WaveFormat
    {
        public WaveFormatMono() : base(AudioUtils.sampleRate, 16, 1)
        {

        }

    }

    public class WaveFormatStereo : WaveFormat
    {
        public WaveFormatStereo() : base(AudioUtils.sampleRate, 16, 2)
        {

        }

    }
}
