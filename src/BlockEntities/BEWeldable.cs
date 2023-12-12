using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.BlockEntities
{
    public class BEWeldable : BlockEntityContainer
    {
        protected string i18nPrefix = "Welding";
        protected int NeededFlux = 0;
        protected int HammerHits;
        protected InventoryGeneric Inv;

        public override InventoryBase Inventory => throw new Exception("BEWeldable is meant to be inherited but wasn't!");

        public override string InventoryClassName => throw new Exception("BEWeldable is meant to be inherited but wasn't!");

        public virtual bool TestReadyToMerge(bool triggerMessage = true)
        {
            throw new Exception("BEWeldable is meant to be inherited but wasn't!");
        }

        public virtual void OnHammerHitOver(IPlayer byPlayer, Vec3d hitPosition)
        {
            throw new Exception("BEWeldable is meant to be inherited but wasn't!");
        }
    }
}
