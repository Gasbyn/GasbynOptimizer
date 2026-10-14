using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace GasbynOptimizer.Services;

/// <summary>
/// Compiled-in optimization catalog. The catalog data ships inside the application assembly;
/// no external optimizations.json file is read at runtime.
/// </summary>
public static class BuiltInOptimizationCatalog
{
    private const string CatalogJson = @"{
  ""schemaVersion"": 2,
  ""sourceVersion"": ""2.0.B18-extended"",
  ""title"": ""Gasbyn Optimizer – kompletní katalog optimalizací (ON/OFF, plné operace)"",
  ""purpose"": ""Rozšířený inventář všech optimalizací s reálnými registry/service/cmd operacemi."",
  ""important"": ""Každá optimalizace má on.operations a off.operations. Každá operace je typu reg / sc / bcd / netsh / powercfg / fsutil / schtasks / cmd / pnputil. Pole 'description' u on/off popisuje, co se stane při zapnutí/vypnutí."",

  ""categories"": [
    { ""id"": ""Gaming"",   ""label"": ""Gaming"",              ""order"": 1 },
    { ""id"": ""Windows"",  ""label"": ""Windows & UI"",        ""order"": 2 },
    { ""id"": ""Privacy"",  ""label"": ""Soukromí & telemetrie"", ""order"": 3 },
    { ""id"": ""Network"",  ""label"": ""Síť"",                 ""order"": 4 },
    { ""id"": ""Power"",    ""label"": ""Napájení"",            ""order"": 5 },
    { ""id"": ""CPU"",      ""label"": ""CPU"",                 ""order"": 6 },
    { ""id"": ""GPU"",      ""label"": ""GPU"",                 ""order"": 7 },
    { ""id"": ""Storage"",  ""label"": ""Storage & RAM"",       ""order"": 8 },
    { ""id"": ""KBM"",      ""label"": ""Klávesnice & myš"",    ""order"": 9 },
    { ""id"": ""Security"", ""label"": ""Zabezpečení & mitigace"", ""order"": 10 },
    { ""id"": ""Cleanup"",  ""label"": ""Čištění"",             ""order"": 11 }
  ],

