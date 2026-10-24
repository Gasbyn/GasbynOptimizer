using System.Management;
using System.Text.RegularExpressions;

namespace GasbynOptimizer.Core;

public enum GpuVendor { Unknown, Nvidia, Amd, Intel }

public sealed record GpuInfo(string Name, GpuVendor Vendor);

/// <summary>Zjištěný hardware a systém tohoto počítače. Podle něj se určuje, co je podporované a doporučené.</summary>
public sealed class SystemProfile
{
    public string WindowsName { get; init; } = "Windows";
    public int Build { get; init; }
    public bool IsWindows11 => Build >= 22000;

    public string CpuName { get; init; } = "";
    public string CpuVendor { get; init; } = ""; // "Intel", "AMD" nebo prázdné, když se nepodařilo zjistit
    public int Cores { get; init; }
    public int Threads { get; init; }

    public IReadOnlyList<GpuInfo> Gpus { get; init; } = Array.Empty<GpuInfo>();

    public string BoardVendor { get; init; } = "";
    public string BoardModel { get; init; } = "";

    /// <summary>true = notebook, false = stolní PC, null = nepodařilo se zjistit.</summary>
    public bool? IsLaptop { get; init; }

    /// <summary>true = systémový disk je SSD, false = HDD, null = nepodařilo se zjistit.</summary>
    public bool? SystemDriveIsSsd { get; init; }

    public double RamGb { get; init; }

    public bool HasGpu(GpuVendor vendor) => Gpus.Any(g => g.Vendor == vendor);

    public bool HasIntelArc => Gpus.Any(g => g.Vendor == GpuVendor.Intel &&
                                             g.Name.Contains("Arc", StringComparison.OrdinalIgnoreCase));

    public string GpuText => Gpus.Count == 0 ? "nezjištěno" : string.Join(", ", Gpus.Select(g => g.Name));

    public string BoardText => string.IsNullOrWhiteSpace(BoardVendor) && string.IsNullOrWhiteSpace(BoardModel)
        ? "nezjištěno"
        : (BoardVendor + " " + BoardModel).Trim();

    public string TypeText => IsLaptop switch { true => "Notebook", false => "Stolní PC", _ => "nezjištěno" };

    public string DriveText => SystemDriveIsSsd switch { true => "SSD", false => "HDD", _ => "nezjištěno" };

