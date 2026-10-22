using MiddiFranchi.Domain.Financial;

namespace MiddiFranchi.Web.Components.Shared;

public sealed record CategoryAdminRow(
    Category Category,
    int OperationCount);

public sealed record CategoryAdminGroup(
    string Name,
    CategoryType Type,
    IReadOnlyList<CategoryAdminRow> Rows,
    int OperationCount)
{
    public Category Primary => Rows[0].Category;

    public int SubcategoryCount => Rows.Count;

    public string Meta => Type == CategoryType.Expense
        ? $"{SubcategoryCount} {(SubcategoryCount == 1 ? "sottocategoria" : "sottocategorie")} · {OperationCount} {(OperationCount == 1 ? "operazione" : "operazioni")}"
        : $"{OperationCount} {(OperationCount == 1 ? "operazione" : "operazioni")}";
}
