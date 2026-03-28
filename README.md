# AI Writing Helper

A Windows system tray app that fixes typos and transcribes speech using AI (not
yet implemented), triggered by global hotkeys.

## Why this exists

I built this for myself. I hate typing and I make a lot of mistakes doing it.
Going back to fix typos all the time is super annoying. This app sits in the
system tray and is available instantly via hotkeys in whatever application I
happen to be in.

## Features

The app has two main functions: fixing text you have already typed, and dictating new text by voice.

**Typo and grammar fixing.** Copy text to your clipboard, press the hotkey (Ctrl+Alt+Space by default), and the app sends it to an LLM that fixes typos and grammar mistakes while leaving your formatting alone, markdown included. The corrected text goes back to your clipboard and you hear a success sound. You can edit the system prompt if you want the corrections to work differently.

**Voice dictation.** Press a hotkey to start recording, speak, press it again to stop. The audio gets transcribed and either lands on your clipboard or gets inserted at the cursor, depending on how you have it configured. Language detection is automatic.

The app lives in the system tray. Right-click the icon to open settings or quit. There is no main window.

All feedback is through sounds and system tray notifications, so you always know
what happened without needing to look at anything. This matters because I use
the NVDA screen reader. Every control has a proper accessible name, tab order
covers everything, and nothing requires vision to operate.

## Current status

Typo fixing works. The system tray app, settings dialog, hotkeys, LLM integration, and audio feedback are all in place. Voice dictation is not implemented yet.

Currently there are no pre-built releases. You need to build it from source.

## Requirements

- Windows 10 or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download) for building
- An API key for an OpenAI-compatible LLM service (for typo fixing). The default config points to Cerebras, but any compatible endpoint will do.
- An ElevenLabs API key (for voice dictation, once that is implemented)

## Building from source

Clone the repository and build with the .NET CLI:

```bash
git clone https://github.com/ohylli/ai-writing-helper.git
cd ai-writing-helper
dotnet build
```

Run the app:

```bash
dotnet run --project src/AIWritingHelper
```

Publish a self-contained single-file executable:

```bash
dotnet publish src/AIWritingHelper -c Release --self-contained -p:PublishSingleFile=true
```

Run the tests:

```bash
# unit tests
dotnet test --filter "Category!=Integration"
# integration tests requires LLM API key.
dotnet test --filter "Category=Integration" 
```

Integration tests require a `.env` file in `tests/AIWritingHelper.Tests/` (copy `.env.example` and add your API key). Tests are skipped automatically if the key is missing.

## Configuration

Everything is configured through the Settings dialog, accessible from the system tray icon. Settings are stored as YAML in `%APPDATA%\AIWritingHelper\`.

The dialog has three tabs:

- **General** -- hotkey bindings, start with Windows, log level.
- **Typo Fixing** -- LLM endpoint URL, API key, model, and the system prompt that controls how corrections behave.
- **Dictation** -- speech-to-text API key and model, microphone selection, output mode (clipboard or direct insertion).

Both service tabs have a Test Connection button so you can verify your API setup before trying to use it.

## Tech stack

- C# / .NET 10 / WinForms
- Win32 `RegisterHotKey` for global hotkeys
- NAudio for microphone recording
- Serilog for structured logging
- YamlDotNet for settings persistence
- Microsoft.Extensions.DependencyInjection for DI

## License

MIT. See [LICENSE](LICENSE).
