# DeeClipper

DeeClipper is a persistent clipboard manager for Windows 10 and 11. It runs quietly in the system tray, remembers everything you copy (text, links, images, copied files), and allows you to find and reuse them later with instant search, text transformations, and robust privacy protections.

Items can be **PINNED**, and pinned items are never deleted automatically, no matter how much is copied afterward. Unpinned history is automatically trimmed to a configurable limit.

---

## 🔒 Privacy Rules & Zero Network Guarantee

1. **100% Offline by Design**:
   - DeeClipper contains **no network capabilities**: no `HttpClient`, no web sockets, no cloud sync, no telemetry, no error reporting, and no background update checks.
   - All copied content stays strictly on your local machine.

2. **Sensitive Content Protection**:
   - Automatically detects sensitive data including:
     - Luhn-valid credit card numbers (Visa, Mastercard, Amex, etc.)
     - JSON Web Tokens (JWT)
     - `-----BEGIN ... PRIVATE KEY-----` cryptographic blocks
     - Cloud & API tokens (`ghp_`, `github_pat_`, `sk-`, `AKIA`, `xoxb-`, `xoxp-`, `AIza`)
     - High-entropy single-word secret strings (20+ characters)
   - Normal URLs and everyday sentences are never falsely flagged.
   - Unpinned sensitive items can be automatically wiped after a configurable timeout (default: 5 minutes).

3. **Password Manager Blocklist**:
   - Automatically ignores clipboard events originating from known password managers:
     - KeePass & KeePassXC
     - 1Password
     - Bitwarden
     - LastPass
     - Dashlane
     - Enpass
     - NordPass

4. **Pinned Item Guarantee**:
   - Pinned items are **never deleted** by count limits, age limits, "Clear history", or sensitive wipes.
   - Pinned items can only be removed by explicitly unpinning and deleting them, or by a "Delete everything including pinned" action that requires typing `DELETE`.

---

## 📁 Storage & File Locations

All application data is stored locally in your Windows profile directory:

```
%LOCALAPPDATA%\DeeClipper\
├── deeclipper.db     # SQLite database (WAL mode, FTS5 virtual table)
├── images\           # Cached image clips and thumbnails
├── logs\             # Local diagnostic logs
└── settings.json     # Configuration file (atomic writes)
```

---

## 🚀 How to Run & Build

### Prerequisites
- Windows 10 or 11 (64-bit)
- [.NET 8.0 Runtime or SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Running from Pre-built Releases
- **Portable Edition (`DeeClipper-portable.zip`)**:
  - Self-contained single-file executable with native libraries bundled.
  - Does not require .NET to be pre-installed on the target machine.
  - Extract and run `DeeClipper.exe`.
- **Lite Edition (`DeeClipper-lite.zip`)**:
  - Compact framework-dependent executable.
  - Requires the .NET 8 Desktop Runtime.
  - Extract and run `DeeClipper.exe`.

### Building from Source
Clone the repository and build using the .NET CLI:

```bash
# Clone the repository
git clone https://github.com/your-username/DeeClipper.git
cd DeeClipper

# Run all unit tests
dotnet test

# Build the solution
dotnet build -c Release

# Publish self-contained single-file executable
dotnet publish src/DeeClipper.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish/full

# Publish framework-dependent lite executable
dotnet publish src/DeeClipper.App -c Release -r win-x64 --self-contained false -o publish/lite
```

---

## 🛠 Solution Structure

- **`src/DeeClipper.Core`**:
  - `.NET 8` class library with **no WPF or Windows UI references**.
  - Houses the SQLite database engine (WAL mode, connection pooling queue, versioned migrations via `PRAGMA user_version`), deduplication logic, retention engine, type/tag classification, sensitive detector, text transforms, and settings.
- **`src/DeeClipper.App`**:
  - `.NET 8` WPF desktop application targeting `net8.0-windows`.
  - Configured with `app.manifest` for `PerMonitorV2` DPI awareness, `asInvoker` security level (never requests administrator privileges), and long path awareness.
- **`tests/DeeClipper.Tests`**:
  - Comprehensive xUnit test suite validating all core logic (deduplication, retention, sensitive detection, transforms, search, and settings).
- **`scripts/make-icons.ps1`**:
  - PowerShell 5.1 script generating multi-resolution ICO assets (`app.ico` and `paused.ico`) across 16, 24, 32, 48, 64, 128, and 256 pixel sizes.
- **`.github/workflows/build.yml`**:
  - Automated CI/CD workflow running tests and creating portable & lite release packages.
