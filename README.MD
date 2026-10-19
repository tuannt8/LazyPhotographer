# RAW & AI Photo Workstation (Avalonia + C++16) 📷

A high-performance RAW photo filtering, adjustment, and quick export workstation designed for photographers. Built with a C++ 16-bit image processing engine, AI intelligence, a shared C# core library, and an **Avalonia UI** desktop frontend.

---

## 🏛 Architecture & Solution Overview

The Visual Studio solution (`LazyPhotographer.sln`) is composed of five modular projects:

```
LazyPhotographer/
├── LazyPhotographer.sln
├── src/
│   ├── RawProcessing/             [C++ Static Library]
│   │   ├── IRawReader.h           - Abstract interface for RAW file decoders
│   │   ├── RafReader.h/.cpp       - Fujifilm .RAF parser, embedded preview extractor, 16-bit raw decoder
│   │   └── RawTypes.h/.cpp        - 16-bit linear buffer (RawBuffer16), metadata, CFA Bayer/X-Trans
│   │
│   ├── ImageProcessing/           [C++ Static Library]
│   │   ├── ImageProcessor.h/.cpp  - 16-bit tone mapping, exposure, WB, highlights/shadows, curves
│   │   ├── AiProcessor.h/.cpp     - AI scene analysis, AI auto-tone, neural denoise & texture enhancer
│   │   ├── Exporter.h/.cpp        - WIC-based exporter for JPEG, PNG, and 16-bit Master TIFF
│   │   └── ImageAdjustments.h     - Parametric adjustment controls and 256-bin RGB histogram
│   │
│   ├── CsBinding/                 [C++ Dynamic Link Library - CsBinding.dll]
│   │   └── CsBindingExport.h/.cpp - C-ABI extern "C" export interface for .NET P/Invoke
│   │
│   ├── LazyPhotographer.Common/   [C# Shared Core Library]
│   │   ├── Interop/               - NativeMethods.cs, InteropTypes.cs, NativeImageBridge.cs
│   │   ├── Models/                - PhotoItemBase, ImageAdjustmentsModel, ExportPresetModel, Enums
│   │   └── ViewModels/            - MainViewModelBase<TPhotoItem, TImageSource>, RelayCommand
│   │
│   └── LazyPhotographer.Avalonia/ [C# Avalonia Desktop App]
│       ├── Models/                - PhotoItem (wraps Avalonia Bitmap & IBrush)
│       ├── ViewModels/            - MainViewModel (Avalonia Dispatcher & StorageProvider)
│       └── Views/
│           ├── MainWindow.axaml   - Title-bar integrated tabs & global keyboard hotkeys
│           ├── FilteringView.axaml- Tab 1: Grid / Loupe + Filmstrip & Flagging
│           ├── AdjustmentView.axaml- Tab 2: Main Image, Filmstrip & Processing Panel
│           └── ExportView.axaml   - Tab 3: Quick Export Presets & Progress
└── tests/
    └── NativeVerification/        - End-to-end integration test runner
```

---

## ✨ Features & Shortcuts

### Window Title Bar Navigation & Keyboard Hotkeys
- **Title-Bar Tabs**: The 3 main workflow tabs (`1. Filtering`, `2. Adjustment`, `3. Quick Export`) and the flagged counter badge are integrated directly into the custom window title bar with no software title clutter.
- **Keyboard Hotkeys**:
  - `P`: **Pick (Flag)** selected photo
  - `X`: **Reject** selected photo
  - `U`: **Unflag (Clear)** selected photo
  - `←` / `→`: Navigate to previous / next photo
  - `G` / `E`: Switch between **Grid Mode** and **Loupe Mode** in Tab 1

### Tab 1: Filtering & Culling
- **Folder Scanner**: Scans any folder for image files, prioritizing Fujifilm `.RAF` RAW images, as well as JPEG, PNG, and TIFF.
- **View Modes**:
  - **Grid Mode**: Multi-column responsive grid showing photo cards with format badge (`.RAF`), camera model, EXIF exposure parameters (`1/250s f/2.8 ISO 160`), and quick flag buttons. **Double-clicking any image card immediately opens it in Loupe Mode.**
  - **Loupe Mode + Filmstrip**: Large main image preview with a horizontal thumbnail strip underneath for rapid culling.

### Tab 2: Adjustment & AI Processing
- **Center Viewport**: High-quality real-time preview of the developed image with **Before / After** comparison toggle.
- **Bottom Filmstrip**: Scrollable horizontal thumbnail strip of current / flagged photos for instant photo switching.
- **Right Processing Panel**:
  - **AI Intelligence**: `✨ AI Auto-Tone`, `AI Neural Denoise`, and `AI Micro-Detail Texture`.
  - **Light & Tone**: Exposure (-5.0 to +5.0 EV), Contrast, Highlights, Shadows, Whites, Blacks.
  - **Color Balance**: White Balance Temperature (Cool ↔ Warm), Tint (Green ↔ Magenta), Vibrance, Saturation.

### Tab 3: Quick Export
- **Built-in Export Presets**: `16-Bit Master TIFF`, `High Quality JPEG`, `Web & Social (2048px, 85% JPEG)`, and `Lossless PNG`.
- **Batch Export**: Asynchronous multi-threaded export with live progress bar and destination folder shortcut.

---

## 🚀 Building & Running

```powershell
# 1. Build entire Visual Studio solution
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" LazyPhotographer.sln /t:Restore,Build /p:Configuration=Release /p:Platform=x64

# 2. Run the Avalonia desktop application
dotnet run --project src/LazyPhotographer.Avalonia/LazyPhotographer.Avalonia.csproj -c Release
```