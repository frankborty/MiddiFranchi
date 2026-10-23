using Microsoft.EntityFrameworkCore;
using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Portfolio;
using MiddiFranchi.Domain.Shared;
using MiddiFranchi.Infrastructure.Persistence.Entities;

namespace MiddiFranchi.Infrastructure.Persistence.Repositories;

internal static class FinancialOperationMaterializer
{
    public static async Task<List<FinancialOperationRecord>> LoadByIdsAsync(
        MiddiFranchiDbContext context,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await context.FinancialOperations
            .AsNoTracking()
            .Where(operation => ids.Contains(operation.Id))
            .ToListAsync(cancellationToken);
    }

    public static async Task<IReadOnlyList<FinancialOperation>> MaterializeAsync(
        MiddiFranchiDbContext context,
        IReadOnlyCollection<FinancialOperationRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var operationIds = records.Select(record => record.Id).ToArray();
        var feeRecords = await context.Set<FeeComponentRecord>()
            .AsNoTracking()
            .Where(fee => operationIds.Contains(fee.InvestmentOperationId))
            .OrderBy(fee => fee.InvestmentOperationId)
            .ThenBy(fee => fee.Ordinal)
            .ToListAsync(cancellationToken);

        var accountIds = records
            .SelectMany(record => new[]
            {
                record.AccountId,
                record.SourceAccountId,
                record.DestinationAccountId,
                record.SettlementAccountId,
            })
            .Concat(feeRecords.Select(fee => (Guid?)fee.DebitAccountId))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var categoryIds = records
            .Select(record => record.CategoryId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var instrumentIds = records
            .Select(record => record.InstrumentId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var accountRecords = accountIds.Length == 0
            ? []
            : await context.Accounts
                .AsNoTracking()
                .Where(account => accountIds.Contains(account.Id))
                .ToListAsync(cancellationToken);
        var categoryRecords = categoryIds.Length == 0
            ? []
            : await context.Categories
                .AsNoTracking()
                .Where(category => categoryIds.Contains(category.Id))
                .ToListAsync(cancellationToken);
        var instrumentRecords = instrumentIds.Length == 0
            ? []
            : await context.Instruments
                .AsNoTracking()
                .Where(instrument => instrumentIds.Contains(instrument.Id))
                .ToListAsync(cancellationToken);

        var accounts = accountRecords.ToDictionary(account => account.Id, PersistenceMapper.ToDomain);
        var categories = categoryRecords.ToDictionary(category => category.Id, PersistenceMapper.ToDomain);
        var instruments = instrumentRecords.ToDictionary(instrument => instrument.Id, PersistenceMapper.ToDomain);

        var fees = feeRecords
            .GroupBy(fee => fee.InvestmentOperationId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FeeComponent>)group
                    .Select(fee => new FeeComponent(
                        fee.Kind,
                        new PositiveMoney(new Money(
                            fee.AmountAmount,
                            Currency.Parse(fee.AmountCurrencyCode))),
                        accounts.TryGetValue(fee.DebitAccountId, out var debitAccount)
                            ? debitAccount
                            : throw new InvalidOperationException(
                                $"Il conto di addebito della commissione {fee.Id} non è stato caricato.")))
                    .ToArray());

        return PersistenceMapper.ToDomainOperations(
            records, accounts, categories, instruments, fees);
    }
}
