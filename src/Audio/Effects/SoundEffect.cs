using System;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using RPVoiceChat.Utils;

namespace RPVoiceChat.Audio.Effects
{
    public abstract class SoundEffect
    {
        public string Name { get; }

        protected int source;
        protected int effect;
        protected int slot;
        private int nullEffect;

        public bool IsEnabled { get; set; } = false;

        protected SoundEffect(string name, int source)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.source = source;
            this.nullEffect = ALC.EFX.GenEffect();
            this.effect = GenerateEffect();
        }

        public virtual void Apply()
        {
            var device = ALC.GetContextsDevice(ALC.GetCurrentContext());

            if (device == IntPtr.Zero || !ALC.IsExtensionPresent(device, "ALC_EXT_EFX"))
            {
                Logger.client.Debug("[SoundEffect] EFX not supported, effect ignored.");
                return;
            }

            if (IsEnabled)
                return;

            ALC.EFX.Source(source, EFXSourceInteger3.AuxiliarySendFilter, slot, 0, 0);
            IsEnabled = true;
        }

        public virtual void Clear()
        {
            if (!IsEnabled)
                return;

            ALC.EFX.Source(source, EFXSourceInteger3.AuxiliarySendFilter, 0, 0, 0);
            IsEnabled = false;
        }

        protected abstract int GenerateEffect();

        private static readonly Dictionary<string, Func<int, SoundEffect>> registry = new()
        {
            { "cheapmic", source => new CheapMicEffect(source) },
            { "reverb", source => new ReverbEffect(source) },
            { "intoxicated", source => new IntoxicatedEffect(source) },
        };

        public static SoundEffect Create(string name, int source)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var key = name.ToLowerInvariant();

            if (registry.TryGetValue(key, out var factory))
            {
                var effect = factory(source);
                // Ensure the name is properly set on the created instance
                return effect;
            }

            Console.WriteLine($"[RPVoiceChat] SoundEffect \"{name}\" not found in registry.");
            return null;
        }

        public static void Register(string name, Func<int, SoundEffect> factory)
        {
            if (string.IsNullOrWhiteSpace(name) || factory == null)
                return;

            var key = name.ToLowerInvariant();

            if (registry.ContainsKey(key))
            {
                Console.WriteLine($"[RPVoiceChat] Warning: SoundEffect \"{name}\" is already registered. It will be overwritten.");
            }

            registry[key] = factory;
        }
    }
}