    /// <summary>Jednořádkové shrnutí pod nadpisem stránky.</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string> { WindowsName };
            if (CpuName.Length > 0) parts.Add(CpuName);
            if (Gpus.Count > 0) parts.Add(Gpus[0].Name);
            if (IsLaptop == true) parts.Add("notebook");
            return string.Join("  ·  ", parts);
        }
    }

    // --- detekce -----------------------------------------------------------------------------

    private static readonly string[] IgnoredAdapters =
    {
        "Microsoft Basic", "Remote Display", "Virtual", "Parsec", "Meta Virtual", "Hyper-V", "Citrix", "VMware", "Idd"
    };

    private static readonly int[] LaptopChassis = { 8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32 };
    private static readonly int[] DesktopChassis = { 3, 4, 5, 6, 7, 13, 15, 16, 17, 23, 24, 35, 36 };

    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var cleaned = text.Replace("(R)", "").Replace("(TM)", "").Replace("(tm)", "").Replace("(r)", "");
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }

    private static List<ManagementBaseObject> Query(string scope, string wql)
    {
        var result = new List<ManagementBaseObject>();
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, wql);
            foreach (var item in searcher.Get())
                result.Add(item);
        }
        catch (Exception ex)
        {
            Log.Write("Dotaz na hardware selhal (" + wql + "): " + ex.Message);
        }
        return result;
    }

    private static string Text(ManagementBaseObject item, string property)
    {
        try { return Clean(item[property]?.ToString()); }
        catch { return ""; }
    }

    private static GpuVendor VendorOf(string name, string compat)
    {
        var text = name + " " + compat;
        if (text.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("GeForce", StringComparison.OrdinalIgnoreCase)) return GpuVendor.Nvidia;
        if (text.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("ATI", StringComparison.Ordinal)) return GpuVendor.Amd;
        if (text.Contains("Intel", StringComparison.OrdinalIgnoreCase)) return GpuVendor.Intel;
        return GpuVendor.Unknown;
    }

    public static SystemProfile Detect()
    {
        const string cim = @"root\cimv2";

        // Windows
        var windowsName = "Windows";
        var os = Query(cim, "SELECT Caption FROM Win32_OperatingSystem").FirstOrDefault();
        if (os != null)
        {
            var caption = Text(os, "Caption").Replace("Microsoft ", "");
            if (caption.Length > 0) windowsName = caption;
        }
        var build = Environment.OSVersion.Version.Build;

        // Procesor
        var cpuName = "";
        var cpuVendor = "";
        var cores = 0;
        var threads = 0;
        var cpu = Query(cim, "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor").FirstOrDefault();
        if (cpu != null)
        {
            cpuName = Text(cpu, "Name");
            var maker = Text(cpu, "Manufacturer");
            if (maker.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                cpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase)) cpuVendor = "Intel";
            else if (maker.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                     cpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase)) cpuVendor = "AMD";

            int.TryParse(Text(cpu, "NumberOfCores"), out cores);
            int.TryParse(Text(cpu, "NumberOfLogicalProcessors"), out threads);
        }

        // Grafické karty
        var gpus = new List<GpuInfo>();
        foreach (var video in Query(cim, "SELECT Name, AdapterCompatibility FROM Win32_VideoController"))
        {
            var name = Text(video, "Name");
            if (name.Length == 0) continue;
            if (IgnoredAdapters.Any(i => name.Contains(i, StringComparison.OrdinalIgnoreCase))) continue;
            if (gpus.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;

            gpus.Add(new GpuInfo(name, VendorOf(name, Text(video, "AdapterCompatibility"))));
        }

        // Základní deska
        var boardVendor = "";
        var boardModel = "";
        var board = Query(cim, "SELECT Manufacturer, Product FROM Win32_BaseBoard").FirstOrDefault();
        if (board != null)
        {
            boardVendor = Text(board, "Manufacturer");
            boardModel = Text(board, "Product");
        }

        // Notebook nebo stolní PC
        bool? laptop = null;
        var enclosure = Query(cim, "SELECT ChassisTypes FROM Win32_SystemEnclosure").FirstOrDefault();
        if (enclosure?["ChassisTypes"] is ushort[] chassis)
        {
            if (chassis.Any(c => LaptopChassis.Contains(c))) laptop = true;
            else if (chassis.Any(c => DesktopChassis.Contains(c))) laptop = false;
        }
        if (laptop == null)
            laptop = Query(cim, "SELECT Name FROM Win32_Battery").Count > 0 ? true : null;

        // Paměť
        double ramGb = 0;
        var computer = Query(cim, "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem").FirstOrDefault();
        if (computer != null && double.TryParse(Text(computer, "TotalPhysicalMemory"), out var bytes))
            ramGb = Math.Round(bytes / 1024 / 1024 / 1024);

        var profile = new SystemProfile
        {
            WindowsName = windowsName,
            Build = build,
            CpuName = cpuName,
            CpuVendor = cpuVendor,
            Cores = cores,
            Threads = threads,
            Gpus = gpus,
            BoardVendor = boardVendor,
            BoardModel = boardModel,
            IsLaptop = laptop,
            SystemDriveIsSsd = DetectSystemDriveSsd(),
            RamGb = ramGb
        };

        Log.Write("Systém: " + profile.Summary + " | deska: " + profile.BoardText + " | disk: " + profile.DriveText +
                  " | typ: " + profile.TypeText);
        return profile;
    }

    private static bool? DetectSystemDriveSsd()
    {
        const string storage = @"root\Microsoft\Windows\Storage";
        try
        {
            var disks = Query(storage, "SELECT DeviceId, MediaType FROM MSFT_PhysicalDisk");
            if (disks.Count == 0) return null;

            // Najdeme fyzický disk, na kterém leží systémový oddíl.
            var systemLetter = char.ToUpperInvariant(Environment.SystemDirectory[0]);
            string? diskNumber = null;
            foreach (var partition in Query(storage, "SELECT DriveLetter, DiskNumber FROM MSFT_Partition"))
            {
                var letterValue = partition["DriveLetter"];
                if (letterValue == null) continue;
                var letter = char.ToUpperInvariant(Convert.ToChar(Convert.ToUInt16(letterValue)));
                if (letter == systemLetter)
                {
                    diskNumber = Text(partition, "DiskNumber");
                    break;
                }
            }

            ManagementBaseObject? disk = null;
            if (diskNumber != null)
                disk = disks.FirstOrDefault(d => Text(d, "DeviceId") == diskNumber);

            if (disk != null)
            {
                return Text(disk, "MediaType") switch { "4" => true, "3" => false, _ => null };
            }

            // Bez přesného určení rozhodne jen jednoznačná situace (všechny disky stejného typu).
            var types = disks.Select(d => Text(d, "MediaType")).Distinct().ToList();
            if (types.Count == 1) return types[0] switch { "4" => true, "3" => false, _ => null };
            return null;
        }
        catch (Exception ex)
        {
            Log.Write("Typ systémového disku se nepodařilo zjistit: " + ex.Message);
            return null;
        }
    }
}

