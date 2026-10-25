using MiddiFranchi.Application.DataManagement;

namespace MiddiFranchi.Web.Services;

public static class DataSectionLabels
{
    public static IReadOnlyList<string> Describe(DataSections sections)
    {
        var labels = new List<string>(7);

        if (sections.Includes(DataSections.Accounts))
        {
            labels.Add("Conti");
        }

        if (sections.Includes(DataSections.Categories))
        {
            labels.Add("Categorie");
        }

        if (sections.Includes(DataSections.Instruments))
        {
            labels.Add("Strumenti finanziari");
        }

        if (sections.Includes(DataSections.Movements))
        {
            labels.Add("Movimenti");
        }

        if (sections.Includes(DataSections.AccountHistory))
        {
            labels.Add("Storico valore conti");
        }

        if (sections.Includes(DataSections.PriceHistory))
        {
            labels.Add("Storico valore strumenti");
        }

        if (sections.Includes(DataSections.PriceSettings))
        {
            labels.Add("Impostazioni prezzi");
        }

        return labels;
    }

    public static string DescribeInline(DataSections sections) =>
        string.Join(", ", Describe(sections));
}
