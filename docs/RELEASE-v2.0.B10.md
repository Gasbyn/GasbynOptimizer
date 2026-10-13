# Gasbyn Optimizer v2.0.B10

## Benchmark improvements

- Before starting a 60-second FPS benchmark, verify that the selected target process is running.
- If Fortnite is not running, open Fortnite through the Epic Games Launcher and wait up to 180 seconds for its process to appear.
- For other process names, attempt to launch the named executable and wait up to 30 seconds.
- Show progress while waiting, then begin capture only after the target process is detected.
- If automatic startup fails or times out, show a clear error instead of recording an empty benchmark.

## Compatibility and notes

- Existing manually selected PresentMon paths and automatic PresentMon installation are preserved.
- This launches the game/app when possible; it does not install applications or bypass their launchers.
