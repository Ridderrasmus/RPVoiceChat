using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Inventory
{
    /// <summary>
    /// ItemSlot that only accepts items whose Collectible.Code.Path equals <see cref="AllowedPath"/>.
    /// Same pattern as former SlotPrinterPaper / SlotLucerneFat: restrict insert via CanHold only.
    /// </summary>
    public class SlotRestrictedItemPath : ItemSlot
    {
        /// <summary>Code path to match (e.g. "fat", "firewood", "paperslip", "telegram").</summary>
        public string AllowedPath { get; }

        public SlotRestrictedItemPath(InventoryBase inventory, string allowedPath, int maxSlotStackSize = 64)
            : base(inventory)
        {
            AllowedPath = allowedPath ?? "";
            MaxSlotStackSize = maxSlotStackSize;
        }

        public override bool CanHold(ItemSlot sourceSlot)
        {
            if (string.IsNullOrEmpty(AllowedPath) || sourceSlot?.Itemstack == null)
                return false;
            return sourceSlot.Itemstack.Collectible?.Code?.Path == AllowedPath;
        }
    }
}
