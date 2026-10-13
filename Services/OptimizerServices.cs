using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Management;

namespace GasbynOptimizer.Services;

public sealed class OptimizerPreferences
{
    public bool LoggingEnabled { get; set; } = true;
    public bool AdvancedRecommendations { get; set; }
}

public static class OptimizerPreferencesStore
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GasbynOptimizer");
    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public static OptimizerPreferences Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new OptimizerPreferences();

            var loaded = JsonSerializer.Deserialize<OptimizerPreferences>(File.ReadAllText(SettingsPath));
            return loaded ?? new OptimizerPreferences();
        }
        catch
        {
            // Defaults are safe, and detailed optimization logs are enabled by default.
            return new OptimizerPreferences();
        }
    }

    public static void Save(OptimizerPreferences preferences)
    {
        Directory.CreateDirectory(DataDirectory);
        var tempPath = SettingsPath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(tempPath, SettingsPath, overwrite: true);
    }
}

public static class OptimizerAuditLog
{
    public static string LogDirectory => Path.Combine(OptimizerPreferencesStore.DataDirectory, "Logs");
    public static string LogPath => Path.Combine(LogDirectory, "optimization.jsonl");

    public static void WriteChange(
        string tweakId,
        string tweakName,
        string action,
        bool success,
        IEnumerable<string> operations,
        string details)
    {
        try
        {
            if (!OptimizerPreferencesStore.Load().LoggingEnabled)
                return;

            Directory.CreateDirectory(LogDirectory);
            var entry = new
            {
                timestampLocal = DateTimeOffset.Now.ToString("O"),
                application = "Gasbyn Optimizer",
                tweakId,
                tweakName,
                action,
                success,
                operations = operations.ToArray(),
                details
            };
            File.AppendAllText(LogPath, JsonSerializer.Serialize(entry) + Environment.NewLine, new UTF8Encoding(false));
        }
        catch
        {
            // Logging must never stop or roll back an optimization.
        }
    }
}

public sealed record HardwareInventory(
    string Cpu,
    string Gpu,
    string Motherboard,
    string Disks,
    string Memory,
    string Windows)
{
    public bool HasNvidiaGpu => Gpu.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);
    public bool HasAmdGpu => !HasNvidiaGpu &&
                            (Gpu.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                             Gpu.Contains("Radeon", StringComparison.OrdinalIgnoreCase));
    public bool HasAmdCpu => Cpu.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                            Cpu.Contains("Ryzen", StringComparison.OrdinalIgnoreCase);

    public string BuildRecommendations(bool advanced)
    {
        var recommendations = new List<string>();

        if (HasNvidiaGpu)
            recommendations.Add("GPU: NVIDIA detekována. AMD Radeon-only nastavení se nedoporučuje a zůstává vypnuté.");
        else if (HasAmdGpu)
            recommendations.Add("GPU: AMD Radeon detekována. Aplikace nepřepisuje ovladačové profily ani napětí/frekvence.");
        else
            recommendations.Add("GPU: výrobce není jednoznačný; používej pouze obecné vratné Windows úpravy.");

        if (HasAmdCpu)
            recommendations.Add("CPU: AMD/Ryzen detekován. Intel-only optimalizace by nebyly kompatibilní; BIOS a napětí se nemění.");
        else if (Cpu.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            recommendations.Add("CPU: Intel detekován. AMD-only CPU tweaky se nepoužijí; BIOS a napětí se nemění.");
        else
            recommendations.Add("CPU: použij obecné Windows úpravy; automatické taktování ani změny napětí nejsou součástí optimizeru.");

        var memoryPart = Memory.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (double.TryParse(memoryPart, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.CurrentCulture, out var gb))
            recommendations.Add(gb >= 16
                ? "RAM: kapacita alespoň 16 GB detekována. Upřednostni stabilitu a měření FPS; plošné vypínání služeb není nutné."
                : "RAM: méně než 16 GB nebo omezená kapacita. Zkontroluj aplikace na pozadí; systémové služby nevypínej naslepo.");

        if (Disks.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
            Disks.Contains("SSD", StringComparison.OrdinalIgnoreCase))
            recommendations.Add("Disk: model naznačuje SSD/NVMe. Neprováděj automatické defragmentace ani agresivní mazání shader cache.");
        else
            recommendations.Add("Disk: typ úložiště nebyl jednoznačně ověřen. Aplikace nebude měnit diskové politiky ani plán údržby.");

        recommendations.Add(string.IsNullOrWhiteSpace(Motherboard) || Motherboard.Contains("nedostupná", StringComparison.OrdinalIgnoreCase)
            ? "Základní deska: informace nejsou dostupné; nastavení BIOSu/UEFI zůstává nedotčeno."
            : "Základní deska: " + Motherboard + ". BIOS/UEFI a napěťové volby se automaticky nemění.");

        if (advanced)
            recommendations.Add("Pokročilý režim je zapnutý: nekompatibilní moduly se zobrazují jako informační upozornění, neznamená to automatické povolení jejich příkazů.");

        return string.Join(Environment.NewLine, recommendations.Select(x => "• " + x));
    }

    public string GetCompatibilityNote(string id, bool advanced)
    {
        if (id == "amd-gpu-profile" && !HasAmdGpu)
            return advanced
                ? "INFO · AMD Radeon GPU nebyla detekována. Tento GPU modul zůstává vypnutý, protože není kompatibilní s aktuálním hardwarem."
                : "NEDOPORUČENO · Není detekována AMD Radeon GPU; modul je zakázaný.";

        return id switch
        {
            "game-mode" => "DOPORUČENO · Obecné herní nastavení Windows; přínos FPS se liší podle hry a systému.",
            "game-dvr" => "PODMÍNĚNĚ · Doporučeno, pokud nepotřebuješ nahrávání klipů na pozadí.",
            "visual-effects" => "VOLITELNÉ · Může omezit vizuální efekty; nezaručuje vyšší FPS ve hře.",
            "power-plan" => "PODMÍNĚNĚ · Pro stolní PC na napájení ze sítě; může zvýšit spotřebu a teploty.",
            "transparency" => "VOLITELNÉ · Vypne průhlednost Windows; přínos je hlavně v odezvě/UI, ne ve FPS.",
            "fast-menus" => "VOLITELNÉ · Upraví odezvu nabídek a animaci oken; vzhled Windows se změní.",
            _ => "INFO · Kompatibilita závisí na konkrétní konfiguraci."
        };
    }
}

