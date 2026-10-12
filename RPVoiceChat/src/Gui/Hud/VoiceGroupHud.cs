using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using RPVoiceChat.Audio;
using RPVoiceChat.Client;
using RPVoiceChat.Config;
using RPVoiceChat.Networking;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace RPVoiceChat.Gui
{
    public class VoiceGroupHud : HudElement
    {
        private readonly VoiceGroupClientManager groups;
        private readonly MicrophoneManager microphone;
        private readonly AudioOutputManager output;
        private readonly long tick;
        private readonly List<(string Uid, bool Online, GroupHighlight Highlight, GuiElementDynamicText Status)> rows = new();
        private VoiceGroupUiStatePacket lastState;
        private double lastScale;
        private int lastMaxRows;

        public VoiceGroupHud(ICoreClientAPI capi, VoiceGroupClientManager groups, MicrophoneManager microphone, AudioOutputManager output) : base(capi)
        {
            this.groups = groups;
            this.microphone = microphone;
            this.output = output;
            tick = capi.Event.RegisterGameTickListener(Update, 100);
        }

        private void Update(float dt)
        {
            if (capi.Render.FrameWidth == 0 || capi.Render.FrameHeight == 0) return;
            var group = groups.OwnGroup;
            if (!ModConfig.ClientConfig.ShowHud || !groups.State.Enabled || group == null)
            {
                if (IsOpened()) TryClose();
                return;
            }

            int maxRows = Math.Max(1, (int)((capi.Render.FrameHeight / RuntimeEnv.GUIScale - 180) / 34));
            if (lastState != groups.State || lastMaxRows != maxRows || lastScale != RuntimeEnv.GUIScale)
            {
                var members = group.Members.OrderBy(uid => uid != capi.World.Player?.PlayerUID)
                    .ThenBy(uid => groups.PlayerName(uid), StringComparer.OrdinalIgnoreCase).ToArray();
                Compose(group.Name, members, maxRows);
                lastState = groups.State;
                lastMaxRows = maxRows;
                lastScale = RuntimeEnv.GUIScale;
            }
            if (!IsOpened()) TryOpen();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                bool talking = row.Online && (row.Uid == capi.World.Player?.PlayerUID
                    ? microphone.Transmitting && !ModConfig.ClientConfig.IsMuted
                        && RPVoiceChatClient.VoiceBanManagerInstance?.IsPlayerBanned(row.Uid) != true
                    : output.IsPlayerTalking(row.Uid));
                if (row.Highlight.Speaking == talking) continue;
                row.Highlight.Speaking = talking;
                row.Status.SetNewText(talking ? UIUtils.I18n("Gui.VoiceGroups.Speaking") : "");
            }
        }

        private void Compose(string name, string[] members, int maxRows)
        {
            SingleComposer?.Dispose();
            rows.Clear();
            int rowCount = Math.Min(maxRows, members.Length);
            int columns = Math.Max(1, (members.Length + maxRows - 1) / maxRows);
            var bounds = new ElementBounds
            {
                Alignment = EnumDialogArea.RightMiddle,
                BothSizing = ElementSizing.Fixed,
                fixedWidth = columns * 270,
                fixedHeight = 34 + rowCount * 34,
                fixedPaddingX = 12,
                fixedPaddingY = 12
            };
            var composer = capi.Gui.CreateCompo("rpvcVoiceGroupHud", bounds)
                .AddStaticText(UIUtils.I18n("Gui.VoiceGroups.HudTitle", name), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 0, 265, 30));
            for (int i = 0; i < members.Length; i++)
            {
                double x = (i / maxRows) * 270;
                double y = 34 + (i % maxRows) * 34;
                string uid = members[i];
                bool online = groups.IsOnline(uid);
                string playerName = groups.PlayerName(uid);
                if (playerName.Length > 21) playerName = playerName.Substring(0, 20) + "…";
                composer.AddInteractiveElement(new GroupHighlight(capi, ElementBounds.Fixed(x, y, 262, 30)), "highlight" + i);
                composer.AddDynamicText(playerName, CairoFont.WhiteSmallText().WithFontSize(14).WithColor(online
                        ? new double[] { 1, 1, 1, 1 } : new double[] { 0.6, 0.6, 0.6, 1 }),
                    ElementBounds.Fixed(x + 10, y + 5, 165, 22), "name" + i);
                composer.AddDynamicText(online ? "" : UIUtils.I18n("Gui.VoiceGroups.Offline"),
                    CairoFont.WhiteSmallText().WithFontSize(12), ElementBounds.Fixed(x + 180, y + 7, 80, 20), "status" + i);
            }
            SingleComposer = composer.Compose();
            for (int i = 0; i < members.Length; i++)
                rows.Add((members[i], groups.IsOnline(members[i]), (GroupHighlight)SingleComposer.GetElement("highlight" + i), SingleComposer.GetDynamicText("status" + i)));
        }

        // Cache both backgrounds. Speaking changes select a texture, without Cairo/GL allocations.
        private sealed class GroupHighlight : GuiElement
        {
            private int quietTexture;
            private int speakingTexture;
            public bool Speaking { get; set; }

            public GroupHighlight(ICoreClientAPI capi, ElementBounds bounds) : base(capi, bounds) { }

            public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic)
            {
                Bounds.CalcWorldBounds();
                CreateBackground(false, ref quietTexture);
                CreateBackground(true, ref speakingTexture);
            }

            private void CreateBackground(bool active, ref int texture)
            {
                using var surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt);
                using var ctx = new Context(surface);
                ctx.SetSourceRGBA(active ? 0.12 : 0.05, active ? 0.42 : 0.06, active ? 0.24 : 0.07, 0.88);
                ctx.Paint();
                if (active)
                {
                    ctx.SetSourceRGBA(0.4, 1, 0.6, 1);
                    ctx.Rectangle(0, 0, 3 * RuntimeEnv.GUIScale, Bounds.InnerHeight);
                    ctx.Fill();
                }
                generateTexture(surface, ref texture);
            }

            public override void RenderInteractiveElements(float deltaTime)
                => api.Render.Render2DTexture(Speaking ? speakingTexture : quietTexture, Bounds);

            public override void Dispose()
            {
                if (quietTexture != 0) api.Gui.DeleteTexture(quietTexture);
                if (speakingTexture != 0) api.Gui.DeleteTexture(speakingTexture);
                quietTexture = speakingTexture = 0;
                base.Dispose();
            }
        }

        public override void Dispose()
        {
            capi.Event.UnregisterGameTickListener(tick);
            rows.Clear();
            base.Dispose();
        }
    }
}
