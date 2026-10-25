using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Persistence;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Repositories;

public sealed class AppMetadataRepository : IAppMetadataRepository
{
    private const string SeedCompletedValue = "done";

    private readonly MiddiFranchiDbContext _context;

    public AppMetadataRepository(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AppMetadataEntry>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await _context.AppMetadata
            .AsNoTracking()
            .OrderBy(metadata => metadata.Key)
            .ToListAsync(cancellationToken);

        return records
            .Select(record => new AppMetadataEntry(record.Key, record.Value, record.UpdatedAt))
            .ToArray();
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return await _context.AppMetadata
            .AsNoTracking()
            .Where(metadata => metadata.Key == key)
            .Select(metadata => metadata.Value)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsSeedCompletedAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var value = await GetValueAsync(key, cancellationToken);
        return string.Equals(value, SeedCompletedValue, StringComparison.Ordinal);
    }

    public async Task SetAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var record = await _context.AppMetadata
            .SingleOrDefaultAsync(metadata => metadata.Key == key, cancellationToken);

        if (record is null)
        {
            _context.AppMetadata.Add(new AppMetadataRecord
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            record.Value = value;
            record.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetManyAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            return;
        }

        var keys = values.Keys.ToArray();
        var records = await _context.AppMetadata
            .Where(metadata => keys.Contains(metadata.Key))
            .ToDictionaryAsync(metadata => metadata.Key, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var (key, value) in values)
        {
            if (records.TryGetValue(key, out var record))
            {
                record.Value = value;
                record.UpdatedAt = now;
            }
            else
            {
                _context.AppMetadata.Add(new AppMetadataRecord
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = now,
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var record = await _context.AppMetadata
            .SingleOrDefaultAsync(metadata => metadata.Key == key, cancellationToken);
        if (record is null)
        {
            return;
        }

        _context.AppMetadata.Remove(record);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
