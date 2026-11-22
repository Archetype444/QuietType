Private, offline contextual autocorrect for Windows 11 x64.

**Install:** download **QuietType-1.0.0-Setup-x64.exe**, run it, then close the first-run Settings window. QuietType stays in the system tray (the hidden-icons popup beside the clock). The installer includes the runtime, English dictionary and quantized DistilBERT model. No account or additional downloads required.

- Fast spelling correction plus a CPU-only local neural model for ambiguous words.
- Two inference threads, below-normal process priority, efficiency mode and idle model unloading.
- Pause/resume with **Ctrl+Alt+P**; undo a recent correction with **Ctrl+Alt+Z** before changing the text or cursor.
- Tray settings for startup, delay, protected words and app exclusions.
- Password fields, selected/read-only text, browser address bars and code-like tokens are skipped.
- No cloud calls, telemetry, clipboard access, typing history or automatic update checks.
- MIT application code; Apache-2.0 model; dependency notices included.

**Scope:** English, supported accessible Windows text fields. Corrections happen after a completed word and a short pause. Some custom editors, elevated apps, games and browser fields are unsupported and skipped. Real-word substitutions are experimental and off by default. This is conservative autocorrect, not a guarantee of perfect spelling or universal app coverage.

**Windows notice:** this build is unsigned and may trigger an unknown-publisher/SmartScreen notice. No signing certificate is included. SHA-256 checksums and automated Windows editing, password-field, installer and uninstaller test reports are attached.

The portable ZIP is an alternative. Extract the whole folder and run QuietType.exe. Keep its assets and DLLs together. Disable automatic startup in Settings before moving or deleting a portable installation.
