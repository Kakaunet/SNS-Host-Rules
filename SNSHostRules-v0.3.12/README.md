# SNS Host Rules v0.3.12 — official-server +25 host-only registration

**Target:** Among Us Windows Steam 2026.9.29; BepInEx 6 IL2CPP; host installs only.

## What changed

Built **directly on the user-tested, working v0.3.11**. All gameplay patches (door interception, forced vent ejection, illegal sabotages and kills, offender-attributed warning messages, voting/meeting rules, 10-second countdown, F7 toggle) and Innersloth's official `ModManager` stamp code are **unchanged**. The only code changes are:

1. The plugin version is updated to 0.3.12.
2. Two narrowly scoped Harmony hooks implement host-only official-server registration:
   - `Constants.GetBroadcastVersion()` postfix adds **25** to the int32 version on online connections, implementing the documented host-only authority flag.
   - `Constants.IsVersionModded()` prefix sets `true` on online connections, matching the established [TownOfHost implementation](https://github.com/tukasa0001/TownOfHost/blob/main/Patches/ConstantsPatch.cs).
3. Local-game mode is excluded from both modifications.
4. The original and modified protocol versions are logged **once per launch**.

The exact target method names and return types were checked against the user's 2026.9.29 `Assembly-CSharp.dll` interop assembly. No risky broad `UpdateSystem` patches are introduced.

## Why +25, not AU MCI GUID

The [official Innersloth modding technical document](https://github.com/Innersloth-LLC/AmongUsModdingInformation) states that adding +25 to the client network protocol version enables host authority for **host-only** mods. AU MCI registration via `CurrentModRegistration.ModRegistrationGuidString` is designed for **all-client** mods and separates matchmaking by mod GUID. SNS is host-only: **do not set an AU MCI GUID for SNS.**

The [Among Us Mod Policy](https://www.innersloth.com/among-us-mod-policy/) (last modified July 30, 2026) requires gameplay-changing mods on official servers to register on lobby creation. This release introduces the host-only mechanism, but **official-server acceptance is not testable by compiling**.

## Build and backed-up install

Exit Among Us first. From the extracted project directory:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\build-and-check.ps1" -Install
```

This is the same build/backup/install script as v0.3.11. The transient policy override applies only to the new PowerShell process. It backs up the previous DLL as `*.dll.disabled` before installing the new DLL. Nothing downloads or modifies global Windows execution policy.

Manual alternative:

```powershell
dotnet build .\SNSHostRules.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Among Us"
```

Then back up `Among Us\BepInEx\plugins\SNSHostRules.dll` and replace it with `bin\Release\net6.0\SNSHostRules.dll`. Keep your **known-working v0.3.11** for rollback.

## Online validation (with consenting players)

1. Start the modded game and look for all patch-success logs, especially:
   ```
   SNS patched: HostOnlyBroadcastVersionPatch
   SNS patched: HostOnlyIsModdedPatch
   SNS +25 registration: protocol <vanilla> -> <vanilla+25> (host-only authority flag; official-server acceptance requires testing).
   ```
2. **Create a new official-server lobby after restarting the game** (do not reuse a pre-registration room). Registration happens at network connection/creation time. F7 does **not** toggle registration; it continues to toggle gameplay enforcement only after you are host.
3. Share the **private room code** with an unmodded friend on the same region and current game build. Registered host-only rooms may be absent from the public matchmaking list; this is expected, and not a reason to disable registration.
4. Confirm the official mod stamp, warnings and each rule from both clients' perspectives. Test death of host, door sync, vent boot, meeting flow, second-strike kicks and end-game conditions.
5. If the lobby is refused/disconnected, record the **host BepInEx LogOutput.log** and any on-screen error, and roll back to the known-working v0.3.11 while diagnosing. The patch-installed line or `+25` calculation proves only what the client sent, **not** that the official server accepted the room.

### Compatibility note

The network version offset applies to **online connections initiated with this mod loaded**, not solely to the moment you press F7. It may alter your ability to join someone else's ordinary vanilla room. For ordinary unmodded play, exit the game and temporarily disable the SNS DLL (rename it to `SNSHostRules.dll.disabled`) before restarting. This is preferable to risking unregistered gameplay changes in official rooms.

If an updated official Among Us game build changes networking rules, re-verify against that build rather than force the protocol.

## Mod policy disclaimer

This mod is not affiliated with Among Us or Innersloth LLC, and the content contained therein is not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein are property of Innersloth LLC. © Innersloth LLC.

**Status:** Source verified and archive integrity checked; this environment cannot perform a full Windows IL2CPP build or test an official online lobby. v0.3.12 is a **registration test candidate**, not a guaranteed live-server release.
