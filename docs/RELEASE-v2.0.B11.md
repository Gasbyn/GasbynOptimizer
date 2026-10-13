# Gasbyn Optimizer v2.0.B11

## Distribution

- Enable self-contained, single-file Windows publishing.
- Publish a direct `GasbynOptimizer.exe` download so users do not need to extract a whole app folder.
- Keep `GasbynOptimizer-win-x64.zip` as a one-file ZIP asset for the built-in updater, which verifies the official GitHub SHA-256 digest before applying an update.
- Native runtime libraries and bundled content are configured to be embedded in the EXE and extracted to the .NET temporary bundle directory at launch when required.

## Notes

- Existing settings remain in the user's LocalAppData profile.
- GitHub Actions validates that the single-file publish produces `GasbynOptimizer.exe` before creating release assets.
