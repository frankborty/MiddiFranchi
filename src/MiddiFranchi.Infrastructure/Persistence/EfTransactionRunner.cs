using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Application.Persistence;

namespace MiddiFranchi.Infrastructure.Persistence;

public sealed class EfTransactionRunner : ITransactionRunner
{
    private readonly MiddiFranchiDbContext _context;

    public EfTransactionRunner(MiddiFranchiDbContext context)
    {
        _context = context;
    }

    public async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var ownsTransaction = _context.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
