# Log Analyser

**Log Analyser** is a lightweight, high-performance desktop log viewing and analysis tool built with WPF and .NET. Designed for software engineers, systems administrators, and DevOps personnel, it provides rapid log inspection, non-locking file streaming, flexible regex filtering, and an eye-comfort user interface.

---

## Key Features

* **High Performance & Low RAM Footprint**: Built with WPF UI virtualization (`VirtualizingStackPanel`) to render hundreds of thousands of log entries seamlessly with low memory overhead (~30MB RAM).
* **Non-Locking File Streaming (`FileShare.ReadWrite`)**: Opens and reads live application and system logs without locking the target files, allowing background services to write continuously without file-lock errors.
* **Eye-Comfort Dual Themes**: Includes custom Slate Dark Mode and Eye-Comfort Light Mode, featuring soft pastel level indicators for Error, Warning, Critical, Info, Debug, and Trace entries to eliminate eye strain.
* **Advanced Search & Filtering**:
  * Filter by Log Level: `ALL`, `CRT`, `ERR`, `WRN`, `INF`, `DBG`, `TRC` with live entry counts.
  * Real-time Text Filtering with instant clear (`✕`) action.
  * Case Sensitivity (`Aa`) and Regular Expression (`.*`) matching.
* **Exporting Capabilities**: Export currently filtered log entries directly into standard `.txt`, `.csv`, or structured `.json` format.
* **Auto-Refresh & Optional Live Tail**:
  * Periodic background auto-refresh (2-second polling).
  * Optional **Live Tail** auto-scrolling to automatically stick to the latest log line as new entries arrive.
* **Selected Entry Detail Drawer**: Select any row to open a bottom drawer displaying line-wrapped log messages, full stack traces, and a one-click **Copy Log** button.

---

## Tech Stack & Architecture

* **Framework**: .NET 10 / .NET 8 WPF (`Microsoft.NET.Sdk`)
* **Language**: C# 12+
* **UI Controls**: WPF DataGrid with virtualization, dynamic ResourceDictionary themes.
* **File Processing**: `System.IO.FileStream` with UTF-8 `StreamReader` and shared non-blocking read access.

---

## Getting Started

### Prerequisites

* [.NET SDK](https://dotnet.microsoft.com/) (.NET 8.0 or later / .NET 10.0)
* Windows OS (7 / 10 / 11)

### Building & Running

1. Clone or navigate to the repository directory:
   ```bash
   cd "Log Analyser"
   ```

2. Build the application:
   ```bash
   dotnet build
   ```

3. Run the executable:
   ```bash
   dotnet run
   ```

---

## Project Structure

```
Log Analyser/
├── App.xaml                  # Application entry point & theme initialization
├── App.xaml.cs               # Application code-behind
├── MainWindow.xaml           # Main user interface layout & styling
├── MainWindow.xaml.cs        # Main window logic, parser, filtering & timers
├── LogAnalyser.csproj        # .NET Project configuration
├── about.txt                 # Application metadata (Version, Build, Release)
├── app_logo.png              # App icon asset
└── Themes/
    ├── Dark.xaml             # Eye-Comfort Slate Dark Theme
    └── Light.xaml            # Eye-Comfort Soft Light Theme
```

---

## Application Metadata (`about.txt`)

The application automatically reads and displays version metadata at the bottom-left status bar from `about.txt`:

```ini
Application Version: 1.0.5
Build: 105
Release: 2026.10.07
```

---

## License

MIT License
