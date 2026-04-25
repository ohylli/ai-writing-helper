# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AI Writing Helper is a Windows system tray utility (C# / .NET 10 / WinForms) that provides AI-powered typo/grammar fixing and voice dictation via global hotkeys. The primary user is blind and uses the NVDA screen reader — accessibility is a core requirement, not an afterthought.

The complete design specification is in `design.md`.

## Build Commands

```bash
dotnet build          # Build the solution
dotnet run --project src/AIWritingHelper   # Run the app
dotnet test --filter "Category!=Integration"   # Run unit tests (default for development)
dotnet test --filter "Category=Integration"    # Run integration tests (only when explicitly requested)
dotnet publish src/AIWritingHelper -c Release --self-contained -p:PublishSingleFile=true  # Single-file exe
```

**Important:** Only run `dotnet test --filter "Category!=Integration"` during normal development. Integration tests hit real APIs and should only be run when the user explicitly asks for them.

Integration tests require a `.env` file in `tests/AIWritingHelper.Tests/` (copy `.env.example` and add your API key). Tests are skipped automatically if the key is missing.

## Architecture

```
src/AIWritingHelper/
  Core/       # Business logic and provider interfaces
  Services/   # LLM (OpenAI-compatible API) and STT (ElevenLabs Scribe v2) implementations
  UI/         # WinForms settings dialog, system tray icon
  Audio/      # NAudio microphone recording, system sound playback
  Config/     # YAML settings persistence (%APPDATA%\AIWritingHelper\)
tests/AIWritingHelper.Tests/   # xUnit or NUnit tests
```

**Key patterns:**
- Provider abstraction via interfaces for both LLM and speech-to-text services
- Dependency injection for testability — core logic is separated from UI/hardware
- Single concurrent operation enforced (typo fix or dictation, not both)
- Single-instance app enforcement

**Data flows:**
- Typo fixing: clipboard text → LLM API → corrected text back to clipboard → success sound
- Dictation: hotkey toggle → NAudio record → STT API → text to clipboard or direct paste via SendInput

## Accessibility Requirements

- All WinForms controls need proper accessible names/descriptions
- Logical, complete tab order — every control keyboard-reachable
- No visual-only information; all feedback via audio cues and balloon tip notifications
- Use standard WinForms controls (built-in accessibility support)
- Use `SendInput` API (not `SendKeys`) for simulated input — more reliable with NVDA

## External Services

- **LLM:** OpenAI-compatible API format (configurable base URL, API key, model). Default provider: Cerebras
- **STT:** ElevenLabs Scribe v2 with auto language detection
- Both use 30-second API timeouts

## Settings

Stored as YAML in `%APPDATA%\AIWritingHelper\`. Includes API credentials, hotkey bindings, system prompt (user-editable with sensible default), microphone selection, output mode, log level.

## Current Status

Phases 1–11 are complete — typo fixing and clipboard-mode dictation are both functional end-to-end. Next up: the Dictation settings tab (phase 12) and the direct-insertion output mode (phase 13).

**What's working:**
- System tray app with Settings dialog (General, Typo Fixing, Dictation tabs) and global hotkeys
- Typo fix flow: Ctrl+Alt+Space → clipboard text → LLM API → corrected text back to clipboard → success sound
- Dictation flow (clipboard mode): Ctrl+Alt+D → record → Ctrl+Alt+D → STT → transcript on clipboard → success sound. Empty transcript surfaces "No speech detected" without clobbering the clipboard.
- Core abstractions: `ILLMProvider`, `ISTTProvider`, `IAudioRecorder`, `IClipboardService`, `ISoundPlayer`, `ITrayNotifier`
- `OpenAICompatibleLLMProvider` (Services/) with configurable endpoint/model/API key, 30s timeout
- `OperationLock` (Core/) — semaphore guard ensuring single concurrent operation
- `GlobalHotkeyManager` (Core/) — Win32 `RegisterHotKey` P/Invoke with `MOD_NOREPEAT`
- `SettingsForm` (UI/) — hotkey capture, Test Connection, Start with Windows, all NVDA-accessible
- Audio feedback via `SystemSoundPlayer`, balloon notifications via `TrayNotifier`
- `MicrophoneRecorder` (Audio/) — NAudio `WaveInEvent` wrapper with device selection, 16kHz/16-bit/mono WAV, 1-hour auto-stop
- `ElevenLabsSTTProvider` (Services/) — multipart upload to Scribe v2 (`scribe_v2`), `xi-api-key` header, 30s timeout, returns transcribed text
- `DictationService` (Core/) — toggle pattern, holds `OperationLock` across record→transcribe→clipboard, releases lock cleanly on `RecordingFaulted`

**Not yet implemented:** Dictation tab GUI (STT API key/model/mic dropdown — currently must be edited directly in `settings.yaml`), direct-insertion output mode.

## Implementation Plan

The detailed phased implementation plan is in `implementation-plan.md`. Strategy: build typo fixing end-to-end first (includes all shared infrastructure), then layer dictation on top.
