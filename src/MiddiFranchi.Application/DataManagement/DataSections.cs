namespace MiddiFranchi.Application.DataManagement;

[Flags]
public enum DataSections
{
    None = 0,
    Accounts = 1,
    Categories = 2,
    Instruments = 4,
    Movements = 8,
    AccountHistory = 16,
    PriceHistory = 32,
    PriceSettings = 64,
    AllData = Accounts | Categories | Instruments | Movements | AccountHistory | PriceHistory,
    All = AllData | PriceSettings,
}

public static class DataSectionsExtensions
{
    public static bool Includes(this DataSections selection, DataSections section) =>
        (selection & section) == section;

    public static bool IsFullDataRestore(this DataSections selection) =>
        selection.Includes(DataSections.AllData);

    public static DataSections Normalize(this DataSections selection) =>
        selection & DataSections.All;
}
