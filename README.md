# Gasbyn Optimizer

Optimalizace Windows 10/11 s přepínači **ZAPNUTO / VYPNUTO**. Každá změna jde vrátit zpět.

## Co umí

- **85 optimalizací** v deseti kategoriích (levé menu): soukromí a telemetrie, Windows Update, internet a síť, hraní a výkon, systém a napájení, vzhled a animace, písma, rozhraní, služby a úlohy, hardware a zařízení.
- **Kontrola počítače při startu:** Windows 10/11, procesor, grafika, základní deska, notebook/PC a typ disku. Podle toho se optimalizace označí jako podporované a doporučí se ty vhodné.
- **Pokročilý režim** (Nastavení): nepodporované optimalizace jsou jinak ztmavené se zámkem. Se zapnutým režimem jdou zapnout po potvrzení.
- **Filtry:** Vše, Zapnuto, Vypnuto, Doporučené, Nové, Experimentální, Nepodporované.
- **Přepínač u každé optimalizace.** Zapnutí provede změnu a upozorní na restart, vypnutí vrátí původní stav.
- **Záloha původních hodnot.** Před první změnou si aplikace uloží, jak věc vypadala (registr, typ spouštění služby, plán napájení, stav úlohy). Vypnutí obnoví právě to.
- **Čekací okno při startu.** Při každém spuštění se zjistí aktuální stav všech optimalizací.
- **Bod obnovení systému** před první změnou (jde vypnout v Nastavení) i ručně na tlačítko.
- **Zapnout doporučené** a **Vrátit vše** jedním tlačítkem, vyhledávání a filtry.
- **Plány napájení:** dva vlastní plány (Gasbyn PC a Gasbyn Notebook) se použijí tlačítkem, ne přepínačem. Aplikace při startu pozná, jestli jde o stolní PC nebo notebook, a doporučí ten správný. Původní plán ve Windows zůstane.
- **Údržba** (samostatná stránka se záložkami): úklid dočasných souborů a DNS, vyčištění mezipaměti aplikací, aktualizace aplikací (winget), odinstalace aplikací a čištění prohlížečů (označeno jako nedoporučené, protože smaže cookies a je potřeba se znovu přihlásit).
- Tmavý herní vzhled s logem.

Zdroje nápadů: ChrisTitusTool, Winhance, EXM, GTweaks. Zařazené jsou jen bezpečné a vratné úpravy.
Záměrně chybí věci, které snižují zabezpečení (vypnutí Defenderu, mitigací Spectre/Meltdown, izolace jádra).

## Co zatím chybí

- **FPS tester** (posledních 15 testů) přijde v dalším vydání.

## Automatické aktualizace a vydání

- Při každém spuštění aplikace zkontroluje GitHub Releases. Pokud je novější vydání s instalátorem, zobrazí okno
  „Aktualizuje se Gasbyn Optimizer“, stáhne a ověří instalátor, tiše ho nainstaluje a aplikaci znovu spustí.
  Bez internetu aplikace normálně nastartuje.
- Každý push do `main` (a větve `rewrite/v3-clean`) automaticky vydá nový release `v2.0.N` (první stabilní vydání je `v2.0.2`, tedy Gasbyn Optimizer 2.0; dřívější bety byly `v2.0.b1.N`)
  s instalátorem. Číselná verze 2.0.N.0 se použije v aplikaci i v instalátoru.

## Instalace

Instalátor `GasbynOptimizer-Setup-<verze>.exe` se staví automaticky v GitHub Actions
(záložka *Actions*, artefakt `GasbynOptimizer-Setup`, u tagů `v*` i v *Releases*).
Aplikace vyžaduje oprávnění správce. Změny se zapisují do registru **přihlášeného uživatele**,
takže ji spouštěj pod svým účtem (ne přes jiný administrátorský účet).

## Data aplikace

`%LocalAppData%\GasbynOptimizer`:

- `state.json` – záloha původních hodnot (kvůli vrácení zpět),
- `log.txt` – záznam změn a chyb,
- `settings.json` – nastavení.

## Vývoj

Vyžaduje .NET 10 SDK a Windows.

```
dotnet build
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Instalátor: nainstaluj Inno Setup 6 a spusť `iscc /DAppVersion=3.0.0 installer\GasbynOptimizer.iss`.

### Jak přidat optimalizaci

Do `Core/Catalog.cs` přidej položku `Tweak` s popisem a seznamem dílčích změn:

- `RegistryOp` – hodnota v registru (zapnutá hodnota, výchozí hodnota pro vrácení),
- `ServiceOp` – typ spouštění služby,
- `TaskOp` – naplánovaná úloha,
- `PowerPlanImportOp` – plán napájení ze souboru `.pow` v `Resources/PowerPlans`,
- `CommandOp` – obecný příkaz.

Zálohu a vrácení zpět zajišťuje `TweakService` automaticky.
