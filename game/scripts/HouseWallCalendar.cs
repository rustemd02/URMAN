using System.Globalization;
using System.Text;
using Godot;

namespace Urman.Godot;

/// <summary>
/// Printed face for the existing .49 x .63 x .02 metre wall-calendar page.
/// The whole Gregorian year is shown; no day is selected and no story state is owned here.
/// </summary>
public static class HouseWallCalendar
{
    private const int Year = 2026;
    private const int FontSize = 64;
    private const float PrintDepth = .012f;
    private const char FigureSpace = '\u2007';
    private static readonly string[] MonthNames =
    [
        "ЯНВАРЬ", "ФЕВРАЛЬ", "МАРТ", "АПРЕЛЬ", "МАЙ", "ИЮНЬ",
        "ИЮЛЬ", "АВГУСТ", "СЕНТЯБРЬ", "ОКТЯБРЬ", "НОЯБРЬ", "ДЕКАБРЬ"
    ];

    public static void AddPrintedFace(Node3D page)
    {
        if (page.GetNodeOrNull<Node3D>("Calendar2026Print") is not null) return;
        var print = new Node3D { Name = "Calendar2026Print" };
        print.SetMeta("presentationOnly", true);
        print.SetMeta("calendarYear", Year);
        print.SetMeta("calendarSystem", "Gregorian; Monday first; no selected date");
        page.AddChild(print);

        // This is the same bundled fallback font used by existing Russian Label3D
        // captions, so the print has no dependency on the machine's installed fonts.
        var font = ThemeDB.FallbackFont;
        AddInk(print, "Year", Year.ToString(CultureInfo.InvariantCulture),
            new(0, .291f, PrintDepth), new(.19f, .070f), font, HorizontalAlignment.Center);
        for (var month = 1; month <= 12; month++)
        {
            var column = (month - 1) % 3;
            var row = (month - 1) / 3;
            AddInk(print, $"Month{month:D2}", MonthText(month),
                new(-.2175f + column * .148f, .170f - row * .120f, PrintDepth),
                new(.139f, .100f), font, HorizontalAlignment.Left);
        }
    }

    private static string MonthText(int month)
    {
        var firstColumn = ((int)new DateTime(Year, month, 1).DayOfWeek + 6) % 7;
        var days = DateTime.DaysInMonth(Year, month);
        var text = new StringBuilder(MonthNames[month - 1]);
        text.Append("\nПН\u2007ВТ\u2007СР\u2007ЧТ\u2007ПТ\u2007СБ\u2007ВС");
        for (var row = 0; row < 6; row++)
        {
            text.Append('\n');
            for (var column = 0; column < 7; column++)
            {
                if (column > 0) text.Append(FigureSpace);
                var day = row * 7 + column - firstColumn + 1;
                // Figure spaces retain a numeral's width in the existing font,
                // including the empty Monday-first cells before the first day.
                text.Append(day >= 1 && day <= days
                    ? day.ToString(CultureInfo.InvariantCulture).PadLeft(2, FigureSpace)
                    : new string(FigureSpace, 2));
            }
        }
        return text.ToString();
    }

    private static void AddInk(Node3D parent, string name, string text, Vector3 at,
        Vector2 bounds, Font font, HorizontalAlignment alignment)
    {
        const float lineSpacing = 2f;
        var lines = text.Split('\n');
        var width = lines.Max(line => font.GetStringSize(line, HorizontalAlignment.Left, -1, FontSize).X);
        var height = font.GetHeight(FontSize) * lines.Length + lineSpacing * (lines.Length - 1);
        var label = new Label3D
        {
            Name = name, Text = text, Position = at, Font = font, FontSize = FontSize,
            PixelSize = Mathf.Min(bounds.X / Mathf.Max(1, width), bounds.Y / Mathf.Max(1, height)),
            LineSpacing = lineSpacing, HorizontalAlignment = alignment, VerticalAlignment = VerticalAlignment.Top,
            Modulate = new Color("493f34"), OutlineSize = 0, Shaded = true, DoubleSided = false,
            NoDepthTest = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(label);
    }
}