/// <summary>Pravidla podpory optimalizací. Vrací důvod nepodpory, nebo null, když je vše v pořádku.</summary>
public static class Needs
{
    public static string? Windows10(SystemProfile p) =>
        p.IsWindows11 ? "Určeno pro Windows 10." : null;

    public static string? Desktop(SystemProfile p) =>
        p.IsLaptop == true ? "Určeno pro stolní PC. Na notebooku zvyšuje spotřebu a zahřívání." : null;

    public static string? Laptop(SystemProfile p) =>
        p.IsLaptop == false ? "Určeno pro notebooky." : null;

    public static string? Nvidia(SystemProfile p) =>
        p.HasGpu(GpuVendor.Nvidia) ? null : "Vyžaduje grafickou kartu NVIDIA.";

    public static string? AmdGpu(SystemProfile p) =>
        p.HasGpu(GpuVendor.Amd) ? null : "Vyžaduje grafickou kartu AMD Radeon.";

    public static string? IntelCpu(SystemProfile p) =>
        p.CpuVendor == "Intel" ? null : "Vyžaduje procesor Intel.";

    public static string? AmdCpu(SystemProfile p) =>
        p.CpuVendor == "AMD" ? null : "Vyžaduje procesor AMD.";

    public static string? HagsGpu(SystemProfile p) =>
        p.HasGpu(GpuVendor.Nvidia) || p.HasGpu(GpuVendor.Amd) || p.HasIntelArc
            ? null
            : "Vyžaduje grafiku NVIDIA GeForce, AMD Radeon nebo Intel Arc.";

    /// <summary>Neznámý typ disku se bere jako podporovaný, jen jednoznačné HDD je nepodporované.</summary>
    public static string? Ssd(SystemProfile p) =>
        p.SystemDriveIsSsd == false ? "Vyžaduje systém na SSD disku." : null;

    public static string? Asus(SystemProfile p) =>
        p.BoardVendor.Contains("ASUS", StringComparison.OrdinalIgnoreCase) ? null : "Vyžaduje základní desku nebo notebook ASUS.";

    public static string? Msi(SystemProfile p) =>
        p.BoardVendor.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase) ||
        p.BoardVendor.Contains("MSI", StringComparison.OrdinalIgnoreCase)
            ? null
            : "Vyžaduje základní desku nebo notebook MSI.";
}
