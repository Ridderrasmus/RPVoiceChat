# RPVoiceChat - A proximity voip mod for Vintage Story

## Table of contents
- [About](#about)
- [Installation](#installation)
- [Usage](#usage)
- [Configuration (For users)](#configuration-for-users)
- [Configuration (For server owners)](#configuration-for-server-owners)
- [Maintainers](#maintainers)
- [Licenses](#licenses)

## About
RPVoiceChat is a proximity voip mod for Vintage Story. It allows players to talk to each other in-game using their microphone. The volume of the voice depends on the distance between the players.

The mod is partially inspired by [TFAR](https://steamcommunity.com/workshop/filedetails/?id=894678801) for [Arma 3](https://arma3.com/) and [Simple Voice Chat](https://www.curseforge.com/minecraft/mc-mods/simple-voice-chat) for [Minecraft](https://www.minecraft.net/en-us).

**Documentation & Help**  
Most common questions are answered in the **Wiki**:  
https://github.com/Ridderrasmus/RPVoiceChat/wiki

If you encounter issues, check the **Troubleshooting guide**:  
https://github.com/Ridderrasmus/RPVoiceChat/wiki/Troubleshooting

---

## Installation
1. Click on the "1-click install" button or download the latest version of the mod from the [mod page](https://mods.vintagestory.at/rpvoicechat).
2. If you choose the "Download" option, place the downloaded file in the `Mods` folder of your Vintage Story installation.
3. Start the game and enjoy!
4. (Optional) Double-check your keybinds in the settings menu. All keybinds start with `RPVoice` and are located in the HUD and Interface category.

---

## Server Setup
1. Start your server.
2. Done!

For advanced setups and performance tuning, see:  
https://github.com/Ridderrasmus/RPVoiceChat/wiki/Server-Administration

---

## Usage

### Talking
To talk to other players you must configure your microphone input threshold or enable push-to-talk (PTT).

- Default config menu keybind: `;`
- Default PTT keybind: `CAPS LOCK`

> Note: As of the current game version, keybinds cannot be assigned to mouse buttons.

### Voice levels
The mod includes three voice levels:
- Whisper
- Normal
- Shout

The default keybind to cycle voice levels is `Shift + Tab`.

More details are available in the User Guide:  
https://github.com/Ridderrasmus/RPVoiceChat/wiki/User-Guide

---

## Configuration (For users)

All user-related configuration options are documented in the wiki:
https://github.com/Ridderrasmus/RPVoiceChat/wiki/User-Guide

Client configuration file location:
`ModConfig/rpvoicechat-client.json`

---

## Configuration (For server owners)

All server-side configuration options are documented here:
https://github.com/Ridderrasmus/RPVoiceChat/wiki/Server-Administration

Server configuration file location:
`ModConfig/rpvoicechat-server.json`

### In-game configuration commands
World-specific `/rpvc` commands are documented here:
https://github.com/Ridderrasmus/RPVoiceChat/wiki/Server-Administration#in-game-configuration-commands

---

## Maintainers
Currently maintained by:
- [Ridderrasmus](https://github.com/Ridderrasmus) – Creator and maintainer
- [Dmitry221060](https://github.com/Dmitry221060) – Maintainer
- [RomainOdeval](https://github.com/RomainOdeval) – Maintainer
- [Faithfulshot](https://github.com/Faithfulshot) – 3D models
- Nixie – Audio design

Previously maintained by:
- [blakdragan7](https://github.com/blakdragan7) – Maintainer

Want to contribute?
https://github.com/Ridderrasmus/RPVoiceChat/pulls

---

## Licenses
The mod is licensed under the [MIT License](LICENSE).

Used libraries:
- [Opus](https://opus-codec.org/) – BSD 3-Clause License
- [RNNoise](https://jmvalin.ca/demo/rnnoise/) – BSD License

Sound effects from [Freesound.org](https://freesound.org/):
- [Furniture – Drawers open & close](https://freesound.org/people/Vrymaa/sounds/802695/) by Vrymaa – CC0
- [Dot matrix printer](https://freesound.org/people/DisasterServices/sounds/320008/) by DisasterServices – CC0

