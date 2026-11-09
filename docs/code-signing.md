# Podpis kódu (SignPath Foundation)

Podepsaný EXE a instalátor výrazně snižují falešné poplachy a varování SmartScreen. SignPath Foundation podepisuje
open source projekty zdarma. Žádost podává vlastník repozitáře (Gasbyn), protože potřebuje ověření identity.

## Kroky
1. Otevři https://signpath.org/ a zvol „Apply for free code signing for open source“.
2. Vyplň formulář podle textu níže.
3. Po schválení přidej do workflow krok SignPath (`signpath/github-action-submit-signing-request`) pro EXE i instalátor.

## Text žádosti
- Projekt: Gasbyn Optimizer, https://github.com/Gasbyn/GasbynOptimizer
- Popis: Open source nástroj pro Windows, který mění nastavení systému (registr, služby, plány napájení) pro vyšší výkon a soukromí. Každou změnu lze vrátit zpět.
- Licence a zdrojový kód: veřejný repozitář na GitHubu, sestavení probíhá v GitHub Actions.
- Důvod podpisu: antiviry (např. AVG) chybně označují optimalizační nástroj jako hrozbu.
