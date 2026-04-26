# AI Writing Helper — Implementation Plan

## Context

We have a design doc (`design.md`) and no code yet. The goal is to build a Windows system tray utility (C# / .NET 10 / WinForms) with two features: AI typo fixing and voice dictation. The primary user is blind and uses NVDA — accessibility is critical throughout.

**Strategy:** Build typo fixing end-to-end first (it's simpler and forces all shared infrastructure into existence), then layer dictation on top.

---

## Phase 1: Project Scaffolding & Logging

Create the solution structure, add NuGet dependencies, configure logging, verify it builds and runs (empty tray app).

- [x] Create `AIWritingHelper.sln`, `src/AIWritingHelper/AIWritingHelper.csproj` (.NET 10, WinForms, single-file publish ready)
- [x] Create `tests/AIWritingHelper.Tests/AIWritingHelper.Tests.csproj` (xUnit)
- [x] Create folder skeleton: `Core/`, `Services/`, `UI/`, `Audio/`, `Config/`
- [x] Add NuGet packages: `YamlDotNet`, `NAudio`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`, `Serilog` (+ file sink)
- [x] Configure Serilog with file sink (log to `%APPDATA%\AIWritingHelper\logs\`)
  - New timestamped log file per app startup (e.g., `log-2026-02-19_143022.txt`)
  - On startup, delete all but the 3 most recent log files
  - Log level driven by settings (once settings exist; hardcode a default initially)
  - Wire into DI as `ILogger<T>`
- [x] Minimal `Program.cs`: application entry point, DI container setup, WinForms `Application.Run()` with a placeholder tray icon
- [x] Single-instance enforcement using a named `Mutex`
- [x] App-wide `CancellationTokenSource` for graceful shutdown — cancelled on app exit so in-flight API calls terminate immediately instead of waiting for timeout
- [x] Verify: `dotnet build` succeeds, `dotnet run` shows a tray icon, second instance exits

---

## Phase 2: Settings & Configuration

Build the settings infrastructure — everything else depends on it.

- [x] `Config/AppSettings.cs` — settings model class (all fields from design doc: API keys, endpoints, model names, hotkeys, system prompt, mic selection, output mode, log level, start-with-Windows)
- [x] `Config/SettingsManager.cs` — load/save YAML to `%APPDATA%\AIWritingHelper\settings.yaml`, sensible defaults, create directory if missing
- [x] Default system prompt for typo fixing (from design doc: fix typos/grammar, preserve formatting/markdown/meaning)
- [x] Unit tests: round-trip save/load, defaults applied for missing fields, corrupt file handling

---

## Phase 3: Core Interfaces & Sound Feedback

Define the abstractions and build the sound/notification feedback system.

- [x] `Core/ILLMProvider.cs` — `Task<string> FixTextAsync(string text, string systemPrompt, CancellationToken ct)`
- [x] `Core/ISTTProvider.cs` — `Task<string> TranscribeAsync(Stream audio, CancellationToken ct)` (interface only for now)
- [x] `Core/IClipboardService.cs` — `string? GetText()`, `void SetText(string text)` (abstraction over WinForms clipboard for testability)
  - **Note:** WinForms clipboard access must happen on the STA thread. The implementation must marshal calls to the UI thread.
- [x] `Core/ISoundPlayer.cs` — `void PlaySuccess()`, `void PlayError()`, `void PlayRecordingStart()`, `void PlayRecordingStop()`
- [x] `Core/ITrayNotifier.cs` — `void ShowNotification(string title, string message)`
- [x] `Audio/SystemSoundPlayer.cs` — implementation using Windows system sounds (`SystemSounds.Asterisk`, `.Hand`, etc.)
- [x] `UI/TrayNotifier.cs` — implementation using `NotifyIcon.ShowBalloonTip()`

---

## Phase 4: LLM Service

Implement the OpenAI-compatible API client.

- [x] `Services/OpenAICompatibleLLMProvider.cs` — implements `ILLMProvider`
  - `HttpClient` with configurable base URL, API key (Bearer token), model name
  - Base URL is expected to include the version prefix (e.g., `https://api.cerebras.ai/v1`). The provider appends `/chat/completions` to the base URL.
  - Parse response, extract assistant message content
  - 30-second timeout via `CancellationTokenSource`
  - Proper error handling (HTTP errors, JSON parse errors, timeout)
- [x] Unit tests with mocked `HttpMessageHandler`: success, API error, timeout, malformed response

---

## Phase 5: Typo Fix Orchestration

The core workflow that ties clipboard, LLM, sound, and notifications together.

- [x] `Core/TypoFixService.cs`
  - Read clipboard text → validate non-empty
  - Call LLM provider with system prompt from settings
  - Write corrected text back to clipboard
  - Play success sound
  - On error: play error sound + show tray notification with description
- [x] `Core/OperationLock.cs` — ensures only one operation (typo fix or dictation) runs at a time; returns busy status if locked
- [x] Unit tests: happy path, empty clipboard, API failure, busy rejection

---

## Phase 6: Global Hotkeys & System Tray

Wire up hotkey registration and the tray icon.

- [x] `Core/GlobalHotkeyManager.cs` — Win32 `RegisterHotKey`/`UnregisterHotKey` via P/Invoke, modifier+key format
  - Private `HotkeyWindow` (`NativeWindow` subclass) receives `WM_HOTKEY` messages
  - `ParseHotkey(string)` parses "Ctrl+Alt+Space" format into modifiers + virtual key code
  - `MOD_NOREPEAT` prevents repeat when held (important for accessibility)
  - Error handling for conflicts (hotkey already registered by another app)
- [x] `UI/TrayApplicationContext.cs` — `ApplicationContext` subclass
  - `NotifyIcon` with icon and context menu (Settings, Quit)
  - Receives hotkey events, triggers `TypoFixService` via `Lazy<T>` (breaks circular DI dependency)
  - Shows startup notification if hotkey registration fails
- [x] Wire into `Program.cs` with DI

---

## Phase 7: Settings GUI (General + Typo Fixing tabs)

- [x] `UI/SettingsForm.cs` — WinForms `Form` with `TabControl`, programmatic layout (no designer), 500×480, `FixedDialog`
  - **General tab:** log level dropdown (`ComboBox` DropDownList), configurable hotkeys for typo fix and dictation (read-only TextBox + "Set New Hotkey" capture button per hotkey)
  - **Typo Fixing tab:** API endpoint, API key (password masked), model name, "Test Connection" button, system prompt multi-line textbox with `AcceptsReturn` and vertical scrollbar
  - **Dictation tab:** placeholder label ("Dictation settings will be available in a future update.")
- [x] Accessibility: every control has `AccessibleName`/`AccessibleDescription`, logical tab order, keyboard-navigable
- [x] Save (Alt+S) / Cancel (Alt+C) buttons with `AcceptButton`/`CancelButton` wiring (Enter saves, Escape cancels)
- [x] `PopulateFromSettings` loads current `AppSettings` values on open; `OnSaveClick` writes back and persists via `SettingsManager`
- [x] "Test Connection" snapshots current settings, temporarily applies form values, calls `FixTextAsync`, restores originals in `finally`
- [x] Hot-reload: log level updated via `LoggingLevelSwitch.MinimumLevel`; API settings read from `AppSettings` on next call (no explicit reload needed)
- [x] `TrayApplicationContext` updated: new DI deps (`SettingsManager`, `LoggingLevelSwitch`, `ILLMProvider`, `ILoggerFactory`), Settings menu item before separator before Quit, single-instance `SettingsForm` guard
- [x] Hotkey capture/re-registration UI: "Set New Hotkey" buttons enter keyboard capture mode via `KeyPreview`, `FormatHotkey` produces strings compatible with `ParseHotkey`. On save, hotkeys are validated via Win32 registration before persisting — atomic rollback on failure with distinct error messages. `SettingsForm` receives `GlobalHotkeyManager` from `TrayApplicationContext`.
- [x] Start with Windows checkbox

---

## Phase 8: Polish & End-to-End Testing (Typo Fixing)

Manual end-to-end validation of the complete typo fixing flow.

- [x] Copy text with typos → press hotkey → verify corrected text on clipboard → hear success sound
- [x] Test with empty clipboard, non-text clipboard, API down, invalid API key
- [x] Test hotkey conflict detection
- [x] Test settings save/load round-trip through GUI
- [x] Test single-instance enforcement
- [x] NVDA screen reader: verify all settings controls are announced, tab order works, balloon tips announced
- [x] Run `dotnet test` — all unit tests pass

---

## Phase 9: Audio Recording

- [x] `Audio/MicrophoneRecorder.cs` — NAudio `WaveInEvent` wrapper
  - Start/stop recording to a `MemoryStream` (WAV format)
  - Configurable device selection (enumerate devices, select by settings)
  - 1-hour max duration auto-stop
- [x] `Core/IAudioRecorder.cs` — interface: `Start()`, `Stop() → Stream`, `EnumerateDevices() → List<AudioDevice>`

---

## Phase 10: Speech-to-Text Service

- [x] `Services/ElevenLabsSTTProvider.cs` — implements `ISTTProvider`
  - POST audio to ElevenLabs Scribe v2 endpoint (multipart form upload)
  - Verify that the WAV format from NAudio is accepted by the API (ElevenLabs does accept WAV)
  - Parse transcription response
  - 30-second timeout
  - Error handling
- [x] Unit tests with mocked HTTP

---

## Phase 11: Dictation Orchestration (clipboard mode)

Get the basic dictation flow working end-to-end with clipboard output only. Direct insertion is deferred to phase 13 so we can validate the simpler path first.

- [x] `Core/DictationService.cs`
  - Toggle pattern: first hotkey press starts recording, second stops
  - On stop: send audio to STT provider, get text
  - Write transcribed text to clipboard
  - Play appropriate sounds (start, stop, success, error)
  - Uses `OperationLock` from Phase 5
  - Empty STT result (silence) surfaces "No speech detected" without clobbering the clipboard
  - `RecordingFaulted` event handler releases the lock and notifies the user on mid-recording hardware failure
- [x] Unit tests
- [x] Wired into DI (`Program.cs`) via `Lazy<DictationService>` and dispatched from `TrayApplicationContext.OnHotkeyPressed`

---

## Phase 12: Settings GUI — Dictation Tab & End-to-End Validation

Fill in the Dictation tab with everything needed for the clipboard-mode flow, then validate end-to-end. Output mode UI is intentionally omitted — it lands in phase 13 alongside direct insertion.

- [x] Fill in the Dictation tab: API key, model name, "Test Connection", microphone dropdown
- [x] Microphone dropdown populated from `IAudioRecorder.EnumerateDevices()`. A "(Default)" entry maps to empty `MicrophoneDeviceName` (system default). Saved devices that aren't currently present (e.g., USB mic unplugged) are added back to the combo so save round-trips don't silently drop the user's setting.
- [x] Accessibility: same standards as Phase 7
- [x] Register dictation hotkey alongside typo fix hotkey (already done in phase 11 — `TrayApplicationContext.RegisterHotkeys` registers both)
- [x] `ISTTProvider.TranscribeAsync` overload that accepts API key + model name as parameters (mirrors `ILLMProvider.FixTextAsync` overload), so Test Connection doesn't need to mutate the live settings singleton
- [x] `Audio/SilentWavGenerator` — small helper that produces a 16 kHz / 16-bit / mono silent WAV stream for the STT test request
- [x] End-to-end dictation: press hotkey → speak → press hotkey → text on clipboard (manual)
- [x] Test concurrent operation rejection (trigger typo fix during dictation) (manual)
- [x] NVDA testing for dictation tab controls (manual)

---

## Phase 13: Direct Insertion Output Mode

Now that the clipboard-mode dictation flow is proven, add direct insertion as an alternative output mode.

- [ ] `Core/DirectInsertionService.cs` — save clipboard → set text → `SendInput` Ctrl+V → restore clipboard
  - Uses Win32 `SendInput` P/Invoke (not `SendKeys`)
  - **Note:** A delay is needed between the `SendInput` paste and clipboard restore, otherwise the restore happens before the target app processes the paste. This is inherently fragile — clipboard mode remains the reliable default.
- [ ] Add output mode radio buttons (clipboard / direct insertion) to the Dictation settings tab; persist to `AppSettings`
- [ ] Wire `DictationService` to dispatch to clipboard or direct insertion based on the configured output mode
- [ ] Unit tests for `DirectInsertionService` and the mode dispatch in `DictationService`
- [ ] End-to-end test of direct insertion mode

---

## Phase 14: Final Integration & Testing

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
