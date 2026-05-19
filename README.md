# MassRecall SC Evo Launcher

Windows launcher that downloads patch archives and extracts them into the StarCraft II installation folder.

`MassRecall SC Evo Launcher.exe` is the ready-to-run launcher. It installs the main archive by copying only the `Mods` and `Maps` folders into the StarCraft II folder. Korean voice files install by default; when the English voice checkbox is selected, English voice files install instead. Voice archives are flattened so only the two voice mod entries are copied directly into the `Mods` folder.
After installation, downloaded archives are deleted from the temp folder, and a `Mass Recall.lnk` shortcut is created on the desktop with paths adjusted to the selected StarCraft II install folder.
The launcher also has an `Uninstall` button. It removes files recorded during installation and deletes the desktop shortcut. The `Remove SCMR.SC2Bank before install` checkbox deletes matching bank files under the user's `Documents\StarCraft II` folder before installing.
Place `MassRecallBanner.png` next to `MassRecall SC Evo Launcher.exe` to show the banner image at the top of the launcher.

## Download Links

- Main files: `https://drive.google.com/file/d/10dY2bqNPpNDpZwNaEjOWp6W6qkucNI50/view?usp=drive_link`
- Korean voice files: `https://drive.google.com/file/d/16P1eeCS-C2b0QI-fmCEaD8Q5_lKAwPS6/view?usp=drive_link`
- English voice files: `https://drive.google.com/file/d/1Xa4nVuvgLnFXdK24e0deMthilHzE7qgA/view?usp=drive_link`
- Version JSON: `https://drive.google.com/file/d/1XiEN8y6h4VuCCvJUxYnRkFm5ixMQV_jD/view?usp=drive_link`

The C# launcher has these URLs embedded in `MassRecallScEvoLauncher.cs` and supports Google Drive file-link downloads directly. Main and voice packages are ZIP files.

## Run

Run `MassRecall SC Evo Launcher.exe`.

## Build the C# exe

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:"MassRecall SC Evo Launcher.exe" /resource:"MassRecallBanner.png",MassRecallBanner.png /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll MassRecallScEvoLauncher.cs
```
