using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace GasbynOptimizer.UI;

public static class SupportLinks
{
    public const string Server = "https://discord.gg/VC4xf9DQ7A";
    public const string Chat = "https://discord.gg/N4TM66Ybgq";

    public static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* bez prohlížeče se odkaz neotevře */ }
    }

    /// <summary>Verze podmínek. Při změně textu se zvýší a uživatel je potvrdí znovu.</summary>
    public const int TermsVersion = 1;

    public const string Terms =
@"1. Co je Gasbyn Optimizer
Gasbyn Optimizer je nástroj, který mění nastavení Windows (registr, služby, naplánované úlohy, plány napájení a další), aby počítač běžel rychleji, hladceji a soukroměji. Nepatří společnostem Microsoft, NVIDIA, AMD, Intel, ASUS, MSI, Google, Brave, Spotify, Discord, Valve, Epic Games ani jiným. Jejich názvy a ochranné známky patří jejich vlastníkům a uvádíme je jen pro popis funkcí.

2. Používáš na vlastní riziko
Aplikace zasahuje do systému. Používáš ji na vlastní odpovědnost. Autoři a provozovatelé Gasbyn Optimizeru neodpovídají za žádné přímé ani nepřímé škody, ztrátu dat, nestabilitu či nefunkčnost systému, her nebo programů, ztrátu záruky ani jiné následky, které vzniknou používáním aplikace. Aplikace je poskytována „tak, jak je“, bez jakékoli záruky.

3. Optimalizace mají různou míru rizika
Většina optimalizací je bezpečná a jde vrátit zpět. Optimalizace označené jako „Experimentální“ nebo v kategorii „Pokročilé“ jsou hlubší, mohou snížit zabezpečení (např. mitigace procesoru, Defender, firewall, SmartScreen), vypnout funkce systému (tisk, Bluetooth, vzdálená plocha, aktualizace) nebo vyžadovat restart. Zapínej je jen, když víš, co dělají. Některé akce, například odinstalace Microsoft Edge nebo čištění souborů, nejdou vrátit zpět.

4. Záloha a vrácení změn
Aplikace si před změnou pamatuje původní hodnoty a umí vytvořit bod obnovení Windows. Přesto doporučujeme zálohovat důležitá data. Vrácení původního stavu u některých změn nemusí být dokonalé, například pokud mezitím systém nebo jiný program nastavení změnil.

5. Bezpečnost a soukromí
Aplikace nesbírá osobní údaje a neodesílá informace o tvém počítači. Přistupuje k internetu jen kvůli kontrole a stažení nové verze z GitHubu. Soubory záloh a protokol změn zůstávají ve složce aplikace na tvém počítači.

6. Aktualizace
Při každém spuštění aplikace zkontroluje novou verzi a případně se sama aktualizuje. Tyto podmínky se mohou s aktualizací změnit. Pokud se změní podstatně, požádáme tě o nový souhlas.

7. Co dělat při potížích
Pokud se objeví problém, nejdřív zkus optimalizaci vypnout (vrátí původní stav), případně použij bod obnovení Windows. Když to nepomůže, kontaktuj nás na našem Discord serveru, rádi pomůžeme. Postup: připoj se na server a napiš do chatu pro hlášení problémů. Uveď verzi aplikace, verzi Windows, co jsi zapnul a co přesně se stalo.

8. Souhlas
Kliknutím na „Souhlasím a pokračovat“ potvrzuješ, že jsi podmínky přečetl, rozumíš jim a používáš aplikaci na vlastní odpovědnost. Pokud nesouhlasíš, aplikaci nepoužívej. Text podmínek najdeš kdykoli v Nastavení.";
}

public partial class TermsWindow : Window
{
    public TermsWindow(bool requireAccept)
    {
        InitializeComponent();
        TermsText.Text = SupportLinks.Terms;

        if (!requireAccept)
        {
            Subtitle.Text = "Podmínky, které jsi odsouhlasil(a).";
            AgreeCheck.Visibility = Visibility.Collapsed;
            DeclineButton.Visibility = Visibility.Collapsed;
            AcceptButton.Content = "Zavřít";
            AcceptButton.IsEnabled = true;
        }
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Server_Click(object sender, RoutedEventArgs e) => SupportLinks.Open(SupportLinks.Server);

    private void Chat_Click(object sender, RoutedEventArgs e) => SupportLinks.Open(SupportLinks.Chat);

    private void Agree_Click(object sender, RoutedEventArgs e) => AcceptButton.IsEnabled = AgreeCheck.IsChecked == true;

    private void Accept_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Decline_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
