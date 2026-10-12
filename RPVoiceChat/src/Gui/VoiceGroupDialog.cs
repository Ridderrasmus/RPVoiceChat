using System;
using System.Linq;
using RPVoiceChat.Client;
using RPVoiceChat.Networking;
using RPVoiceChat.Util;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class VoiceGroupDialog : GuiDialog
    {
        private const int PageSize = 6;
        private readonly VoiceGroupClientManager groups;
        private int tab;
        private int page;
        private string newName = "";
        private string inviteUid;
        private string status = "";
        private bool statusError;
        private string confirmation;
        private Action confirmedAction;
        private bool queued;
        private bool disposed;
        public override string ToggleKeyCombinationCode => null;

        public VoiceGroupDialog(ICoreClientAPI capi, VoiceGroupClientManager groups) : base(capi)
        {
            this.groups = groups;
            groups.Changed += QueueCompose;
            groups.ActionResult += OnResult;
        }

        private static string T(string key, params object[] args) => UIUtils.I18n("Gui.VoiceGroups." + key, args);
        private static string Short(string text, int length = 28) => text?.Length > length ? text.Substring(0, length - 1) + "…" : text ?? "";

        public override bool TryOpen()
        {
            Compose();
            bool opened = base.TryOpen();
            if (opened) groups.Refresh();
            return opened;
        }

        public override void OnGuiClosed()
        {
            confirmation = null;
            confirmedAction = null;
            base.OnGuiClosed();
        }

        private void QueueCompose()
        {
            if (disposed || queued || !IsOpened()) return;
            queued = true;
            capi.Event.EnqueueMainThreadTask(() =>
            {
                queued = false;
                if (!disposed && IsOpened()) Compose();
            }, "rpvoicechat:groupDialog");
        }

        private void OnResult(VoiceGroupActionResultPacket result)
        {
            status = result.Message;
            statusError = !result.Success;
            QueueCompose();
        }

        private void Send(VoiceGroupAction action, string name = null, string uid = null, bool value = false)
        {
            if (!groups.Send(action, name, uid, value)) return;
            status = T("Working");
            statusError = false;
            confirmation = null;
            confirmedAction = null;
            QueueCompose();
        }

        private void Confirm(string message, Action action)
        {
            confirmation = message;
            confirmedAction = action;
            QueueCompose();
        }

        private void Button(GuiComposer composer, string text, double x, double y, double width, Action action, bool enabled = true)
        {
            string key = "button-" + x + "-" + y;
            composer.AddSmallButton(text, () =>
            {
                if (enabled && !groups.ActionPending) action();
                return true;
            }, ElementBounds.Fixed(x, y, width, 28), key: key);
            ((GuiElementTextButton)composer.GetElement(key)).Enabled = enabled && !groups.ActionPending;
        }

        private static void Label(GuiComposer composer, string text, double x, double y, double width = 610, double height = 30)
            => composer.AddStaticText(text, CairoFont.WhiteSmallText(), ElementBounds.Fixed(x, y, width, height));

        private void Compose()
        {
            SingleComposer?.Dispose();
            var bg = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing(ElementSizing.FitToChildren);
            var composer = capi.Gui.CreateCompo("rpvcVoiceGroups", ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(bg)
                .AddDialogTitleBar(T("Title"), () => TryClose())
                .BeginChildElements(bg);
            Button(composer, T("MyGroup"), 0, 40, 170, () => ChangeTab(0));
            Button(composer, T("Browse"), 180, 40, 170, () => ChangeTab(1));
            Button(composer, T("Invitations", groups.State.Invitations.Count), 360, 40, 170, () => ChangeTab(2));
            Button(composer, T("Refresh"), 540, 40, 100, () => groups.Refresh());

            if (!groups.IsReady) Label(composer, T("Loading"), 0, 95);
            else if (!groups.State.Enabled)
            {
                Label(composer, T("Disabled"), 0, 95, 620, 65);
                if (groups.State.CanManageServer)
                    Button(composer, T("Enable"), 0, 175, 270, () => Send(VoiceGroupAction.SetEnabled, value: true));
            }
            else if (confirmation != null)
            {
                Label(composer, confirmation, 0, 120, 620, 100);
                Button(composer, T("Confirm"), 0, 240, 180, () => confirmedAction?.Invoke());
                Button(composer, T("Cancel"), 195, 240, 180, () => { confirmation = null; confirmedAction = null; QueueCompose(); });
            }
            else if (tab == 0) ComposeMyGroup(composer);
            else if (tab == 1) ComposeBrowse(composer);
            else ComposeInvitations(composer);

            composer.AddStaticText(status, CairoFont.WhiteSmallText().WithColor(statusError
                    ? new double[] { 1, 0.55, 0.45, 1 } : new double[] { 0.65, 0.9, 0.7, 1 }),
                ElementBounds.Fixed(0, 520, 640, 65));
            SingleComposer = composer.EndChildElements().Compose();
            (SingleComposer.GetElement("newGroupName") as GuiElementTextInput)?.SetValue(newName);
        }

        private void ChangeTab(int selected)
        {
            tab = selected;
            page = 0;
            confirmation = null;
            confirmedAction = null;
            QueueCompose();
        }

        private void ComposeMyGroup(GuiComposer composer)
        {
            var group = groups.OwnGroup;
            if (group == null)
            {
                Label(composer, T("NoGroup"), 0, 100, 620, 70);
                Button(composer, T("FindGroup"), 0, 200, 250, () => ChangeTab(1));
                return;
            }
            bool owner = group.OwnerPlayerUid == capi.World.Player?.PlayerUID;
            bool manage = owner || groups.State.CanManageServer;
            Label(composer, T("GroupSummary", group.Name, group.Members.Count), 0, 90, 430);
            Label(composer, T("Owner", Short(groups.PlayerName(group.OwnerPlayerUid))), 0, 120, 430);
            Button(composer, T(group.InviteOnly ? "MakeOpen" : "MakePrivate"), 440, 95, 200,
                () => Send(VoiceGroupAction.SetInviteOnly, value: !group.InviteOnly), manage);

            var members = group.Members.OrderBy(uid => uid != group.OwnerPlayerUid)
                .ThenBy(uid => groups.PlayerName(uid), StringComparer.OrdinalIgnoreCase).ToArray();
            ClampPage(members.Length);
            int row = 0;
            foreach (string uid in members.Skip(page * PageSize).Take(PageSize))
            {
                string name = groups.PlayerName(uid);
                string detail = !groups.IsOnline(uid) ? T("Offline") : uid == group.OwnerPlayerUid ? T("GroupOwner") : T("Online");
                Label(composer, Short(name, 32), 0, 164 + row * 34, 340);
                Label(composer, detail, 345, 164 + row * 34, 160);
                if (manage && uid != group.OwnerPlayerUid && uid != capi.World.Player?.PlayerUID)
                    Button(composer, T("Remove"), 530, 158 + row * 34, 110,
                        () => Confirm(T("ConfirmKick", name), () => Send(VoiceGroupAction.Kick, uid: uid)));
                row++;
            }
            Pagination(composer, members.Length, 369);

            var candidates = groups.State.Players.Where(p => p.Online && !group.Members.Contains(p.Uid))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            if (manage && candidates.Length > 0)
            {
                if (!candidates.Any(p => p.Uid == inviteUid)) inviteUid = candidates[0].Uid;
                composer.AddDropDown(candidates.Select(p => p.Uid).ToArray(), candidates.Select(p => p.Name).ToArray(),
                    Array.FindIndex(candidates, p => p.Uid == inviteUid), (value, _) => inviteUid = value,
                    ElementBounds.Fixed(0, 414, 425, 28), "invitePlayer");
                Button(composer, T("Invite"), 440, 414, 200, () => Send(VoiceGroupAction.Invite, uid: inviteUid));
            }
            else if (manage) Label(composer, T("NoPlayersToInvite"), 0, 415);

            Button(composer, T("Leave"), 0, 467, 200,
                () => Confirm(T(owner ? "ConfirmOwnerLeave" : "ConfirmLeave", group.Name), () => Send(VoiceGroupAction.Leave)));
            if (manage) Button(composer, T("Delete"), 220, 467, 200,
                () => Confirm(T("ConfirmDelete", group.Name), () => Send(VoiceGroupAction.Delete, group.Name)));
        }

        private void ComposeBrowse(GuiComposer composer)
        {
            composer.AddTextInput(ElementBounds.Fixed(0, 94, 425, 30), value => newName = value,
                CairoFont.TextInput(), "newGroupName");
            Button(composer, T("Create"), 440, 94, 200, () => Send(VoiceGroupAction.Create, newName), groups.OwnGroup == null);
            Label(composer, T("CreateHint"), 0, 133, 630);
            var all = groups.State.Groups.OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            ClampPage(all.Length);
            int row = 0;
            foreach (var group in all.Skip(page * PageSize).Take(PageSize))
            {
                bool invited = groups.State.Invitations.Contains(group.Name);
                bool own = groups.OwnGroup?.Name == group.Name;
                Label(composer, Short(group.Name, 24), 0, 180 + row * 38, 240);
                Label(composer, T(group.InviteOnly ? "PrivateSummary" : "OpenSummary", group.Members.Count), 245, 180 + row * 38, 160);
                Button(composer, T(own ? "Joined" : "Join"), 420, 174 + row * 38, 105, () =>
                {
                    if (groups.OwnGroup != null)
                        Confirm(T("ConfirmSwitch", group.Name), () => Send(VoiceGroupAction.Join, group.Name));
                    else Send(VoiceGroupAction.Join, group.Name);
                }, !own && (!group.InviteOnly || invited));
                if (groups.State.CanManageServer)
                    Button(composer, T("Delete"), 535, 174 + row * 38, 105,
                        () => Confirm(T("ConfirmDelete", group.Name), () => Send(VoiceGroupAction.Delete, group.Name)));
                row++;
            }
            if (all.Length == 0) Label(composer, T("NoGroups"), 0, 200);
            Pagination(composer, all.Length, 417);
            if (groups.State.CanManageServer)
                Button(composer, T("Disable"), 0, 467, 310,
                    () => Confirm(T("ConfirmDisable"), () => Send(VoiceGroupAction.SetEnabled, value: false)));
        }

        private void ComposeInvitations(GuiComposer composer)
        {
            Label(composer, T("InvitationsHint"), 0, 95, 620, 60);
            var invites = groups.State.Invitations;
            ClampPage(invites.Count);
            int row = 0;
            foreach (string name in invites.Skip(page * PageSize).Take(PageSize))
            {
                Label(composer, Short(name), 0, 174 + row * 40, 350);
                Button(composer, T("Accept"), 370, 168 + row * 40, 125, () =>
                {
                    if (groups.OwnGroup != null)
                        Confirm(T("ConfirmSwitch", name), () => Send(VoiceGroupAction.Accept, name));
                    else Send(VoiceGroupAction.Accept, name);
                });
                Button(composer, T("Decline"), 510, 168 + row * 40, 130, () => Send(VoiceGroupAction.Decline, name));
                row++;
            }
            if (invites.Count == 0) Label(composer, T("NoInvitations"), 0, 180);
            Pagination(composer, invites.Count, 435);
        }

        private void ClampPage(int count) => page = Math.Min(page, Math.Max(0, (count - 1) / PageSize));

        private void Pagination(GuiComposer composer, int count, int y)
        {
            if (count <= PageSize) return;
            Button(composer, T("Previous"), 0, y, 150, () => { page--; QueueCompose(); }, page > 0);
            Label(composer, T("Page", page + 1, (count + PageSize - 1) / PageSize), 175, y + 5, 260);
            Button(composer, T("Next"), 490, y, 150, () => { page++; QueueCompose(); }, (page + 1) * PageSize < count);
        }

        public override void Dispose()
        {
            disposed = true;
            groups.Changed -= QueueCompose;
            groups.ActionResult -= OnResult;
            base.Dispose();
        }
    }
}
