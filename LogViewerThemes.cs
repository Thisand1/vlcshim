namespace VlcShimDebugFr;

internal sealed record LogViewerTheme(
    string Id,
    string Label,
    int BackgroundArgb,
    int SurfaceArgb,
    int ForegroundArgb,
    int MutedArgb,
    int AccentArgb,
    int SelectionArgb,
    int InfoArgb,
    int WarningArgb,
    int ErrorArgb,
    int StripeArgb);

internal static class LogViewerThemes
{
    public static readonly LogViewerTheme Default = new(
        "matrix",
        "Matrix",
        unchecked((int)0xFF14161F),
        unchecked((int)0xFF1B1F2B),
        unchecked((int)0xFFC7FFD8),
        unchecked((int)0xFF7D9586),
        unchecked((int)0xFF58F2A9),
        unchecked((int)0x88318063),
        unchecked((int)0xFF59D7FF),
        unchecked((int)0xFFFFCC66),
        unchecked((int)0xFFFF7E7E),
        unchecked((int)0x221D2B25));

    private static readonly IReadOnlyList<LogViewerTheme> Themes = new[]
    {
        Default,
        new LogViewerTheme(
            "tokyo-night",
            "Tokyo Night",
            unchecked((int)0xFF1A1B26),
            unchecked((int)0xFF24283B),
            unchecked((int)0xFFA9B1D6),
            unchecked((int)0xFF6C7397),
            unchecked((int)0xFFBB9AF7),
            unchecked((int)0x8833467C),
            unchecked((int)0xFF7DCFFF),
            unchecked((int)0xFFE0AF68),
            unchecked((int)0xFFF7768E),
            unchecked((int)0x2224283B)),
        new LogViewerTheme(
            "tokyo-night-storm",
            "Tokyo Night Storm",
            unchecked((int)0xFF24283B),
            unchecked((int)0xFF292E42),
            unchecked((int)0xFFC0CAF5),
            unchecked((int)0xFF7E88B6),
            unchecked((int)0xFFBB9AF7),
            unchecked((int)0x88364A82),
            unchecked((int)0xFF7AA2F7),
            unchecked((int)0xFFE0AF68),
            unchecked((int)0xFFF7768E),
            unchecked((int)0x22292E42)),
        new LogViewerTheme(
            "idx-dark",
            "Monospace IDX Dark",
            unchecked((int)0xFF10151D),
            unchecked((int)0xFF16202B),
            unchecked((int)0xFFA4AFBD),
            unchecked((int)0xFF6F7B8C),
            unchecked((int)0xFFA87FFB),
            unchecked((int)0x66FFFFFF),
            unchecked((int)0xFF25A6E9),
            unchecked((int)0xFFFFA23E),
            unchecked((int)0xFFF76769),
            unchecked((int)0x2216202B)),
        new LogViewerTheme(
            "amber",
            "Amber",
            unchecked((int)0xFF1C1208),
            unchecked((int)0xFF28190C),
            unchecked((int)0xFFFFD38B),
            unchecked((int)0xFFC9A06C),
            unchecked((int)0xFFFFA23E),
            unchecked((int)0x886D4B16),
            unchecked((int)0xFFFFD38B),
            unchecked((int)0xFFFFA23E),
            unchecked((int)0xFFFF8A5B),
            unchecked((int)0x2228190C)),
        new LogViewerTheme(
            "ice",
            "Ice",
            unchecked((int)0xFF0E1820),
            unchecked((int)0xFF132430),
            unchecked((int)0xFFD8F3FF),
            unchecked((int)0xFF87A7BA),
            unchecked((int)0xFF7DCFFF),
            unchecked((int)0x8824506A),
            unchecked((int)0xFF7DCFFF),
            unchecked((int)0xFFF7D774),
            unchecked((int)0xFFFF8A8A),
            unchecked((int)0x22132430))
    };

    public static IReadOnlyList<LogViewerTheme> All => Themes;

    public static LogViewerTheme Get(string? id)
    {
        return Themes.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? Default;
    }
}