  ""optimizations"": [

    
    {
      ""id"": ""game-mode"",
      ""name"": ""Windows Game Mode"",
      ""description"": ""Povolí herní režim Windows. Přínos závisí na PC a hře; FPS není garantováno."",
      ""category"": ""Gaming"",
      ""badges"": [""DOPORUČENO"", ""FPS"", ""GAMING""],
      ""implementation"": ""registry"",
      ""on"": {
        ""description"": ""Uloží původní hodnoty a nastaví AutoGameModeEnabled=1 a AllowAutoGameMode=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\GameBar"", ""value"": ""AllowAutoGameMode"",   ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\GameBar"", ""value"": ""AutoGameModeEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""Obnoví původní hodnoty (obě na 0)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\GameBar"", ""value"": ""AllowAutoGameMode"",   ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\GameBar"", ""value"": ""AutoGameModeEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      }
    },
    {
      ""id"": ""game-dvr"",
      ""name"": ""Vypnout Game DVR a záznamy na pozadí"",
      ""description"": ""Omezí zachytávání Windows na pozadí. Může vypnout nahrávání klipů přes Xbox Game Bar."",
      ""category"": ""Gaming"",
      ""badges"": [""PODMÍNĚNĚ"", ""FPS"", ""ZÁZNAMY""],
      ""implementation"": ""registry"",
      ""on"": {
        ""description"": ""Nastaví GameDVR_Enabled=0, AppCaptureEnabled=0, AudioCaptureEnabled=0, CursorCaptureEnabled=0, HistoricalCaptureEnabled=0 a zásadu AllowGameDVR=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""UseNexusForGameBarEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""GameDVR_Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""AppCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""AudioCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""CursorCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""HistoricalCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""System\\GameConfigStore"", ""value"": ""GameDVR_Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""Software\\Policies\\Microsoft\\Windows\\GameDVR"", ""value"": ""AllowGameDVR"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí všechny hodnoty na 1 (zapne záznamy)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""UseNexusForGameBarEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""GameDVR_Enabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""AppCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""AudioCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""CursorCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""HistoricalCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""System\\GameConfigStore"", ""value"": ""GameDVR_Enabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""Software\\Policies\\Microsoft\\Windows\\GameDVR"", ""value"": ""AllowGameDVR"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""xbox-services"",
      ""name"": ""Xbox služby (Gaming Services)"",
      ""description"": ""Vypne Xbox Live, Game Save, Game Input a Network API služby. Může rozbít Xbox aplikace a hry vyžadující Xbox Live."",
      ""category"": ""Gaming"",
      ""badges"": [""PODMÍNĚNĚ"", ""SLUŽBY""],
      ""on"": {
        ""description"": ""Nastaví služby xbgm, XblAuthManager, XblGameSave, XboxGipSvc, XboxNetApiSvc na Disabled."",
        ""operations"": [
          { ""type"": ""sc"", ""name"": ""xbgm"",           ""start"": ""disabled"" },
          { ""type"": ""sc"", ""name"": ""XblAuthManager"", ""start"": ""disabled"" },
          { ""type"": ""sc"", ""name"": ""XblGameSave"",    ""start"": ""disabled"" },
          { ""type"": ""sc"", ""name"": ""XboxGipSvc"",     ""start"": ""disabled"" },
          { ""type"": ""sc"", ""name"": ""XboxNetApiSvc"",  ""start"": ""disabled"" }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí všechny Xbox služby na výchozí start (demand/auto)."",
        ""operations"": [
          { ""type"": ""sc"", ""name"": ""xbgm"",           ""start"": ""demand"" },
          { ""type"": ""sc"", ""name"": ""XblAuthManager"", ""start"": ""demand"" },
          { ""type"": ""sc"", ""name"": ""XblGameSave"",    ""start"": ""demand"" },
          { ""type"": ""sc"", ""name"": ""XboxGipSvc"",     ""start"": ""demand"" },
          { ""type"": ""sc"", ""name"": ""XboxNetApiSvc"",  ""start"": ""demand"" }
        ]
      }
    },
    {
      ""id"": ""mouse-acceleration"",
      ""name"": ""Vypnout akceleraci myši"",
      ""description"": ""Vypne Windows pointer acceleration (Enhance pointer precision)."",
      ""category"": ""KBM"",
      ""badges"": [""VOLITELNÉ"", ""GAMING"", ""AIM""],
      ""on"": {
        ""description"": ""Nastaví MouseSpeed=0, MouseThreshold1=0, MouseThreshold2=0 (surový vstup)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Mouse"", ""value"": ""MouseSpeed"",      ""regType"": ""REG_SZ"", ""data"": ""0"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Mouse"", ""value"": ""MouseThreshold1"", ""regType"": ""REG_SZ"", ""data"": ""0"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Mouse"", ""value"": ""MouseThreshold2"", ""regType"": ""REG_SZ"", ""data"": ""0"" }
        ]
      },
      ""off"": {
        ""description"": ""Obnoví Windows výchozí akceleraci (MouseSpeed=1, thresholds=6/10)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Mouse"", ""value"": ""MouseSpeed"",      ""regType"": ""REG_SZ"", ""data"": ""1"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Mouse"", ""value"": ""MouseThreshold1"", ""regType"": ""REG_SZ"", ""data"": ""6"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Mouse"", ""value"": ""MouseThreshold2"", ""regType"": ""REG_SZ"", ""data"": ""10"" }
        ]
      }
    },
    {
      ""id"": ""keyboard-response"",
      ""name"": ""Zrychlit klávesnici (delay/speed)"",
      ""description"": ""Sníží zpoždění opakování kláves a zvýší rychlost opakování."",
      ""category"": ""KBM"",
      ""badges"": [""VOLITELNÉ"", ""KBM""],
      ""on"": {
        ""description"": ""Nastaví KeyboardDelay=0 a KeyboardSpeed=31."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Keyboard"", ""value"": ""KeyboardDelay"", ""regType"": ""REG_SZ"", ""data"": ""0"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Keyboard"", ""value"": ""KeyboardSpeed"", ""regType"": ""REG_SZ"", ""data"": ""31"" }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí výchozí KeyboardDelay=1 a KeyboardSpeed=31 (nebo dle Windows)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Keyboard"", ""value"": ""KeyboardDelay"", ""regType"": ""REG_SZ"", ""data"": ""1"" }
        ]
      }
    },
    {
      ""id"": ""accessibility-keys-off"",
      ""name"": ""Vypnout Filter/Toggle/Sticky/Mouse Keys"",
      ""description"": ""Vypne zbytečné přístupnostní klávesové funkce, které občas blokují Shift při hraní."",
      ""category"": ""KBM"",
      ""badges"": [""VOLITELNÉ""],
      ""on"": {
        ""description"": ""Nastaví Flags=122/58/506/0 pro Keyboard Response, ToggleKeys, StickyKeys, MouseKeys."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Accessibility\\Keyboard Response"", ""value"": ""Flags"", ""regType"": ""REG_SZ"", ""data"": ""122"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Accessibility\\ToggleKeys"",        ""value"": ""Flags"", ""regType"": ""REG_SZ"", ""data"": ""58"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Accessibility\\StickyKeys"",        ""value"": ""Flags"", ""regType"": ""REG_SZ"", ""data"": ""506"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Accessibility\\MouseKeys"",         ""value"": ""Flags"", ""regType"": ""REG_SZ"", ""data"": ""0"" }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní hodnoty Flags – Windows se vrátí k výchozímu chování."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Accessibility\\Keyboard Response"", ""value"": ""Flags"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Accessibility\\ToggleKeys"",        ""value"": ""Flags"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Accessibility\\StickyKeys"",        ""value"": ""Flags"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Accessibility\\MouseKeys"",         ""value"": ""Flags"" }
        ]
      }
    },
    {
      ""id"": ""csrss-priority"",
      ""name"": ""Priorita CSRSS (KBM ovladače)"",
      ""description"": ""Zvýší CPU a IO prioritu csrss.exe, který zpracovává vstup z klávesnice a myši."",
      ""category"": ""KBM"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""Nastaví CpuPriorityClass=4 a IoPriority=3 pro csrss.exe."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\csrss.exe\\PerfOptions"", ""value"": ""CpuPriorityClass"", ""regType"": ""REG_DWORD"", ""data"": 4 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\csrss.exe\\PerfOptions"", ""value"": ""IoPriority"",     ""regType"": ""REG_DWORD"", ""data"": 3 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí CpuPriorityClass=3 a IoPriority=2 (Windows default)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\csrss.exe\\PerfOptions"", ""value"": ""CpuPriorityClass"", ""regType"": ""REG_DWORD"", ""data"": 3 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\csrss.exe\\PerfOptions"", ""value"": ""IoPriority"",     ""regType"": ""REG_DWORD"", ""data"": 2 }
        ]
      }
    },
    {
      ""id"": ""mouse-queue-size"",
      ""name"": ""Velikost fronty myši/klávesnice"",
      ""description"": ""Zmenší buffer pro myš a klávesnici na 65 (vhodné pro high-end CPU a 1000Hz myši)."",
      ""category"": ""KBM"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""Nastaví MouseDataQueueSize=65, KeyboardDataQueueSize=65."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\mouclass\\Parameters"", ""value"": ""MouseDataQueueSize"",    ""regType"": ""REG_DWORD"", ""data"": 65 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\kbdclass\\Parameters"", ""value"": ""KeyboardDataQueueSize"", ""regType"": ""REG_DWORD"", ""data"": 65 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí výchozí hodnotu 100."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\mouclass\\Parameters"", ""value"": ""MouseDataQueueSize"",    ""regType"": ""REG_DWORD"", ""data"": 100 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\kbdclass\\Parameters"", ""value"": ""KeyboardDataQueueSize"", ""regType"": ""REG_DWORD"", ""data"": 100 }
        ]
      }
    },
    {
      ""id"": ""usb-power-savings-off"",
      ""name"": ""Vypnout USB úspory energie"",
      ""description"": ""Zakáže USB selective suspend a zvýší prioritu USB řadičů na 31."",
      ""category"": ""KBM"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""DisableSelectiveSuspend=1 a ThreadPriority=31 pro usbxhci, USBHUB3, nvlddmkm, NDIS."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\USB"", ""value"": ""DisableSelectiveSuspend"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\usbxhci\\Parameters"", ""value"": ""ThreadPriority"", ""regType"": ""REG_DWORD"", ""data"": 31 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\USBHUB3\\Parameters"", ""value"": ""ThreadPriority"", ""regType"": ""REG_DWORD"", ""data"": 31 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\nvlddmkm\\Parameters"", ""value"": ""ThreadPriority"", ""regType"": ""REG_DWORD"", ""data"": 31 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\NDIS\\Parameters"", ""value"": ""ThreadPriority"", ""regType"": ""REG_DWORD"", ""data"": 31 }
        ]
      },
      ""off"": {
        ""description"": ""Obnoví USB selective suspend (DisableSelectiveSuspend=0), ostatní hodnoty odstraní."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\USB"", ""value"": ""DisableSelectiveSuspend"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\usbxhci\\Parameters"", ""value"": ""ThreadPriority"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\USBHUB3\\Parameters"", ""value"": ""ThreadPriority"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\nvlddmkm\\Parameters"", ""value"": ""ThreadPriority"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\NDIS\\Parameters"", ""value"": ""ThreadPriority"" }
        ]
      }
    },
    {
      ""id"": ""hags"",
      ""name"": ""Hardware-accelerated GPU scheduling (HAGS)"",
      ""description"": ""Zapne HAGS. Podpora závisí na GPU, ovladači a Windows; po změně restart."",
      ""category"": ""GPU"",
      ""badges"": [""POKROČILÉ"", ""GPU"", ""RESTART""],
      ""on"": {
        ""description"": ""Nastaví HwSchMode=2."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers"", ""value"": ""HwSchMode"", ""regType"": ""REG_DWORD"", ""data"": 2 }
        ]
      },
      ""off"": {
        ""description"": ""Nastaví HwSchMode=1 (nebo odstraní)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers"", ""value"": ""HwSchMode"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""fullscreen-optimizations"",
      ""name"": ""Vypnout Fullscreen Optimizations pro Fortnite"",
      ""description"": ""Nastaví kompatibilitní volbu Windows pro Fortnite executable (DISABLEDXMAXIMIZEDWINDOWEDMODE)."",
      ""category"": ""Gaming"",
      ""badges"": [""POKROČILÉ"", ""FORTNITE""],
      ""implementation"": ""existing-csharp"",
      ""on"": {
        ""description"": ""Nastaví DISABLEDXMAXIMIZEDWINDOWEDMODE pro nalezený Fortnite exe. Nutný ruční výběr cesty nebo detekce."",
        ""operations"": []
      },
      ""off"": {
        ""description"": ""Odstraní hodnotu DISABLEDXMAXIMIZEDWINDOWEDMODE."",
        ""operations"": []
      }
    },
    {
      ""id"": ""gpu-preference"",
      ""name"": ""Fortnite – preferovat výkonnou GPU"",
      ""description"": ""Nastaví DirectX GpuPreference=2 pro Fortnite. Na PC s jednou GPU se nemusí projevit."",
      ""category"": ""Gaming"",
      ""badges"": [""PODMÍNĚNĚ"", ""FPS"", ""POKROČILÉ""],
      ""on"": {
        ""description"": ""Zapíše GpuPreference=2 do UserGpuPreferences pro nalezený Fortnite exe."",
        ""operations"": []
      },
      ""off"": {
        ""description"": ""Odstraní hodnotu GpuPreference pro Fortnite."",
        ""operations"": []
      }
    },
    {
      ""id"": ""gamebar-startup-panel"",
      ""name"": ""Vypnout úvodní panel Xbox Game Bar"",
      ""description"": ""Vypne úvodní panel Game Bar, samotná aplikace zůstane nainstalovaná."",
      ""category"": ""Gaming"",
      ""badges"": [""GAMING"", ""WINDOWS""],
      ""on"": {
        ""description"": ""Nastaví ShowStartupPanel=0 a UseNexusForGameBarEnabled=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""ShowStartupPanel"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""UseNexusForGameBarEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí obě hodnoty na 1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""ShowStartupPanel"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""UseNexusForGameBarEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""gaming-dvr-full-off"",
      ""name"": ""Vypnout kompletní záznam herního režimu (GameDVR + Nexus)"",
      ""description"": ""Kompletně vypne Xbox Game DVR, Nexus i startup panel. Více agresivní než základní game-dvr."",
      ""category"": ""Gaming"",
      ""badges"": [""PODMÍNĚNĚ"", ""FPS""],
      ""on"": {
        ""description"": ""Kombinuje game-dvr + nexus + startup panel do jednoho."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""UseNexusForGameBarEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""ShowStartupPanel"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""GameDVR_Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""AppCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""AudioCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""CursorCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""HistoricalCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""System\\GameConfigStore"", ""value"": ""GameDVR_Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""Software\\Policies\\Microsoft\\Windows\\GameDVR"", ""value"": ""AllowGameDVR"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí všechny na 1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""UseNexusForGameBarEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""ShowStartupPanel"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\GameBar"", ""value"": ""GameDVR_Enabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""AppCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""AudioCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""CursorCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR"", ""value"": ""HistoricalCaptureEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""System\\GameConfigStore"", ""value"": ""GameDVR_Enabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""Software\\Policies\\Microsoft\\Windows\\GameDVR"", ""value"": ""AllowGameDVR"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },

    
    {
      ""id"": ""visual-effects"",
      ""name"": ""Výkonové vizuální efekty"",
      ""description"": ""Přepne Windows do režimu nejlepšího výkonu."",
      ""category"": ""Windows"",
      ""badges"": [""VOLITELNÉ"", ""VZHLED""],
      ""on"": {
        ""description"": ""Nastaví VisualFXSetting=2 (Best Performance)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects"", ""value"": ""VisualFXSetting"", ""regType"": ""REG_DWORD"", ""data"": 2 }
        ]
      },
      ""off"": {
        ""description"": ""Nastaví VisualFXSetting=0 (Let Windows choose)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects"", ""value"": ""VisualFXSetting"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      }
    },
    {
      ""id"": ""transparency"",
      ""name"": ""Vypnout efekty průhlednosti"",
      ""description"": ""Vypne průhledné prvky Windows."",
      ""category"": ""Windows"",
      ""badges"": [""VOLITELNÉ"", ""VZHLED""],
      ""on"": {
        ""description"": ""EnableTransparency=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"", ""value"": ""EnableTransparency"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""EnableTransparency=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"", ""value"": ""EnableTransparency"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""fast-menus"",
      ""name"": ""Rychlejší nabídky a animace oken"",
      ""description"": ""Zkrátí prodlevu nabídek a vypne animaci minimalizace/maximalizace."",
      ""category"": ""Windows"",
      ""badges"": [""VOLITELNÉ"", ""ODEZVA"", ""UI""],
      ""on"": {
        ""description"": ""MenuShowDelay=100, MinAnimate=0, TaskbarAnimations=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Desktop"", ""value"": ""MenuShowDelay"",  ""regType"": ""REG_SZ"", ""data"": ""100"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Desktop\\WindowMetrics"", ""value"": ""MinAnimate"", ""regType"": ""REG_SZ"", ""data"": ""0"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""TaskbarAnimations"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""MenuShowDelay=400, MinAnimate=1, TaskbarAnimations=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Desktop"", ""value"": ""MenuShowDelay"",  ""regType"": ""REG_SZ"", ""data"": ""400"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Desktop\\WindowMetrics"", ""value"": ""MinAnimate"", ""regType"": ""REG_SZ"", ""data"": ""1"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""TaskbarAnimations"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""startup-delay"",
      ""name"": ""Zkrátit odklad spouštění aplikací"",
      ""description"": ""StartupDelayInMSec=0 – aplikace se spustí dříve po přihlášení."",
      ""category"": ""Windows"",
      ""badges"": [""VOLITELNÉ"", ""STARTUP""],
      ""on"": {
        ""description"": ""StartupDelayInMSec=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Serialize"", ""value"": ""StartupDelayInMSec"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní StartupDelayInMSec (Windows default ~10s)."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Serialize"", ""value"": ""StartupDelayInMSec"" }
        ]
      }
    },
    {
      ""id"": ""widgets"",
      ""name"": ""Skrýt tlačítko Widgets"",
      ""description"": ""Skryje tlačítko Widgetů na hlavním panelu."",
      ""category"": ""Windows"",
      ""badges"": [""VOLITELNÉ"", ""UI""],
      ""on"": {
        ""description"": ""TaskbarDa=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""TaskbarDa"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""TaskbarDa=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""TaskbarDa"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""show-hidden-files"",
      ""name"": ""Zobrazit skryté soubory"",
      ""description"": ""Zobrazí skryté soubory v Průzkumníku."",
      ""category"": ""Windows"",
      ""badges"": [""VOLITELNÉ"", ""WINDOWS""],
      ""on"": {
        ""description"": ""Hidden=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""Hidden"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""Hidden=2 (skryté)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""Hidden"", ""regType"": ""REG_DWORD"", ""data"": 2 }
        ]
      }
    },
    {
      ""id"": ""show-file-extensions"",
      ""name"": ""Zobrazit přípony souborů"",
      ""description"": ""Zobrazí přípony .exe, .cmd atd."",
      ""category"": ""Windows"",
      ""badges"": [""VOLITELNÉ"", ""WINDOWS""],
      ""on"": {
        ""description"": ""HideFileExt=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""HideFileExt"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""HideFileExt=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""HideFileExt"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""dark-mode"",
      ""name"": ""Tmavý režim Windows"",
      ""description"": ""Přepne systémové aplikace a systém do tmavého režimu."",
      ""category"": ""Windows"",
      ""badges"": [""VOLITELNÉ"", ""VZHLED""],
      ""on"": {
        ""description"": ""AppsUseLightTheme=0 a SystemUsesLightTheme=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"", ""value"": ""AppsUseLightTheme"",   ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"", ""value"": ""SystemUsesLightTheme"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Obě hodnoty na 1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"", ""value"": ""AppsUseLightTheme"",   ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"", ""value"": ""SystemUsesLightTheme"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""hide-task-view"",
      ""name"": ""Skrýt tlačítko Zobrazení úloh"",
      ""description"": ""Skryje tlačítko Task View na hlavním panelu."",
      ""category"": ""Windows"",
      ""badges"": [""VOLITELNÉ"", ""UI""],
      ""on"": {
        ""description"": ""ShowTaskViewButton=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""ShowTaskViewButton"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""ShowTaskViewButton=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""ShowTaskViewButton"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""hide-meet-now"",
      ""name"": ""Skrýt Meet Now na hlavním panelu"",
      ""description"": ""Skryje ikonu Meet Now (starší verze Windows 10)."",
      ""category"": ""Windows"",
      ""badges"": [""VOLITELNÉ""],
      ""on"": {
        ""description"": ""HideSCAMeetNow=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer"", ""value"": ""HideSCAMeetNow"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní HideSCAMeetNow."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer"", ""value"": ""HideSCAMeetNow"" }
        ]
      }
    },
    {
      ""id"": ""hide-people-taskbar"",
      ""name"": ""Skrýt ikonu Lidé"",
      ""description"": ""Skryje starší panel Lidé na hlavním panelu."",
      ""category"": ""Windows"",
      ""badges"": [""VOLITELNÉ""],
      ""on"": {
        ""description"": ""PeopleBand=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""PeopleBand"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""PeopleBand=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced"", ""value"": ""PeopleBand"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },

    
    {
      ""id"": ""telemetry-disable"",
      ""name"": ""Vypnout telemetrii a diagnostiku (kompletní)"",
      ""description"": ""Vypne DiagTrack, DPS, WER, CEIP, schtasks a AllowTelemetry. Kompletní balík – více než 30 operací."",
      ""category"": ""Privacy"",
      ""badges"": [""SOUKROMÍ"", ""DOPORUČENO""],
      ""on"": {
        ""description"": ""Vypne DiagTrack, dmwappushservice, nastaví AllowTelemetry=0, vypne 27 telemetrických schtasks a nastaví registry zásady DataCollection."",
        ""operations"": [
          { ""type"": ""sc"",  ""name"": ""DiagTrack"",              ""start"": ""disabled"" },
          { ""type"": ""sc"",  ""name"": ""dmwappushservice"",       ""start"": ""disabled"" },
          { ""type"": ""sc"",  ""name"": ""diagnosticshub.standardcollector.service"", ""start"": ""disabled"" },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection"", ""value"": ""AllowTelemetry"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection"", ""value"": ""DoNotShowFeedbackNotifications"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection"", ""value"": ""AllowCommercialDataPipeline"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection"", ""value"": ""AllowTelemetry"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\SQMClient\\Windows"", ""value"": ""CEIPEnable"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\SQMClient\\Windows"", ""value"": ""CEIPEnable"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\SQMClient\\Windows"", ""value"": ""SqmLoggerRunning"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Customer Experience Improvement Program\\BthSQM"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Customer Experience Improvement Program\\KernelCeipTask"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Customer Experience Improvement Program\\UsbCeip"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Customer Experience Improvement Program\\Uploader"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Application Experience\\ProgramDataUpdater"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Application Experience\\StartupAppTask"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Application Experience\\AitAgent"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\DiskDiagnostic\\Microsoft-Windows-DiskDiagnosticDataCollector"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\DiskDiagnostic\\Microsoft-Windows-DiskDiagnosticResolver"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Power Efficiency Diagnostics\\AnalyzeSystem"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Autochk\\Proxy"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Maintenance\\WinSAT"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Windows Error Reporting\\QueueReporting"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\CloudExperienceHost\\CreateObjectTask"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\DiskFootprint\\Diagnostics"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\PI\\Sqm-Tasks"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\NetTrace\\GatherNetworkInfo"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\AppID\\SmartScreenSpecific"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Time Synchronization\\ForceSynchronizeTime"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Time Synchronization\\SynchronizeTime"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\WindowsUpdate\\Automatic App Update"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Device Information\\Device"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Shell\\FamilySafetyMonitor"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Shell\\FamilySafetyRefresh"", ""state"": ""disable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Shell\\FamilySafetyUpload"", ""state"": ""disable"" }
        ]
      },
      ""off"": {
        ""description"": ""Zapne služby zpět (DiagTrack=demand, dmwappushservice=demand), povolí telemetrii (AllowTelemetry=1) a všechny schtasks znovu povolí."",
        ""operations"": [
          { ""type"": ""sc"",  ""name"": ""DiagTrack"",              ""start"": ""demand"" },
          { ""type"": ""sc"",  ""name"": ""dmwappushservice"",       ""start"": ""demand"" },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection"", ""value"": ""AllowTelemetry"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator"", ""state"": ""enable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser"", ""state"": ""enable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Application Experience\\ProgramDataUpdater"", ""state"": ""enable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Power Efficiency Diagnostics\\AnalyzeSystem"", ""state"": ""enable"" },
          { ""type"": ""schtasks"", ""task"": ""\\Microsoft\\Windows\\Windows Error Reporting\\QueueReporting"", ""state"": ""enable"" }
        ]
      }
    },
    {
      ""id"": ""autologger-disable"",
      ""name"": ""Vypnout WMI Autologgery"",
      ""description"": ""Vypne 38 zbytečných autologgerů (AppModel, DefenderApiLogger, DiagLog atd.)."",
      ""category"": ""Privacy"",
      ""badges"": [""POKROČILÉ"", ""TELEMETRIE""],
      ""on"": {
        ""description"": ""Nastaví Start=0 pro každý autologger v HKLM\\SYSTEM\\CurrentControlSet\\Control\\WMI\\Autologger\\(*)."",
        ""operations"": [
          { ""type"": ""reg-batch"", ""hive"": ""HKLM"", ""basePath"": ""SYSTEM\\CurrentControlSet\\Control\\WMI\\Autologger"",
            ""subkeys"": [""AppModel"",""Cellcore"",""Circular Kernel Context Logger"",""CloudExperienceHostOobe"",""DataMarket"",""DefenderApiLogger"",""DefenderAuditLogger"",""DiagLog"",""HolographicDevice"",""iclsClient"",""iclsProxy"",""LwtNetLog"",""Mellanox-Kernel"",""Microsoft-Windows-AssignedAccess-Trace"",""Microsoft-Windows-Setup"",""NBSMBLOGGER"",""PEAuthLog"",""RdrLog"",""ReadyBoot"",""SetupPlatform"",""SetupPlatformTel"",""SocketHeciServer"",""SpoolerLogger"",""SQMLogger"",""TCPIPLOGGER"",""TileStore"",""Tpm"",""TPMProvisioningService"",""UBPM"",""WdiContextLog"",""WFP-IPsec Trace"",""WiFiDriverIHVSession"",""WiFiDriverIHVSessionRepro"",""WiFiSession"",""WinPhoneCritical""],
            ""value"": ""Start"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí Start=1 pro všechny autologgery."",
        ""operations"": [
          { ""type"": ""reg-batch"", ""hive"": ""HKLM"", ""basePath"": ""SYSTEM\\CurrentControlSet\\Control\\WMI\\Autologger"",
            ""subkeys"": [""AppModel"",""Cellcore"",""Circular Kernel Context Logger"",""CloudExperienceHostOobe"",""DataMarket"",""DefenderApiLogger"",""DefenderAuditLogger"",""DiagLog"",""HolographicDevice"",""iclsClient"",""iclsProxy"",""LwtNetLog"",""Mellanox-Kernel"",""Microsoft-Windows-AssignedAccess-Trace"",""Microsoft-Windows-Setup"",""NBSMBLOGGER"",""PEAuthLog"",""RdrLog"",""ReadyBoot"",""SetupPlatform"",""SetupPlatformTel"",""SocketHeciServer"",""SpoolerLogger"",""SQMLogger"",""TCPIPLOGGER"",""TileStore"",""Tpm"",""TPMProvisioningService"",""UBPM"",""WdiContextLog"",""WFP-IPsec Trace"",""WiFiDriverIHVSession"",""WiFiDriverIHVSessionRepro"",""WiFiSession"",""WinPhoneCritical""],
            ""value"": ""Start"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""wer-disable"",
      ""name"": ""Vypnout Windows Error Reporting"",
      ""description"": ""Vypne hlášení chyb Windows a Error Reporting."",
      ""category"": ""Privacy"",
      ""badges"": [""SOUKROMÍ""],
      ""on"": {
        ""description"": ""Nastaví Disabled=1, DoReport=0, LoggingDisabled=1 v Policies WER."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Error Reporting"", ""value"": ""Disabled"",        ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Error Reporting"", ""value"": ""DoReport"",        ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Error Reporting"", ""value"": ""LoggingDisabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\PCHealth\\ErrorReporting"", ""value"": ""DoReport"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting"", ""value"": ""Disabled"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní zásady a vrátí výchozí chování WER."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Error Reporting"", ""value"": ""Disabled"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Error Reporting"", ""value"": ""DoReport"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Error Reporting"", ""value"": ""LoggingDisabled"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\PCHealth\\ErrorReporting"", ""value"": ""DoReport"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting"", ""value"": ""Disabled"" }
        ]
      }
    },
    {
      ""id"": ""auditpol-reduce"",
      ""name"": ""Omezit audity Windows"",
      ""description"": ""Sníží množství auditních záznamů na minimum (jen failure:Enable)."",
      ""category"": ""Privacy"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""Nastaví audit pro Process Termination, RPC Events, Filtering Platform Connection, Other System Events, Security State Change, Security System Extension, System Integrity na failure:Enable."",
        ""operations"": [
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""Process Termination\"" /failure:Enable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""RPC Events\"" /failure:Enable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""Filtering Platform Connection\"" /failure:Enable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""Other System Events\"" /failure:Enable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""Security State Change\"" /failure:Enable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""Security System Extension\"" /failure:Enable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""System Integrity\"" /failure:Enable"" }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí audit na výchozí (No Auditing) pro stejné kategorie."",
        ""operations"": [
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""Process Termination\"" /success:Disable /failure:Disable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""RPC Events\"" /success:Disable /failure:Disable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""Filtering Platform Connection\"" /success:Disable /failure:Disable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""Other System Events\"" /success:Disable /failure:Disable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""Security State Change\"" /success:Disable /failure:Disable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""Security System Extension\"" /success:Disable /failure:Disable"" },
          { ""type"": ""cmd"", ""command"": ""Auditpol /set /subcategory:\""System Integrity\"" /success:Disable /failure:Disable"" }
        ]
      }
    },
    {
      ""id"": ""consumer-features-off"",
      ""name"": ""Vypnout spotřebitelské a doporučené funkce Windows"",
      ""description"": ""Kompletně vypne návrhy, Spotlight, tipy a reklamní ID. Sloučení mnoha menších tweaků."",
      ""category"": ""Privacy"",
      ""badges"": [""SOUKROMÍ""],
      ""on"": {
        ""description"": ""Vypne CloudContent zásady, ContentDeliveryManager, TailoredExperiences, ActivityFeed, feedback notifications, klávesu PublishUserActivities a UploadUserActivities."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent"", ""value"": ""DisableWindowsConsumerFeatures"",      ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent"", ""value"": ""DisableThirdPartySuggestions"",       ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent"", ""value"": ""DisableWindowsSpotlightFeatures"",    ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent"", ""value"": ""DisableWindowsSpotlightWindowsWelcomeExperience"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent"", ""value"": ""DisableSoftLanding"",                  ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection"", ""value"": ""DoNotShowFeedbackNotifications"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\System"", ""value"": ""EnableActivityFeed"",     ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\System"", ""value"": ""PublishUserActivities"",  ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\System"", ""value"": ""UploadUserActivities"",   ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Privacy"", ""value"": ""TailoredExperiencesWithDiagnosticDataEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo"", ""value"": ""Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\AdvertisingInfo"", ""value"": ""DisabledByGroupPolicy"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""PreInstalledAppsEnabled"",      ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""SilentInstalledAppsEnabled"",   ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""OemPreInstalledAppsEnabled"",   ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""ContentDeliveryAllowed"",       ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""SubscribedContentEnabled"",      ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""PreInstalledAppsEverEnabled"",  ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""SubscribedContent-338389Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""SubscribedContent-310093Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""SubscribedContent-338387Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""SubscribedContent-338392Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""SubscribedContent-338393Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""SubscribedContent-353696Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí cloud content zásady na 0 a ContentDeliveryManager na 1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent"", ""value"": ""DisableWindowsConsumerFeatures"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent"", ""value"": ""DisableThirdPartySuggestions"",  ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager"", ""value"": ""ContentDeliveryAllowed"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""sync-nastaveni-off"",
      ""name"": ""Vypnout synchronizaci nastavení Windows"",
      ""description"": ""Vypne Sync přes Microsoft účet pro všechna nastavení."",
      ""category"": ""Privacy"",
      ""badges"": [""SOUKROMÍ""],
      ""on"": {
        ""description"": ""Nastaví DisableSettingSync=2 a všechny dílčí synchronizace=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\SettingSync"", ""value"": ""SyncPolicy"", ""regType"": ""REG_DWORD"", ""data"": 5 },
          { ""type"": ""reg-batch"", ""hive"": ""HKLM"", ""basePath"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\SettingSync"",
            ""subkeys"": [""DisableSettingSync"",""DisableSettingSyncUserOverride"",""DisableAppSyncSettingSync"",""DisableAppSyncSettingSyncUserOverride"",""DisableApplicationSettingSync"",""DisableApplicationSettingSyncUserOverride"",""DisableCredentialsSettingSync"",""DisableCredentialsSettingSyncUserOverride"",""DisableDesktopThemeSettingSync"",""DisableDesktopThemeSettingSyncUserOverride"",""DisablePersonalizationSettingSync"",""DisablePersonalizationSettingSyncUserOverride"",""DisableStartLayoutSettingSync"",""DisableStartLayoutSettingSyncUserOverride"",""DisableSyncOnPaidNetwork"",""DisableWebBrowserSettingSync"",""DisableWebBrowserSettingSyncUserOverride"",""DisableWindowsSettingSync"",""DisableWindowsSettingSyncUserOverride""],
            ""value"": null, ""regType"": ""REG_DWORD"", ""data"": 2 }
        ]
      },
      ""off"": {
        ""description"": ""Nastaví SyncPolicy=0 a zásady na 0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\SettingSync"", ""value"": ""SyncPolicy"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg-batch"", ""hive"": ""HKLM"", ""basePath"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\SettingSync"",
            ""subkeys"": [""DisableSettingSync"",""DisableSettingSyncUserOverride"",""DisableAppSyncSettingSync"",""DisableApplicationSettingSync"",""DisableCredentialsSettingSync"",""DisableDesktopThemeSettingSync"",""DisablePersonalizationSettingSync"",""DisableStartLayoutSettingSync"",""DisableSyncOnPaidNetwork"",""DisableWebBrowserSettingSync"",""DisableWindowsSettingSync""],
            ""value"": null, ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      }
    },
    {
      ""id"": ""cross-device-clipboard-off"",
      ""name"": ""Vypnout synchronizaci schránky mezi zařízeními"",
      ""description"": ""Vypne cross-device clipboard sync. Místní schránka zůstane funkční."",
      ""category"": ""Privacy"",
      ""badges"": [""SOUKROMÍ""],
      ""on"": {
        ""description"": ""AllowCrossDeviceClipboard=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Clipboard"", ""value"": ""AllowCrossDeviceClipboard"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""AllowCrossDeviceClipboard=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Clipboard"", ""value"": ""AllowCrossDeviceClipboard"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""shared-experiences-off"",
      ""name"": ""Vypnout Nearby Sharing a Shared Experiences"",
      ""description"": ""Vypne sdílení obsahu mezi zařízeními v okolí."",
      ""category"": ""Privacy"",
      ""badges"": [""SOUKROMÍ""],
      ""on"": {
        ""description"": ""CdpSessionUserAuthzPolicy=0, NearShareChannelUserAuthzPolicy=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CDP"", ""value"": ""CdpSessionUserAuthzPolicy"",       ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CDP"", ""value"": ""NearShareChannelUserAuthzPolicy"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Obě hodnoty na 1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CDP"", ""value"": ""CdpSessionUserAuthzPolicy"",       ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CDP"", ""value"": ""NearShareChannelUserAuthzPolicy"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""notifications-off"",
      ""name"": ""Vypnout oznámení Windows a Centrum oznámení"",
      ""description"": ""Kompletně vypne oznámení, Centrum oznámení a zvuky oznámení."",
      ""category"": ""Windows"",
      ""badges"": [""POZOR"", ""UI""],
      ""on"": {
        ""description"": ""ToastEnabled=0, DisableNotificationCenter=1, vypne všechny dílčí kanály."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PushNotifications"", ""value"": ""ToastEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer"", ""value"": ""DisableNotificationCenter"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings"", ""value"": ""NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"",           ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings"", ""value"": ""NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings\\QuietHours"", ""value"": ""Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí oznámení (ToastEnabled=1, DisableNotificationCenter=0, zvuky=1)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\PushNotifications"", ""value"": ""ToastEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer"", ""value"": ""DisableNotificationCenter"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings"", ""value"": ""NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings"", ""value"": ""NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings\\QuietHours"", ""value"": ""Enabled"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""activity-feed-off"",
      ""name"": ""Vypnout Activity Feed / Timeline"",
      ""description"": ""Vypne historii aktivit a její sync."",
      ""category"": ""Privacy"",
      ""badges"": [""SOUKROMÍ""],
      ""on"": {
        ""description"": ""EnableActivityFeed=0, PublishUserActivities=0, UploadUserActivities=0, EnableFeeds=0, AllowNewsAndInterests=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\System"", ""value"": ""EnableActivityFeed"",    ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\System"", ""value"": ""PublishUserActivities"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\System"", ""value"": ""UploadUserActivities"",  ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Feeds"", ""value"": ""EnableFeeds"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft"", ""value"": ""AllowNewsAndInterests"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí všechny na 1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\System"", ""value"": ""EnableActivityFeed"",    ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\System"", ""value"": ""PublishUserActivities"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\System"", ""value"": ""UploadUserActivities"",  ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""insiders-experiments-off"",
      ""name"": ""Vypnout Windows Insider Experiments"",
      ""description"": ""Vypne experimentální funkce Windows A/B testování."",
      ""category"": ""Privacy"",
      ""badges"": [""SOUKROMÍ""],
      ""on"": {
        ""description"": ""AllowExperimentation=0 v HKLM policy i default."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\PolicyManager\\current\\device\\System"", ""value"": ""AllowExperimentation"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\PolicyManager\\default\\System\\AllowExperimentation"", ""value"": ""value"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí AllowExperimentation=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\PolicyManager\\current\\device\\System"", ""value"": ""AllowExperimentation"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\PolicyManager\\default\\System\\AllowExperimentation"", ""value"": ""value"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""bing-search-off"",
      ""name"": ""Vypnout Bing a webové výsledky ve Start"",
      ""description"": ""Vypne webové vyhledávání v Start menu a zvýraznění ve vyhledávání."",
      ""category"": ""Privacy"",
      ""badges"": [""SOUKROMÍ""],
      ""on"": {
        ""description"": ""BingSearchEnabled=0, CortanaConsent=0, IsDynamicSearchBoxEnabled=0, DisableSearchBoxSuggestions=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Search"", ""value"": ""BingSearchEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Search"", ""value"": ""CortanaConsent"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\SearchSettings"", ""value"": ""IsDynamicSearchBoxEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Policies\\Microsoft\\Windows\\Explorer"", ""value"": ""DisableSearchBoxSuggestions"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí BingSearchEnabled=1 a ostatní odstraní."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Search"", ""value"": ""BingSearchEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""Software\\Policies\\Microsoft\\Windows\\Explorer"", ""value"": ""DisableSearchBoxSuggestions"" }
        ]
      }
    },
    {
      ""id"": ""autoplay-autorun-off"",
      ""name"": ""Vypnout Autoplay a AutoRun"",
      ""description"": ""Vypne automatické přehrávání médií a AutoRun."",
      ""category"": ""Privacy"",
      ""badges"": [""SOUKROMÍ""],
      ""on"": {
        ""description"": ""DisableAutoplay=1, NoDriveTypeAutoRun=255."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\AutoplayHandlers"", ""value"": ""DisableAutoplay"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer"", ""value"": ""NoDriveTypeAutoRun"", ""regType"": ""REG_DWORD"", ""data"": 255 }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní obě hodnoty."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\AutoplayHandlers"", ""value"": ""DisableAutoplay"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer"", ""value"": ""NoDriveTypeAutoRun"" }
        ]
      }
    },
    {
      ""id"": ""edge-chrome-tuning"",
      ""name"": ""Vypnout Edge/Chrome prelaunch, background a metriky"",
      ""description"": ""Sjednocený tweak pro Edge a Chrome: vypne prelaunch, běh na pozadí a metriky, zapne sleeping tabs."",
      ""category"": ""Windows"",
      ""badges"": [""APLIKACE""],
      ""on"": {
        ""description"": ""Edge: AllowPrelaunch=0, BackgroundModeEnabled=0, SleepingTabsEnabled=1. Chrome: BackgroundModeEnabled=0, MetricsReportingEnabled=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Edge\\Main"", ""value"": ""AllowPrelaunch"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Edge"", ""value"": ""BackgroundModeEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Edge"", ""value"": ""SleepingTabsEnabled"",   ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Google\\Chrome"", ""value"": ""BackgroundModeEnabled"",   ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Google\\Chrome"", ""value"": ""MetricsReportingEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní všechny zásady Edge/Chrome."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Edge\\Main"", ""value"": ""AllowPrelaunch"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Edge"", ""value"": ""BackgroundModeEnabled"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Edge"", ""value"": ""SleepingTabsEnabled"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Google\\Chrome"", ""value"": ""BackgroundModeEnabled"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Google\\Chrome"", ""value"": ""MetricsReportingEnabled"" }
        ]
      }
    },
    {
      ""id"": ""location-access-off"",
      ""name"": ""Zakázat aplikacím přístup k poloze"",
      ""description"": ""Odepře aplikacím přístup k poloze."",
      ""category"": ""Privacy"",
      ""badges"": [""POZOR""],
      ""on"": {
        ""description"": ""LetAppsAccessLocation=2 (Force Deny)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy"", ""value"": ""LetAppsAccessLocation"", ""regType"": ""REG_DWORD"", ""data"": 2 }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní zásadu (Let user decide)."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy"", ""value"": ""LetAppsAccessLocation"" }
        ]
      }
    },

    
    {
      ""id"": ""tcp-tuning"",
      ""name"": ""Kompletní TCP/IP tuning (netsh + PowerShell + registry)"",
      ""description"": ""Aplikuje sadu TCP optimalizací: DNS priorita, MaxUserPort, TTL, CTCP, MTU 1500, TCP nastavení a vypnutí heuristik."",
      ""category"": ""Network"",
      ""badges"": [""POKROČILÉ"", ""SÍŤ""],
      ""on"": {
        ""description"": ""Nastaví DnsPriority, LocalPriority, HostsPriority, NetbtPriority, MaxUserPort=65534, TcpTimedWaitDelay=30, DefaultTTL=64, ctcp congestion provider, MTU 1500, TCPNoDelay/AckFrequency, heuristics off."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\ServiceProvider"", ""value"": ""DnsPriority"",   ""regType"": ""REG_DWORD"", ""data"": 6 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\ServiceProvider"", ""value"": ""LocalPriority"", ""regType"": ""REG_DWORD"", ""data"": 4 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\ServiceProvider"", ""value"": ""HostsPriority"", ""regType"": ""REG_DWORD"", ""data"": 5 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\ServiceProvider"", ""value"": ""NetbtPriority"", ""regType"": ""REG_DWORD"", ""data"": 7 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters"", ""value"": ""MaxUserPort"",       ""regType"": ""REG_DWORD"", ""data"": 65534 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters"", ""value"": ""TcpTimedWaitDelay"", ""regType"": ""REG_DWORD"", ""data"": 30 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters"", ""value"": ""DefaultTTL"",        ""regType"": ""REG_DWORD"", ""data"": 64 },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set supplemental internet congestionprovider=ctcp"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set heuristics Disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global chimney=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global dca=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global netdma=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global rsc=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global maxsynretransmissions=2"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global timestamps=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global ecncapability=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh interface ipv4 set subinterface \""Wi-Fi\"" mtu=1500 store=persistent"" },
          { ""type"": ""netsh"", ""command"": ""netsh interface ipv6 set subinterface \""Wi-Fi\"" mtu=1500 store=persistent"" },
          { ""type"": ""netsh"", ""command"": ""netsh interface ipv4 set subinterface \""Ethernet\"" mtu=1500 store=persistent"" },
          { ""type"": ""netsh"", ""command"": ""netsh interface ipv6 set subinterface \""Ethernet\"" mtu=1500 store=persistent"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Set-NetTcpSetting -SettingName internet -EcnCapability enabled"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Set-NetTcpSetting -SettingName internet -Timestamps enabled"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Set-NetTcpSetting -SettingName internet -MaxSynRetransmissions 2"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Set-NetTcpSetting -SettingName internet -NonSackRttResiliency disabled"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Set-NetTcpSetting -SettingName internet -InitialRto 2000"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Set-NetTcpSetting -SettingName internet -MinRto 300"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Set-NetTCPSetting -SettingName internet -ScalingHeuristics disabled"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Set-NetOffloadGlobalSetting -ReceiveSegmentCoalescing disabled"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Set-NetOffloadGlobalSetting -ReceiveSideScaling disabled"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Set-NetOffloadGlobalSetting -Chimney disabled"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Disable-NetAdapterLso -Name *"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Disable-NetAdapterChecksumOffload -Name *"" },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile"", ""value"": ""NetworkThrottlingIndex"", ""regType"": ""REG_DWORD"", ""data"": 4294967295 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí výchozí hodnoty (CTCP=default, MaxUserPort odstraněn, heuristiky zapnuté, offloady povolené, MTU standard)."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\ServiceProvider"", ""value"": ""DnsPriority"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\ServiceProvider"", ""value"": ""LocalPriority"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\ServiceProvider"", ""value"": ""HostsPriority"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\ServiceProvider"", ""value"": ""NetbtPriority"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters"", ""value"": ""MaxUserPort"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters"", ""value"": ""TcpTimedWaitDelay"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters"", ""value"": ""DefaultTTL"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set supplemental internet congestionprovider=default"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set heuristics Enabled"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Enable-NetAdapterLso -Name *"" },
          { ""type"": ""cmd"", ""command"": ""PowerShell.exe Enable-NetAdapterChecksumOffload -Name *"" }
        ]
      }
    },
    {
      ""id"": ""ipv6-disable"",
      ""name"": ""Vypnout IPv6"",
      ""description"": ""Vypne IPv6 přes registry. Může rozbít některé sítě (VPN, Xbox)."",
      ""category"": ""Network"",
      ""badges"": [""POZOR"", ""SÍŤ""],
      ""on"": {
        ""description"": ""DisabledComponents=0xFF (255) v TCPIP6\\Parameters."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\services\\TCPIP6\\Parameters"", ""value"": ""DisabledComponents"", ""regType"": ""REG_DWORD"", ""data"": 255 }
        ]
      },
      ""off"": {
        ""description"": ""DisabledComponents=0 (IPv6 plně zapnuto)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\services\\TCPIP6\\Parameters"", ""value"": ""DisabledComponents"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      }
    },
    {
      ""id"": ""active-probing-off"",
      ""name"": ""Vypnout Active Probing (NCSI)"",
      ""description"": ""Vypne testování připojení k internetu přes NCSI. Může zobrazovat offline ikonu i s funkčním netem."",
      ""category"": ""Network"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""EnableActiveProbing=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""System\\ControlSet001\\services\\NlaSvc\\Parameters\\Internet"", ""value"": ""EnableActiveProbing"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""EnableActiveProbing=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""System\\ControlSet001\\services\\NlaSvc\\Parameters\\Internet"", ""value"": ""EnableActiveProbing"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""task-offload-disable"",
      ""name"": ""Vypnout Task Offload"",
      ""description"": ""Vypne task offload v TCP stack."",
      ""category"": ""Network"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""netsh taskoffload=disabled."",
        ""operations"": [
          { ""type"": ""netsh"", ""command"": ""netsh int ip set global taskoffload=disabled"" }
        ]
      },
      ""off"": {
        ""description"": ""netsh taskoffload=enabled."",
        ""operations"": [
          { ""type"": ""netsh"", ""command"": ""netsh int ip set global taskoffload=enabled"" }
        ]
      }
    },
    {
      ""id"": ""mld-icmp-off"",
      ""name"": ""Optimalizace MLD a ICMP (naruší FiveM)"",
      ""description"": ""Vypne MLD, ICMP redirects a různá TCP rozšíření. POZOR: narušuje FiveM."",
      ""category"": ""Network"",
      ""badges"": [""POZOR"", ""FIVEM""],
      ""on"": {
        ""description"": ""Vypne dhcpmediasense, mediasenseeventlog, icmpredirects, zapne chimney/dca, vypne netdma/rsc/timestamps/ecn."",
        ""operations"": [
          { ""type"": ""netsh"", ""command"": ""netsh int ip set global dhcpmediasense=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int ip set global mediasenseeventlog=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int ip set global icmpredirects=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global chimney=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global dca=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global netdma=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global rsc=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global maxsynretransmissions=2"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global timestamps=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global ecncapability=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set security mpp=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set security profiles=disabled"" }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí vše na enabled/default."",
        ""operations"": [
          { ""type"": ""netsh"", ""command"": ""netsh int ip set global dhcpmediasense=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int ip set global mediasenseeventlog=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int ip set global icmpredirects=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global chimney=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global dca=disabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global netdma=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global rsc=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global maxsynretransmissions=5"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global timestamps=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set global ecncapability=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set security mpp=enabled"" },
          { ""type"": ""netsh"", ""command"": ""netsh int tcp set security profiles=enabled"" }
        ]
      }
    },
    {
      ""id"": ""nic-tuning"",
      ""name"": ""Optimalizace síťové karty (Ethernet)"",
      ""description"": ""Vypne úspory energie NIC, EEE, Green Ethernet, Wake-on-LAN, nastaví buffers 2048/1024."",
      ""category"": ""Network"",
      ""badges"": [""POKROČILÉ"", ""SÍŤ""],
      ""on"": {
        ""description"": ""Pro každou NIC vypne EEE, Green Ethernet, ulpmode, wake-on-lan, nastaví TransmitBuffers=2048, ReceiveBuffers=1024, RSS=1."",
        ""operations"": [
          { ""type"": ""cmd-fornic"", ""values"": [
            { ""name"": ""AdvancedEEE"",             ""regType"": ""REG_SZ"", ""data"": ""0"" },
            { ""name"": ""*EEE"",                    ""regType"": ""REG_SZ"", ""data"": ""0"" },
            { ""name"": ""EEE"",                     ""regType"": ""REG_SZ"", ""data"": ""0"" },
            { ""name"": ""EnablePME"",               ""regType"": ""REG_SZ"", ""data"": ""0"" },
            { ""name"": ""AutoPowerSaveModeEnabled"",""regType"": ""REG_SZ"", ""data"": ""0"" },
            { ""name"": ""EnableGreenEthernet"",     ""regType"": ""REG_SZ"", ""data"": ""0"" },
            { ""name"": ""EnableSavePowerNow"",      ""regType"": ""REG_SZ"", ""data"": ""0"" },
            { ""name"": ""EnablePowerManagement"",   ""regType"": ""REG_SZ"", ""data"": ""0"" },
            { ""name"": ""ReduceSpeedOnPowerDown"",  ""regType"": ""REG_SZ"", ""data"": ""0"" },
            { ""name"": ""EnableWakeOnLan"",         ""regType"": ""REG_SZ"", ""data"": ""0"" },
            { ""name"": ""*WakeOnMagicPacket"",      ""regType"": ""REG_SZ"", ""data"": ""0"" },
            { ""name"": ""TransmitBuffers"",         ""regType"": ""REG_SZ"", ""data"": ""2048"" },
            { ""name"": ""ReceiveBuffers"",          ""regType"": ""REG_SZ"", ""data"": ""1024"" },
            { ""name"": ""RSS"",                     ""regType"": ""REG_SZ"", ""data"": ""1"" },
            { ""name"": ""*FlowControl"",            ""regType"": ""REG_SZ"", ""data"": ""0"" }
          ] }
        ]
      },
      ""off"": {
        ""description"": ""Smaže všechny nastavené hodnoty a vrátí výchozí stav NIC."",
        ""operations"": [
          { ""type"": ""cmd-fornic-delete"", ""values"": [""AdvancedEEE"",""*EEE"",""EEE"",""EnablePME"",""AutoPowerSaveModeEnabled"",""EnableGreenEthernet"",""EnableSavePowerNow"",""EnablePowerManagement"",""ReduceSpeedOnPowerDown"",""EnableWakeOnLan"",""*WakeOnMagicPacket"",""TransmitBuffers"",""ReceiveBuffers"",""RSS"",""*FlowControl""] }
        ]
      }
    },
    {
      ""id"": ""ie-addons-off"",
      ""name"": ""Vypnout internetové doplňky (IE/MSHTML)"",
      ""description"": ""Vypne starší ActiveX doplňky Internet Explorer / MSHTML."",
      ""category"": ""Network"",
      ""badges"": [""SOUKROMÍ""],
      ""on"": {
        ""description"": ""Nastaví Flags=1 a Version=* pro sadu 25 CLSID v Ext\\Settings."",
        ""operations"": [
          { ""type"": ""cmd-forlist"", ""list"": [""{2933BF90-7B36-11D2-B20E-00C04F983E60}"",""{2933BF91-7B36-11D2-B20E-00C04F983E60}"",""{2933BF94-7B36-11D2-B20E-00C04F983E60}"",""{3050F819-98B5-11CF-BB82-00AA00BDCE0B}"",""{333C7BC4-460F-11D0-BC04-0080C7055A83}"",""{373984C9-B845-449B-91E7-45AC83036ADE}"",""{64AB4BB7-111E-11D1-8F79-00C04FC2FBE1}"",""{6BF52A52-394A-11D3-B153-00C04F79FAA6}"",""{884E2049-217D-11DA-B2A4-000E7BBB2B09}"",""{884E2051-217D-11DA-B2A4-000E7BBB2B09}"",""{88D96A05-F192-11D4-A65F-0040963251E5}"",""{88D96A06-F192-11D4-A65F-0040963251E5}"",""{88D96A07-F192-11D4-A65F-0040963251E5}"",""{88D96A08-F192-11D4-A65F-0040963251E5}"",""{88D96A0A-F192-11D4-A65F-0040963251E5}"",""{8E4062D9-FE1B-4B9E-AA16-5E8EEF68F48E}"",""{D2517915-48CE-4286-970F-921E881B8C5C}"",""{EE09B103-97E0-11CF-978F-00A02463E06F}"",""{F5078F32-C551-11D3-89B9-0000F81FE221}"",""{F5078F33-C551-11D3-89B9-0000F81FE221}"",""{F5078F34-C551-11D3-89B9-0000F81FE221}"",""{F5078F35-C551-11D3-89B9-0000F81FE221}"",""{F5078F36-C551-11D3-89B9-0000F81FE221}"",""{F5078F39-C551-11D3-89B9-0000F81FE221}"",""{F6D90F12-9C73-11D3-B32E-00C04F990BB4}"",""{F6D90F14-9C73-11D3-B32E-00C04F990BB4}""], ""hive"": ""HKCU"", ""basePath"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Ext\\Settings"", ""value"": ""Flags"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""Smaže Flags a Version pro všechny CLSID."",
        ""operations"": [
          { ""type"": ""cmd-forlist-delete"", ""list"": [""{2933BF90-7B36-11D2-B20E-00C04F983E60}"",""{2933BF91-7B36-11D2-B20E-00C04F983E60}"",""{2933BF94-7B36-11D2-B20E-00C04F983E60}"",""{3050F819-98B5-11CF-BB82-00AA00BDCE0B}"",""{333C7BC4-460F-11D0-BC04-0080C7055A83}"",""{373984C9-B845-449B-91E7-45AC83036ADE}"",""{64AB4BB7-111E-11D1-8F79-00C04FC2FBE1}"",""{6BF52A52-394A-11D3-B153-00C04F79FAA6}"",""{884E2049-217D-11DA-B2A4-000E7BBB2B09}"",""{884E2051-217D-11DA-B2A4-000E7BBB2B09}"",""{88D96A05-F192-11D4-A65F-0040963251E5}"",""{88D96A06-F192-11D4-A65F-0040963251E5}"",""{88D96A07-F192-11D4-A65F-0040963251E5}"",""{88D96A08-F192-11D4-A65F-0040963251E5}"",""{88D96A0A-F192-11D4-A65F-0040963251E5}"",""{8E4062D9-FE1B-4B9E-AA16-5E8EEF68F48E}"",""{D2517915-48CE-4286-970F-921E881B8C5C}"",""{EE09B103-97E0-11CF-978F-00A02463E06F}"",""{F5078F32-C551-11D3-89B9-0000F81FE221}"",""{F5078F33-C551-11D3-89B9-0000F81FE221}"",""{F5078F34-C551-11D3-89B9-0000F81FE221}"",""{F5078F35-C551-11D3-89B9-0000F81FE221}"",""{F5078F36-C551-11D3-89B9-0000F81FE221}"",""{F5078F39-C551-11D3-89B9-0000F81FE221}"",""{F6D90F12-9C73-11D3-B32E-00C04F990BB4}"",""{F6D90F14-9C73-11D3-B32E-00C04F990BB4}""], ""hive"": ""HKCU"", ""basePath"": ""Software\\Microsoft\\Windows\\CurrentVersion\\Ext\\Settings"" }
        ]
      }
    },
    {
      ""id"": ""qos-policy"",
      ""name"": ""QoS politika pro hru/aplikaci"",
      ""description"": ""Vytvoří QoS politiku s DSCP=46 pro vybraný .exe soubor. Vyžaduje ruční zadání názvu profilu a .exe."",
      ""category"": ""Network"",
      ""badges"": [""POKROČILÉ"", ""FPS""],
      ""implementation"": ""existing-csharp"",
      ""on"": {
        ""description"": ""Uživatel zadá název profilu a .exe. Vytvoří registry pod Policies\\Microsoft\\Windows\\QoS\\<profil>."",
        ""operations"": []
      },
      ""off"": {
        ""description"": ""Smaže vytvořený profil."",
        ""operations"": []
      }
    },

    
    {
      ""id"": ""power-plan-ultimate"",
      ""name"": ""Ultimate / High Performance power plan"",
      ""description"": ""Aktivuje nebo duplikuje plán Vysoký výkon / Ultimate Power Plan."",
      ""category"": ""Power"",
      ""badges"": [""POKROČILÉ"", ""VÝKON""],
      ""on"": {
        ""description"": ""Duplikuje Ultimate schéma (e9a42b02...) nebo High Performance (8c5e7fda...), přejmenuje na 'Ultimate Power Plan' a aktivuje."",
        ""operations"": [
          { ""type"": ""cmd"", ""command"": ""for /f \""tokens=3\"" %%G in ('powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 ^| findstr /i \""Power Scheme GUID\""') do powercfg -changename %%G \""Ultimate Power Plan\"""" }
        ]
      },
      ""off"": {
        ""description"": ""Obnoví výchozí plány: powercfg -restoredefaultschemes."",
        ""operations"": [
          { ""type"": ""powercfg"", ""command"": ""powercfg -restoredefaultschemes"" }
        ]
      }
    },
    {
      ""id"": ""power-throttling-off"",
      ""name"": ""Vypnout Power Throttling"",
      ""description"": ""Vypne Windows Power Throttling pro všechny procesy."",
      ""category"": ""Power"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""PowerThrottlingOff=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\PowerThrottling"", ""value"": ""PowerThrottlingOff"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""PowerThrottlingOff=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\PowerThrottling"", ""value"": ""PowerThrottlingOff"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      }
    },
    {
      ""id"": ""coalescing-timer-off"",
      ""name"": ""Vypnout Coalescing Timer Interval"",
      ""description"": ""Vypne timer coalescing pro nižší latenci."",
      ""category"": ""Power"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""CoalescingTimerInterval=0 v 7 cestách (Session Manager, Power, Executive, kernel, atd.)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\kernel"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Executive"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\ModernSleep"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vše na 1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\kernel"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Executive"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\ModernSleep"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""CoalescingTimerInterval"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""energy-estimation-off"",
      ""name"": ""Vypnout Power Telemetry a Energy Estimation"",
      ""description"": ""Vypne záznamy spotřeby energie a telemetrii."",
      ""category"": ""Power"",
      ""badges"": [""SOUKROMÍ""],
      ""on"": {
        ""description"": ""DisableTaggedEnergyLogging=1, TelemetryMaxApplication=0, TelemetryMaxTagPerApplication=0, EnergyEstimationEnabled=0, EventProcessorEnabled=0, CsEnabled=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\TaggedEnergy"", ""value"": ""DisableTaggedEnergyLogging"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\TaggedEnergy"", ""value"": ""TelemetryMaxApplication"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\TaggedEnergy"", ""value"": ""TelemetryMaxTagPerApplication"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""EnergyEstimationEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""EventProcessorEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""CsEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vše na 1 (nebo 0 pro logging disable)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\TaggedEnergy"", ""value"": ""DisableTaggedEnergyLogging"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\TaggedEnergy"", ""value"": ""TelemetryMaxApplication"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\TaggedEnergy"", ""value"": ""TelemetryMaxTagPerApplication"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""EnergyEstimationEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""EventProcessorEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""CsEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""fast-startup-off"",
      ""name"": ""Vypnout Fast Startup (rychlý start)"",
      ""description"": ""Vypne hybridní rychlý start. Doporučeno pro dual-boot a stabilitu."",
      ""category"": ""Power"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""HiberbootEnabled=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power"", ""value"": ""HiberbootEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""HiberbootEnabled=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power"", ""value"": ""HiberbootEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""hibernation-off"",
      ""name"": ""Vypnout hibernaci"",
      ""description"": ""Vypne hibernaci a smaže hiberfil.sys. Uvolní místo na disku."",
      ""category"": ""Power"",
      ""badges"": [""POKROČILÉ"", ""DISK""],
      ""on"": {
        ""description"": ""powercfg /h off a HibernateEnabled=0."",
        ""operations"": [
          { ""type"": ""powercfg"", ""command"": ""powercfg /h off"" },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""HibernateEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""powercfg /h on a HibernateEnabled=1."",
        ""operations"": [
          { ""type"": ""powercfg"", ""command"": ""powercfg /h on"" },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""HibernateEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""sleep-study-off"",
      ""name"": ""Vypnout Sleep Study a Modern Standby"",
      ""description"": ""Vypne diagnostiku spánku a Modern Standby."",
      ""category"": ""Power"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""SleepStudyDisabled=1, PlatformAoAcOverride=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power"", ""value"": ""SleepStudyDisabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""PlatformAoAcOverride"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""SleepStudyDisabled=0, PlatformAoAcOverride=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power"", ""value"": ""SleepStudyDisabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""PlatformAoAcOverride"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""storage-power-savings-off"",
      ""name"": ""Vypnout úspory energie pro úložiště (HIPM/DIPM/HDD Parking/StorPort)"",
      ""description"": ""Vypne HDD parkování, HIPM/DIPM a StorPort idle napříč systémem."",
      ""category"": ""Power"",
      ""badges"": [""POKROČILÉ"", ""DISK""],
      ""on"": {
        ""description"": ""Pro StorPort nastaví EnableIdlePowerManagement=0, pro HIPM/DIPM/HDD Parking hodnoty=0."",
        ""operations"": [
          { ""type"": ""cmd-forstorport"", ""value"": ""EnableIdlePowerManagement"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""cmd-forservice"", ""names"": [""EnableHIPM"",""EnableDIPM"",""EnableHDDParking""], ""value"": null, ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí EnableIdlePowerManagement=1 a HIPM/DIPM/HDD Parking=1."",
        ""operations"": [
          { ""type"": ""cmd-forstorport"", ""value"": ""EnableIdlePowerManagement"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""cmd-forservice"", ""names"": [""EnableHIPM"",""EnableDIPM"",""EnableHDDParking""], ""value"": null, ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },

    
    {
      ""id"": ""bcd-timers"",
      ""name"": ""BCD timery (dynamictick, platformtick, HPET)"",
      ""description"": ""Nastaví BCD časovače pro nižší latenci."",
      ""category"": ""CPU"",
      ""badges"": [""POKROČILÉ"", ""RESTART""],
      ""on"": {
        ""description"": ""Disabledynamictick=yes, useplatformtick=yes, smaže useplatformclock."",
        ""operations"": [
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /set Disabledynamictick yes"" },
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /set useplatformtick yes"" },
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /deletevalue useplatformclock"" }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí BCD na výchozí."",
        ""operations"": [
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /deletevalue Disabledynamictick"" },
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /deletevalue useplatformtick"" }
        ]
      }
    },
    {
      ""id"": ""bcd-memory-mapping"",
      ""name"": ""BCD Memory Mapping"",
      ""description"": ""Nastaví configaccesspolicy, MSI, physical destination pro kernel."",
      ""category"": ""CPU"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""configaccesspolicy=Default, MSI=Default, usephysicaldestination=No, usefirmwarepcisettings=No."",
        ""operations"": [
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /set configaccesspolicy Default"" },
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /set MSI Default"" },
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /set usephysicaldestination No"" },
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /set usefirmwarepcisettings No"" }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní BCD hodnoty."",
        ""operations"": [
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /deletevalue configaccesspolicy"" },
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /deletevalue MSI"" },
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /deletevalue usephysicaldestination"" },
          { ""type"": ""bcdedit"", ""command"": ""bcdedit /deletevalue usefirmwarepcisettings"" }
        ]
      }
    },
    {
      ""id"": ""cpu-core-parking-off"",
      ""name"": ""Vypnout CPU Core Parking"",
      ""description"": ""Nastaví minimální počet aktivních jader na 100% (všechna jádra stále aktivní)."",
      ""category"": ""CPU"",
      ""badges"": [""POKROČILÉ"", ""CPU""],
      ""on"": {
        ""description"": ""CPMINCORES=100, CoreParkingDisabled=1."",
        ""operations"": [
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_processor CPMINCORES 100"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg /setactive SCHEME_CURRENT"" },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""CoreParkingDisabled"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""CPMINCORES=10, CoreParkingDisabled=0."",
        ""operations"": [
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_processor CPMINCORES 10"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg /setactive SCHEME_CURRENT"" },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""CoreParkingDisabled"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      }
    },
    {
      ""id"": ""cpu-power-savings-off"",
      ""name"": ""Vypnout CPU úspory (hybrid sleep, standby, idle scaling, throttling)"",
      ""description"": ""Kompletní vypnutí CPU power saving funkcí."",
      ""category"": ""CPU"",
      ""badges"": [""POKROČILÉ"", ""TEPLOTA""],
      ""on"": {
        ""description"": ""hybridsleep=0, standbyidle=0, PROCTHROTTLEMIN=100, IDLESCALING=1, THROTTLING=0."",
        ""operations"": [
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_sleep hybridsleep 0"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_sleep standbyidle 0"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_processor PROCTHROTTLEMIN 100"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_processor IDLESCALING 1"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_processor THROTTLING 0"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg /setactive SCHEME_CURRENT"" }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí všechny hodnoty na výchozí (1,1,0,0,1)."",
        ""operations"": [
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_sleep hybridsleep 1"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_sleep standbyidle 1"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_processor PROCTHROTTLEMIN 0"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_processor IDLESCALING 0"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg -setacvalueindex scheme_current sub_processor THROTTLING 1"" },
          { ""type"": ""powercfg"", ""command"": ""powercfg /setactive SCHEME_CURRENT"" }
        ]
      }
    },
    {
      ""id"": ""system-responsiveness"",
      ""name"": ""Systémová odezva a priority procesů"",
      ""description"": ""Win32Priority, SystemResponsiveness, IO priority, timeouts. Sloučení několika tweaků."",
      ""category"": ""CPU"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""Win32PrioritySeparation=38, SystemResponsiveness=10, IoPriority=3, zkrátí timeouts (1000 ms)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\PriorityControl"", ""value"": ""Win32PrioritySeparation"", ""regType"": ""REG_DWORD"", ""data"": 38 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile"", ""value"": ""SystemResponsiveness"", ""regType"": ""REG_DWORD"", ""data"": 10 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Reliability"", ""value"": ""IoPriority"", ""regType"": ""REG_DWORD"", ""data"": 3 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Desktop"", ""value"": ""AutoEndTasks"",           ""regType"": ""REG_SZ"", ""data"": ""1"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Desktop"", ""value"": ""HungAppTimeout"",         ""regType"": ""REG_SZ"", ""data"": ""1000"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Desktop"", ""value"": ""WaitToKillAppTimeout"",   ""regType"": ""REG_SZ"", ""data"": ""1000"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Desktop"", ""value"": ""LowLevelHooksTimeout"",   ""regType"": ""REG_SZ"", ""data"": ""1000"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Desktop"", ""value"": ""MenuShowDelay"",          ""regType"": ""REG_SZ"", ""data"": ""0"" },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control"", ""value"": ""WaitToKillServiceTimeout"", ""regType"": ""REG_SZ"", ""data"": ""1000"" }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí Win32PrioritySeparation=10, SystemResponsiveness=14, IoPriority=1, timeouts 5000/20000."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\PriorityControl"", ""value"": ""Win32PrioritySeparation"", ""regType"": ""REG_DWORD"", ""data"": 10 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile"", ""value"": ""SystemResponsiveness"", ""regType"": ""REG_DWORD"", ""data"": 14 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Reliability"", ""value"": ""IoPriority"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Desktop"", ""value"": ""WaitToKillAppTimeout"", ""regType"": ""REG_SZ"", ""data"": ""5000"" },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""Control Panel\\Desktop"", ""value"": ""MenuShowDelay"", ""regType"": ""REG_SZ"", ""data"": ""400"" }
        ]
      }
    },
    {
      ""id"": ""latency-tolerance"",
      ""name"": ""Latency Tolerance (Power + DXGKrnl + GraphicsDrivers)"",
      ""description"": ""Nastaví všechna latency tolerance na 1 pro minimální zpoždění."",
      ""category"": ""CPU"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""Více než 30 hodnot v Power, GraphicsDrivers\\Power a DXGKrnl nastavených na 1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\DXGKrnl"", ""value"": ""MonitorLatencyTolerance"",        ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\DXGKrnl"", ""value"": ""MonitorRefreshLatencyTolerance"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""ExitLatency"",                 ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""ExitLatencyCheckEnabled"",     ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""Latency"",                     ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""LatencyToleranceDefault"",     ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""LatencyToleranceFSVP"",        ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""LatencyTolerancePerfOverride"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""LatencyToleranceScreenOffIR"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""LatencyToleranceVSyncEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""RtlCapabilityCheckLatency"",  ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers\\Power"", ""value"": ""DefaultD3TransitionLatencyActivelyUsed"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers\\Power"", ""value"": ""Latency"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní všechny Latency hodnoty (Windows default)."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\DXGKrnl"", ""value"": ""MonitorLatencyTolerance"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\DXGKrnl"", ""value"": ""MonitorRefreshLatencyTolerance"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""ExitLatency"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""Latency"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power"", ""value"": ""LatencyToleranceDefault"" }
        ]
      }
    },

    
    {
      ""id"": ""nvidia-tuning"",
      ""name"": ""NVIDIA kompletní tuning"",
      ""description"": ""NVIDIA Latency Tolerance, DMA remapping off, logování off, HDCP off, telemetry off. Sloučený tweak."",
      ""category"": ""GPU"",
      ""badges"": [""POKROČILÉ"", ""NVIDIA""],
      ""on"": {
        ""description"": ""Nastaví 20+ hodnot pro NVIDIA driver, vypne DmaRemapping, logování, telemetrii, HDCP."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\nvlddmkm\\Parameters"", ""value"": ""DmaRemappingCompatible"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\ControlSet001\\Services\\nvlddmkm"", ""value"": ""LogWarningEntries"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\ControlSet001\\Services\\nvlddmkm"", ""value"": ""LogPagingEntries"",  ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\ControlSet001\\Services\\nvlddmkm"", ""value"": ""LogEventEntries"",   ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\ControlSet001\\Services\\nvlddmkm"", ""value"": ""LogErrorEntries"",   ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""PreferSystemMemoryContiguous"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""D3PCLatency"",             ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""F1TransitionLatency"",     ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""LOWLATENCY"",              ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""Node3DLowLatency"",        ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""PciLatencyTimerControl"",  ""regType"": ""REG_DWORD"", ""data"": 32 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""RMDeepL1EntryLatencyUsec"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""RMLpwrEiIdleThresholdUs"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí hodnoty na 0 nebo smaže zásahy."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\nvlddmkm\\Parameters"", ""value"": ""DmaRemappingCompatible"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""D3PCLatency"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""LOWLATENCY"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""Node3DLowLatency"" }
        ]
      }
    },
    {
      ""id"": ""amd-gpu-tuning"",
      ""name"": ""AMD GPU tuning"",
      ""description"": ""Vypne AMD power gating, ULPS, ASPM, Radeon Chill/Boost/Anti-Lag, telemetrii a logging."",
      ""category"": ""GPU"",
      ""badges"": [""POKROČILÉ"", ""AMD""],
      ""on"": {
        ""description"": ""Vypne 15+ power saving funkcí AMD GPU."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\AMD Telemetry Service"", ""value"": ""Start"", ""regType"": ""REG_DWORD"", ""data"": 4 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\AMD Crash Defender Service"", ""value"": ""Start"", ""regType"": ""REG_DWORD"", ""data"": 4 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\AMD External Events Utility"", ""value"": ""Start"", ""regType"": ""REG_DWORD"", ""data"": 4 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Services\\amdwddmg"", ""value"": ""ChillEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""KMD_DeLagEnabled"",          ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""KMD_RadeonBoostEnabled"",   ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""PP_SclkDeepSleepDisable"",  ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""PP_ThermalAutoThrottlingEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""DisableSAMUPowerGating"",     ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""DisableUVDPowerGatingDynamic"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""DisableVCEPowerGating"",     ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""DisablePowerGating"",        ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""EnableUlps"",                ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""EnableAspmL0s"",             ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""EnableAspmL1"",              ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí power gating, ULPS, ASPM na 1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""DisableSAMUPowerGating"",   ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""EnableUlps"",              ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""EnableAspmL0s"",           ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000"", ""value"": ""EnableAspmL1"",            ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""intel-gpu-tuning"",
      ""name"": ""Intel GPU tuning"",
      ""description"": ""Vypne overlay quality enhancement, zapne fixed segment, nastaví dedicated segment 512 MB."",
      ""category"": ""GPU"",
      ""badges"": [""POKROČILÉ"", ""INTEL""],
      ""on"": {
        ""description"": ""Pro každý Intel GPU: Disable_OverlayDSQualityEnhancement=1, IncreaseFixedSegment=1, ACPowerPolicyVersion=16898, DCPowerPolicyVersion=16642, DedicatedSegmentSize=512."",
        ""operations"": [
          { ""type"": ""cmd-forintelgpu"", ""values"": [
            { ""name"": ""Disable_OverlayDSQualityEnhancement"", ""regType"": ""REG_DWORD"", ""data"": 1 },
            { ""name"": ""IncreaseFixedSegment"",                 ""regType"": ""REG_DWORD"", ""data"": 1 },
            { ""name"": ""AdaptiveVbEnabled"",                    ""regType"": ""REG_DWORD"", ""data"": 0 },
            { ""name"": ""DisablePFonDP"",                        ""regType"": ""REG_DWORD"", ""data"": 1 },
            { ""name"": ""EnableCompensationForDVI"",             ""regType"": ""REG_DWORD"", ""data"": 1 },
            { ""name"": ""ACPowerPolicyVersion"",                 ""regType"": ""REG_DWORD"", ""data"": 16898 },
            { ""name"": ""DCPowerPolicyVersion"",                 ""regType"": ""REG_DWORD"", ""data"": 16642 }
          ] },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""Software\\Intel\\GMM"", ""value"": ""DedicatedSegmentSize"", ""regType"": ""REG_DWORD"", ""data"": 512 }
        ]
      },
      ""off"": {
        ""description"": ""Vrátí Disable_OverlayDSQualityEnhancement=0, IncreaseFixedSegment=0, ostatní na výchozí."",
        ""operations"": [
          { ""type"": ""cmd-forintelgpu"", ""values"": [
            { ""name"": ""Disable_OverlayDSQualityEnhancement"", ""regType"": ""REG_DWORD"", ""data"": 0 },
            { ""name"": ""IncreaseFixedSegment"",                 ""regType"": ""REG_DWORD"", ""data"": 0 }
          ] }
        ]
      }
    },
    {
      ""id"": ""gpu-driver-search-off"",
      ""name"": ""Vypnout automatické hledání GPU ovladačů"",
      ""description"": ""Zabrání Windows Update instalovat ovladače GPU automaticky."",
      ""category"": ""GPU"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""SearchOrderConfig=0, ExcludeWUDrivers=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DriverSearching"", ""value"": ""SearchOrderConfig"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\WindowsUpdate\\UpdatePolicy\\PolicyState"", ""value"": ""ExcludeWUDrivers"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""SearchOrderConfig=1, smaže ExcludeWUDrivers."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DriverSearching"", ""value"": ""SearchOrderConfig"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\WindowsUpdate\\UpdatePolicy\\PolicyState"", ""value"": ""ExcludeWUDrivers"" }
        ]
      }
    },
    {
      ""id"": ""vrr-optimizations-off"",
      ""name"": ""Vypnout VRR optimalizace DirectX"",
      ""description"": ""Vypne VRROptimizeEnable v DirectXUserGlobalSettings."",
      ""category"": ""GPU"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""DirectXUserGlobalSettings=\""VRROptimizeEnable=0;SwapEffectUpgradeEnable=1;\""."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\DirectX\\UserGpuPreferences"", ""value"": ""DirectXUserGlobalSettings"", ""regType"": ""REG_SZ"", ""data"": ""VRROptimizeEnable=0;SwapEffectUpgradeEnable=1;"" }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní hodnotu."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""SOFTWARE\\Microsoft\\DirectX\\UserGpuPreferences"", ""value"": ""DirectXUserGlobalSettings"" }
        ]
      }
    },
    {
      ""id"": ""dwm-independent-flip"",
      ""name"": ""DWM Independent Flip"",
      ""description"": ""Vypne OverlayTestMode a zapne Independent Flip."",
      ""category"": ""GPU"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""Smaže OverlayTestMode v HKLM\\SOFTWARE\\Microsoft\\Windows\\Dwm."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\Dwm"", ""value"": ""OverlayTestMode"" }
        ]
      },
      ""off"": {
        ""description"": ""Obnoví OverlayTestMode=5 (výchozí)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\Dwm"", ""value"": ""OverlayTestMode"", ""regType"": ""REG_DWORD"", ""data"": 5 }
        ]
      }
    },
    {
      ""id"": ""fso-enable"",
      ""name"": ""Povolit Fullscreen Exclusive (FSO)"",
      ""description"": ""Povolí vynucení fullscreen exclusive módu přes GameConfigStore."",
      ""category"": ""Gaming"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""Všechny GameDVR_FSEBehavior* hodnoty na 0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SYSTEM\\GameConfigStore"", ""value"": ""GameDVR_DSEBehavior"",                 ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SYSTEM\\GameConfigStore"", ""value"": ""GameDVR_FSEBehaviorMode"",             ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SYSTEM\\GameConfigStore"", ""value"": ""GameDVR_EFSEFeatureFlags"",            ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SYSTEM\\GameConfigStore"", ""value"": ""GameDVR_DXGIHonorFSEWindowsCompatible"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKCU"", ""path"": ""SYSTEM\\GameConfigStore"", ""value"": ""GameDVR_HonorUserFSEBehaviorMode"",    ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní hodnoty (Windows default)."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""SYSTEM\\GameConfigStore"", ""value"": ""GameDVR_FSEBehaviorMode"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKCU"", ""path"": ""SYSTEM\\GameConfigStore"", ""value"": ""GameDVR_HonorUserFSEBehaviorMode"" }
        ]
      }
    },

    
    {
      ""id"": ""ntfs-tuning"",
      ""name"": ""NTFS tuning (memory usage, MFT zone, delete notify, encrypt)"",
      ""description"": ""Optimalizace NTFS: větší MFT zone, memory usage=2, zapnutá delete notify."",
      ""category"": ""Storage"",
      ""badges"": [""POKROČILÉ"", ""DISK""],
      ""on"": {
        ""description"": ""fsutil behavior: memoryusage=2, mftzone=4, Disabledeletenotify=1, encryptpagingfile=0."",
        ""operations"": [
          { ""type"": ""fsutil"", ""command"": ""fsutil behavior set memoryusage 2"" },
          { ""type"": ""fsutil"", ""command"": ""fsutil behavior set mftzone 4"" },
          { ""type"": ""fsutil"", ""command"": ""fsutil behavior set Disabledeletenotify 1"" },
          { ""type"": ""fsutil"", ""command"": ""fsutil behavior set encryptpagingfile 0"" }
        ]
      },
      ""off"": {
        ""description"": ""fsutil behavior: memoryusage=1, mftzone=1."",
        ""operations"": [
          { ""type"": ""fsutil"", ""command"": ""fsutil behavior set memoryusage 1"" },
          { ""type"": ""fsutil"", ""command"": ""fsutil behavior set mftzone 1"" }
        ]
      }
    },
    {
      ""id"": ""write-cache-disable-flush"",
      ""name"": ""Zakázat vyprázdnění vyrovnávací paměti zápisu"",
      ""description"": ""Zakáže flush cache – pozor na výpadky napájení (riziko ztráty dat)."",
      ""category"": ""Storage"",
      ""badges"": [""POZOR"", ""DISK""],
      ""on"": {
        ""description"": ""Pro všechny disky: UserWriteCacheSetting=1, CacheIsPowerProtected=1."",
        ""operations"": [
          { ""type"": ""cmd-forstorage"", ""values"": [
            { ""name"": ""UserWriteCacheSetting"", ""regType"": ""REG_DWORD"", ""data"": 1 },
            { ""name"": ""CacheIsPowerProtected"", ""regType"": ""REG_DWORD"", ""data"": 1 }
          ] }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní obě hodnoty (Windows default)."",
        ""operations"": [
          { ""type"": ""cmd-forstorage-delete"", ""values"": [""UserWriteCacheSetting"",""CacheIsPowerProtected""] }
        ]
      }
    },
    {
      ""id"": ""last-access-off"",
      ""name"": ""Vypnout Last Access Time"",
      ""description"": ""Vypne sledování času posledního přístupu k souborům."",
      ""category"": ""Storage"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""fsutil behavior set disableLastAccess 1."",
        ""operations"": [
          { ""type"": ""fsutil"", ""command"": ""fsutil behavior set disableLastAccess 1"" }
        ]
      },
      ""off"": {
        ""description"": ""fsutil behavior set disableLastAccess 0."",
        ""operations"": [
          { ""type"": ""fsutil"", ""command"": ""fsutil behavior set disableLastAccess 0"" }
        ]
      }
    },
    {
      ""id"": ""8dot3-disable"",
      ""name"": ""Vypnout 8.3 krátké názvy souborů"",
      ""description"": ""Vypne generování krátkých názvů souborů (DOS kompatibilita)."",
      ""category"": ""Storage"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""fsutil behavior set disable8dot3 1."",
        ""operations"": [
          { ""type"": ""fsutil"", ""command"": ""fsutil behavior set disable8dot3 1"" }
        ]
      },
      ""off"": {
        ""description"": ""fsutil behavior set disable8dot3 0."",
        ""operations"": [
          { ""type"": ""fsutil"", ""command"": ""fsutil behavior set disable8dot3 0"" }
        ]
      }
    },
    {
      ""id"": ""ssd-power-savings-off"",
      ""name"": ""Vypnout SSD/HDD úspory energie"",
      ""description"": ""Vypne úsporné stavy SSD/HDD (IdleExitEnergy) pro nižší latenci disku."",
      ""category"": ""Storage"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""Nastaví IdleExitEnergyMicroJoules, IdleExitLatencyMs, IdlePowerMw na 0, IdleTimeLengthMs na 4294967295 pro SD/SSD IdleState 1-3."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\Storage\\SSD\\IdleState\\1"", ""value"": ""IdlePowerMw"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\Storage\\SSD\\IdleState\\1"", ""value"": ""IdleTimeLengthMs"", ""regType"": ""REG_DWORD"", ""data"": 4294967295 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\Storage\\SSD\\IdleState\\2"", ""value"": ""IdlePowerMw"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\Storage\\SSD\\IdleState\\3"", ""value"": ""IdlePowerMw"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Odstraní všechny IdleState hodnoty."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\Storage\\SSD\\IdleState\\1"", ""value"": ""IdlePowerMw"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\Storage\\SSD\\IdleState\\1"", ""value"": ""IdleTimeLengthMs"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\Storage\\SSD\\IdleState\\2"", ""value"": ""IdlePowerMw"" },
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Power\\EnergyEstimation\\Storage\\SSD\\IdleState\\3"", ""value"": ""IdlePowerMw"" }
        ]
      }
    },
    {
      ""id"": ""ram-tuning"",
      ""name"": ""RAM tuning (SvcHostSplit, LargeSystemCache, Paging, Prefetch)"",
      ""description"": ""Kompletní RAM optimalizace: SvcHostSplitThreshold podle velikosti RAM, vypnutí LargeSystemCache, zapnutí/vypnutí Paging Executive, Prefetch."",
      ""category"": ""Storage"",
      ""badges"": [""POKROČILÉ""],
      ""on"": {
        ""description"": ""SvcHostSplitThreshold=16777216 (16GB), LargeSystemCache=0, DisablePagingExecutive=1, EnablePrefetcher=0, EnablePrefetch=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control"", ""value"": ""SvcHostSplitThresholdInKB"", ""regType"": ""REG_DWORD"", ""data"": 16777216 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management"", ""value"": ""LargeSystemCache"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management"", ""value"": ""DisablePagingExecutive"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters"", ""value"": ""EnablePrefetcher"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters"", ""value"": ""EnablePrefetch"",   ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers"", ""value"": ""DpiMapIommuContiguous"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      },
      ""off"": {
        ""description"": ""Obnoví výchozí hodnoty, odstraň SvcHostSplitThresholdInKB."",
        ""operations"": [
          { ""type"": ""reg-delete"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control"", ""value"": ""SvcHostSplitThresholdInKB"" },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management"", ""value"": ""DisablePagingExecutive"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters"", ""value"": ""EnablePrefetcher"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters"", ""value"": ""EnableSuperfetch"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },

    
    {
      ""id"": ""mitigations-off"",
      ""name"": ""Vypnout mitigace (CFG, SEHOP, ASLR, SmartScreen)"",
      ""description"": ""Vypne bezpečnostní mitigace pro vyšší výkon. POZOR: snižuje bezpečnost systému."",
      ""category"": ""Security"",
      ""badges"": [""POZOR"", ""VÝKON""],
      ""on"": {
        ""description"": ""DisableExceptionChainValidation=1, KernelSEHOPEnabled=0, EnableCfg=0, ProtectionMode=0, MoveImages=0, EnableSmartScreen=0, SmartScreenEnabled=Off."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\kernel"", ""value"": ""DisableExceptionChainValidation"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\kernel"", ""value"": ""KernelSEHOPEnabled"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management"", ""value"": ""EnableCfg"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager"", ""value"": ""ProtectionMode"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management"", ""value"": ""MoveImages"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\System"", ""value"": ""EnableSmartScreen"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer"", ""value"": ""SmartScreenEnabled"", ""regType"": ""REG_SZ"", ""data"": ""Off"" }
        ]
      },
      ""off"": {
        ""description"": ""Obnoví všechny mitigace na bezpečné hodnoty."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\kernel"", ""value"": ""DisableExceptionChainValidation"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\kernel"", ""value"": ""KernelSEHOPEnabled"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management"", ""value"": ""EnableCfg"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager"", ""value"": ""ProtectionMode"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management"", ""value"": ""MoveImages"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Windows\\System"", ""value"": ""EnableSmartScreen"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer"", ""value"": ""SmartScreenEnabled"", ""regType"": ""REG_SZ"", ""data"": ""On"" }
        ]
      }
    },
    {
      ""id"": ""biometrics-off"",
      ""name"": ""Vypnout biometrii (Windows Hello)"",
      ""description"": ""Vypne biometrické funkce (Windows Hello otisk/obličej)."",
      ""category"": ""Security"",
      ""badges"": [""VOLITELNÉ""],
      ""on"": {
        ""description"": ""Biometrics\\Enabled=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Biometrics"", ""value"": ""Enabled"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""Biometrics\\Enabled=1."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Policies\\Microsoft\\Biometrics"", ""value"": ""Enabled"", ""regType"": ""REG_DWORD"", ""data"": 1 }
        ]
      }
    },
    {
      ""id"": ""uac-off"",
      ""name"": ""Vypnout UAC"",
      ""description"": ""Vypne User Account Control. POZOR: snižuje bezpečnost systému."",
      ""category"": ""Security"",
      ""badges"": [""POZOR""],
      ""on"": {
        ""description"": ""EnableLUA=0, ConsentPromptBehaviorAdmin=0."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System"", ""value"": ""EnableLUA"", ""regType"": ""REG_DWORD"", ""data"": 0 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System"", ""value"": ""ConsentPromptBehaviorAdmin"", ""regType"": ""REG_DWORD"", ""data"": 0 }
        ]
      },
      ""off"": {
        ""description"": ""EnableLUA=1, ConsentPromptBehaviorAdmin=5 (výchozí)."",
        ""operations"": [
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System"", ""value"": ""EnableLUA"", ""regType"": ""REG_DWORD"", ""data"": 1 },
          { ""type"": ""reg"", ""hive"": ""HKLM"", ""path"": ""SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System"", ""value"": ""ConsentPromptBehaviorAdmin"", ""regType"": ""REG_DWORD"", ""data"": 5 }
        ]
      }
    },

    
    {
      ""id"": ""temp-cleanup"",
      ""name"": ""Vyčištění TEMP složek"",
      ""description"": ""Smaže obsah uživatelského a systémového TEMP. Vyžaduje potvrzení."",
      ""category"": ""Cleanup"",
      ""badges"": [""CLEANUP""],
      ""kind"": ""cleanup"",
      ""requiresConfirmation"": true,
      ""on"": {
        ""description"": ""Smaže %TEMP% a C:\\Windows\\Temp (zamčené soubory přeskočí)."",
        ""operations"": [
          { ""type"": ""cmd"", ""command"": ""del /q /f /s \""%TEMP%\\*\"" 2>nul & for /d %%x in (\""%TEMP%\\*\"") do @rd /s /q \""%%x\"" 2>nul"" },
          { ""type"": ""cmd"", ""command"": ""del /q /f /s \""C:\\Windows\\Temp\\*\"" 2>nul & for /d %%x in (\""C:\\Windows\\Temp\\*\"") do @rd /s /q \""%%x\"" 2>nul"" }
        ]
      },
      ""off"": {
        ""description"": ""Není relevantní (jednorázová akce, ne toggle)."",
        ""operations"": []
      }
    },
    {
      ""id"": ""pnputil-cleanup"",
      ""name"": ""Čištění zbytečných dat zařízení (pnputil)"",
      ""description"": ""Odstraní neznámá zařízení přes pnputil /remove-device."",
      ""category"": ""Cleanup"",
      ""badges"": [""POKROČILÉ""],
      ""kind"": ""cleanup"",
      ""requiresConfirmation"": true,
      ""on"": {
        ""description"": ""Projde všechna zařízení se Status Unknown a odstraní je."",
        ""operations"": [
          { ""type"": ""cmd"", ""command"": ""powershell -Command \""Get-PnpDevice | ? Status -eq Unknown | ForEach-Object { pnputil /remove-device $_.InstanceId }\"""" }
        ]
      },
      ""off"": {
        ""description"": ""Nelze vrátit (jednorázová akce)."",
        ""operations"": []
      }
    },
    {
      ""id"": ""shader-cache-cleanup"",
      ""name"": ""Vyčištění shader cache"",
      ""description"": ""Smaže DirectX/NVIDIA shader cache. Cache se časem znovu vytvoří."",
      ""category"": ""Cleanup"",
      ""badges"": [""CLEANUP""],
      ""kind"": ""cleanup"",
      ""requiresConfirmation"": true,
      ""on"": {
        ""description"": ""Smaže DX shader cache a NVIDIA cache."",
        ""operations"": [
          { ""type"": ""cmd"", ""command"": ""del /q /f /s \""%LOCALAPPDATA%\\D3DSCache\\*\"" 2>nul"" },
          { ""type"": ""cmd"", ""command"": ""del /q /f /s \""%LOCALAPPDATA%\\NVIDIA\\DXCache\\*\"" 2>nul"" },
          { ""type"": ""cmd"", ""command"": ""del /q /f /s \""%LOCALAPPDATA%\\NVIDIA\\GLCache\\*\"" 2>nul"" }
        ]
      },
      ""off"": {
        ""description"": ""Nelze vrátit."",
        ""operations"": []
      }
    },
    {
      ""id"": ""browser-cache-cleanup"",
      ""name"": ""Vyčištění browser cache"",
      ""description"": ""Smaže cache Chrome, Edge a Firefox (zamčené soubory přeskočí)."",
      ""category"": ""Cleanup"",
      ""badges"": [""CLEANUP""],
      ""kind"": ""cleanup"",
      ""requiresConfirmation"": true,
      ""on"": {
        ""description"": ""Smaže cache z Chrome, Edge a Firefox profilů."",
        ""operations"": [
          { ""type"": ""cmd"", ""command"": ""del /q /f /s \""%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Cache\\*\"" 2>nul"" },
          { ""type"": ""cmd"", ""command"": ""del /q /f /s \""%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\Default\\Cache\\*\"" 2>nul"" },
          { ""type"": ""cmd"", ""command"": ""del /q /f /s \""%LOCALAPPDATA%\\Mozilla\\Firefox\\Profiles\\*\\cache2\\*\"" 2>nul"" }
        ]
      },
      ""off"": {
        ""description"": ""Nelze vrátit."",
        ""operations"": []
      }
    },
    {
      ""id"": ""windows-update-cache-cleanup"",
      ""name"": ""Vyčištění Windows Update cache"",
      ""description"": ""Smaže cache Windows Update (SoftwareDistribution\\Download)."",
      ""category"": ""Cleanup"",
      ""badges"": [""CLEANUP""],
      ""kind"": ""cleanup"",
      ""requiresConfirmation"": true,
      ""on"": {
        ""description"": ""Zastaví wuauserv, smaže cache, spustí wuauserv."",
        ""operations"": [
          { ""type"": ""sc"", ""name"": ""wuauserv"", ""start"": ""disabled"" },
          { ""type"": ""cmd"", ""command"": ""del /q /f /s \""C:\\Windows\\SoftwareDistribution\\Download\\*\"" 2>nul"" },
          { ""type"": ""sc"", ""name"": ""wuauserv"", ""start"": ""demand"" }
        ]
      },
      ""off"": {
        ""description"": ""Nelze vrátit (jednorázová akce)."",
        ""operations"": []
      }
    },

    
    {
      ""id"": ""profile-fortnite-ping-test"",
      ""name"": ""Fortnite – měření pingu na servery"",
      ""description"": ""Pingne Fortnite servery (EU, NAE, NAC, NAW, BR, Asia, ME, OCE) a zobrazí výsledek."",
      ""category"": ""Gaming"",
      ""badges"": [""DIAGNOSTIKA""],
      ""implementation"": ""existing-csharp"",
      ""on"": {
        ""description"": ""Uživatel vybere region a proběhne ping -n 35. Nejde o toggle."",
        ""operations"": []
      },
      ""off"": {
        ""description"": ""Není relevantní."",
        ""operations"": []
      }
    }
  ],

  ""maintenanceActions"": [
    
    { ""id"": ""user-temp"",       ""name"": ""Uživatelský TEMP"",       ""category"": ""Cleanup"", ""requiresConfirmation"": true, ""implementation"": ""existing-csharp"" },
    { ""id"": ""windows-temp"",    ""name"": ""Windows + User TEMP"",    ""category"": ""Cleanup"", ""requiresConfirmation"": true, ""implementation"": ""existing-csharp"" },
    { ""id"": ""shader-cache"",    ""name"": ""Shader Cache"",           ""category"": ""Cleanup"", ""requiresConfirmation"": true, ""implementation"": ""existing-csharp"" },
    { ""id"": ""browser-cache"",   ""name"": ""Browser Cache"",          ""category"": ""Cleanup"", ""requiresConfirmation"": true, ""implementation"": ""existing-csharp"" },
    { ""id"": ""crash-dumps"",     ""name"": ""Crash Dumps & Logs"",     ""category"": ""Cleanup"", ""requiresConfirmation"": true, ""implementation"": ""existing-csharp"" },
    { ""id"": ""gpu-cache"",       ""name"": ""GPU / Shader Cache"",     ""category"": ""Cleanup"", ""requiresConfirmation"": true, ""implementation"": ""existing-csharp"" },
    { ""id"": ""wu-cache"",        ""name"": ""Windows Update Cache"",   ""category"": ""Cleanup"", ""requiresConfirmation"": true, ""implementation"": ""existing-csharp"" },
    { ""id"": ""do-cache"",        ""name"": ""Delivery Optimization"",  ""category"": ""Cleanup"", ""requiresConfirmation"": true, ""implementation"": ""existing-csharp"" },
    { ""id"": ""recycle-bin"",     ""name"": ""Recycle Bin"",            ""category"": ""Cleanup"", ""requiresConfirmation"": true, ""irreversible"": true, ""implementation"": ""existing-csharp"" }
  ],

  ""diagnosticAndUtilityActions"": [
    { ""id"": ""deep-scan"",       ""name"": ""Deep Scan"",           ""category"": ""scan"",        ""implementation"": ""existing-csharp"" },
    { ""id"": ""large-files"",     ""name"": ""Large Files"",          ""category"": ""scan"",        ""implementation"": ""existing-csharp"" },
    { ""id"": ""startup-registry"",""name"": ""Startup Registry"",     ""category"": ""diagnostic"",  ""implementation"": ""existing-csharp"" },
    { ""id"": ""startup-folder"",  ""name"": ""Startup Folder"",       ""category"": ""diagnostic"",  ""implementation"": ""existing-csharp"" },
    { ""id"": ""scheduled-tasks"", ""name"": ""Scheduled Tasks"",      ""category"": ""diagnostic"",  ""implementation"": ""existing-csharp"" },
    { ""id"": ""connection-info"", ""name"": ""Connection Info"",      ""category"": ""diagnostic"",  ""implementation"": ""existing-csharp"" },
    { ""id"": ""dns-check"",       ""name"": ""DNS Check"",            ""category"": ""diagnostic"",  ""implementation"": ""existing-csharp"" },
    { ""id"": ""network-health"",  ""name"": ""TCP/IP Health"",        ""category"": ""diagnostic"",  ""implementation"": ""existing-csharp"" },
    { ""id"": ""system-drive"",    ""name"": ""System Drive"",         ""category"": ""diagnostic"",  ""implementation"": ""existing-csharp"" },
    { ""id"": ""cpu-quick-test"",  ""name"": ""CPU Quick Test"",       ""category"": ""benchmark"",   ""implementation"": ""existing-csharp"" },
    { ""id"": ""memory-snapshot"", ""name"": ""Memory Snapshot"",      ""category"": ""diagnostic"",  ""implementation"": ""existing-csharp"" },
    { ""id"": ""tweak-backups"",   ""name"": ""Tweak Backups"",        ""category"": ""backup"",      ""implementation"": ""existing-csharp"" },
    { ""id"": ""restore-all"",     ""name"": ""Restore All"",          ""category"": ""backup"",      ""implementation"": ""existing-csharp"" },
    { ""id"": ""change-history"",  ""name"": ""Change History"",       ""category"": ""diagnostic"",  ""implementation"": ""existing-csharp"" },
    { ""id"": ""recycle-bin-info"",""name"": ""Recycle Bin Info"",     ""category"": ""cleanup-info"",""implementation"": ""existing-csharp"" },
    { ""id"": ""network-reset"",   ""name"": ""Network Reset Tools"",  ""category"": ""diagnostic"",  ""implementation"": ""existing-csharp"" },
    { ""id"": ""apps-manager"",    ""name"": ""Apps Manager"",         ""category"": ""apps"",        ""implementation"": ""existing-csharp"" }
  ],

  ""application"": {
    ""id"": ""gasbyn-optimizer"",
    ""name"": ""Gasbyn Optimizer"",
    ""version"": ""2.0.B18-extended"",
    ""navigation"": {
      ""defaultPage"": ""Dashboard"",
      ""items"": [
        { ""id"": ""dashboard"", ""label"": ""Dashboard"", ""page"": ""Dashboard"", ""icon"": ""home"", ""order"": 1 },
        { ""id"": ""tweaks"",    ""label"": ""Tweaks"",    ""page"": ""Tweaks"",    ""icon"": ""zap"",  ""order"": 2 },
        { ""id"": ""cleanup"",   ""label"": ""Cleanup"",   ""page"": ""Cleanup"",   ""icon"": ""brush-cleaning"", ""order"": 3 },
        { ""id"": ""deep-clean"",""label"": ""Deep Clean"",""page"": ""Deep Clean"",""icon"": ""scan-search"", ""order"": 4 },
        { ""id"": ""startup"",   ""label"": ""Startup"",   ""page"": ""Startup"",   ""icon"": ""rocket"", ""order"": 5 },
        { ""id"": ""apps-manager"",""label"":""Apps Manager"",""page"":""Apps Manager"",""icon"":""app-window"",""order"": 6 },
        { ""id"": ""gaming"",    ""label"": ""Gaming"",    ""page"": ""Gaming"",    ""icon"": ""gamepad-2"", ""order"": 7 },
        { ""id"": ""network"",   ""label"": ""Network"",   ""page"": ""Network"",   ""icon"": ""globe"", ""order"": 8 },
        { ""id"": ""health-check"",""label"":""Health Check"",""page"":""Health Check"",""icon"":""heart-pulse"",""order"": 9 },
        { ""id"": ""benchmark"", ""label"": ""Benchmark"", ""page"": ""Benchmark"", ""icon"": ""chart-no-axes-combined"", ""order"": 10 },
        { ""id"": ""backup"",    ""label"": ""Backup"",    ""page"": ""Backup"",    ""icon"": ""database-backup"", ""order"": 11 },
        { ""id"": ""pc-info"",   ""label"": ""PC Info"",   ""page"": ""PC Info"",   ""icon"": ""monitor"", ""order"": 12 },
        { ""id"": ""settings"",  ""label"": ""Settings"",  ""page"": ""Settings"",  ""icon"": ""settings"", ""order"": 13 }
      ]
    },
    ""runtime"": {
      ""targetFramework"": ""net10.0-windows"",
      ""targetArchitecture"": ""x64"",
      ""selfContained"": true,
      ""stateStorage"": ""%LocalAppData%\\GasbynOptimizer"",
      ""safety"": {
        ""saveOriginalValuesBeforeChange"": true,
        ""restoreOriginalValuesWhenDisabling"": true,
        ""automaticallyDisableDefender"": false,
        ""automaticallyDisableServices"": false,
        ""automaticallyChangeIpv6"": false,
        ""automaticallyChangeBootTimers"": false,
        ""destructiveCleanupRequiresConfirmation"": true,
        ""blockUnimplementedActions"": true
      }
    }
  },

  ""changeLog"": [
    {
      ""version"": ""2.0.B18-extended"",
      ""change"": ""Přidány všechny tweaky z původního .cmd nástroje (Services, Registry, CMD). Každý tweak má plné ON/OFF s reálnými operacemi. Opraveny duplicitní badges a prázdné registryOperations u původních záznamů. Přidány kategorie Gaming, Windows, Privacy, Network, Power, CPU, GPU, Storage, KBM, Security, Cleanup.""
    }
  ]
}
";

    public sealed record Definition(string Id, string Name, string Description, string Category, string[] Badges, string OnDescription, string OffDescription);

    private static readonly JsonDocument Catalog = JsonDocument.Parse(CatalogJson);
    private static readonly Dictionary<string, JsonElement> ById = Catalog.RootElement.GetProperty("optimizations")
        .EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString()!, x => x, StringComparer.OrdinalIgnoreCase);
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GasbynOptimizer", "builtin-catalog-state.json");
    private static readonly string BackupDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GasbynOptimizer", "builtin-catalog-backups");

    private sealed class RegistrySnapshot
    {
        public string Hive { get; set; } = "HKCU";
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Exists { get; set; }
        public string Kind { get; set; } = "String";
        public string Data { get; set; } = "";
    }

    public static IReadOnlyList<Definition> All => ById.Values
        .Select(x => new Definition(GetString(x, "id"), GetString(x, "name"), GetString(x, "description"),
            GetString(x, "category"), x.GetProperty("badges").EnumerateArray().Select(b => b.GetString() ?? "").ToArray(),
            GetString(x.GetProperty("on"), "description"), GetString(x.GetProperty("off"), "description")))
        .OrderBy(x => CategoryOrder(x.Category)).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public static bool ContainsWorkspaceId(string id) =>
        id.StartsWith("catalog:", StringComparison.OrdinalIgnoreCase) && ById.ContainsKey(id["catalog:".Length..]);

    public static Definition? Find(string id)
    {
        if (!id.StartsWith("catalog:", StringComparison.OrdinalIgnoreCase)) return null;
        var key = id["catalog:".Length..];
        if (!ById.TryGetValue(key, out var x)) return null;
        return new Definition(GetString(x, "id"), GetString(x, "name"), GetString(x, "description"),
            GetString(x, "category"), x.GetProperty("badges").EnumerateArray().Select(b => b.GetString() ?? "").ToArray(),
            GetString(x.GetProperty("on"), "description"), GetString(x.GetProperty("off"), "description"));
    }

    public static bool IsMarkedEnabled(string workspaceId)
    {
        var id = workspaceId["catalog:".Length..];
        try
        {
            if (!File.Exists(StatePath)) return false;
            using var doc = JsonDocument.Parse(File.ReadAllText(StatePath));
            return doc.RootElement.TryGetProperty(id, out var value) && value.ValueKind == JsonValueKind.True;
        }
        catch { return false; }
    }

    public static void Apply(string workspaceId, bool enable)
    {
        var id = workspaceId["catalog:".Length..];
        if (!ById.TryGetValue(id, out var item)) throw new InvalidOperationException("Neznámá optimalizace: " + id);
        var section = item.GetProperty(enable ? "on" : "off");
        var operations = section.GetProperty("operations").EnumerateArray().ToArray();
        var completed = 0;
        Exception? operationError = null;
        var backupPath = Path.Combine(BackupDirectory, id + ".json");
        try
        {
            if (enable && !File.Exists(backupPath))
            {
                var snapshots = CaptureRegistrySnapshots(item.GetProperty("on").GetProperty("operations").EnumerateArray());
                Directory.CreateDirectory(BackupDirectory);
                File.WriteAllText(backupPath, JsonSerializer.Serialize(snapshots, new JsonSerializerOptions { WriteIndented = true }));
            }

            try
            {
                foreach (var operation in operations) { Execute(operation); completed++; }
            }
            catch (Exception ex) { operationError = ex; }

            // Registry values are restored to their exact pre-ON values after the catalog's OFF
            // operations run. This preserves missing values and original value types as well.
            if (!enable && File.Exists(backupPath))
            {
                var snapshots = JsonSerializer.Deserialize<List<RegistrySnapshot>>(File.ReadAllText(backupPath)) ?? new();
                RestoreRegistrySnapshots(snapshots);
                File.Delete(backupPath);
            }

            if (operationError is not null) throw operationError;
            var state = LoadState();
            state[id] = enable;
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Optimalizace „{GetString(item, "name")}“ nebyla dokončena. Provedeno {completed}/{operations.Length} operací. Původní registry hodnoty se obnovují při OFF, pokud se podařilo uložit jejich zálohu. Detail: {ex.Message}", ex);
        }
    }

    private static Dictionary<string, bool> LoadState()
    {
        try
        {
            return File.Exists(StatePath)
                ? JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(StatePath)) ?? new(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
        catch { return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase); }
    }

    private static int CategoryOrder(string category) => category switch
    {
        "Gaming" => 1, "Windows" => 2, "Privacy" => 3, "Network" => 4, "Power" => 5,
        "CPU" => 6, "GPU" => 7, "Storage" => 8, "KBM" => 9, "Security" => 10, "Cleanup" => 11, _ => 99
    };

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static RegistryKey Hive(string hive) => hive.ToUpperInvariant() switch
    {
        "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
        "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
        "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
        "HKU" or "HKEY_USERS" => Registry.Users,
        "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
        _ => throw new InvalidOperationException("Nepodporovaný registry hive: " + hive)
    };

    private static object RegistryData(JsonElement data, string type)
    {
        return type.ToUpperInvariant() switch
        {
            "REG_DWORD" => data.ValueKind == JsonValueKind.Number ? data.GetInt32() : int.Parse(data.ToString(), CultureInfo.InvariantCulture),
            "REG_QWORD" => data.ValueKind == JsonValueKind.Number ? data.GetInt64() : long.Parse(data.ToString(), CultureInfo.InvariantCulture),
            "REG_BINARY" => Convert.FromHexString(data.ToString()),
            "REG_MULTI_SZ" => data.ValueKind == JsonValueKind.Array ? data.EnumerateArray().Select(x => x.ToString()).ToArray() : data.ToString().Split(';'),
            _ => data.ValueKind == JsonValueKind.String ? data.GetString() ?? "" : data.ToString()
        };
    }

    private static RegistryValueKind RegistryKind(string type) => type.ToUpperInvariant() switch
    {
        "REG_DWORD" => RegistryValueKind.DWord, "REG_QWORD" => RegistryValueKind.QWord,
        "REG_BINARY" => RegistryValueKind.Binary, "REG_MULTI_SZ" => RegistryValueKind.MultiString,
        "REG_EXPAND_SZ" => RegistryValueKind.ExpandString, _ => RegistryValueKind.String
    };

    private static void SetValue(string hive, string path, string name, string type, JsonElement data)
    {
        using var root = Hive(hive);
        using var key = root.CreateSubKey(path, writable: true) ?? throw new InvalidOperationException("Nelze otevřít registry klíč: " + path);
        key.SetValue(name, RegistryData(data, type), RegistryKind(type));
    }

    private static void Execute(JsonElement op)
    {
        var type = GetString(op, "type");
        switch (type)
        {
            case "reg":
                SetValue(GetString(op, "hive"), GetString(op, "path"), GetString(op, "value"), GetString(op, "regType"), op.GetProperty("data"));
                break;
            case "reg-delete":
                using (var root = Hive(GetString(op, "hive")))
                using (var key = root.OpenSubKey(GetString(op, "path"), writable: true))
                    key?.DeleteValue(GetString(op, "value"), throwOnMissingValue: false);
                break;
            case "reg-batch":
                foreach (var subkey in op.GetProperty("subkeys").EnumerateArray())
                    SetValue(GetString(op, "hive"), GetString(op, "basePath").TrimEnd('\\') + "\\" + subkey.GetString(),
                        GetString(op, "value"), GetString(op, "regType"), op.GetProperty("data"));
                break;
            case "sc":
                Run("sc.exe", new[] { "config", GetString(op, "name"), "start=", NormalizeServiceStart(GetString(op, "start")) });
                break;
            case "schtasks":
                Run("schtasks.exe", new[] { "/Change", "/TN", GetString(op, "task"), GetString(op, "state").Equals("disable", StringComparison.OrdinalIgnoreCase) ? "/Disable" : "/Enable" });
                break;
            case "cmd":
            case "netsh":
            case "powercfg":
            case "bcdedit":
            case "fsutil":
                RunShell(GetString(op, "command"));
                break;
            case "cmd-fornic":
            case "cmd-fornic-delete":
                ApplyAdapterValues(op, type.EndsWith("-delete", StringComparison.Ordinal));
                break;
            case "cmd-forlist":
            case "cmd-forlist-delete":
                ApplyClassList(op, type.EndsWith("-delete", StringComparison.Ordinal));
                break;
            case "cmd-forstorport":
                ApplyStoragePort(op);
                break;
            case "cmd-forservice":
                ApplyStorageServices(op);
                break;
            case "cmd-forintelgpu":
                ApplyIntelGpu(op);
                break;
            case "cmd-forstorage":
                ApplyStorageController(op, false);
                break;
            case "cmd-forstorage-delete":
                ApplyStorageController(op, true);
                break;
            default:
                throw new InvalidOperationException("Nepodporovaný typ operace: " + type);
        }
    }

    private static List<RegistrySnapshot> CaptureRegistrySnapshots(IEnumerable<JsonElement> operations)
    {
        var snapshots = new Dictionary<string, RegistrySnapshot>(StringComparer.OrdinalIgnoreCase);
        void Capture(string hive, string path, string name)
        {
            var keyId = hive + "|" + path + "|" + name;
            if (snapshots.ContainsKey(keyId)) return;
            using var root = Hive(hive);
            using var key = root.OpenSubKey(path);
            var value = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value is null)
            {
                snapshots[keyId] = new RegistrySnapshot { Hive = hive, Path = path, Name = name, Exists = false };
                return;
            }
            var kind = key!.GetValueKind(name);
            snapshots[keyId] = new RegistrySnapshot
            {
                Hive = hive, Path = path, Name = name, Exists = true, Kind = kind.ToString(),
                Data = SerializeSnapshotValue(value, kind)
            };
        }

        void CaptureClassValues(string classPath, IEnumerable<string> names, Func<RegistryKey, bool>? filter = null)
        {
            using var root = Registry.LocalMachine.OpenSubKey(classPath);
            if (root is null) return;
            foreach (var childName in root.GetSubKeyNames().Where(x => int.TryParse(x, out _)))
            {
                using var child = root.OpenSubKey(childName);
                if (child is null || (filter is not null && !filter(child))) continue;
                foreach (var name in names) Capture("HKLM", classPath + "\\" + childName, name);
            }
        }

        foreach (var op in operations)
        {
            var type = GetString(op, "type");
            switch (type)
            {
                case "reg":
                case "reg-delete":
                    Capture(GetString(op, "hive"), GetString(op, "path"), GetString(op, "value"));
                    break;
                case "reg-batch":
                    foreach (var subkey in op.GetProperty("subkeys").EnumerateArray())
                        Capture(GetString(op, "hive"), GetString(op, "basePath").TrimEnd('\\') + "\\" + subkey.GetString(), GetString(op, "value"));
                    break;
                case "cmd-fornic":
                    CaptureClassValues(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}",
                        Values(op).Select(x => GetString(x, "name")));
                    break;
                case "cmd-forlist":
                    foreach (var clsid in op.GetProperty("list").EnumerateArray().Select(x => x.GetString() ?? ""))
                        Capture(GetString(op, "hive"), GetString(op, "basePath").TrimEnd('\\') + "\\" + clsid, GetString(op, "value", "Flags"));
                    break;
                case "cmd-forstorport":
                    Capture("HKLM", @"SYSTEM\CurrentControlSet\Services\storport\Parameters\Device", GetString(op, "value"));
                    break;
                case "cmd-forservice":
                    foreach (var service in new[] { "storahci", "stornvme", "iaStorV", "iaStorAC" })
                    foreach (var name in op.GetProperty("names").EnumerateArray().Select(x => x.GetString() ?? ""))
                        Capture("HKLM", $@"SYSTEM\CurrentControlSet\Services\{service}\Parameters\Device", name);
                    break;
                case "cmd-forintelgpu":
                    CaptureClassValues(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}",
                        Values(op).Select(x => GetString(x, "name")),
                        child => (child.GetValue("DriverDesc")?.ToString() ?? "").Contains("Intel", StringComparison.OrdinalIgnoreCase));
                    break;
                case "cmd-forstorage":
                    CaptureClassValues(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e97b-e325-11ce-bfc1-08002be10318}",
                        Values(op).Select(x => GetString(x, "name")));
                    break;
            }
        }
        return snapshots.Values.ToList();
    }

    private static string GetString(JsonElement element, string name, string fallback) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static string SerializeSnapshotValue(object value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        RegistryValueKind.Binary => Convert.ToBase64String((byte[])value),
        RegistryValueKind.MultiString => JsonSerializer.Serialize((string[])value),
        _ => value.ToString() ?? ""
    };

    private static void RestoreRegistrySnapshots(IEnumerable<RegistrySnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            using var root = Hive(snapshot.Hive);
            if (!snapshot.Exists)
            {
                using var existing = root.OpenSubKey(snapshot.Path, writable: true);
                existing?.DeleteValue(snapshot.Name, throwOnMissingValue: false);
                continue;
            }

            using var key = root.CreateSubKey(snapshot.Path, writable: true)
                ?? throw new InvalidOperationException("Nelze obnovit registry klíč: " + snapshot.Path);
            var kind = Enum.Parse<RegistryValueKind>(snapshot.Kind);
            object value = kind switch
            {
                RegistryValueKind.DWord => int.Parse(snapshot.Data, CultureInfo.InvariantCulture),
                RegistryValueKind.QWord => long.Parse(snapshot.Data, CultureInfo.InvariantCulture),
                RegistryValueKind.Binary => Convert.FromBase64String(snapshot.Data),
                RegistryValueKind.MultiString => JsonSerializer.Deserialize<string[]>(snapshot.Data) ?? Array.Empty<string>(),
                _ => snapshot.Data
            };
            key.SetValue(snapshot.Name, value, kind);
        }
    }

    private static string NormalizeServiceStart(string start) => start.ToLowerInvariant() switch
    {
        "disabled" => "disabled", "auto" => "auto", "demand" or "manual" => "demand", "delayed-auto" => "delayed-auto",
        _ => throw new InvalidOperationException("Neznámý start type služby: " + start)
    };

    private static void Run(string file, IEnumerable<string> args)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        if (!process.Start()) throw new InvalidOperationException("Nelze spustit " + file);
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { try { process.Kill(true); } catch { } throw new TimeoutException(file + " překročil limit 60 sekund."); }
        Task.WaitAll(stdoutTask, stderrTask);
        if (process.ExitCode != 0) throw new InvalidOperationException($"{file} skončil s kódem {process.ExitCode}: {stderrTask.Result} {stdoutTask.Result}".Trim());
    }

    private static void RunShell(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) throw new InvalidOperationException("Prázdný systémový příkaz.");
        using var process = new Process { StartInfo = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, Arguments = "/d /s /c \"" + command + "\"" } };
        if (!process.Start()) throw new InvalidOperationException("Nelze spustit příkaz: " + command);
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120000)) { try { process.Kill(true); } catch { } throw new TimeoutException("Příkaz překročil limit 120 sekund: " + command); }
        Task.WaitAll(stdoutTask, stderrTask);
        if (process.ExitCode != 0) throw new InvalidOperationException($"Příkaz skončil s kódem {process.ExitCode}: {stderrTask.Result} {stdoutTask.Result}".Trim());
    }

    private static IEnumerable<JsonElement> Values(JsonElement op) => op.GetProperty("values").EnumerateArray();

    private static void ApplyAdapterValues(JsonElement op, bool delete)
    {
        const string classPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
        using var root = Registry.LocalMachine.OpenSubKey(classPath, writable: true) ?? throw new InvalidOperationException("Registry síťových adaptérů není dostupný.");
        var found = 0;
        foreach (var childName in root.GetSubKeyNames().Where(x => int.TryParse(x, out _)))
        {
            using var child = root.OpenSubKey(childName, writable: true);
            if (child is null || string.IsNullOrWhiteSpace(child.GetValue("DriverDesc")?.ToString())) continue;
            found++;
            if (delete)
            {
                foreach (var value in op.GetProperty("values").EnumerateArray().Select(x => x.GetString() ?? "")) child.DeleteValue(value, false);
            }
            else foreach (var entry in Values(op)) SetChildValue(child, GetString(entry, "name"), GetString(entry, "regType"), entry.GetProperty("data"));
        }
        if (found == 0) throw new InvalidOperationException("Nebyl nalezen žádný síťový adaptér pro úpravu.");
    }

    private static void ApplyClassList(JsonElement op, bool delete)
    {
        var hive = GetString(op, "hive");
        var basePath = GetString(op, "basePath");
        var valueName = GetString(op, "value");
        foreach (var clsid in op.GetProperty("list").EnumerateArray().Select(x => x.GetString() ?? ""))
        {
            var path = basePath.TrimEnd('\\') + "\\" + clsid;
            if (delete)
            {
                using var root = Hive(hive);
                using var key = root.OpenSubKey(path, writable: true);
                key?.DeleteValue(valueName, false);
            }
            else SetValue(hive, path, valueName, GetString(op, "regType"), op.GetProperty("data"));
        }
    }

    private static void ApplyStoragePort(JsonElement op)
    {
        const string path = @"SYSTEM\CurrentControlSet\Services\storport\Parameters\Device";
        SetValue("HKLM", path, GetString(op, "value"), GetString(op, "regType"), op.GetProperty("data"));
    }

    private static void ApplyStorageServices(JsonElement op)
    {
        foreach (var service in new[] { "storahci", "stornvme", "iaStorV", "iaStorAC" })
        foreach (var name in op.GetProperty("names").EnumerateArray().Select(x => x.GetString() ?? ""))
            SetValue("HKLM", $@"SYSTEM\CurrentControlSet\Services\{service}\Parameters\Device", name,
                GetString(op, "regType"), op.GetProperty("data"));
    }

    private static void ApplyIntelGpu(JsonElement op)
    {
        const string classPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        using var root = Registry.LocalMachine.OpenSubKey(classPath, writable: true) ?? throw new InvalidOperationException("Registry grafických adaptérů není dostupný.");
        var found = false;
        foreach (var childName in root.GetSubKeyNames().Where(x => int.TryParse(x, out _)))
        {
            using var child = root.OpenSubKey(childName, writable: true);
            if (child is null || !(child.GetValue("DriverDesc")?.ToString() ?? "").Contains("Intel", StringComparison.OrdinalIgnoreCase)) continue;
            found = true;
            foreach (var entry in Values(op)) SetChildValue(child, GetString(entry, "name"), GetString(entry, "regType"), entry.GetProperty("data"));
        }
        if (!found) throw new InvalidOperationException("Intel GPU nebyla nalezena; tato optimalizace se na tomto PC nepoužije.");
    }

    private static void ApplyStorageController(JsonElement op, bool delete)
    {
        const string classPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e97b-e325-11ce-bfc1-08002be10318}";
        using var root = Registry.LocalMachine.OpenSubKey(classPath, writable: true) ?? throw new InvalidOperationException("Registry řadičů disků není dostupný.");
        var found = false;
        foreach (var childName in root.GetSubKeyNames().Where(x => int.TryParse(x, out _)))
        {
            using var child = root.OpenSubKey(childName, writable: true);
            if (child is null) continue;
            found = true;
            if (delete)
            {
                foreach (var name in op.GetProperty("values").EnumerateArray().Select(x => x.GetString() ?? "")) child.DeleteValue(name, false);
            }
            else foreach (var entry in Values(op)) SetChildValue(child, GetString(entry, "name"), GetString(entry, "regType"), entry.GetProperty("data"));
        }
        if (!found) throw new InvalidOperationException("Nebyl nalezen podporovaný řadič úložiště.");
    }

    private static void SetChildValue(RegistryKey key, string name, string type, JsonElement data) =>
        key.SetValue(name, RegistryData(data, type), RegistryKind(type));
}
