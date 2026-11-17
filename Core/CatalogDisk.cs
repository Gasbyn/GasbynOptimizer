namespace GasbynOptimizer.Core;

/// <summary>
/// Optimalizace disku podle typu systémového disku (HDD, SSD, NVMe). Optimalizace pro jiný typ disku, než který počítač má,
/// jsou nepodporované a jdou použít jen v pokročilém režimu. Doporučené jsou jen tam, kde to dává smysl.
/// </summary>
internal static class CatalogDisk
{
    internal const string CatDisk = "Disk";

    private const string Fs = @"SYSTEM\CurrentControlSet\Control\FileSystem";
    private const string Overrides = @"SYSTEM\CurrentControlSet\Policies\Microsoft\FeatureManagement\Overrides";
    private const string PowerPolicy = @"SOFTWARE\Policies\Microsoft\Power\PowerSettings\";

    public static IReadOnlyList<Tweak> Create()
    {
        return new List<Tweak>
        {
            new()
            {
                Id = "disk-ntfs-lastaccess", Category = CatDisk, IsNew = true, Recommended = true, RequiresRestart = true,
                Name = "Disk: nezapisovat čas posledního přístupu k souborům",
                Description = "NTFS přestane při každém čtení souboru zapisovat čas posledního přístupu. Méně zápisů na disk, rychlejší práce se spoustou souborů.",
                Note = "Programy, které čas posledního přístupu používají (některé zálohovací nástroje), ho už nedostanou. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    // 0x80000001 = vypnuto (systémem spravované), 0x80000002 = výchozí hodnota Windows
                    Catalog.Hklm(Fs, "NtfsDisableLastAccessUpdate", 0x80000001u, 0x80000002u)
                }
            },
            new()
            {
                Id = "disk-hdd-no-spindown", Category = CatDisk, IsNew = true, Requires = Needs.Hdd,
                RecommendWhen = p => p.SystemDriveIsSsd == false && p.IsLaptop != true,
                RequiresRestart = false,
                Name = "HDD: nevypínat pevný disk při nečinnosti",
                Description = "Pevný disk se na síťovém napájení nezastaví po několika minutách nečinnosti. Odpadne zpomalení a zadrhnutí, když se po pauze znovu roztáčí.",
                Note = "Určeno pro pevný disk (HDD) ve stolním PC. Disk víc hučí a trochu zvýší spotřebu, na baterii se nic nemění.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(PowerPolicy + @"0012ee47-9041-4b5d-9b77-535fba8b1442\6738e2c4-e8a5-4a42-b16a-e040e769756e", "ACSettingIndex", 0, null)
                }
            },
            new()
            {
                Id = "disk-hdd-ntfs-memory", Category = CatDisk, IsNew = true, Requires = Needs.Hdd, RequiresRestart = true,
                RecommendWhen = p => p.SystemDriveIsSsd == false && p.RamGb >= 16,
                Name = "HDD: větší paměť pro souborový systém NTFS",
                Description = "NTFS dostane víc paměti na ukládání informací o souborech. Pevný disk pak míň hledá a složky s tisíci soubory se otevírají rychleji.",
                Note = "Určeno pro pevný disk (HDD) a počítač s dostatkem paměti RAM. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(Fs, "NtfsMemoryUsage", 2, null)
                }
            },
            new()
            {
                Id = "disk-nvme-native-driver", Category = CatDisk, IsNew = true, Requires = Needs.Nvme, Experimental = true, RequiresRestart = true,
                MinBuild = 26100,
                Name = "NVMe: nativní ovladač Windows (rychlejší fronta příkazů)",
                Description = "Zapne v novějším Windows 11 nový ovladač disků NVMe, který lépe využívá více jader procesoru. Může zrychlit náhodné čtení a zápis.",
                Note = "Neoficiální a experimentální. Disk se ve Správci zařízení přesune mezi „Úložné disky“. Některé zálohovací a diskové nástroje s ním nemusí fungovat. Vyžaduje restart.",
                Confirm = "Zapnout nativní ovladač NVMe?\n\nJe to experimentální úprava. Doporučujeme mít před ní zálohu dat a bod obnovení. Vypnutím se vrátí původní ovladač.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(Overrides, "735209102", 1, null),
                    Catalog.Hklm(Overrides, "1853569164", 1, null),
                    Catalog.Hklm(Overrides, "156965516", 1, null)
                }
            }
        };
    }
}
