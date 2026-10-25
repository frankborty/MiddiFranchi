namespace MiddiFranchi.Application.DataManagement;

[Flags]
public enum DataDataset
{
    None = 0,
    Movements = 1,
    Instruments = 2,
    Accounts = 4,
    Categories = 8,
    Everything = Movements | Instruments | Accounts | Categories,
}

public static class DataDatasetExtensions
{
    public static bool Includes(this DataDataset selection, DataDataset dataset) =>
        (selection & dataset) == dataset;
}
