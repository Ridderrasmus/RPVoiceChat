using NAudio.Wave;
using System;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Concentus.Structs;
using Concentus.Enums;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace rpvoicechat
{
    public class RPVoiceChatCommon : ModSystem
    {
        ICoreAPI api;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            this.api = api;

            // Register network channel
            api.Network.RegisterChannel("rpvoicechat")
                .RegisterMessageType(typeof(PlayerAudioPacket))
                .RegisterMessageType(typeof(ConnectionPacket));


            // Item registry (Not used yet)
            api.RegisterItemClass("ItemVoiceTransciever", typeof(ItemVoiceTransciever));

        }
    }
}
