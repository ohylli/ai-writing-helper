# AI Writing Helper v1.0.0

First public release. AI Writing Helper is a Windows system tray utility that
fixes typos and transcribes speech using AI, triggered by global hotkeys.

## Downloads

Two builds are available below. **If you are not sure which one to pick, get
the self-contained build** — it just works, no setup required.

- **`AIWritingHelper-v1.0.0-self-contained.exe`** (~50 MB) — Bundles the .NET 10
  runtime. No install required. Recommended for most users.
- **`AIWritingHelper-v1.0.0-framework-dependent.exe`** (~2 MB) — Smaller download,
  but requires the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
  to already be installed.

Both are single-file executables — just download and run.

## Requirements

- Windows 10 or later
- An API key for an OpenAI-compatible LLM service (for typo fixing). The default
  config points to Cerebras, but any compatible endpoint works.
- An ElevenLabs API key (for voice dictation)

## Getting started

1. Download one of the executables above and run it. The app appears in your
   system tray.
2. Right-click the tray icon and open Settings.
3. Enter your API keys in the Typo Fixing and Dictation tabs. Use the Test
   Connection buttons to verify everything works.
4. Press **Ctrl+Alt+Space** to fix typos in clipboard text, or **Ctrl+Alt+D**
   to toggle dictation. Both hotkeys are rebindable in Settings.

## Features

- **Typo and grammar fixing** — clipboard text in, corrected text out,
  formatting preserved (markdown, code, line breaks).
- **Voice dictation** — toggle-to-record, automatic language detection via
  ElevenLabs Scribe v2. Output goes to the clipboard or pastes directly at the
  cursor, configurable per preference.
- **Accessibility built in** — every control is keyboard-reachable, has a
  proper accessible name, and works cleanly with the NVDA screen reader. All
  feedback is via audio cues and system tray notifications.
- Single-instance app, single concurrent operation, no main window.

## Notes

- Settings are stored as YAML in `%APPDATA%\AIWritingHelper\`.
- Windows SmartScreen may warn the first time you run the executable since it
  is unsigned. Click "More info" → "Run anyway".
- Source code, full documentation, and license: see the
  [README](https://github.com/ohylli/ai-writing-helper/blob/main/README.md).
