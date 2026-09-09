using RPVoiceChat.Util;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

public class BEBehaviorRingable : BlockEntityBehavior
{
    public string BellPartCode { get; set; } = string.Empty;
    public DateTime? LastRung { get; set; }

    public BEBehaviorRingable(Vintagestory.API.Common.BlockEntity blockEntity) : base(blockEntity)
    {
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        BellPartCode = tree.GetString("bellPartCode") ?? string.Empty;

        string lastRungStr = tree.GetString("lastRung");
        if (!string.IsNullOrEmpty(lastRungStr))
        {
            if (DateTime.TryParseExact(lastRungStr, "o", null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                LastRung = parsed;
            }
            else
            {
                LastRung = null;
            }
        }
        else
        {
            LastRung = null;
        }

        base.FromTreeAttributes(tree, worldAccessForResolve);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetString("bellPartCode", BellPartCode);

        if (LastRung != null)
            tree.SetString("lastRung", LastRung?.ToString("o")); // "o" = ISO 8601
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