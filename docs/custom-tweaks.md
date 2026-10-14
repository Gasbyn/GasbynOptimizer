# Vlastní CMD tweaky

Gasbyn Optimizer načítá vlastní definice z:

`%LocalAppData%\GasbynOptimizer\custom-tweaks.json`

Při prvním spuštění se tento soubor vytvoří z přiložené šablony. Po změně JSON aplikaci restartuj. Každý objekt v poli `[]` představuje jednu kartu s přepínačem.

## Příklad

```json
[
  {
    "id": "dns-cache-example",
    "name": "Příklad: DNS cache",
    "description": "Ukázková definice vlastního síťového příkazu.",
    "category": "Internet",
    "onCommand": "ipconfig /flushdns",
    "offCommand": "ipconfig /displaydns",
    "onDescription": "Vyčistí místní DNS cache.",
    "offDescription": "Vypíše aktuální DNS cache. Neobnovuje předchozí cache.",
    "badges": ["INTERNET", "DELAY", "EXPERIMENTAL"]
  }
]
```

Šablona v aplikaci je záměrně prázdná (`[]`), takže se žádný příklad automaticky nespouští.

## Pole

- `id`: jedinečný identifikátor obsahující pouze písmena, čísla, `-` nebo `_`.
- `name`: název karty.
- `description`: krátký popisek pod názvem.
- `category`: kategorie zobrazená jako štítek, např. `Internet`, `Gaming`, `Windows` nebo `Tweaks`.
- `onCommand` / `offCommand`: příkazy, které se spustí při zapnutí a vypnutí.
- `onDescription` / `offDescription`: vysvětlení obou stavů v tooltipu po najetí myší.
- `badges`: volitelné další štítky, např. `EXPERIMENTAL`, `INCOMPATIBLE`, `FPS`, `PING`, `DELAY`, `INTERNET`, `RESTART` nebo `POZOR`.

Všechny povinné textové položky musí být vyplněné. Každé `id` musí být unikátní. Texty v JSON musí mít správně escapované uvozovky a zpětná lomítka.

## Důležité bezpečnostní informace

- Příkazy spouští `cmd.exe` pod účtem, pod kterým běží aplikace; administrátorská oprávnění získají pouze tehdy, když je aplikace spuštěna jako správce.
- Před zapnutím si vždy zkontroluj celý příkaz. Vlastní CMD příkaz může měnit systém nebo mazat data.
- Aplikace čeká maximálně 120 sekund. Nenulový návratový kód se zobrazí jako chyba.
- Přepínač si pamatuje, zda poslední příkaz ON úspěšně doběhl. Neumí obecně ověřit skutečný stav každého vlastního tweaku.
- Příkaz OFF **není automatické vrácení změn**. Musíš do něj napsat vlastní reset/undo příkaz. Některé příkazy jsou jednorázové akce a nejdou vrátit.
- Štítky jako FPS, PING nebo DELAY jsou jen informace o zamýšleném cíli. Nezaručují zlepšení výkonu ani sítě.
