# QuietType

**Private, offline autocorrect for Windows 11.** A quiet system tray app that combines a fast English spelling engine with a small local neural model. No account. No cloud. No typing history.

## Install

Download **[QuietType-1.0.0-Setup-x64.exe](https://github.com/Archetype444/QuietType/releases/latest)** from Releases and run it. Everything is included: app, runtime, dictionary and model. No administrator rights or separate downloads are needed.

Settings opens on first run. Close it and find the green **Q** in the notification area beside the clock, or inside the **^** hidden-icons popup. Double-click Q for Settings; right-click for Pause, Undo or Quit. QuietType starts quietly on your next Windows sign-in unless you turn that off.

This build is unsigned. Windows may show an unknown-publisher or SmartScreen notice. Checksums and source are available with the release. There is no signing certificate or claim of Microsoft certification.

## Daily use

Type normally in a supported editable text field. After completing a word with a space or punctuation, a short pause lets QuietType correct clear typos. It uses nearby text to resolve ambiguous spellings and leaves uncertain words alone. It preserves capitalization and trailing punctuation.

| Control | Action |
|---|---|
| **Ctrl+Alt+P** | Pause or resume |
| **Ctrl+Alt+Z** | Undo the last correction within 20 seconds, while text/cursor remain unchanged |
| **Right-click tray icon** | Pause, Undo, Settings, Quit |
| **Double-click tray icon** | Open Settings |
| **Personal words** | Protect words you explicitly add |
| **App exclusions** | Skip selected executable names |

The built-in typing test in Settings is an easy way to try `teh ` and other typos. Click **Save settings** after changing options. Closing Settings keeps the app running; choose **Quit** to stop it.

## Keeps other apps responsive

- Quantized **DistilBERT**, approximately **68 MB** of model weights, not a multi-gigabyte generative chatbot.
- **Two CPU threads** for neural inference, sequential execution, no busy-spinning thread pools, no GPU or NPU use.
- Below-normal process and worker priority; Windows execution-speed efficiency mode.
- Obvious spelling mistakes use the dictionary and do not run the neural model.
- A maximum of one new AI check per 1.2 seconds; no backlog of checks while you type.
- Bounded nearby context: up to 384 characters before and 128 after the caret, at most 95 model tokens.
- Model unloads after three minutes without AI use, and immediately when pausing or disabling AI.

32 GB of RAM is ample for this design. Exact memory use and inference latency depend on the machine and app. Tests report timing and process memory; these are measurements on the test machine, not promises about your PC.

## Privacy and safeguards

The installed app has no network client, telemetry service, advertising, update checker, account or cloud fallback. Model and dictionary downloads happen **at build time**, not during use. ONNX Runtime telemetry is explicitly disabled before session creation.

The keyboard hook records key codes only to detect word boundaries, shortcuts and stale work. It does not reconstruct or save your keystrokes. Bounded text context is read through Windows accessibility only for a supported editable control. Context and a short-lived undo record are held in memory. No clipboard access, screenshots, correction history or text logging.

Password fields, read-only/selected text, browser address bars, URLs, paths and code-like tokens are skipped. Common password managers, terminals, developer tools and remote-desktop apps are excluded by default. You can extend the list. Settings and explicitly entered personal words are the only app data saved, under `%LOCALAPPDATA%\QuietType`.

Before editing, QuietType rechecks focus, process, field identity, context, caret position and input revision. Pending work is discarded when these change. It never sends Enter or Tab and never requests administrator privileges. Windows and other apps can independently handle text you type into them; QuietType cannot change their privacy policies.

## Honest scope of version 1.0

- **English**, Windows 11 x64, compatible text fields exposing editable UI Automation TextPattern.
- Some custom editors, games, elevated windows and browser controls are unavailable and are skipped. This is not universal text-service integration.
- Correction is conservative and happens on a completed word after a pause. Continuous fast typing can move past a word before it can be safely corrected. It does not rewrite earlier paragraphs or retain everything across apps.
- Names with unknown capitalized spellings, mixed-case identifiers and protected personal words are preserved.
- Real-word substitutions, such as `form`/`from`, are **experimental and off by default**. The model is not a guarantee of grammatical accuracy.
- Undo is deliberately limited to unchanged text/cursor in the same field. It will not modify a document whose state no longer matches.
- Applications with their own autocorrect may interact with this one. Exclude those apps if needed.

## Build and verify

Requires the .NET 10 SDK, Python 3.11+ for asset retrieval, and Windows 11 x64 for the native integration tests and installer. Development and neural engine tests also run on Linux.

```powershell
python scripts/download-assets.py
dotnet run --project tests/QuietType.Tests -c Release -- .
./scripts/build.ps1
./artifacts/app/QuietType.exe --self-test artifacts/windows-self-test.json
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer/QuietType.iss
```

All model/data downloads are pinned by immutable revision and SHA-256. NuGet package versions and lockfiles are checked in. CI actions are pinned to commit SHAs. The release workflow gates publication on Linux neural tests, Windows text editing/undo/password/exclusion tests, and installation/uninstallation of the actual packaged app. `workflow_dispatch` with **publish** checked publishes `v1.0.0` only after those checks pass.

The source is original MIT-licensed code, with free/open-source runtime and model dependencies; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). No device names, device IDs or product IDs belong in bug reports or this repository.

## Uninstall

Quit QuietType from the tray, then use **Windows Settings → Apps → Installed apps → QuietType → Uninstall**. The startup entry is removed. Preferences are retained for reinstalling; delete `%LOCALAPPDATA%\QuietType` after uninstalling if you want to remove them too.

For a portable installation, extract the whole ZIP. Turn off startup in Settings before moving or deleting its folder. Do not run installer and portable copies at the same time.
