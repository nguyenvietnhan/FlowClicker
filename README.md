# FlowClicker

A Windows 11 x64 desktop utility for repeated mouse clicks, multi-point sequences, scrolling, pointer motion, and button hold.

## Install and use

1. Download `FlowClicker-Setup-x64.msi` from [Releases](https://github.com/nguyenvietnhan/FlowClicker/releases/latest) and open it. The installer includes the .NET desktop runtime and is unsigned.
2. Open **FlowClicker** from Start. In **Auto Clicker**, choose **Follow cursor** or **Fixed coordinates**, then press **F6** to start or stop.
3. Press **F7** to pick a fixed coordinate. Press **Shift+Esc** to stop mouse actions and release held buttons.

Uninstall from **Settings → Apps → Installed apps → FlowClicker**. Close FlowClicker before installing an update.

Windows can block synthetic input sent to an application running at higher privileges. At high rates, the target application may process fewer clicks than requested. Fixed coordinates should be picked again if display arrangement changes.

## Build and test

On Windows x64 with the .NET 10 SDK, run these commands from the repository root:

```powershell
dotnet build FlowClicker.slnx -c Release
dotnet run --project tests/FlowClicker.Tests -c Release --no-build
```

The test program uses Windows APIs, including a smoke test that sends mouse-button release events. Run it in an interactive session when you are not dragging or holding a mouse button.

Run `run.bat` to build and open the application locally.

## Build the MSI

Install [WiX Toolset](https://docs.firegiant.com/wix/using-wix/) 6 and run:

```powershell
dotnet publish src/FlowClicker/FlowClicker.csproj -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.15 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o dist/FlowClicker-win-x64
Copy-Item THIRD-PARTY-NOTICES.txt dist/FlowClicker-win-x64/
wix build installer/FlowClicker.wxs -arch x64 -out dist/FlowClicker-Setup-x64.msi -pdbtype none
```

See [architecture](docs/TECHNICAL_DESIGN.md), [license](LICENSE), and [third-party notices](THIRD-PARTY-NOTICES.txt).
