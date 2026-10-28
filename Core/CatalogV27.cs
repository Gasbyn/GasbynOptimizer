namespace GasbynOptimizer.Core;

/// <summary>Optimalizace přidané ve verzi 2.0.0.27: tisk, mapy, dotyk a pero a odinstalace známých aplikací.</summary>
internal static class CatalogV27
{
    public static IReadOnlyList<Tweak> Create()
    {
        var list = new List<Tweak>
        {
            new()
            {
                Id = "printing-features", Category = CatalogHardware.CatHardware,
                Name = "Vypnout některé funkce tiskáren (tisk přes web, stahování ovladačů)",
                Description = "Vypne tisk přes internet (HTTP), automatické stahování ovladačů tiskáren z webu, vzdálený přístup ke sdílené tiskové frontě a instalaci ovladačů bez práv správce. Tisk na lokální tiskárnu dál funguje.",
                Note = "Zavírá známé cesty útoků na tisk a šetří provoz na pozadí. Úplné vypnutí tisku je v Pokročilých.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows NT\Printers", "DisableHTTPPrinting", 1, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows NT\Printers", "DisableWebPnPDownload", 1, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows NT\Printers", "RegisterSpoolerRemoteRpcEndPoint", 2, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows NT\Printers\PointAndPrint", "RestrictDriverInstallationToAdministrators", 1, null),
                    Catalog.Hkcu(@"Software\Microsoft\Windows NT\CurrentVersion\Windows", "LegacyDefaultPrinterMode", 1, null)
                }
            },
            new()
            {
                Id = "offline-maps", Category = Catalog.CatPrivacy,
                Name = "Vypnout offline mapy (automatické stahování a aktualizace map)",
                Description = "Windows nebude na pozadí stahovat a aktualizovat offline mapy a nebude kvůli nim komunikovat přes síť.",
                Note = "Už stažené mapy zůstanou, jen se neaktualizují. Aplikace Mapy lze odinstalovat v kategorii Debloat.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Maps", "AutoDownloadAndUpdateMapData", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Maps", "AllowUntriggeredNetworkTrafficOnSettingsPage", 0, null)
                }
            },
            new()
            {
                Id = "touch-off", Category = CatalogHardware.CatHardware, Experimental = true,
                Name = "Vypnout dotykové funkce (dotyková klávesnice, rukopis)",
                Description = "Zakáže službu Dotykové klávesnice a panelu rukopisu a dotykové gesto. Na počítači bez dotykové obrazovky ušetří paměť a procesor.",
                Note = "Nepoužívej na dotykovém zařízení nebo tabletu. Vypnutí vrátí původní stav.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("TabletInputService", "demand"),
                    Catalog.Hkcu(@"Software\Microsoft\Wisp\Touch", "TouchGate", 0, null)
                }
            },
            new()
            {
                Id = "pen-ink-off", Category = CatalogHardware.CatHardware, Experimental = true,
                Name = "Vypnout pero a kreslení (Windows Ink)",
                Description = "Vypne Windows Ink Workspace (kreslení perem, náčrty na obrazovce) a návrhy aplikací pro pero.",
                Note = "Vhodné, pokud pero ani kreslicí tablet nepoužíváš.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\WindowsInkWorkspace", "AllowWindowsInkWorkspace", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\WindowsInkWorkspace", "AllowSuggestedAppsInWindowsInkWorkspace", 0, null),
                    Catalog.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\PenWorkspace", "PenWorkspaceAppSuggestionsEnabled", 0, null)
                }
            }
        };

        void App(string id, string name, string packages, string desc, bool recommended) => list.Add(new Tweak
        {
            Id = "app-" + id, Category = CatalogDebloat.CatDebloat, ApplyOnly = true, Recommended = recommended,
            DoneText = "Odinstalováno", TodoText = "Nainstalováno",
            Name = name + (recommended ? ": doporučeno odinstalovat" : ": odinstalovat"),
            Description = desc + " Odinstaluje aplikaci pro všechny uživatele.",
            Note = "Aplikaci lze kdykoli znovu nainstalovat z Microsoft Store.",
            Confirm = "Odinstalovat aplikaci „" + name + "“?\n\nZpět půjde nainstalovat jen z Microsoft Store.",
            Ops = new TweakOp[] { new StoreAppOp(packages.Split('|')) }
        });

        App("copilot", "Copilot", "Microsoft.Copilot|Microsoft.Windows.Ai.Copilot.Provider", "Asistent Copilot s umělou inteligencí; většině hráčů jen zabírá místo a paměť.", true);
        App("clock", "Hodiny (budík, časovače)", "Microsoft.WindowsAlarms", "Aplikace Hodiny, budíky a časovače.", true);
        App("camera", "Fotoaparát", "Microsoft.WindowsCamera", "Aplikace Fotoaparát. Webkamera v jiných programech (Discord, OBS) dál funguje.", true);
        App("clipchamp", "Clipchamp (video editor)", "Clipchamp.Clipchamp", "Předinstalovaný video editor.", true);
        App("news", "Zprávy (Bing News)", "Microsoft.BingNews", "Aplikace Zprávy.", true);
        App("weather", "Počasí", "Microsoft.BingWeather", "Aplikace Počasí.", true);
        App("solitaire", "Microsoft Solitaire Collection", "Microsoft.MicrosoftSolitaireCollection", "Karetní hry s reklamami.", true);
        App("phonelink", "Phone Link (Váš telefon)", "Microsoft.YourPhone", "Propojení telefonu s Windows.", true);
        App("gethelp", "Získat pomoc", "Microsoft.GetHelp", "Aplikace Získat pomoc.", true);
        App("tips", "Tipy", "Microsoft.Getstarted", "Aplikace Tipy.", true);
        App("feedback", "Centrum zpětné vazby", "Microsoft.WindowsFeedbackHub", "Odesílání zpětné vazby Microsoftu.", true);
        App("3dviewer", "3D Viewer a Mixed Reality", "Microsoft.Microsoft3DViewer|Microsoft.MixedReality.Portal", "Prohlížeč 3D modelů a portál smíšené reality.", true);
        App("paint3d", "Paint 3D", "Microsoft.MSPaint", "Paint 3D (klasický Malování zůstává).", true);
        App("skype", "Skype", "Microsoft.SkypeApp", "Aplikace Skype.", true);
        App("maps", "Mapy", "Microsoft.WindowsMaps", "Aplikace Mapy.", true);
        App("officehub", "Microsoft 365 (Office hub)", "Microsoft.MicrosoftOfficeHub", "Reklamní odkaz na Microsoft 365.", true);
        App("people", "Lidé", "Microsoft.People", "Aplikace Lidé.", true);
        App("movies", "Filmy a TV", "Microsoft.ZuneVideo", "Přehrávač Filmy a TV.", true);
        App("groove", "Groove hudba / Media Player", "Microsoft.ZuneMusic", "Přehrávač hudby Groove.", true);
        App("cortana", "Cortana", "Microsoft.549981C3F5F10", "Hlasová asistentka Cortana.", true);
        App("powerautomate", "Power Automate", "Microsoft.PowerAutomateDesktop", "Automatizace úloh Power Automate.", true);
        App("teams", "Microsoft Teams (osobní)", "MicrosoftTeams|MSTeams", "Předinstalované Teams pro osobní použití.", true);
        App("devhome", "Dev Home", "Microsoft.Windows.DevHome", "Nástroj pro vývojáře Dev Home.", true);
        App("stickynotes", "Sticky Notes (Poznámky)", "Microsoft.MicrosoftStickyNotes", "Lepící poznámky. Pokud je používáš, nech je.", false);
        App("todo", "Microsoft To Do", "Microsoft.Todos", "Seznam úkolů. Pokud ho používáš, nech ho.", false);
        App("outlookapp", "Outlook (nový)", "Microsoft.OutlookForWindows", "Nový poštovní klient. Pokud ho používáš, nech ho.", false);

        return list;
    }
}
