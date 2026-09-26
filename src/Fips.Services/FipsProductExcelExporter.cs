using ClosedXML.Excel;
using Fips.Data;

namespace Fips.Services;

public static class FipsProductExcelExporter
{
    public static byte[] BuildWorkbook(
        IReadOnlyList<CompassServiceRegisterProduct> standard,
        IReadOnlyList<CompassServiceRegisterProduct> enterprise,
        DateTimeOffset generatedUtc)
    {
        using var wb = new XLWorkbook();
        AddProductSheet(wb, "Standard register", standard);
        AddProductSheet(wb, "Enterprise (active)", enterprise);
        AddAboutSheet(wb, generatedUtc, standard.Count, enterprise.Count);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void AddAboutSheet(XLWorkbook wb, DateTimeOffset generatedUtc, int standardCount, int enterpriseCount)
    {
        var ws = wb.Worksheets.Add("About");
        ws.Cell(1, 1).Value = "Export generated (UTC)";
        ws.Cell(1, 2).Value = generatedUtc.ToString("yyyy-MM-dd HH:mm:ss");
        ws.Cell(2, 1).Value = "Standard register rows";
        ws.Cell(2, 2).Value = standardCount;
        ws.Cell(3, 1).Value = "Enterprise (active) rows";
        ws.Cell(3, 2).Value = enterpriseCount;
        ws.Cell(4, 1).Value = "Source";
        ws.Cell(4, 2).Value = "COMPASS Service Register API (same data as this FIPS frontend).";
        ws.Columns().AdjustToContents();
    }

    private static void AddProductSheet(
        XLWorkbook wb,
        string sheetName,
        IReadOnlyList<CompassServiceRegisterProduct> products)
    {
        var ws = wb.Worksheets.Add(sheetName);
        var headers = new[]
        {
            "FIPS ID", "Name", "Phase", "Business area", "Status", "Enterprise service",
            "Product URL", "Channels", "Types", "Categories", "Contacts"
        };

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;

        var row = 2;
        foreach (var p in products)
        {
            ws.Cell(row, 1).Value = p.UniqueId ?? "";
            ws.Cell(row, 2).Value = p.ProductName ?? "";
            ws.Cell(row, 3).Value = p.Phase ?? "";
            ws.Cell(row, 4).Value = p.BusinessArea ?? "";
            ws.Cell(row, 5).Value = p.Status ?? "";
            ws.Cell(row, 6).Value = p.IsEnterpriseService ? "Yes" : "No";
            ws.Cell(row, 7).Value = p.ProductUrl ?? "";
            ws.Cell(row, 8).Value = FormatLookups(p.Channels);
            ws.Cell(row, 9).Value = FormatLookups(p.Types);
            ws.Cell(row, 10).Value = FormatCategories(p);
            ws.Cell(row, 11).Value = FormatContacts(p);
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static string FormatLookups(IReadOnlyList<CompassNamedLookup> items) =>
        items.Count == 0
            ? ""
            : string.Join(", ", items.Select(i => i.Name).Where(n => !string.IsNullOrWhiteSpace(n)));

    private static string FormatCategories(CompassServiceRegisterProduct p)
    {
        if (p.Categories.Count == 0)
            return "";

        return string.Join(
            "; ",
            p.Categories.Select(c =>
                string.IsNullOrWhiteSpace(c.GroupName)
                    ? (c.Name ?? "")
                    : $"{c.GroupName}: {c.Name}"));
    }

    private static string FormatContacts(CompassServiceRegisterProduct p)
    {
        if (p.Contacts.Count == 0)
            return "";

        return string.Join(
            "; ",
            p.Contacts.Select(c =>
            {
                var role = c.Role ?? "";
                var email = c.Email ?? "";
                var name = c.Name ?? "";
                var bit = string.Join(" — ", new[] { role, name, email }.Where(s => !string.IsNullOrWhiteSpace(s)));
                return bit;
            }));
    }
}
