# AI Writing Helper - Design Document

## Overview

AI Writing Helper is a Windows system tray utility that helps users write more
efficiently through two core features: AI-powered typo/grammar fixing and voice
dictation. The app runs in the background and is operated primarily through
global hotkeys, providing audio feedback for all actions.

## Motivation

Typing is error-prone and time-consuming, especially for users who produce
frequent typos. Manually fixing mistakes interrupts the flow of writing. Voice
dictation offers an alternative input method but typically requires switching to
a dedicated application. AI Writing Helper solves both problems by sitting
quietly in the system tray and being instantly available via hotkeys in any
application.

## Core Features

### 1. Typo / Grammar Fixing

**Flow:**
1. User copies text to clipboard (or selects text in an application).
2. User presses the configured global hotkey.
3. The app reads the clipboard contents.
4. Text is sent to the configured LLM with a system prompt instructing it to fix
   typos and obvious grammar mistakes while preserving formatting (including
   markdown) and original meaning.
5. The corrected text replaces the clipboard contents.
6. A success sound is played.

**Behavior:**
- Strictly fixes typos and grammar by default - no style or tone changes.
- Preserves all formatting including markdown syntax.
- Multi-language support (English and Finnish at minimum, but the LLM handles
  this naturally).
- The system prompt is fully customizable in settings. A sensible default is
  provided and visible to the user, who can edit it entirely.
- If the clipboard is empty or contains non-text content, an error sound is
  played and a system tray notification describes the problem.

### 2. Voice Dictation

**Flow:**
1. User presses the configured global hotkey to start recording.
2. A sound plays to confirm recording has started.
3. User speaks.
4. User presses the same hotkey again to stop recording.
5. Audio is sent to the configured speech-to-text service for transcription.
6. Transcribed text is either placed on the clipboard or inserted directly at
   the current cursor position (configurable in settings).
7. A success sound is played.

**Behavior:**
- Supports both short bursts (a sentence) and longer recordings.
- Maximum recording duration of 1 hour to prevent infinite recordings. When the
  limit is reached, recording stops automatically and proceeds to transcription.
- Multi-language support (English and Finnish at minimum). Language is
  auto-detected by the speech-to-text provider (Scribe v2 supports this
  natively).
- Microphone is selectable in settings.
- Output mode is configurable: clipboard or direct insertion.

**Future considerations:**
- Post-processing transcribed text through an LLM (e.g., for formatting or
  punctuation cleanup).
- Transcribing existing audio files instead of only live microphone input.

### 3. System Tray

- The app lives in the Windows system tray with an icon.
- Right-click context menu provides:
  - Open Settings
  - Quit

### 4. Settings GUI

A windowed settings dialog with three tabs:

**General tab:**
- Global hotkey configuration for typo fixing and dictation. Hotkeys use a
  modifier+key format (e.g., Ctrl+Alt+Space, Ctrl+Shift+T).
- Start with Windows (toggle).
- Logging level selection (Debug / Info / Warning / Error).

**Typo Fixing tab:**
- LLM provider configuration (API endpoint URL, API key, model name).
- "Test Connection" button to validate API connectivity and credentials.
- System prompt editor showing the full default prompt, fully editable by the
  user.

**Dictation tab:**
- Speech-to-text provider configuration (API key, model name).
- "Test Connection" button to validate API connectivity and credentials.
- Microphone selection dropdown.
- Output mode for dictation (clipboard vs direct insertion).

### 5. Error Handling & Feedback

- **Success:** A short success sound is played.
- **Recording started:** A distinct sound is played when dictation recording
  begins.
- **Recording stopped:** A sound is played when dictation recording stops and
  transcription begins.
- **Busy:** If the user triggers an operation while another is already in
  progress, the new operation is ignored and a short error sound is played to
  indicate the app is busy.
- **Error:** An error sound is played and an informative system tray notification
  is shown describing the problem (e.g., "API request failed: invalid API key").
- All sounds use built-in Windows system sounds initially. Custom sound support
  may be added later.

### 6. Logging

- Structured logging with configurable levels: Debug, Info, Warning, Error.
- Debug level for development and troubleshooting.
- Info level for regular use.
- Logs written to a file in the application's data directory.
- Log level is configurable in settings.

## Accessibility

The primary user is blind and uses the NVDA screen reader. The GUI must be fully
accessible:

- All WinForms controls must have proper accessible names and descriptions.
- Tab order must be logical and complete - every control reachable by keyboard.
- No information conveyed by visuals alone; all feedback is provided through
  audio cues and screen-reader-compatible notifications.
- Standard WinForms controls are used wherever possible as they have built-in
  accessibility support.
- System tray notifications use standard Windows balloon tips which screen
  readers announce.
- Settings dialog uses standard labeled controls (no custom-drawn UI).

## Technology Stack

### Runtime & Language
- **C# on .NET 10** (LTS, released November 2025, supported until November
  2028).
