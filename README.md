# MassRecall SC Evo Launcher

Windows launcher that downloads patch archives and extracts them into the StarCraft II installation folder.

`MassRecall SC Evo Launcher.exe` is the ready-to-run launcher. It installs the main archive by copying only the `Mods` and `Maps` folders into the StarCraft II folder. English voice files are optional and install into `Mods` only when the checkbox is selected.
After installation, downloaded `.7z` archives are deleted from the temp folder, and a `Mass Recall.lnk` shortcut is created on the desktop with paths adjusted to the selected StarCraft II install folder.
The launcher also has an `Uninstall` button. It removes files recorded during installation and deletes the desktop shortcut. The `Remove SCMR.SC2Bank before install` checkbox deletes matching bank files under the user's `Documents\StarCraft II` folder before installing.
Place `MassRecallBanner.png` next to `MassRecall SC Evo Launcher.exe` to show the banner image at the top of the launcher.

## Download Links

- Main files: `https://drive.google.com/file/d/1XaP1f1IJseA3f-pa3fCRkGNEzsgAzefg/view?usp=drive_link`
- English voice files: `https://drive.google.com/file/d/1L5bR4Qh22wT8IOouNxkffX-pTfEjcktW/view?usp=drive_link`
- Version JSON: `https://drive.google.com/file/d/1fT06Fw_qgM2_4OdrUnyBQXXK1H788wcI/view?usp=drive_link`

The C# launcher has these URLs embedded in `MassRecallScEvoLauncher.cs`. `launcher.config.json` is kept for the older PowerShell/Python launcher variants.

## Run

PowerShell version, no extra runtime:

```powershell
powershell -ExecutionPolicy Bypass -File .\launcher.ps1
```

Python version:

```powershell
py launcher.py
```

## Build the C# exe

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:"MassRecall SC Evo Launcher.exe" /resource:"MassRecallBanner.png",MassRecallBanner.png /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll MassRecallScEvoLauncher.cs
```
