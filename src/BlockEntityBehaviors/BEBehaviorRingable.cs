using RPVoiceChat.Utils;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RPVoiceChat.GameContent.BlockEntityBehaviors
{
    public class BEBehaviorRingable : BlockEntityBehavior
    {
        public string BellPartCode { get; set; } = string.Empty;
        public DateTime? LastRung { get; set; }

        public BEBehaviorRingable(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            BellPartCode = tree.GetString("bellPartCode") ?? string.Empty;
            LastRung = tree.GetString("lastRung") != null ? DateTime.Parse(tree.GetString("lastRung")) : null;
            base.FromTreeAttributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetString("bellPartCode", BellPartCode);
            if (LastRung != null)
                tree.SetString("lastRung", LastRung?.ToString());
            else if (tree.HasAttribute("lastRung"))
                tree.RemoveAttribute("lastRung");
            base.ToTreeAttributes(tree);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (!string.IsNullOrWhiteSpace(BellPartCode))
            {
                dsc.AppendLine(UIUtils.I18n("General.BellAttached"));
            }
        }
    }
}