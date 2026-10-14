# DeeClipper

DeeClipper is a persistent clipboard manager for Windows 10 and 11. It runs quietly in the system tray, remembers everything you copy (text, links, images, copied files), and allows you to find and reuse them later with instant search, text transformations, and robust privacy protections.

Items can be **PINNED**, and pinned items are never deleted automatically, no matter how much is copied afterward. Unpinned history is automatically trimmed to a configurable limit.

---

## ✨ Features

- **Clipboard Memory**: Captures plain text, rich links, images, screenshots, and copied files automatically.
- **Pin & Protect**: Pin important clips forever. Pinned items survive history clears, retention limits, and sensitive-data wipes. Rename them with custom labels and organize them into custom colored groups. Drag-and-drop to reorder pinned items.
- **Instant Search**: Full-text search indexed with SQLite FTS5 for instantaneous filtering as you type, with ranked results (exact match first, then prefix, then substring).
- **Quick Popup Palette**: Press the global hotkey to summon a compact popup near your cursor. Select with arrow keys and hit Enter to paste directly into the active window.
- **Quick-Paste Slots 1–9**: Enable Ctrl+Alt+1 through Ctrl+Alt+9 to instantly paste your top pinned clips with a single keystroke.
- **Paste As Transforms**: Paste as UPPERCASE, lowercase, Title Case, Single line, Trimmed, or Clean link (strips URL tracking tokens like utm_*, fbclid, gclid).
- **Text Editing**: Edit any text item in-place, or save a modified version as a new clip. Merge multiple selected text items with a custom separator.
- **Item Organization**: Create custom colored groups (e.g. Work, Snippets, Addresses). Assign items to groups via drag-and-drop or context menu. Bulk select, pin, unpin, group, and delete.
- **Backup & Restore**: Export all items, groups, pin ordering, labels, and images into an offline ZIP archive. Import merges by content hash and shows a summary of added vs. skipped items.
- **First-Run Onboarding**: A 3-step guided tour introduces what DeeClipper does, the hotkey, and the privacy rules, with an option to enable Start with Windows.
- **System Tray Integration**: Runs silently in the tray. Left-click to open the main window. Right-click for the context menu (Open, Settings, Pause, Private Mode, Exit). Tray icon changes when monitoring is paused.
- **Multi-Monitor & High DPI**: Full PerMonitorV2 DPI awareness and correct popup positioning on the active monitor. Rounded corners and shadow on Windows 11 with a safe fallback on Windows 10.
- **Designed Empty States**: Friendly, informative empty states when there are no pinned items or no history.
- **Keyboard Accessible**: Full keyboard navigation in the popup (arrow keys, Tab to cycle filters, Alt+1–9 for quick selection, Enter to paste, Esc to close) and in the main window (Ctrl+E to edit, Delete to remove).

---

## ⌨️ Hotkeys & Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+Alt+V` (configurable) | Open the quick popup palette |
| `Ctrl+Alt+1` – `Ctrl+Alt+9` | Quick-paste the Nth pinned item (when enabled) |
| `↑` / `↓` | Navigate items in the popup |
| `Enter` | Paste selected item directly |
| `Shift+Enter` | Paste as plain text |
| `Ctrl+Enter` | Copy to clipboard only (no auto-paste) |
| `Alt+1` – `Alt+9` | Paste the Nth visible item in the popup |
| `Tab` / `Shift+Tab` | Cycle filter chips in the popup |
| `Ctrl+T` | Open "Paste as" transform menu |
| `Ctrl+E` | Edit selected text item |
| `Ctrl+P` | Pin / unpin selected item |
| `Delete` | Delete selected item |
| `Esc` | Close the popup |

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
   - Sensitive items are kept in memory only (never written to disk) unless explicitly pinned.
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
   - Also respects the Windows `ExcludeClipboardContentFromMonitorProcessing` and `CanIncludeInClipboardHistory` privacy flags.

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

## ⚠️ Known Limitation: Pasting into Administrator Apps

DeeClipper runs as a standard user (asInvoker) and never requests administrator privileges. Because of Windows UIPI (User Interface Privilege Isolation), a standard-user process **cannot send keystrokes** (the simulated Ctrl+V used for direct paste) to a window running at a higher elevation level.

If the target application is running as administrator:
- The item will still be **copied to your clipboard** successfully.
- A toast notification will appear saying "Copied. Press Ctrl+V."
- You can manually press `Ctrl+V` in the elevated application to paste.

This is a deliberate security feature of Windows, not a bug. Running DeeClipper as administrator to work around this is **not recommended** as it would compromise the security boundary.

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
  - Houses the SQLite database engine (WAL mode, connection pooling queue, versioned migrations via `PRAGMA user_version`), deduplication logic, retention engine, type/tag classification, sensitive detector, text transforms, settings, and backup service.
- **`src/DeeClipper.App`**:
  - `.NET 8` WPF desktop application targeting `net8.0-windows`.
  - Configured with `app.manifest` for `PerMonitorV2` DPI awareness, `asInvoker` security level (never requests administrator privileges), and long path awareness.
- **`tests/DeeClipper.Tests`**:
  - Comprehensive xUnit test suite validating all core logic (deduplication, retention, sensitive detection, transforms, search, settings, backup round-trip, and organization).
- **`scripts/make-icons.ps1`**:
  - PowerShell 5.1 script generating multi-resolution ICO assets (`app.ico` and `paused.ico`) across 16, 24, 32, 48, 64, 128, and 256 pixel sizes.
- **`.github/workflows/build.yml`**:
  - Automated CI/CD workflow running tests and creating portable & lite release packages.

---

## 📋 Release Notes

### v1.0.0

**New Features**
- Persistent clipboard history with automatic capture of text, links, images, and copied files
- Pinned items that are never automatically deleted, with custom labels, drag-and-drop reordering, and quick-paste slots (Ctrl+Alt+1–9)
- Custom colored groups for organizing clips, with drag-and-drop assignment and bulk operations
- Full-text search with ranked results (exact, prefix, substring) powered by SQLite FTS5
- Quick popup palette (Ctrl+Alt+V) with keyboard navigation and direct paste into the active window
- Paste-as transforms: UPPERCASE, lowercase, Title Case, Single line, Trimmed, Clean link (strips tracking parameters)
- In-place text editing and merge-selected-items with custom separators
- Backup & restore: export to ZIP (items, groups, pin order, labels, images) and import with content-hash deduplication
- 3-step first-run onboarding tour with Start with Windows option
- 8-tab settings window: General, Hotkeys, Popup, History, Privacy, Appearance, Data, About
- Sensitive content detection (credit cards, JWTs, private keys, API tokens, high-entropy strings) with memory-only storage and auto-wipe
- Password manager blocklist (KeePass, 1Password, Bitwarden, LastPass, Dashlane, Enpass, NordPass)
- Windows privacy flag support (ExcludeClipboardContentFromMonitorProcessing, CanIncludeInClipboardHistory)
- System tray integration with pause monitoring, private mode (5/15/60 min), and settings access
- PerMonitorV2 DPI awareness, multi-monitor popup positioning, Windows 11 rounded corners
- Zero network connections: no telemetry, no cloud sync, no update checks, no error reporting

**Privacy & Security**
- 100% offline by design — the codebase contains no network libraries
- Sensitive items kept in memory only, never written to disk unless pinned
- Pinned items survive all retention, clearing, and sensitive-wipe operations
- "Delete everything" requires typing DELETE as confirmation
- Runs as standard user (asInvoker), never requests administrator privileges
