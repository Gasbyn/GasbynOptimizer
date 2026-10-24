using System.Text;

namespace GasbynOptimizer.Core;

public static class RestorePoint
{
    // Windows jinak povolí jen jeden bod obnovení za 24 hodin. Frekvenci na chvíli vypneme
    // a po vytvoření bodu původní hodnotu vrátíme.
    private const string Script = @"
$ErrorActionPreference = 'Stop'
try { Enable-ComputerRestore -Drive ""$($env:SystemDrive)\"" } catch { }
$key = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore'
$name = 'SystemRestorePointCreationFrequency'
$old = $null
try { $old = (Get-ItemProperty -Path $key -Name $name -ErrorAction Stop).$name } catch { }
Set-ItemProperty -Path $key -Name $name -Value 0 -Type DWord
try {
    Checkpoint-Computer -Description 'Gasbyn Optimizer' -RestorePointType 'MODIFY_SETTINGS'
}
finally {
    if ($null -eq $old) { Remove-ItemProperty -Path $key -Name $name -ErrorAction SilentlyContinue }
    else { Set-ItemProperty -Path $key -Name $name -Value $old -Type DWord }
}
";

    public static (bool Ok, string Message) Create()
    {
        try
        {
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(Script));
            var result = Shell.Run(
                Shell.PowerShellExe,
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                240000);

            if (result.ExitCode == 0)
            {
                Log.Write("Bod obnovení vytvořen.");
                return (true, "Bod obnovení byl vytvořen.");
            }

            Log.Write("Bod obnovení selhal: " + result.Output);
            return (false, string.IsNullOrWhiteSpace(result.Output) ? "Neznámá chyba (kód " + result.ExitCode + ")." : result.Output);
        }
        catch (Exception ex)
        {
            Log.Write("Bod obnovení selhal: " + ex.Message);
            return (false, ex.Message);
        }
    }
}
