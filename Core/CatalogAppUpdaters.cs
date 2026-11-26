namespace GasbynOptimizer.Core;

/// <summary>Aktualizace aplikací jen ručně, vysoká priorita her a streamovacích programů, Windows Update jen ručně.</summary>
internal static class CatalogAppUpdaters
{
    private const string Ifeo = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";

    public static IReadOnlyList<Tweak> Create()
    {
        return new List<Tweak>
        {
            new()
            {
                Id = "upd-chrome", Category = Catalog.CatUpdate, IsNew = true, AutoReapply = true, RequiresRestart = false,
                Name = "Chrome / Google Update: vypnout aktualizace na pozadí",
                Description = "Zakáže službu Google Update (včetně verzovaných služeb GoogleUpdaterService a GoogleUpdaterInternalService s číslem verze) a její úlohy. Chrome se pak aktualizuje jen ručně přes Nastavení → O prohlížeči Chrome.",
                Note = "Optimalizace hlídá i nové služby, které Google po každé aktualizaci vytvoří. Aplikace je při spuštění znovu vypne. Zabezpečení: aktualizuj Chrome pravidelně ručně.",
                InstalledCheck = () => Directory.Exists(Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Google\Chrome")) || Directory.Exists(Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Google\Chrome")),
                Ops = new TweakOp[]
                {
                    new ServicePatternOp("gupdate"),
                    new ServicePatternOp("GoogleUpdater"),
                    new TaskPatternOp("", "GoogleUpdateTaskMachine"),
                    new TaskPatternOp("", "GoogleUpdaterTaskSystem"),
                    Catalog.Hklm(@"SOFTWARE\Policies\Google\Update", "AutoUpdateCheckPeriodMinutes", 0, null)
                }
            },
            new()
            {
                Id = "upd-brave", Category = Catalog.CatUpdate, IsNew = true, AutoReapply = true, RequiresRestart = false,
                Name = "Brave: vypnout aktualizace na pozadí",
                Description = "Zakáže služby Brave Update (brave, bravem, verzované služby) a jeho naplánované úlohy. Brave se aktualizuje ručně přes O prohlížeči Brave.",
                Note = "Aplikace nové služby po aktualizacích znovu vypne při spuštění.",
                InstalledCheck = () => Directory.Exists(Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\BraveSoftware")),
                Ops = new TweakOp[]
                {
                    new ServicePatternOp("brave"),
                    new ServicePatternOp("BraveElevationService"),
                    new TaskPatternOp("", "BraveSoftwareUpdateTask")
                }
            },
            new()
            {
                Id = "upd-firefox", Category = Catalog.CatUpdate, IsNew = true, AutoReapply = true, RequiresRestart = false,
                Name = "Firefox: vypnout aktualizace na pozadí",
                Description = "Zakáže službu Mozilla Maintenance a úlohu Firefox Background Update. Firefox se aktualizuje jen ručně nebo při otevření.",
                Note = "Aktualizace pak provedeš v Nastavení → Obecné → Aktualizace Firefoxu.",
                InstalledCheck = () => Directory.Exists(Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Mozilla Firefox")),
                Ops = new TweakOp[]
                {
                    new ServicePatternOp("MozillaMaintenance"),
                    new TaskPatternOp("Mozilla", "Firefox Background Update")
                }
            },
            new()
            {
                Id = "upd-opera", Category = Catalog.CatUpdate, IsNew = true, AutoReapply = true, RequiresRestart = false,
                Name = "Opera: vypnout aktualizace na pozadí",
                Description = "Zakáže naplánované úlohy Opera scheduled Autoupdate, které běží na pozadí.",
                Note = "Opera se aktualizuje při spuštění nebo ručně přes O programu Opera.",
                InstalledCheck = () => Directory.Exists(Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Programs\Opera")) || Directory.Exists(Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Programs\Opera GX")),
                Ops = new TweakOp[]
                {
                    new TaskPatternOp("", "Opera scheduled Autoupdate"),
                    new TaskPatternOp("", "Opera GX scheduled Autoupdate")
                }
            },
            new()
            {
                Id = "upd-adobe", Category = Catalog.CatUpdate, IsNew = true, AutoReapply = true, RequiresRestart = false,
                Name = "Adobe (Acrobat, Creative Cloud): vypnout aktualizace na pozadí",
                Description = "Zakáže služby Adobe Acrobat Update a Adobe Genuine Software a úlohu Adobe Acrobat Update Task.",
                Note = "Adobe programy se pak aktualizují ručně (Nápověda → Zkontrolovat aktualizace).",
                InstalledCheck = () => Directory.Exists(Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Common Files\Adobe")) || Directory.Exists(Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Adobe")),
                Ops = new TweakOp[]
                {
                    new ServicePatternOp("AdobeARMservice"),
                    new ServicePatternOp("AGSService"),
                    new ServicePatternOp("AGMService"),
                    new TaskPatternOp("", "Adobe Acrobat Update Task")
                }
            },
            new()
            {
                Id = "upd-dropbox", Category = Catalog.CatUpdate, IsNew = true, AutoReapply = true, RequiresRestart = false,
                Name = "Dropbox: vypnout aktualizace na pozadí",
                Description = "Zakáže služby Dropbox Update (dbupdate, dbupdatem) a úlohy DropboxUpdateTask. Dropbox se aktualizuje při spuštění.",
                Note = "Synchronizace funguje dál, jen se přestane samo aktualizovat na pozadí.",
                InstalledCheck = () => Directory.Exists(Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Dropbox")) || Directory.Exists(Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Dropbox")),
                Ops = new TweakOp[]
                {
                    new ServicePatternOp("dbupdate"),
                    new TaskPatternOp("", "DropboxUpdateTaskMachine")
                }
            },
            new()
            {
                Id = "wu-manual-only", Category = Catalog.CatUpdate, IsNew = true, RequiresRestart = false,
                Name = "Windows Update jen ručně",
                Description = "Windows se nebude aktualizovat automaticky. Aktualizace se stáhnou a nainstalují, až si je sám vyžádáš v Nastavení → Windows Update → Vyhledat aktualizace.",
                Note = "Nezapomeň aktualizovat ručně alespoň jednou za měsíc kvůli bezpečnostním záplatám.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 1, null)
                }
            },
            new()
            {
                Id = "game-priority-apps", Category = Catalog.CatGaming, IsNew = true, Experimental = true, RequiresRestart = false,
                Name = "Vysoká priorita pro známé hry a streamovací programy",
                Description = "Nastaví vysokou prioritu procesoru těmto programům: OBS Studio (obs64, obs32), Streamlabs (Desktop i OBS), Fortnite, Roblox, Minecraft (Java i Bedrock), Counter-Strike 2 a Valorant. Ostatní programy se nemění.",
                Note = "Experimentální: u Minecraftu Java se priorita nastaví všem programům javaw.exe. Anti-cheaty (Vanguard, Easy Anti-Cheat) by to neměly blokovat, ale je to zásah do nastavení spouštění, takže při problémech vypni. Projeví se po dalším spuštění programu.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(Ifeo + @"\obs64.exe\PerfOptions", "CpuPriorityClass", 3, null),
                    Catalog.Hklm(Ifeo + @"\obs32.exe\PerfOptions", "CpuPriorityClass", 3, null),
                    Catalog.Hklm(Ifeo + @"\Streamlabs Desktop.exe\PerfOptions", "CpuPriorityClass", 3, null),
                    Catalog.Hklm(Ifeo + @"\Streamlabs OBS.exe\PerfOptions", "CpuPriorityClass", 3, null),
                    Catalog.Hklm(Ifeo + @"\FortniteClient-Win64-Shipping.exe\PerfOptions", "CpuPriorityClass", 3, null),
                    Catalog.Hklm(Ifeo + @"\RobloxPlayerBeta.exe\PerfOptions", "CpuPriorityClass", 3, null),
                    Catalog.Hklm(Ifeo + @"\Minecraft.Windows.exe\PerfOptions", "CpuPriorityClass", 3, null),
                    Catalog.Hklm(Ifeo + @"\javaw.exe\PerfOptions", "CpuPriorityClass", 3, null),
                    Catalog.Hklm(Ifeo + @"\cs2.exe\PerfOptions", "CpuPriorityClass", 3, null),
                    Catalog.Hklm(Ifeo + @"\VALORANT-Win64-Shipping.exe\PerfOptions", "CpuPriorityClass", 3, null)
                }
            }
        };
    }
}
