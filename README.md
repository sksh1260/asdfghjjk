# MassRecall SC Evo Launcher

Windows installer that downloads patch archives and extracts them into the StarCraft II installation folder.

`MassRecall SC Evo Launcher.exe` is the ready-to-run installer. It installs the main archive by copying only the `Mods` and `Maps` folders into the StarCraft II folder. Korean voice files install by default; when the English voice checkbox is selected, English voice files install instead. Voice archives are flattened so only the two voice mod entries are copied directly into the `Mods` folder.
After installation, downloaded archives are deleted from the temp folder, and a `Mass Recall.lnk` shortcut is created on the desktop with paths adjusted to the selected StarCraft II install folder.
The installer also has an `Uninstall` button. It removes files recorded during installation and deletes the desktop shortcut. The `Remove SCMR.SC2Bank before install` checkbox deletes matching bank files under the user's `Documents\StarCraft II` folder before installing.
The banner image is embedded in `MassRecall SC Evo Launcher.exe`.
Downloads and extraction include safety checks: ZIP entries are blocked from extracting outside the temp folder, installs run in a background worker so the UI remains responsive, HTTP downloads require HTTPS even after redirects, use the explicit `SCMR-SC-Evo-Installer/26.05.09` User-Agent, and have 30-second timeouts. Optional SHA256 fields in the version JSON are verified immediately after download and before installation. Uninstall only deletes recorded files under the selected StarCraft II `Mods` or `Maps` folders, and directory reparse points are not recursively followed during cleanup.

The installer intentionally does not include a custom MPQ decrypt/parser. Installed version state is tracked in the local installer state file to keep the executable simpler and reduce antivirus heuristic triggers.

Release packaging is intentionally not self-extracting: do not embed the downloaded ZIP archives in the exe, do not use obfuscators, and do not publish as a compressed single-file/self-contained bundle. Ship `MassRecall SC Evo Launcher.exe`; the installer downloads the release ZIP files and verifies SHA256 when hashes are present in the version JSON. For public releases, Authenticode-sign the exe before uploading to reduce SmartScreen and antivirus reputation warnings.

Optional SHA256 keys supported by the version JSON:

- `main_sha256`
- `korean_voice_sha256`
- `english_voice_sha256`
- `voice_sha256`

## Download Links

- Main files: `https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo.zip`
- Korean voice files: `https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo.KR.VO.Assets.Local.zip`
- English voice files: `https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo.EN.VO.Assets.Local.zip`
- Version JSON: `https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo_latest.json`

The C# installer has these URLs embedded in `MassRecallScEvoLauncher.cs` and downloads ZIP files directly from GitHub Releases.

## Run

Run `MassRecall SC Evo Launcher.exe`.

## Build the C# exe

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /debug- /out:"MassRecall SC Evo Launcher.exe" /resource:"MassRecallBanner.png",MassRecallBanner.png /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll MassRecallScEvoLauncher.cs
```
