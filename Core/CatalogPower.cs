namespace GasbynOptimizer.Core;

/// <summary>Vlastní plány napájení (PC a notebook). Nejsou to přepínače, plán se jen použije tlačítkem.</summary>
internal static class CatalogPower
{
    internal const string CatPower = "Plány napájení";

    // GUID plánů uvnitř souborů .pow (viz Resources/PowerPlans).
    private const string PcGuid = "38768d5d-72fd-482d-adc5-e0dd5ed7b1bd";
    private const string NotebookGuid = "609dba22-f120-43fc-8612-603e9e701b30";

    public static IReadOnlyList<Tweak> Create()
    {
        return new List<Tweak>
        {
            new()
            {
                Id = "powerplan-pc", Category = CatPower, ApplyOnly = true, RequiresRestart = false,
                Requires = Needs.Desktop,
                RecommendWhen = p => p.IsLaptop == false,
                Name = "Plán napájení: Gasbyn PC",
                Description = "Vlastní výkonnostní plán pro stolní počítače. Použije se tlačítkem a stane se aktivním plánem Windows.",
                Note = "Původní plán ve Windows zůstane, přepnout na něj můžeš v Ovládacích panelech (Možnosti napájení).",
                Ops = new TweakOp[] { new PowerPlanImportOp("PowerPlans.GasbynPC.pow", PcGuid) }
            },
            new()
            {
                Id = "powerplan-notebook", Category = CatPower, ApplyOnly = true, RequiresRestart = false,
                Requires = Needs.Laptop,
                RecommendWhen = p => p.IsLaptop == true,
                Name = "Plán napájení: Gasbyn Notebook",
                Description = "Vlastní plán napájení pro notebooky. Použije se tlačítkem a stane se aktivním plánem Windows.",
                Note = "Původní plán ve Windows zůstane, přepnout na něj můžeš v Ovládacích panelech (Možnosti napájení).",
                Ops = new TweakOp[] { new PowerPlanImportOp("PowerPlans.GasbynNotebook.pow", NotebookGuid) }
            }
        };
    }
}
