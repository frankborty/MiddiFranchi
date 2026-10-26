namespace MiddiFranchi.Web.Components.Shared;

/// <summary>
/// Stato di ordinamento di una tabella: colonna attiva e direzione.
/// </summary>
public sealed class TableSort
{
    public TableSort(string column, bool descending = false)
    {
        Column = column;
        Descending = descending;
    }

    public string Column { get; private set; }

    public bool Descending { get; private set; }

    public bool IsActive(string column) => string.Equals(Column, column, StringComparison.Ordinal);

    public void Toggle(string column)
    {
        if (IsActive(column))
        {
            Descending = !Descending;
            return;
        }

        Column = column;
        Descending = false;
    }

    public string AriaSort(string column) =>
        !IsActive(column) ? "none" : Descending ? "descending" : "ascending";

    public string Arrow(string column) => IsActive(column) ? (Descending ? "↓" : "↑") : "";

    public IEnumerable<T> Apply<T, TKey>(
        IEnumerable<T> items,
        Func<T, TKey> key,
        IComparer<TKey>? comparer = null) =>
        Descending
            ? items.OrderByDescending(key, comparer)
            : items.OrderBy(key, comparer);
}
