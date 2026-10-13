# Gasbyn Optimizer v2.0.B7

## What's new
- FPS Benchmark can automatically download and prepare the official PresentMon x64 console executable.
- PresentMon is downloaded from the official GameTechDev/PresentMon GitHub release and checked against GitHub's published SHA-256 digest before installation.
- The managed copy is stored under `%LocalAppData%\GasbynOptimizer\Tools\PresentMon`.
- Benchmark runs automatically check for and install the latest stable PresentMon version when using the managed copy.
- Manual executable selection remains available as an override.

## How to use
Open **Benchmark** in Gasbyn Optimizer and click **Automaticky stáhnout / aktualizovat PresentMon**, or simply start the 60-second FPS test. If PresentMon is missing, the benchmark prepares it automatically.
