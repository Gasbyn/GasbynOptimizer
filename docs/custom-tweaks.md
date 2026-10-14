# Vlastní optimalizace v Gasbyn Optimizeru

Na stránce **Tweaks** klikni na **PŘIDAT VLASTNÍ OPTIMALIZACI**. Nemusíš upravovat zdrojový kód. Definice se ukládají do:

%LocalAppData%\GasbynOptimizer\custom-tweaks.json

Každá optimalizace má příkaz pro ON, příkaz pro OFF a dva samostatné kontrolní příkazy, které ověřují skutečný stav Windows. Stav přepínače se neurčuje podle historie kliknutí ani podle souboru JSON. Kontroly se spouštějí při startu aplikace a při načítání stránky Tweaks.

## Jak fungují kontrolní příkazy

- **Kontrola stavu ON** musí skončit s návratovým kódem 0, pokud je optimalizace v systému opravdu aktivní.
- **Kontrola stavu OFF** musí skončit s návratovým kódem 0, pokud je optimalizace v systému opravdu vypnutá.
- Když se ON i OFF kontrola shodují (obě uspějí nebo obě selžou), stav bude UNKNOWN a přepínač zůstane v neurčeném stavu, dokud kontrolu neopravíš. Aplikace nebude hádat.
- Kontrolní příkazy mají limit 15 sekund. Příkazy ON/OFF mají limit 120 sekund.
- U starších definic bez kontrolních příkazů se stav zobrazí jako UNKNOWN; doplň isOnCommand a isOffCommand.

## JSON příklad

~~~json
[
  {
    "id": "example-registry-toggle",
    "name": "Příklad: testovací hodnota registru",
    "description": "Ukázka, jak ověřit skutečnou hodnotu registru.",
    "category": "Windows",
    "onCommand": "reg add \"HKCU\\Software\\GasbynExample\" /v Enabled /t REG_DWORD /d 1 /f",
    "offCommand": "reg add \"HKCU\\Software\\GasbynExample\" /v Enabled /t REG_DWORD /d 0 /f",
    "isOnCommand": "reg query \"HKCU\\Software\\GasbynExample\" /v Enabled | findstr /i \"0x1\" >nul",
    "isOffCommand": "reg query \"HKCU\\Software\\GasbynExample\" /v Enabled | findstr /i \"0x0\" >nul",
    "onDescription": "Nastaví testovací hodnotu Enabled na 1.",
    "offDescription": "Nastaví testovací hodnotu Enabled na 0.",
    "badges": ["WINDOWS", "EXPERIMENTAL"]
  }
]
~~~

Tento příklad je pouze ukázka schématu, nikoliv doporučená optimalizace. Kontrolní příkazy musí být přesně přizpůsobené změně, kterou tvůj ON/OFF příkaz skutečně provádí.

## Pole

- id: jedinečný identifikátor; pouze písmena, čísla, - nebo _.
- name, description: název karty a krátký popis.
- category: štítek kategorie, například Internet, Gaming, Windows nebo Tweaks.
- onCommand / offCommand: změna, která se provede při přepnutí.
- isOnCommand / isOffCommand: kontrola skutečného stavu v systému; návratový kód 0 znamená, že daná podmínka platí.
- onDescription / offDescription: vysvětlení účinku obou stavů v tooltipu.
- badges: volitelné štítky oddělené čárkou v editoru, například FPS, PING, DELAY, INTERNET, RESTART, EXPERIMENTAL nebo INCOMPATIBLE.

## Bezpečnost

- Příkazy se spouštějí přes cmd.exe pod aktuálním Windows účtem. Pokud je aplikace spuštěná jako správce, příkazy mohou mít administrátorská oprávnění.
- Před uložením si zkontroluj všechny čtyři příkazy. Vlastní CMD příkaz může změnit systém nebo poškodit/smazat data.
- OFF není automatická záloha. Musíš napsat vlastní příkaz, který změnu vrátí. Jednorázové příkazy, například vyčištění cache, nemusí mít smysluplný protikrok.
- Štítky jako FPS, PING nebo DELAY neznamenají zaručené zlepšení výkonu či sítě.
