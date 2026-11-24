# FlowClicker

A Windows 11 x64 desktop utility for repeated mouse clicks, multi-point sequences, scrolling, pointer motion, and button hold.

## Install and use

1. Download `FlowClicker-Setup-x64.msi` from [Releases](https://github.com/nguyenvietnhan/FlowClicker/releases/latest) and open it. The installer includes the .NET desktop runtime and is unsigned.
2. Open **FlowClicker** from Start. In **Auto Clicker**, choose **Follow cursor** or **Fixed coordinates**, then press **F6** to start or stop.
3. Press **F7** to pick a fixed coordinate (or append a point when **Multi-Point** is open), **F8** to start or stop a Multi-Point sequence, and **Shift+Esc** to stop all mouse actions and release held buttons.
4. Settings and Multi-Point steps are saved automatically to `%LocalAppData%\FlowClicker\settings.json`.

Uninstall from **Settings → Apps → Installed apps → FlowClicker**.

Windows can block synthetic input sent to an application running at higher privileges. At high rates, the target application may process fewer clicks than requested. Fixed coordinates should be picked again if display arrangement changes.

## Build and test

On Windows x64 with the .NET 10 SDK, run these commands from the repository root:

```powershell
dotnet build FlowClicker.slnx -c Release
dotnet test FlowClicker.slnx -c Release --no-build --filter "Category!=Desktop"
```

To also run the interactive Windows session tests (kernel timer quantum, high-precision wait benchmark, and `SendInput` button-release smoke test):

```powershell
dotnet test FlowClicker.slnx -c Release --no-build
```

Run `run.bat` to build and open the application locally.

## Build the MSI

Install [WiX Toolset](https://docs.firegiant.com/wix/using-wix/) 6 with `WixToolset.Util.wixext` and run:

```powershell
dotnet publish src/FlowClicker/FlowClicker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o dist/FlowClicker-win-x64
Copy-Item THIRD-PARTY-NOTICES.txt dist/FlowClicker-win-x64/
wix build installer/FlowClicker.wxs -ext WixToolset.Util.wixext -arch x64 -out dist/FlowClicker-Setup-x64.msi -pdbtype none
```

See [architecture](docs/TECHNICAL_DESIGN.md), [license](LICENSE), and [third-party notices](THIRD-PARTY-NOTICES.txt).
