using System;
using System.Runtime.InteropServices;

namespace RPVoiceChat.Audio.Effects
{
    internal static class RNNoise
    {
        private const string Lib = "Lib/RNNoise.dll";
        public const int FRAME_SIZE = 480;

        [DllImport(Lib, EntryPoint = "rnnoise_create", ExactSpelling = true)]
        public static extern IntPtr Create(IntPtr model);

        [DllImport(Lib, EntryPoint = "rnnoise_destroy", ExactSpelling = true)]
        public static extern void Destroy(IntPtr state);

        [DllImport(Lib, EntryPoint = "rnnoise_process_frame", ExactSpelling = true)]
        public static extern float DenoiseFrame(IntPtr state, IntPtr dataOut, IntPtr dataIn);
    }
}