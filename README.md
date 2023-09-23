# RPVoiceChat - A proximity voip mod for Vintage Story

## Table of contents
- [About](#about)
- [Installation](#installation)
- [Usage](#usage)
- [Configuration (For users)](#configuration-users)
- [Configuration (For server owners)](#configuration-server)
- [Maintainers](#maintainers)

## About
RPVoiceChat is a proximity voip mod for Vintage Story. It allows players to talk to each other in-game using their microphone. The volume of the voice depends on the distance between the players. The mod is partially inspired by the mod [TFAR](https://steamcommunity.com/workshop/filedetails/?id=894678801) for [Arma3](https://arma3.com/) as well as the mod [Simple Voice Chat](https://www.curseforge.com/minecraft/mc-mods/simple-voice-chat) for [Minecraft](https://www.minecraft.net/en-us).

Not only does the mod allow players to talk to each other in-game, but it also adds various communication-centric content to the game. But that will all be configurable.

## Installation
1. Download the latest version of the mod from the [mod page](https://mods.vintagestory.at/rpvoicechat).
2. Place the downloaded file in the `Mods` folder of your Vintage Story installation.
3. Start the game and enjoy!
(4.) Optional: Double check your keybinds in the settings menu. All keybinds start with `RPVC` and are located in the hud and interface category.
 
## Usage
### Talking
To talk to other players you will need to set up your audio input threshold or turn on push to talk (PTT) in the config menu.
The default keybind for the config menu is `P` and the default keybind for PTT is `CAPS LOCK`. (Note as of current game version these keybinds cannot be applied to mouse buttons)

### Different voice levels
There are currently 3 different voice levels in the mod. These are whisper, normal and shout.
The default keybind to change between these is `Shift + Tab`. This can be changed in the settings menu.

### Configuring audio
You can configure your microphone volume and the volume of everyone else in your config menu.

## <a name="configuration-users"></a>Configuration (For users)
The mod is highly configurable and can be configured more deeply in the modconfig file. You'll find this file in the `ModConfig` folder in the same directory as your `Mods` folder. The file is called `rpvoicechat.json`.
The settings relevant to users are:
- `ManualPortForwarding` - Determines whether the mod should skip port forwarding with UPnP and assume that ports are open. **Setting this to true means that you take full responsibility for opening ports and if they are not open mod will work incorrectly.** The default value is `false`.
- `PushToTalkEnabled` - Whether push to talk is enabled. The default value is `false`.
- `IsLoopbackEnabled` - The setting that defines whether you should be able to hear what you're transmitting. The default value is `false`.
- `IsDenoisingEnabled` - Whether or not your audio should be denoised. The default value is `false`.
- `IsHUDShown` - Whether or not the HUD should be shown. The default value is `true`.
- `IsMuted` - Whether or not you are muted. The default value is `false`.
- `OutputGain` - The volume level of other players in percent (0-200). The default value is `100`.
- `InputGain` - The volume level of your microphone in percent (0-100). The default value is `100`.
- `InputThreshold` - The current setting of your audio input threshold in percent (0-100). The default value is `20`.
- `MaxInputThreshold` - This configures how finely tuned the input threshold should be. The smaller number the more sensitive the input will be. The default value is `0.24`.
- `BackgroungNoiseThreshold` - Sensitivity of the denoiser in percent (0-100). 0 means all audio is voice, 100 means all audio is noise. If audio is detected as noise it will be denoised at max strength. The default value is `50`.
- `VoiceDenoisingStrength` - Intensity of voice denoising in percent (0-100). Low value won't remove noise when you are speaking, high value may decrease audio quality. The default value is `80`.
 
## <a name="configuration-server"></a>Configuration (For server owners)
The mod is highly configurable and can be configured more deeply in the modconfig file. You'll find this file in the `ModConfig` folder in the same directory as your `Mods` folder. The file is called `rpvoicechat.json`.
The settings relevant to server owners are:
- `ManualPortForwarding` - Determines whether the mod should skip port forwarding with UPnP and assume that ports are open. **Setting this to true means that you take full responsibility for opening ports and if they are not open mod will work incorrectly.** The default value is `false`.
- `ServerPort` - The port to use for the voice networking if ports are being forwarded manually. The default value is `52525`.
- `ServerIP` - The ip to send to clients to connect to if manually port forwarding. The default value is `null`.
- `AdditionalContent` - Whether additional modded content(bells, horns, etc) should be enabled. The default value is `true`.

## Maintainers
Currently the mod is maintained by the following people:
- [Ridderrasmus](https://github.com/Ridderrasmus) - Creator and maintainer
- [Dmitry221060](https://github.com/Dmitry221060) - Maintainer
- [Faithfulshot](https://github.com/Faithfulshot) - 3d models
- [Nixie]() - Audio design

The mod was previously maintained by the following people:
- [blakdragan7](https://github.com/blakdragan7) - Maintainer

If you want to add something to the mod, feel free to make a pull request.
If you have any questions, feel free to contact me on discord (ridderrasmus).