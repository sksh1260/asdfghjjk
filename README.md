# MassRecall SC Evo Launcher

Windows launcher that downloads patch archives and extracts them into the StarCraft II installation folder.

`MassRecall SC Evo Launcher.exe` is the ready-to-run launcher. It installs the main archive by copying only the `Mods` and `Maps` folders into the StarCraft II folder. Korean voice files install by default; when the English voice checkbox is selected, English voice files install instead. Voice archives are flattened so only the two voice mod entries are copied directly into the `Mods` folder.
After installation, downloaded archives are deleted from the temp folder, and a `Mass Recall.lnk` shortcut is created on the desktop with paths adjusted to the selected StarCraft II install folder.
The launcher also has an `Uninstall` button. It removes files recorded during installation and deletes the desktop shortcut. The `Remove SCMR.SC2Bank before install` checkbox deletes matching bank files under the user's `Documents\StarCraft II` folder before installing.
The banner image is embedded in `MassRecall SC Evo Launcher.exe`.

## Download Links

- Main files: `https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo.zip`
- Korean voice files: `https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo.KR.VO.Assets.Local.zip`
- English voice files: `https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo.EN.VO.Assets.Local.zip`
- Version JSON: `https://github.com/sksh1260/asdfghjjk/releases/download/SCMR_SC_Evo/SCMR_SCEvo_latest.json`

The C# launcher has these URLs embedded in `MassRecallScEvoLauncher.cs` and downloads ZIP files directly from GitHub Releases.

## Run

Run `MassRecall SC Evo Launcher.exe`.

## Build the C# exe

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:"MassRecall SC Evo Launcher.exe" /resource:"MassRecallBanner.png",MassRecallBanner.png /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll MassRecallScEvoLauncher.cs
```
