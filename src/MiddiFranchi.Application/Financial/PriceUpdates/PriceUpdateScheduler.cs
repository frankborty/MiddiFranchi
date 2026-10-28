namespace MiddiFranchi.Application.Financial;

/// <summary>
/// Calcola le occorrenze dello scheduler assumendo che <c>now</c> sia già espresso
/// nell'ora locale dell'utente.
/// </summary>
public static class PriceUpdateScheduler
{
    /// <summary>
    /// "Adesso" nel fuso orario scelto dall'utente: l'orario pianificato è locale all'utente,
    /// non al server (che in Docker spesso gira in UTC).
    /// </summary>
    public static DateTimeOffset ResolveNow(PriceUpdateSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.TimeZoneId) ||
            !TimeZoneInfo.TryFindSystemTimeZoneById(settings.TimeZoneId, out var zone))
        {
            return DateTimeOffset.Now;
        }

        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
    }

    public static DateTimeOffset? GetMostRecentScheduledTime(
        PriceUpdateSettings settings,
        DateTimeOffset now)
    {
        if (!settings.Enabled)
        {
            return null;
        }

        return settings.Frequency switch
        {
            PriceUpdateFrequency.Daily =>
                MostRecentDaily(settings.TimeOfDay, now, weekdaysOnly: false),
            PriceUpdateFrequency.Weekdays =>
                MostRecentDaily(settings.TimeOfDay, now, weekdaysOnly: true),
            PriceUpdateFrequency.Every12Hours =>
                MostRecentInterval(settings.TimeOfDay, now, TimeSpan.FromHours(12)),
            PriceUpdateFrequency.Every6Hours =>
                MostRecentInterval(settings.TimeOfDay, now, TimeSpan.FromHours(6)),
            _ => null,
        };
    }

    public static DateTimeOffset? GetNextScheduledTime(
        PriceUpdateSettings settings,
        DateTimeOffset now)
    {
        if (!settings.Enabled)
        {
            return null;
        }

        return settings.Frequency switch
        {
            PriceUpdateFrequency.Daily =>
                NextDaily(settings.TimeOfDay, now, weekdaysOnly: false),
            PriceUpdateFrequency.Weekdays =>
                NextDaily(settings.TimeOfDay, now, weekdaysOnly: true),
            PriceUpdateFrequency.Every12Hours =>
                NextInterval(settings.TimeOfDay, now, TimeSpan.FromHours(12)),
            PriceUpdateFrequency.Every6Hours =>
                NextInterval(settings.TimeOfDay, now, TimeSpan.FromHours(6)),
            _ => null,
        };
    }

    public static bool IsDue(
        PriceUpdateSettings settings,
        PriceUpdateStatus status,
        DateTimeOffset now)
    {
        var due = GetMostRecentScheduledTime(settings, now);
        if (due is null)
        {
            return false;
        }

        return status.LastRunAt is not { } lastRun || lastRun < due;
    }

    private static DateTimeOffset MostRecentDaily(
        TimeOnly time,
        DateTimeOffset now,
        bool weekdaysOnly)
    {
        var candidate = At(now.Date, time, now.Offset);
        if (candidate > now)
        {
            candidate = candidate.AddDays(-1);
        }

        if (weekdaysOnly)
        {
            while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                candidate = candidate.AddDays(-1);
            }
        }

        return candidate;
    }

    private static DateTimeOffset NextDaily(
        TimeOnly time,
        DateTimeOffset now,
        bool weekdaysOnly)
    {
        var candidate = At(now.Date, time, now.Offset);
        if (candidate <= now)
        {
            candidate = candidate.AddDays(1);
        }

        if (weekdaysOnly)
        {
            while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                candidate = candidate.AddDays(1);
            }
        }

        return candidate;
    }

    private static DateTimeOffset MostRecentInterval(
        TimeOnly anchorTime,
        DateTimeOffset now,
        TimeSpan interval)
    {
        var candidate = At(now.Date, anchorTime, now.Offset);
        while (candidate > now)
        {
            candidate = candidate.Subtract(interval);
        }

        while (candidate.Add(interval) <= now)
        {
            candidate = candidate.Add(interval);
        }

        return candidate;
    }

    private static DateTimeOffset NextInterval(
        TimeOnly anchorTime,
        DateTimeOffset now,
        TimeSpan interval)
    {
        var candidate = At(now.Date, anchorTime, now.Offset);
        while (candidate <= now)
        {
            candidate = candidate.Add(interval);
        }

        return candidate;
    }

    private static DateTimeOffset At(DateTime date, TimeOnly time, TimeSpan offset) =>
        new(date.Date + time.ToTimeSpan(), offset);
}
