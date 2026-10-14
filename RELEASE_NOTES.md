# DeeClipper Release Notes

## v1.0.0

### New Features

- **Persistent clipboard history**: Automatically captures plain text, rich links, images, screenshots, and copied files
- **Pinned items**: Pin important clips forever — pinned items survive history clears, retention limits, and sensitive wipes. Rename with custom labels, organize into colored groups, drag-and-drop reorder, and assign quick-paste slots (Ctrl+Alt+1–9)
- **Full-text search**: Ranked results (exact match, prefix match, substring) powered by SQLite FTS5
- **Quick popup palette** (Ctrl+Alt+V): Compact popup near cursor with keyboard navigation and direct paste into the active window
- **Paste-as transforms**: UPPERCASE, lowercase, Title Case, Single line, Trimmed, Clean link (strips utm_*, fbclid, gclid tracking parameters)
- **Text editing**: Edit items in-place or save as new; merge multiple selected items with custom separators
- **Custom groups**: Create colored groups, assign items via drag-and-drop, bulk operations for pin/unpin/group/delete
- **Backup & restore**: Export to ZIP (items, groups, pin order, labels, images); import merges by content hash with summary
- **First-run onboarding**: 3-step guided tour with Start with Windows option
- **Settings window**: 8 tabs — General, Hotkeys, Popup, History, Privacy, Appearance, Data, About
- **System tray integration**: Pause monitoring, private mode (5/15/60 min), quick access to settings and window

### Privacy & Security

- 100% offline — no network libraries, no telemetry, no cloud sync, no update checks
- Sensitive content detection: credit cards (Luhn), JWTs, private key blocks, API tokens, high-entropy strings
- Sensitive items kept in memory only, never written to disk unless pinned; auto-wipe after configurable timeout
- Password manager blocklist: KeePass, KeePassXC, 1Password, Bitwarden, LastPass, Dashlane, Enpass, NordPass
- Respects Windows privacy flags (ExcludeClipboardContentFromMonitorProcessing, CanIncludeInClipboardHistory)
- Runs as standard user (asInvoker), never requests administrator privileges

### Known Limitation

- Pasting directly into applications running as administrator is not possible due to Windows UIPI. The item is still copied to the clipboard; press Ctrl+V manually in the elevated app.
