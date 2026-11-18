# Antivirus a falešné poplachy

Gasbyn Optimizer mění nastavení Windows (registr, služby, plány napájení), a proto na něj některé antiviry chybně upozorní.
Aplikace je open source, neskrývá se a nic neodesílá.

## Co aplikace dělá proti falešným poplachům
- PowerShellu předává skripty čitelně přes standardní vstup, bez `-EncodedCommand` a bez `-ExecutionPolicy Bypass`.
- EXE a instalátor mají metadata vydavatele (Gasbyn, Gasbyn Optimizer).
- Ke každému vydání se přikládá `SHA256SUMS.txt`. Zkontroluj instalátor příkazem
  `Get-FileHash .\GasbynOptimizer-Setup-*.exe -Algorithm SHA256` a porovnej s hodnotou v souboru.

## Co nejde zařídit zdarma a bez účtu u třetí strany
Jediné spolehlivé řešení je **podpis kódu** (reputace vydavatele u SmartScreenu a antivirů). Zdarma ho nabízí
SignPath Foundation pro open source projekty, viz `docs/code-signing.md`. Žádost musí podat vlastník repozitáře.

## Když antivirus soubor zablokuje
1. Přidej složku aplikace do výjimek antiviru.
2. Nahlas soubor jako falešný poplach u výrobce antiviru (u AVG/Avast: https://www.avast.com/false-positive-file-form.php).
3. Napiš na Discord (hlášení problémů).