public static class HardwareInventoryService
{
    public static HardwareInventory Scan()
    {
        var cpu = FirstWmiValue("SELECT Name FROM Win32_Processor", "Name", "CPU nedostupné");
        var gpuValues = WmiValues("SELECT Name FROM Win32_VideoController", "Name");
        var gpu = gpuValues.Count == 0 ? "GPU nedostupné" : string.Join("; ", gpuValues);
        var boardManufacturer = FirstWmiValue("SELECT Manufacturer FROM Win32_BaseBoard", "Manufacturer", "");
        var boardProduct = FirstWmiValue("SELECT Product FROM Win32_BaseBoard", "Product", "");
        var motherboard = string.Join(" ", new[] { boardManufacturer, boardProduct }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(motherboard)) motherboard = "Základní deska nedostupná";

        var diskValues = WmiValues("SELECT Model, Size, MediaType FROM Win32_DiskDrive", "Model");
        var disks = diskValues.Count == 0 ? "Disky nedostupné" : string.Join("; ", diskValues.Take(6));
        var memory = "RAM nedostupná";
        try
        {
            var bytes = Convert.ToDouble(new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem")
                .Get().Cast<ManagementObject>().First()["TotalPhysicalMemory"]);
            memory = $"{bytes / 1024d / 1024d / 1024d:0.#} GB RAM";
        }
        catch { }
        var windows = Environment.OSVersion.VersionString;
        try
        {
            windows = FirstWmiValue("SELECT Caption FROM Win32_OperatingSystem", "Caption", windows);
        }
        catch { }
        return new HardwareInventory(cpu, gpu, motherboard, disks, memory, windows);
    }

    private static string FirstWmiValue(string query, string property, string fallback)
    {
        try
        {
            return WmiValues(query, property).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? fallback;
        }
        catch { return fallback; }
    }

    private static List<string> WmiValues(string query, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            return searcher.Get().Cast<ManagementObject>()
                .Select(x => x[property]?.ToString() ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch { return new List<string>(); }
    }
}
