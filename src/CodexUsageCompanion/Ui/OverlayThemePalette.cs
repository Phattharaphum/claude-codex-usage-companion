namespace CodexUsageCompanion.Ui;

internal sealed record OverlayThemePalette(
    string RootBackground,
    string RootBorder,
    string Shadow,
    string HeaderForeground,
    string CardBackground,
    string CardBorder,
    string CardTitle,
    string SecondaryText,
    string StatusText,
    string ErrorText,
    string EmptyCell,
    string Green,
    string Yellow,
    string Orange,
    string Red,
    string Gray)
{
    public static OverlayThemePalette Dark { get; } = new(
        "#F2181B1F",
        "#40FFFFFF",
        "#80000000",
        "#FFE6E9EC",
        "#FF22272D",
        "#FF303740",
        "#FFF2F4F6",
        "#FFA3ACB5",
        "#FF8A939C",
        "#FFFF8A80",
        "#FF2E343B",
        "#FF3DD598",
        "#FFF2C94C",
        "#FFFF9F43",
        "#FFFF6B6B",
        "#FF7A838C");

    public static OverlayThemePalette Light { get; } = new(
        "#FFFAFBFC",
        "#FFDDE2E7",
        "#00000000",
        "#FF2B3036",
        "#FFFFFFFF",
        "#FFE2E6EA",
        "#FF1B1F23",
        "#FF5B646D",
        "#FF6B747D",
        "#FFC62828",
        "#FFE4E8EC",
        "#FF12805A",
        "#FF946F00",
        "#FFC25A00",
        "#FFC62828",
        "#FF6F7880");
}
