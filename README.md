# SNS Host Rules

**A host-only rules-enforcement mod for Shift ’N Seek in Among Us.** Only the host installs the mod; other players can join with an unmodified game client.

**Current release: v0.3.12** — tested in multiplayer by the project maintainer.

> **Compatibility:** Among Us **2026.9.29**, **Windows Steam (64-bit)**, **BepInEx 6 Unity IL2CPP (Windows x64)**. Other Among Us versions, storefronts, and operating systems are **not yet verified**. Game updates can break the mod.

## Features

- Automatically announces the Shift ’N Seek rules, including at game start.
- Blocks impostor door closing, unauthorized sabotages, and illegal kills.
- Prevents impostors from remaining inside vents.
- Provides host-enforced warnings and penalties for rule violations.
- Maintains meeting/ejection safeguards and a 10-second start countdown.
- Displays Innersloth's official mod stamp and uses **host-only +25 online lobby registration**.
- **F7** toggles SNS gameplay-rule enforcement **for the host**.

**Important:** SNS modifies gameplay and is meant to be used transparently in Shift ’N Seek lobbies. Registered modded lobbies might not appear in the normal public matchmaking list; joining by room code is recommended. Turning SNS enforcement off with F7 does **not** undo network registration for an already running game.

## Installation (for players and hosts)

You need:

1. A legitimate copy of **Among Us on Windows Steam**, matching the supported game version.
2. **BepInEx 6 — Unity IL2CPP, Windows x64**. Download it from the [official BepInEx bleeding-edge builds](https://builds.bepinex.dev/projects/bepinex_be). Look for a file named similarly to `BepInEx-Unity.IL2CPP-win-x64-...zip` (the build number may differ). **Do not download the Mono or win-x86 build.**
3. The compiled **`SNSHostRules.dll`** from this repository's **Releases** page (open the newest compatible release and find the DLL under **Assets**). **Do not download GitHub's automatic “Source code (zip)” as the installer.**

### First-time setup

1. **Close Among Us.**
2. In Steam, right-click **Among Us → Manage → Browse local files**. This opens the folder containing `Among Us.exe`. For many Steam installations the folder is:
   `C:\Program Files (x86)\Steam\steamapps\common\Among Us`
   Your install location may differ.
3. Extract the **contents** of the BepInEx 6 Unity IL2CPP **win-x64** archive directly into that folder, alongside `Among Us.exe` — not inside an extra subfolder. The game directory should now have a `BepInEx` folder and the BepInEx loader files.
4. **Launch Among Us once**, let BepInEx initialize, then close the game. The first startup may take longer while IL2CPP interop files are generated.
5. Open `Among Us\BepInEx\plugins\` (create `plugins` if it does not exist).
6. Copy the downloaded **`SNSHostRules.dll`** into that `plugins` folder. Do **not** put the DLL next to `Among Us.exe`, inside `BepInEx\core`, or in `BepInEx\interop`.
7. Launch Among Us again. The SNS status indicator and the official mod stamp should be visible. If needed, review BepInEx's log file under `Among Us\BepInEx\` (usually `LogOutput.log` or `LogOutput.txt`) for `Loading [SNS Host Rules 0.3.12]` or `SNS Host Rules v0.3.12 loaded`.
8. **Create a lobby as host** and press **F7** to enable enforcement. Press F7 again to disable SNS rule enforcement. Non-host players cannot activate SNS on their own.

Example installation layout:

```text
Among Us/
├── Among Us.exe
├── BepInEx/
│   ├── core/
│   ├── interop/                 (created at first startup)
│   └── plugins/
│       └── SNSHostRules.dll    <-- install the release asset here
├── ... BepInEx loader files ...
└── Among Us_Data/
```

**Other players joining your lobby do not need to install BepInEx or SNS.** Only the host does. For online play, everyone should use compatible Among Us game versions and the same server region. Share the room code if the lobby does not appear in the public list.

### Updating / removing SNS

- **Update:** Close the game and replace `BepInEx\plugins\SNSHostRules.dll` with the newer release DLL. Back up the old DLL elsewhere first. Never leave two active SNS DLLs in plugins.
- **Temporarily disable:** Close the game and rename `SNSHostRules.dll` to `SNSHostRules.dll.disabled`, then restart the game.
- **Uninstall SNS only:** Close the game and remove `BepInEx\plugins\SNSHostRules.dll`. Do not remove all of BepInEx if other mods rely on it.
- **Want ordinary vanilla networking?** Close the game, disable/remove the SNS DLL, and restart. F7 only toggles rule enforcement; it does not undo the online **+25** registration.

### Troubleshooting

- **Nothing appears in game:** Verify `BepInEx 6 Unity IL2CPP win-x64`, the game version, and the DLL's exact location. Confirm that BepInEx itself loads by checking its log.
- **The game crashes or patches fail after updating Among Us:** SNS is only verified on **2026.9.29**. A different game version may require an SNS update.
- **A player cannot find your lobby:** Official-server modded lobbies may not be visible in the regular list. Try joining with the private code and the same region. Do not remove mod registration to attempt to force public visibility.
- **F7 does nothing:** Ensure you are the **host**. Review the SNS status label and BepInEx log. Try pressing F7 while the game window has focus.
- **Don't know which archive to download:** For ordinary players, choose the `SNSHostRules.dll` **release asset**, not the source project, build script, or automatic GitHub source ZIP.

## Building from source (developers only)

Normal players **do not need** Visual Studio, the .NET SDK, or PowerShell scripts to use SNS.

Developers need a .NET SDK capable of targeting **`net6.0`**, and an installed Among Us game with **BepInEx 6 IL2CPP already launched once** so that `BepInEx/interop` exists. The `.csproj` references the game's locally generated interop assemblies and the BepInEx/Unity/Harmony dependencies: do **not** redistribute those assemblies with this project.

From a PowerShell terminal in the source project directory:

```powershell
dotnet build .\SNSHostRules.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Among Us"
```

Adjust `GameDir` to the actual game installation path. Build output:

```text
bin\Release\net6.0\SNSHostRules.dll
```

Or build, back up the installed version, and install automatically:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\build-and-check.ps1" -Install
```

The script defaults to Steam's common Windows game path. If installed elsewhere, pass `-GameDir "YOUR_GAME_DIRECTORY"`. Review the script before running it; PowerShell's execution policy may otherwise reject an unsigned `.ps1` file. This script is **for developers only** and is not part of the player install process.

## License and acknowledgement

The repository includes a `LICENSE` file containing the **GNU General Public License v3.0**. Review your intended licensing and rights to the code before publishing.

SNS Host Rules is an independent fan-made mod and is not officially supported by Innersloth. It uses the game-provided mod stamp and registers gameplay changes on official-server lobby creation. Refer to [Innersloth's Among Us Mod Policy](https://www.innersloth.com/among-us-mod-policy/) and [technical modding documentation](https://github.com/Innersloth-LLC/AmongUsModdingInformation).

**Required mod policy disclaimer:**

> This mod is not affiliated with Among Us or Innersloth LLC, and the content contained therein is not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein are property of Innersloth LLC. © Innersloth LLC.
