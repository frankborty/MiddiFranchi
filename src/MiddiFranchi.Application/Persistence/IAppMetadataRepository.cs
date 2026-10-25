namespace MiddiFranchi.Application.Persistence;

public sealed record AppMetadataEntry(string Key, string Value, DateTimeOffset UpdatedAt);

public interface IAppMetadataRepository
{
    Task<IReadOnlyList<AppMetadataEntry>> ListAsync(CancellationToken cancellationToken = default);

    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> IsSeedCompletedAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);

    Task SetManyAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
