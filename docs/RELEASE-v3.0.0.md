# Gasbyn Optimizer v3.0.0

## Co je nové
- **Quick Optimize** teď dělá bezpečný herní balíček (ne jen TEMP):
  - User TEMP cleanup
  - Windows Game Mode ON (s backupem)
  - Game DVR na pozadí OFF (s backupem)
  - Vizuální efekty → výkon (s backupem)
  - **Network Lite**: TCP heuristics OFF, timestamps OFF, RSS ON, autotuning normal, ECN OFF, Teredo OFF, flush DNS
- Verze aplikace **3.0.0** pro in-app updater

## Co záměrně NENÍ z CMD 1.0 / 2.0
Nepřenášíme hromadné vypínání služeb (Spooler, WSearch, Themes, Windows Update, Xbox…),
výjimky Windows Defender, agresivní NVIDIA preemption / latency registry masáže,
ani slepé mazání shader cache. To často rozbije tisk, hledání, update a bezpečnost.

## Inspirace (bez kopírování nebezpečných částí)
Gasbyn Optimizer CMD 1.0/2.0, GTweaks / EXM styl (vratné přepínače), Chris Titus Tool (TCP hygiena, ne nuking OS).

## Update
Stáhne se přes vestavěný updater z GitHub Releases (`GasbynOptimizer-win-x64.zip` + SHA-256).
