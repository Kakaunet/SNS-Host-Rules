# Publisher guide — SNS Host Rules v0.3.12

This document is for the maintainer, **not** the end-user installer.

## A. What to publish

| Where | Include | Do NOT include |
|---|---|---|
| **Repository root** | `README.md`, `LICENSE`, `.gitignore`, `SNSHostRules.csproj`, `build-and-check.ps1`, `src/Plugin.cs`, optionally `docs/` | `bin/`, `obj/`, full Among Us folder, BepInEx installation, `Assembly-CSharp.dll`, `Hazel.dll`, generated interop, logs, user data |
| **GitHub Release `v0.3.12` → Assets** | The **actual successfully tested** `SNSHostRules.dll` taken from your **installed** `Among Us/BepInEx/plugins/` folder | Entire compiler/SDK, game EXE, BepInEx binaries, old DLL, `.dll.disabled` backups, debugging logs |

The ZIP named `SNSHostRules-GitHub-Ready-v0.3.12.zip` is a **source repository preparation kit**, not the player's installation package. It intentionally does not contain a DLL. The DLL must be supplied from the maintainer's tested Windows PC. Never claim a DLL inside this ZIP has been tested, as there is no DLL in the kit.

Do **not** attach the previous v0.3.12 **source ZIP** as the sole release asset. GitHub automatically provides “Source code (zip)” and “Source code (tar.gz)” assets, but those **cannot** be installed by normal players.

## B. Create the repository

1. Sign in to GitHub, go to **https://github.com/new**.
2. Name the repository **`SNSHostRules`** (or another name you prefer). Description suggestion: **Host-only Shift ’N Seek rules enforcement for Among Us (Windows Steam, BepInEx 6 IL2CPP).**
3. Choose **Public** if you intend an open-source release. Select **no automatic README, `.gitignore`, or license** because this kit already includes them.
4. Create the repository.
5. Extract the `SNSHostRules-GitHub-Ready-v0.3.12.zip` **somewhere outside the Among Us installation**, e.g. Documents.
6. Upload **the files inside** the extracted folder to the repository root, preserving `src/` and `docs/`. In GitHub use **Add file → Upload files**, drag files and folders, then **Commit changes**. Check that `README.md`, `LICENSE`, `.gitignore`, and `.csproj` are visible at the root, and `src/Plugin.cs` lives under `src/`.
7. Confirm the source code says `[BepInPlugin(..., "0.3.12")]` and that your uploaded build matches it. **Do not re-label this v1.0.0 without actually updating and testing the compiled plugin.**
8. Review the GPLv3 `LICENSE` choice and any third-party code rights before publishing.

## C. Attach the tested DLL in a GitHub Release

1. With **Among Us closed**, find the **multiplayer-tested** `SNSHostRules.dll` at `.../Among Us/BepInEx/plugins/SNSHostRules.dll` on your own machine. Copy it to a temporary release staging folder (outside the game). Do not rename or modify the DLL.
2. Optional but recommended: Compute its SHA-256 checksum with PowerShell:
   ```powershell
   Get-FileHash -Algorithm SHA256 ".\SNSHostRules.dll"
   ```
   Save that hash in the release notes so players can verify downloads.
3. On your GitHub repository open **Releases → Draft a new release**.
4. Create/select the tag **`v0.3.12`** targeting the branch containing the exact v0.3.12 source.
5. Title: **`SNS Host Rules v0.3.12 — Multiplayer-tested`**.
6. Paste the description from `docs/RELEASE_NOTES_v0.3.12.md`, editing anything you have not independently confirmed.
7. Under **Assets / Attach binaries**, attach **the tested `SNSHostRules.dll`**. Wait until the asset appears; check the exact name.
8. Preview the release and verify the Source Code assets are **not** mistaken for the game installer. Publish the release.
9. Test the published workflow as a stranger would: open the release in a browser, download **only the attached DLL**, and follow the README on an appropriate installation.

## D. Recommended repository settings

- Enable **Issues** for bug reports; ask for game version, OS, BepInEx version, reproduction steps, and relevant *redacted* log lines. Do not ask people to post lobby codes or account identifiers.
- Use GitHub's default source archive only as an option for developers.
- Consider tagging repository topics: `among-us`, `bepinex`, `il2cpp`, `host-only-mod`.
- Keep future changes on a separate branch, then tag releases from exactly the code corresponding to their compiled DLLs.

## E. Important compatibility and legal checks

- Tested target: **Among Us Windows Steam 2026.9.29**. Do not claim macOS/Linux/Epic/console support based on the current project.
- Requires **BepInEx 6 Unity IL2CPP Windows x64** for the host; normal players joining the lobby do **not** need SNS/BepInEx.
- **The `.NET SDK` and build script are not player dependencies.** Don't instruct normal users to run a PowerShell command.
- All official-server gameplay-mod registration and the mod-stamp code remain part of the mod; don't remove them to improve public matchmaking visibility.
- Keep the Innersloth disclaimer in your public README (already included) and comply with the current [mod policy](https://www.innersloth.com/among-us-mod-policy/).
- Existing `LICENSE` text is GPLv3. Verify you want to license the project that way and have rights to publish your included source.
- If Among Us updates, **re-test before announcing compatibility**. If a future binary has changed, also update the version in `Plugin.cs`, release title and tag.

## Official guides used

- BepInEx: https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html
- BepInEx binary builds: https://builds.bepinex.dev/projects/bepinex_be
- GitHub repo creation: https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-new-repository
- GitHub uploading files: https://docs.github.com/en/repositories/working-with-files/managing-files/adding-a-file-to-a-repository
- GitHub release assets: https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository
- Innersloth mod policy: https://www.innersloth.com/among-us-mod-policy/
- Innersloth technical documentation: https://github.com/Innersloth-LLC/AmongUsModdingInformation
