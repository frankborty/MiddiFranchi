using MiddiFranchi.Application.Financial;
using Shouldly;

namespace MiddiFranchi.UnitTests.Application;

public sealed class PriceUpdateSchedulerTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    [Fact]
    public void DailyBeforeConfiguredTime_ShouldReturnPreviousDay()
    {
        var settings = Settings(PriceUpdateFrequency.Daily);
        var now = At(2026, 10, 22, 11, 0);

        PriceUpdateScheduler.GetMostRecentScheduledTime(settings, now)
            .ShouldBe(At(2026, 10, 21, 12, 0));
    }

    [Fact]
    public void DailyAfterConfiguredTime_ShouldReturnSameDay()
    {
        var settings = Settings(PriceUpdateFrequency.Daily);
        var now = At(2026, 10, 22, 13, 0);

        PriceUpdateScheduler.GetMostRecentScheduledTime(settings, now)
            .ShouldBe(At(2026, 10, 22, 12, 0));
    }

    [Fact]
    public void WeekdaysOnSaturday_ShouldReturnFriday()
    {
        var settings = Settings(PriceUpdateFrequency.Weekdays);
        var saturday = At(2026, 10, 24, 13, 0);

        PriceUpdateScheduler.GetMostRecentScheduledTime(settings, saturday)
            .ShouldBe(At(2026, 10, 23, 12, 0));
    }

    [Fact]
    public void WeekdaysOnMondayMorning_ShouldReturnFriday()
    {
        var settings = Settings(PriceUpdateFrequency.Weekdays);
        var monday = At(2026, 10, 26, 11, 0);

        PriceUpdateScheduler.GetMostRecentScheduledTime(settings, monday)
            .ShouldBe(At(2026, 10, 23, 12, 0));
    }

    [Fact]
    public void EveryTwelveHoursBeforeAnchor_ShouldReturnMidnight()
    {
        var settings = Settings(PriceUpdateFrequency.Every12Hours);
        var now = At(2026, 10, 22, 11, 30);

        PriceUpdateScheduler.GetMostRecentScheduledTime(settings, now)
            .ShouldBe(At(2026, 10, 22, 0, 0));
        PriceUpdateScheduler.GetNextScheduledTime(settings, now)
            .ShouldBe(At(2026, 10, 22, 12, 0));
    }

    [Fact]
    public void EveryTwelveHoursAfterAnchor_ShouldReturnNoon()
    {
        var settings = Settings(PriceUpdateFrequency.Every12Hours);
        var now = At(2026, 10, 22, 13, 0);

        PriceUpdateScheduler.GetMostRecentScheduledTime(settings, now)
            .ShouldBe(At(2026, 10, 22, 12, 0));
        PriceUpdateScheduler.GetNextScheduledTime(settings, now)
            .ShouldBe(At(2026, 10, 23, 0, 0));
    }

    [Fact]
    public void EverySixHours_ShouldRespectTheInterval()
    {
        var settings = Settings(PriceUpdateFrequency.Every6Hours);
        var now = At(2026, 10, 22, 13, 30);

        PriceUpdateScheduler.GetMostRecentScheduledTime(settings, now)
            .ShouldBe(At(2026, 10, 22, 12, 0));
        PriceUpdateScheduler.GetNextScheduledTime(settings, now)
            .ShouldBe(At(2026, 10, 22, 18, 0));
    }

    [Fact]
    public void DisabledSettings_ShouldNotProduceScheduledTimes()
    {
        var settings = Settings(PriceUpdateFrequency.Daily) with { Enabled = false };
        var now = At(2026, 10, 22, 13, 0);

        PriceUpdateScheduler.GetMostRecentScheduledTime(settings, now).ShouldBeNull();
        PriceUpdateScheduler.GetNextScheduledTime(settings, now).ShouldBeNull();
    }

    [Fact]
    public void IsDue_ShouldBeTrueWhenLastRunIsOlderThanLatestOccurrence()
    {
        var settings = Settings(PriceUpdateFrequency.Daily);
        var now = At(2026, 10, 22, 13, 0);
        var status = Status(At(2026, 10, 21, 13, 0));

        PriceUpdateScheduler.IsDue(settings, status, now).ShouldBeTrue();
    }

    [Fact]
    public void IsDue_ShouldBeFalseWhenLastRunIsNewerThanLatestOccurrence()
    {
        var settings = Settings(PriceUpdateFrequency.Daily);
        var now = At(2026, 10, 22, 13, 0);
        var status = Status(At(2026, 10, 22, 12, 30));

        PriceUpdateScheduler.IsDue(settings, status, now).ShouldBeFalse();
    }

    private static PriceUpdateSettings Settings(PriceUpdateFrequency frequency) =>
        new(
            Enabled: true,
            TimeOfDay: new TimeOnly(12, 0),
            Frequency: frequency,
            Exchange: "XMIL");

    private static PriceUpdateStatus Status(DateTimeOffset lastRun) =>
        new(
            LastRunAt: lastRun,
            LastSuccessAt: lastRun,
            IsRunning: false,
            LastError: null,
            Results: []);

    private static DateTimeOffset At(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, Offset);
}
