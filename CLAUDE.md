# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AI Writing Helper is a Windows system tray utility (C# / .NET 10 / WinForms) that provides AI-powered typo/grammar fixing and voice dictation via global hotkeys. The primary user is blind and uses the NVDA screen reader — accessibility is a core requirement, not an afterthought.

The complete design specification is in `design.md`.

## Build Commands

```bash
dotnet build          # Build the solution
dotnet run --project src/AIWritingHelper   # Run the app
dotnet test           # Run all tests
dotnet publish src/AIWritingHelper -c Release --self-contained -p:PublishSingleFile=true  # Single-file exe
```

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

Phases 1-3 are complete. The app runs as a system tray icon with single-instance enforcement, Serilog logging, and YAML-based settings persistence (`AppSettings` + `SettingsManager` in `Config/`). Settings are loaded at startup and registered in DI.

Phase 3 added core abstraction interfaces (`ILLMProvider`, `ISTTProvider`, `IClipboardService`, `ISoundPlayer`, `ITrayNotifier` in `Core/`) and their implementations: `SystemSoundPlayer` (Audio/), `ClipboardService` (Core/), and `TrayNotifier` (UI/). All three are registered in DI. `ILLMProvider` and `ISTTProvider` have no implementations yet — those come in Phases 4 and 10. Next up: Phase 4.

## Implementation Plan

The detailed phased implementation plan is in `implementation-plan.md`. Strategy: build typo fixing end-to-end first (includes all shared infrastructure), then layer dictation on top.
