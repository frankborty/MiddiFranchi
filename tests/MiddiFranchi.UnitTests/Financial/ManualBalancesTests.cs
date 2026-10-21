using MiddiFranchi.Domain.Financial;
using MiddiFranchi.Domain.Shared;
using Shouldly;

namespace MiddiFranchi.UnitTests.Financial;

public class ManualBalancesTests
{
    private static readonly Currency Euro = Currency.Euro;
    private static readonly Account Account = new(
        Guid.NewGuid(), "Conto corrente", AccountType.Checking, Euro, AccountStatus.Active);
    private static readonly Account OtherAccount = new(
        Guid.NewGuid(), "Altro conto", AccountType.Checking, Euro, AccountStatus.Active);

    [Fact]
    public void Latest_ShouldReturnTheMostRecentlyRegisteredReading()
    {
        var earlier = Reading(RecordedAt: new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), amount: 100m);
        var later = Reading(RecordedAt: new(2026, 4, 2, 9, 0, 0, TimeSpan.Zero), amount: 80m);

        ManualBalances.Latest([earlier, later], Account.Id)
            .ShouldBe(later);
    }

    [Fact]
    public void Latest_ShouldBreakTiesWithTheIdentifier()
    {
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var recordedAt = new DateTimeOffset(2026, 4, 2, 9, 0, 0, TimeSpan.Zero);
        var first = Reading(recordedAt, 70m, id: firstId);
        var second = Reading(recordedAt, 90m, id: secondId);

        ManualBalances.Latest([second, first], Account.Id)
            .ShouldBe(second);
    }

    [Fact]
    public void Latest_ShouldReturnNullWithoutReadings()
    {
        ManualBalances.Latest([], Account.Id).ShouldBeNull();

        var otherReading = Reading(
            new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), 100m, null, null, OtherAccount);

        ManualBalances.Latest([otherReading], Account.Id).ShouldBeNull();
    }

    [Fact]
    public void Latest_ShouldIgnoreTheEconomicDate()
    {
        var futureReading = Reading(
            RecordedAt: new(2026, 4, 2, 9, 0, 0, TimeSpan.Zero),
            amount: 120m,
            validAt: new DateOnly(2026, 12, 31));

        ManualBalances.Latest([futureReading], Account.Id)
            .ShouldBe(futureReading);
    }

    [Fact]
    public void LatestOnOrBefore_ShouldReturnTheReadingValidOnTheRequestedDate()
    {
        var april = Reading(RecordedAt: new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero), amount: 100m, validAt: new DateOnly(2026, 4, 10));
        var may = Reading(RecordedAt: new(2026, 5, 10, 9, 0, 0, TimeSpan.Zero), amount: 120m, validAt: new DateOnly(2026, 5, 10));

        ManualBalances.LatestOnOrBefore([april, may], Account.Id, new DateOnly(2026, 4, 30))
            .ShouldBe(april);
        ManualBalances.LatestOnOrBefore([april, may], Account.Id, new DateOnly(2026, 5, 10))
            .ShouldBe(may);
    }

    [Fact]
    public void LatestOnOrBefore_ShouldBreakSameDateTiesWithRegistrationThenIdentifier()
    {
        var recordedEarlier = new DateTimeOffset(2026, 4, 5, 8, 0, 0, TimeSpan.Zero);
        var recordedLater = new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero);
        var smallerId = Guid.Parse("00000000-0000-0000-0000-00000000000a");
        var largerId = Guid.Parse("00000000-0000-0000-0000-00000000000b");
        var morning = Reading(recordedEarlier, 50m, id: largerId);
        var evening = Reading(recordedLater, 60m, id: largerId);
        var eveningWithSmallerId = Reading(recordedLater, 70m, id: smallerId);

        ManualBalances.LatestOnOrBefore([morning, evening, eveningWithSmallerId], Account.Id, new DateOnly(2026, 4, 30))
            .ShouldBe(evening);
    }

    [Fact]
    public void LatestOnOrBefore_ShouldReturnNullBeforeTheFirstReading()
    {
        var reading = Reading(RecordedAt: new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero), amount: 100m, validAt: new DateOnly(2026, 4, 10));

        ManualBalances.LatestOnOrBefore([reading], Account.Id, new DateOnly(2026, 4, 9))
            .ShouldBeNull();
    }

    private static ManualBalance Reading(
        DateTimeOffset RecordedAt,
        decimal amount,
        DateOnly? validAt = null,
        Guid? id = null) => Reading(RecordedAt, amount, validAt, id, Account);

    private static ManualBalance Reading(
        DateTimeOffset recordedAt,
        decimal amount,
        DateOnly? validAt,
        Guid? id,
        Account account) =>
        new(
            id ?? Guid.NewGuid(),
            account,
            new Money(amount, Euro),
            validAt ?? new DateOnly(2026, 4, 1),
            recordedAt);
}
