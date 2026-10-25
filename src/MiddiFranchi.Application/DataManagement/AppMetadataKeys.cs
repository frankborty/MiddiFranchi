using MiddiFranchi.Application.Financial;

namespace MiddiFranchi.Application.DataManagement;

public static class AppMetadataKeys
{
    public const string SeedSpecialAccounts = SpecialAccountsSeeder.SeedKey;
    public const string SeedCategories = DefaultCategoriesSeeder.SeedKey;
    public const string LastRestoreAt = "data.lastRestoreAt";
    public const string LastDeletionAt = "data.lastDeletionAt";

    public static IReadOnlyDictionary<string, string> SeedMarkers { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SeedSpecialAccounts] = "done",
            [SeedCategories] = "done",
        };
}
