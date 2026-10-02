# Build

Use .NET 6 SDK, PowerShell 7 and a local Stardew Valley installation with SMAPI.

```powershell
pwsh -File ./build.ps1 -PackageTag RC4 -GamePath 'D:\SteamLibrary\steamapps\common\Stardew Valley'
```

Output: dist/BetterBeads-1.1.0-RC4.zip. The script never starts the game or installs into Mods.
The current project still uses the internal LiteBuild=true switch for historical compatibility; the released mod is Better Beads.
Compile retained historical code with `dotnet build BetterBeads -c Release -p:LiteBuild=false -p:GamePath=...`; it is not the current release.

Manual checks: tools/OnlineChecks, BlueprintSaveChecks, FurnitureFinishChecks, ForgeChecks and PatchSmoke. Some offline artwork fixtures need local read-only game references and are not distributed.
Text: BetterBeads/i18n. Art: BetterBeads/assets. Editors: art/editor.html and art/text-editor.html.
Do not commit game assemblies, extracted sprite sheets, caches, bin/obj, saves or personal config/imports.

