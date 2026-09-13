using System;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using RPVoiceChat.Util;

namespace RPVoiceChat.Audio.Effects
{
    public abstract class SoundEffect : IDisposable
    {
        public string Name { get; }

        protected int source;
        protected int effect;
        protected int slot;
        private bool disposed;

        public bool IsEnabled { get; set; } = false;

        protected SoundEffect(string name, int source)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.source = source;
            var device = ALC.GetContextsDevice(ALC.GetCurrentContext());
            if (device == IntPtr.Zero || !ALC.IsExtensionPresent(device, "ALC_EXT_EFX"))
                throw new NotSupportedException("OpenAL EFX is unavailable; using dry voice.");
            OALW.ClearError();
            try
            {
                effect = GenerateEffect();
                if (AL.GetError() != ALError.NoError) throw new InvalidOperationException("Unable to configure OpenAL effect.");
            }
            catch { Dispose(); throw; }
        }

        public virtual void Apply()
        {
            if (disposed) return;
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

        protected static int AllocateEffect()
        {
            int id = ALC.EFX.GenEffect();
            if (AL.GetError() == ALError.NoError && id != 0) return id;
            if (id != 0) ALC.EFX.DeleteEffect(id);
            throw new InvalidOperationException("No OpenAL effects available.");
        }

        protected static int AllocateSlot()
        {
            int id = ALC.EFX.GenAuxiliaryEffectSlot();
            if (AL.GetError() == ALError.NoError && id != 0) return id;
            if (id != 0) ALC.EFX.DeleteAuxiliaryEffectSlot(id);
            throw new InvalidOperationException("No OpenAL effect slots available.");
        }

        protected static int AllocateFilter()
        {
            int id = ALC.EFX.GenFilter();
            if (AL.GetError() == ALError.NoError && id != 0) return id;
            if (id != 0) ALC.EFX.DeleteFilter(id);
            throw new InvalidOperationException("No OpenAL filters available.");
        }

        protected virtual void ReleaseFilters() { }

        public void Dispose()
        {
            if (disposed) return;
            Clear();
            disposed = true;
            ReleaseFilters();
            if (slot != 0)
            {
                ALC.EFX.AuxiliaryEffectSlot(slot, EffectSlotInteger.Effect, 0);
                ALC.EFX.DeleteAuxiliaryEffectSlot(slot);
                slot = 0;
            }
            if (effect != 0) { ALC.EFX.DeleteEffect(effect); effect = 0; }
        }

        private static readonly Dictionary<string, Func<int, SoundEffect>> registry = new()
        {
            { "cheapmic", source => new CheapMicEffect(source) },
            { "reverb", source => new ReverbEffect(source) },
        };

        public static SoundEffect Create(string name, int source)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Check if source is valid before creating effect
            if (source <= 0)
            {
                Console.WriteLine($"[RPVoiceChat] Cannot create SoundEffect \"{name}\": invalid source ID {source}");
                return null;
            }

            var key = name.ToLowerInvariant();

            if (registry.TryGetValue(key, out var factory))
            {
                try
                {
                    var effect = factory(source);
                    // Ensure the name is properly set on the created instance
                    return effect;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[RPVoiceChat] Error creating SoundEffect \"{name}\": {e.Message}");
                    return null;
                }
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
