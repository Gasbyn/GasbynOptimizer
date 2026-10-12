# Testování Full Optimizeru

## Bezpečný lokální test
1. Otevři GitHub Desktop a přihlas se ke stejnému GitHub účtu, který má přístup k soukromému repozitáři.
2. Klonuj `Gasbyn/GasbynOptimizer`, pokud ho ještě nemáš lokálně.
3. V GitHub Desktop vyber branch `feature/full-optimizer-v1` (ne `main`).
4. Otevři složku projektu ve Visual Studio 2022 s podporou .NET desktop development, nebo ve VS Code s .NET SDK 10.
5. Spusť build před spuštěním aplikace: `dotnet build GasbynOptimizer.csproj -c Debug`.
6. Spusť aplikaci z Visual Studia (F5) nebo příkazem `dotnet run --project GasbynOptimizer.csproj`.
7. Před prvním testem si vytvoř Windows Restore Point v aplikaci nebo v nastavení Ochrany systému. Každý modul měň zvlášť a ověř jeho návrat přes tlačítko pro obnovení původního stavu.

## Jak testovat změny průběžně přes Git
- Po změně kódu ve Visual Studio/VS Code ulož soubory, spusť build a aplikaci znovu.
- Pokud se změny dělají na GitHubu přes web/ChatGPT, v GitHub Desktop klikni na **Fetch origin** a potom **Pull origin** na stejné feature branch.
- Nepřepínej na `main`, dokud není pull request zkontrolovaný a build zelený.
- Git sám o sobě aplikaci do počítače „live“ nepřenáší ani ji automaticky nespouští. Pro skutečný hot reload musí být aplikace spuštěná pod debuggerem a změny musí podporovat .NET Hot Reload; systémové tweak operace je i tak potřeba testovat odděleně.

## FPS testy
- FPS tester potřebuje oficiální PresentMon executable vybraný ručně v aplikaci.
- Nech Fortnite nebo jiný podporovaný proces běžet a testuj ve stejné scéně, stejném rozlišení, stejném FPS limitu a stejném stavu OBS.
- Historie drží nejvýše 15 posledních výsledků v lokálním profilu uživatele. Výsledek benchmarku není záruka, že konkrétní tweak FPS zvýšil.

## Záměrně vynechané zásahy
Neprovádíme hromadné vypínání služeb, Defenderu, Windows Update, IPv6 ani bootovacích časovačů. Tyto zásahy se liší podle zařízení a jejich přínos pro FPS/ping není univerzální; bezpečnostní a síťové funkce mohou přestat fungovat.
