using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace OutfitStudio.Services;

// Ghi file .xlsx một sheet bằng OOXML thuần (không cần gói NuGet). Ô chữ ghi dạng inline string.
public static class XlsxWriter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static byte[] Build(string sheetName, string[] headers, IEnumerable<object?[]> rows)
    {
        var data = rows.ToList();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
            Add(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Add(zip, "xl/workbook.xml",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"{Ns}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                $"<sheets><sheet name=\"{Esc(SafeSheetName(sheetName))}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add(zip, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            Add(zip, "xl/styles.xml",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"{Ns}\">" +
                "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8E8E8\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/></cellXfs>" +
                "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
            Add(zip, "xl/worksheets/sheet1.xml", SheetXml(headers, data));
        }
        return ms.ToArray();
    }

    private static string SheetXml(string[] headers, List<object?[]> data)
    {
        var sb = new StringBuilder();
        sb.Append($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"{Ns}\">");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        sb.Append("<cols>");
        for (var c = 0; c < headers.Length; c++)
        {
            var len = headers[c].Length;
            foreach (var r in data)
            if (c < r.Length)
                len = Math.Max(len, Text(r[c]).Length);
            sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{Math.Min(Math.Max(len + 3, 10), 60)}\" customWidth=\"1\"/>");
        }
        sb.Append("</cols><sheetData><row r=\"1\">");
        for (var c = 0; c < headers.Length; c++)
            sb.Append(Cell(c, 1, headers[c], 1));
        sb.Append("</row>");
        for (var r = 0; r < data.Count; r++)
        {
            sb.Append($"<row r=\"{r + 2}\">");
            for (var c = 0; c < headers.Length && c < data[r].Length; c++)
                sb.Append(Cell(c, r + 2, data[r][c], 0));
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");
        if (data.Count > 0)
            sb.Append($"<autoFilter ref=\"A1:{Col(headers.Length - 1)}{data.Count + 1}\"/>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static string Cell(int col, int row, object? value, int style)
    {
        var s = style == 0 ? "" : $" s=\"{style}\"";
        var r = Col(col) + row;
        switch (value)
        {
            case null:
                return $"<c r=\"{r}\"{s}/>";
            case int or long or short or byte:
                return $"<c r=\"{r}\"{s}><v>{Convert.ToInt64(value, CultureInfo.InvariantCulture)}</v></c>";
            case decimal d:
                return $"<c r=\"{r}\"{s}><v>{d.ToString(CultureInfo.InvariantCulture)}</v></c>";
            case double d:
                return $"<c r=\"{r}\"{s}><v>{d.ToString("R", CultureInfo.InvariantCulture)}</v></c>";
            default:
                return $"<c r=\"{r}\"{s} t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(Text(value))}</t></is></c>";
        }
    }

    private static string Text(object? v) => v switch
    {
        null => "",
        DateTime dt => dt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? ""
    };

    private static string Col(int index)
    {
        var s = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
            s = (char)('A' + (n - 1) % 26) + s;
        return s;
    }

    // Bỏ ký tự điều khiển không hợp lệ trong XML rồi escape.
    private static string Esc(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch < 0x20 && ch is not ('\t' or '\n' or '\r'))
                continue;
            sb.Append(ch switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                _ => ch.ToString()
            });
        }
        return sb.ToString();
    }

    private static string SafeSheetName(string name)
    {
        var clean = new string(name.Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
        if (clean.Length == 0)
            clean = "Sheet1";
        return clean.Length > 31 ? clean[..31] : clean;
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        w.Write(content);
    }
}
