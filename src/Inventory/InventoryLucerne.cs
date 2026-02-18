#nullable enable
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Inventory
{
    public class InventoryLucerne : InventoryGeneric
    {
        public SlotLucerneFat FatSlot => (SlotLucerneFat)slots[0];
        public SlotLucerneFirewood FirewoodSlot => (SlotLucerneFirewood)slots[1];

        public InventoryLucerne() : base(2, "lucerne-temp", "temp", null, OnNewSlot) { }

        public InventoryLucerne(ICoreAPI api, BlockPos? pos = null)
            : base(2, "lucerne-" + (pos?.ToString() ?? "fake"), pos?.ToString() ?? "-fake", api, OnNewSlot)
        {
            Pos = pos;
        }

        private static ItemSlot OnNewSlot(int slotId, InventoryGeneric self)
        {
            return slotId switch
            {
                0 => new SlotLucerneFat(self),
                1 => new SlotLucerneFirewood(self),
                _ => throw new System.ArgumentOutOfRangeException(nameof(slotId), "InventoryLucerne has 2 slots")
            };
        }
    }
}
