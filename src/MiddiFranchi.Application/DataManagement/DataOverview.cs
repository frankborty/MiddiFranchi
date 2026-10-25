namespace MiddiFranchi.Application.DataManagement;

public sealed record DataOverview(
    DataCounts Counts,
    SchemaVersionInfo Schema);
