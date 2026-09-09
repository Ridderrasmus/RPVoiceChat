using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using RPVoiceChat.GameContent.BlockEntity;

namespace RPVoiceChat.Systems
{
    public class PrinterSystem : ModSystem
    {
        private List<BlockEntityTelegraph> telegraphsWithPrinters = new List<BlockEntityTelegraph>();
        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            
            // Register a timer to check for auto-save every second
            api.Event.RegisterGameTickListener(OnGameTick, 1000); // Every second
        }

        private void OnGameTick(float dt)
        {
            // Check all telegraphs with printers for auto-save
            for (int i = telegraphsWithPrinters.Count - 1; i >= 0; i--)
            {
                var telegraph = telegraphsWithPrinters[i];
                if (telegraph?.Api?.World == null)
                {
                    telegraphsWithPrinters.RemoveAt(i);
                    continue;
                }

                telegraph.CheckAutoSave();
            }
        }

        public void RegisterTelegraphWithPrinter(BlockEntityTelegraph telegraph)
        {
            if (!telegraphsWithPrinters.Contains(telegraph))
            {
                telegraphsWithPrinters.Add(telegraph);
            }
        }

        public void UnregisterTelegraphWithPrinter(BlockEntityTelegraph telegraph)
        {
            telegraphsWithPrinters.Remove(telegraph);
        }
    }
}
