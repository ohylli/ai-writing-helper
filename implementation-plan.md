# AI Writing Helper — Implementation Plan

## Context

We have a design doc (`design.md`) and no code yet. The goal is to build a Windows system tray utility (C# / .NET 10 / WinForms) with two features: AI typo fixing and voice dictation. The primary user is blind and uses NVDA — accessibility is critical throughout.

**Strategy:** Build typo fixing end-to-end first (it's simpler and forces all shared infrastructure into existence), then layer dictation on top.

---

## Phase 1: Project Scaffolding & Logging

Create the solution structure, add NuGet dependencies, configure logging, verify it builds and runs (empty tray app).

- [ ] Create `AIWritingHelper.sln`, `src/AIWritingHelper/AIWritingHelper.csproj` (.NET 10, WinForms, single-file publish ready)
- [ ] Create `tests/AIWritingHelper.Tests/AIWritingHelper.Tests.csproj` (xUnit)
- [ ] Create folder skeleton: `Core/`, `Services/`, `UI/`, `Audio/`, `Config/`
- [ ] Add NuGet packages: `YamlDotNet`, `NAudio`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`, `Serilog` (+ file sink)
- [ ] Configure Serilog with file sink (log to `%APPDATA%\AIWritingHelper\logs\`)
  - New timestamped log file per app startup (e.g., `log-2026-02-19_143022.txt`)
  - On startup, delete all but the 3 most recent log files
  - Log level driven by settings (once settings exist; hardcode a default initially)
  - Wire into DI as `ILogger<T>`
- [ ] Minimal `Program.cs`: application entry point, DI container setup, WinForms `Application.Run()` with a placeholder tray icon
- [ ] Single-instance enforcement using a named `Mutex`
- [ ] App-wide `CancellationTokenSource` for graceful shutdown — cancelled on app exit so in-flight API calls terminate immediately instead of waiting for timeout
- [ ] Verify: `dotnet build` succeeds, `dotnet run` shows a tray icon, second instance exits

---

## Phase 2: Settings & Configuration

Build the settings infrastructure — everything else depends on it.

- [ ] `Config/AppSettings.cs` — settings model class (all fields from design doc: API keys, endpoints, model names, hotkeys, system prompt, mic selection, output mode, log level, start-with-Windows)
- [ ] `Config/SettingsManager.cs` — load/save YAML to `%APPDATA%\AIWritingHelper\settings.yaml`, sensible defaults, create directory if missing
- [ ] Default system prompt for typo fixing (from design doc: fix typos/grammar, preserve formatting/markdown/meaning)
- [ ] Unit tests: round-trip save/load, defaults applied for missing fields, corrupt file handling

---

## Phase 3: Core Interfaces & Sound Feedback

Define the abstractions and build the sound/notification feedback system.

- [ ] `Core/ILLMProvider.cs` — `Task<string> FixTextAsync(string text, string systemPrompt, CancellationToken ct)`
- [ ] `Core/ISTTProvider.cs` — `Task<string> TranscribeAsync(Stream audio, CancellationToken ct)` (interface only for now)
- [ ] `Core/IClipboardService.cs` — `string? GetText()`, `void SetText(string text)` (abstraction over WinForms clipboard for testability)
  - **Note:** WinForms clipboard access must happen on the STA thread. The implementation must marshal calls to the UI thread.
- [ ] `Core/ISoundPlayer.cs` — `void PlaySuccess()`, `void PlayError()`, `void PlayRecordingStart()`, `void PlayRecordingStop()`
- [ ] `Core/ITrayNotifier.cs` — `void ShowNotification(string title, string message)`
- [ ] `Audio/SystemSoundPlayer.cs` — implementation using Windows system sounds (`SystemSounds.Asterisk`, `.Hand`, etc.)
- [ ] `UI/TrayNotifier.cs` — implementation using `NotifyIcon.ShowBalloonTip()`

---

## Phase 4: LLM Service

Implement the OpenAI-compatible API client.

- [ ] `Services/OpenAICompatibleLLMProvider.cs` — implements `ILLMProvider`
  - `HttpClient` with configurable base URL, API key (Bearer token), model name
  - Base URL is expected to include the version prefix (e.g., `https://api.cerebras.ai/v1`). The provider appends `/chat/completions` to the base URL.
  - Parse response, extract assistant message content
  - 30-second timeout via `CancellationTokenSource`
  - Proper error handling (HTTP errors, JSON parse errors, timeout)
- [ ] Unit tests with mocked `HttpMessageHandler`: success, API error, timeout, malformed response

---

## Phase 5: Typo Fix Orchestration

The core workflow that ties clipboard, LLM, sound, and notifications together.

- [ ] `Core/TypoFixService.cs`
  - Read clipboard text → validate non-empty
  - Call LLM provider with system prompt from settings
  - Write corrected text back to clipboard
  - Play success sound
  - On error: play error sound + show tray notification with description
- [ ] `Core/OperationLock.cs` — ensures only one operation (typo fix or dictation) runs at a time; returns busy status if locked
- [ ] Unit tests: happy path, empty clipboard, API failure, busy rejection

---

## Phase 6: Global Hotkeys & System Tray

Wire up hotkey registration and the tray icon.

- [ ] `Core/GlobalHotkeyManager.cs` — Win32 `RegisterHotKey`/`UnregisterHotKey` via P/Invoke, modifier+key format
  - Register configured hotkey for typo fix
  - Handle `WM_HOTKEY` messages
  - **Note:** `RegisterHotKey` delivers `WM_HOTKEY` to a specific window handle. The implementation needs an HWND — typically a hidden `NativeWindow` or the form handle. Decide on exact approach during implementation.
  - Error handling for conflicts (hotkey already registered by another app)
- [ ] `UI/TrayApplicationContext.cs` — `ApplicationContext` subclass
  - `NotifyIcon` with icon and context menu (Settings, Quit)
  - Receives hotkey events, triggers `TypoFixService`
  - Shows startup notification if hotkey registration fails
- [ ] Wire into `Program.cs` with DI

---

## Phase 7: Settings GUI (General + Typo Fixing tabs)

- [ ] `UI/SettingsForm.cs` — WinForms `Form` with `TabControl`
  - **General tab:** hotkey config for typo fix (and dictation placeholder), start-with-Windows checkbox, log level dropdown
    - Hotkey configuration UX TBD — decide during implementation whether to use a key-capture control or another approach (key-capture is likely more accessible for a blind user)
  - **Typo Fixing tab:** API endpoint URL, API key (password masked), model name, "Test Connection" button, system prompt multi-line textbox (shows default, fully editable)
  - **Dictation tab:** placeholder controls (filled in Phase 12)
- [ ] Accessibility: every control has `AccessibleName`/`AccessibleDescription`, logical tab order, keyboard-navigable
- [ ] Save/Cancel buttons, load current settings on open, save to `SettingsManager`
- [ ] "Test Connection" calls the LLM with a trivial test prompt, shows success/failure
- [ ] Hot-reload: after saving, re-register hotkeys if changed, update log level
- [ ] Start-with-Windows implementation: add/remove registry entry in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` based on the checkbox setting

---

## Phase 8: Polish & End-to-End Testing (Typo Fixing)

Manual end-to-end validation of the complete typo fixing flow.

- [ ] Copy text with typos → press hotkey → verify corrected text on clipboard → hear success sound
- [ ] Test with empty clipboard, non-text clipboard, API down, invalid API key
- [ ] Test hotkey conflict detection
- [ ] Test settings save/load round-trip through GUI
- [ ] Test single-instance enforcement
- [ ] NVDA screen reader: verify all settings controls are announced, tab order works, balloon tips announced
- [ ] Run `dotnet test` — all unit tests pass

---

## Phase 9: Audio Recording

- [ ] `Audio/MicrophoneRecorder.cs` — NAudio `WaveInEvent` wrapper
  - Start/stop recording to a `MemoryStream` (WAV format)
  - Configurable device selection (enumerate devices, select by settings)
  - 1-hour max duration auto-stop
- [ ] `Core/IAudioRecorder.cs` — interface: `Start()`, `Stop() → Stream`, `EnumerateDevices() → List<AudioDevice>`

---

## Phase 10: Speech-to-Text Service

- [ ] `Services/ElevenLabsSTTProvider.cs` — implements `ISTTProvider`
  - POST audio to ElevenLabs Scribe v2 endpoint (multipart form upload)
  - Verify that the WAV format from NAudio is accepted by the API (ElevenLabs does accept WAV)
  - Parse transcription response
  - 30-second timeout
  - Error handling
- [ ] Unit tests with mocked HTTP

---

## Phase 11: Dictation Orchestration

- [ ] `Core/DictationService.cs`
  - Toggle pattern: first hotkey press starts recording, second stops
  - On stop: send audio to STT provider, get text
  - Output mode from settings: clipboard or direct insertion
  - Play appropriate sounds (start, stop, success, error)
  - Uses `OperationLock` from Phase 5
- [ ] `Core/DirectInsertionService.cs` — save clipboard → set text → `SendInput` Ctrl+V → restore clipboard
  - Uses Win32 `SendInput` P/Invoke (not `SendKeys`)
  - **Note:** A delay is needed between the `SendInput` paste and clipboard restore, otherwise the restore happens before the target app processes the paste. This is inherently fragile — clipboard mode remains the reliable default.
- [ ] Unit tests

---

## Phase 12: Settings GUI — Dictation Tab

- [ ] Fill in the Dictation tab: API key, model name, "Test Connection", microphone dropdown, output mode radio buttons
- [ ] Microphone dropdown populated from `IAudioRecorder.EnumerateDevices()`
- [ ] Accessibility: same standards as Phase 7
- [ ] Register dictation hotkey alongside typo fix hotkey

---

## Phase 13: Final Integration & Testing

- [ ] End-to-end dictation: press hotkey → speak → press hotkey → text appears
- [ ] Test both output modes (clipboard, direct insertion)
- [ ] Test concurrent operation rejection (trigger typo fix during dictation)
- [ ] NVDA testing for dictation tab controls
- [ ] All unit tests pass
- [ ] `dotnet publish` single-file exe works
- [ ] Update README with usage instructions

---

## Verification

After each phase, verify with:
1. `dotnet build` — no errors or warnings
2. `dotnet test` — all tests pass
3. `dotnet run --project src/AIWritingHelper` — manual smoke test of new functionality
4. For GUI phases: NVDA screen reader walkthrough of new controls