- **Note:** .NET 10 officially supports Windows 10 only on Enterprise and IoT
  LTSC editions. Windows 10 Home/Pro is not in the supported OS matrix (though
  it will likely work in practice). Windows 10 consumer support ended October
  2025 (ESU available through October 2026).
- **WinForms** for the settings GUI and system tray integration.

### Build & Development
- **dotnet CLI** for building, running, and publishing.
- **Visual Studio Code** as the editor.
- **Single-file executable** publishing (`dotnet publish` with single-file
  option).

### Version Control
- **Git** and **GitHub** for source control and collaboration.

### External Services

**LLM for typo fixing:**
- Initial provider: [Cerebras](https://www.cerebras.ai/) (free tier available).
- Uses the **OpenAI-compatible API format** (base URL + API key + model name).
- This is the abstraction layer: any provider or local tool (e.g., Ollama) that
  exposes an OpenAI-compatible endpoint can be used by changing the settings.

**Speech-to-text for dictation:**
- Initial provider: [ElevenLabs Scribe
  v2](https://elevenlabs.io/docs/api-reference/speech-to-text/convert).
- Provider abstraction allows swapping to other services in the future.

### Audio
- **NAudio** (or similar library) for microphone recording and audio capture.
- **System.Media.SoundPlayer** or equivalent for playing Windows system sounds.

### Testing
- **xUnit** or **NUnit** for automated tests.
- Focus testing on key logic that is hard to verify manually:
  - Provider abstraction layer (API request/response handling).
  - Clipboard text processing pipeline.
  - Settings persistence (save/load).
  - System prompt handling.
- UI and hardware-dependent features (hotkeys, microphone, tray icon) are
  harder to unit test and will rely on manual verification, but the design
  should separate logic from UI to maximize testable surface area.

## Application Behavior

### Single Instance

The app enforces single-instance execution. If the user launches a second
instance, it exits immediately (optionally bringing the existing instance's
settings window to the foreground).

### Hotkey Conflict Handling

- When the user configures a new hotkey in settings, the app attempts to register
  it and shows an error if the hotkey is already in use by another application.
- On startup, if a configured hotkey cannot be registered, a system tray
  notification informs the user of the conflict.

### Concurrency

Only one operation (typo fix or dictation) can run at a time. If the user
triggers a second operation while one is in progress, it is ignored and a short
error sound is played.

### API Timeouts

API calls to the LLM and speech-to-text services use a 30-second timeout. On
timeout, an error sound is played and a system tray notification is shown.

## Architecture Considerations

### Provider Abstraction

Both the LLM and speech-to-text services are accessed through abstraction
interfaces. This allows:
- Swapping providers by changing configuration, not code.
- For the LLM: any OpenAI-compatible API works out of the box. The app sends
  requests to a configurable base URL with configurable model name.
- For speech-to-text: an interface defines the contract (send audio, receive
  text). The initial implementation targets ElevenLabs, but the interface allows
  adding other providers.
- Future support for local models (e.g., Ollama for LLM, Whisper for STT).

### Separation of Concerns for Testability

The design separates:
- **Core logic** (text processing, API communication, settings management) from
  **UI** (WinForms, tray icon, hotkey registration).
- This allows core logic to be thoroughly unit tested without depending on UI
  components or hardware.
- Services are injected via interfaces, making them mockable in tests.

### Settings Persistence

- Settings stored in a YAML file in the user's application data directory
  (`%APPDATA%\AIWritingHelper\`).
- Includes: API keys, model names, endpoint URLs, hotkey bindings, system
  prompt, microphone selection, output mode, logging level, startup preference.

### Direct Text Insertion

- When configured for direct insertion instead of clipboard, the app uses a
  simulated paste approach: save the current clipboard contents, place the new
  text on the clipboard, simulate Ctrl+V via the Windows `SendInput` API, then
  restore the original clipboard contents.
- `SendInput` is preferred over the WinForms `SendKeys` class for reliability,
  especially alongside screen readers like NVDA.
- **Known limitation:** There is a small race condition window between pasting and
  restoring the clipboard. If the user copies something during this window, their
  clipboard content may be overwritten by the restore.
- Clipboard mode (no automatic pasting) remains the reliable default.

## Project Structure (Preliminary)

```
AIWritingHelper/
  src/
    AIWritingHelper/           # Main application project
      Program.cs
      AIWritingHelper.csproj
      Core/                    # Business logic, provider interfaces
      Services/                # Provider implementations (LLM, STT)
      UI/                      # WinForms forms, tray icon
      Audio/                   # Recording and playback
      Config/                  # Settings management
  tests/
    AIWritingHelper.Tests/     # Unit test project
  AIWritingHelper.sln
  README.md
```

## Out of Scope (for initial version)

- Custom sound files (use system sounds).
- Microphone testing in settings.
- Audio file transcription (future feature).
- LLM post-processing of dictation results (future feature).
- Installer / auto-update (distribute as single-file exe).
- Multiple profiles or presets for different system prompts.
