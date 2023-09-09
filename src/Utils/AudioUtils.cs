namespace RPVoiceChat.Utils
{
    public class AudioUtils
    {
        private static readonly AudioUtils _instance = new AudioUtils();
        public static AudioUtils Instance { get { return _instance; } }

        public static int sampleRate = 48000;

        static AudioUtils()
        {
        }

        private AudioUtils()
        {
        }

        // Apply a low pass filter to the audio data
        //public static byte[] ApplyMuffling(byte[] audio)
        //{
        //    return ApplyLowPassFilter(audio, sampleRate, 1000);
        //}


        //public static byte[] ApplyLowPassFilter(byte[] audioData, int sampleRate, float cutoffFrequency)
        //{
        //    // Convert byte array to short array
        //    short[] shortArray = new short[audioData.Length / 2];
        //    Buffer.BlockCopy(audioData, 0, shortArray, 0, audioData.Length);

        //    // Convert short array to float array
        //    float[] floatArray = Array.ConvertAll(shortArray, s => s / 32768f);

        //    // Create a low pass filter
        //    var filter = BiQuadFilter.LowPassFilter(sampleRate, cutoffFrequency, 1);

        //    // Apply the filter
        //    for (int i = 0; i < floatArray.Length; i++)
        //    {
        //        floatArray[i] = filter.Transform(floatArray[i]);
        //    }

        //    // Convert float array back to short array
        //    shortArray = Array.ConvertAll(floatArray, f => (short)(f * 32767));

        //    // Convert short array back to byte array
        //    byte[] filteredAudioData = new byte[shortArray.Length * 2];
        //    Buffer.BlockCopy(shortArray, 0, filteredAudioData, 0, filteredAudioData.Length);

        //    return filteredAudioData;
        //}
    }
}