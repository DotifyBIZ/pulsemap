using PdfSharp.Fonts;

namespace Pulsemap.App.Core.Export;

/// <summary>
/// Resolves fonts from the Windows Fonts directory rather than embedding/redistributing font
/// files — Segoe UI ships with every Windows install (this app is Windows-only, ADR-0001), and
/// Microsoft's ClearType font EULA generally doesn't permit redistribution outside Windows anyway.
/// </summary>
internal sealed class WindowsFontResolver : IFontResolver
{
    public static readonly WindowsFontResolver Instance = new();

    private static readonly string FontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    public byte[] GetFont(string faceName) => File.ReadAllBytes(Path.Combine(FontsDirectory, $"{faceName}.ttf"));

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "segoeuib" : "segoeui");
}
