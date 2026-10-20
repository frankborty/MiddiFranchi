using Xunit;

namespace MiddiFranchi.IntegrationTests.Infrastructure;

internal static class PostgresTestSettings
{
    public const string EnableVariable = "MIGDIFRANCHI_POSTGRES_TESTS";

    public static bool Enabled
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(EnableVariable);
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   (bool.TryParse(value, out var enabled) && enabled);
        }
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresTestSettings.Enabled)
        {
            Skip = $"Impostare {PostgresTestSettings.EnableVariable}=1 e avviare Docker per eseguire i test PostgreSQL.";
        }
    }
}
