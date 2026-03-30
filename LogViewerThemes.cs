namespace VlcShimDebugFr;

internal sealed record LogViewerTheme(
    string Id,
    string Label,
    int BackgroundArgb,
    int ForegroundArgb,
    int SelectionArgb);

internal static class LogViewerThemes
{
    public static readonly LogViewerTheme Default = new(
        "matrix",
        "Matrix",
        unchecked((int)0xFF14161F),
        unchecked((int)0xFFC7FFD8),
        unchecked((int)0xFF2E6A4F));

    private static readonly IReadOnlyList<LogViewerTheme> Themes = new[]
    {
        Default,
        new LogViewerTheme(
            "tokyo-night",
            "Tokyo Night",
            unchecked((int)0xFF1A1B26),
            unchecked((int)0xFFA9B1D6),
            unchecked((int)0xFF33467C)),
        new LogViewerTheme(
            "tokyo-night-storm",
            "Tokyo Night Storm",
            unchecked((int)0xFF24283B),
            unchecked((int)0xFFC0CAF5),
            unchecked((int)0xFF364A82)),
        new LogViewerTheme(
            "idx-dark",
            "Monospace IDX Dark",
            unchecked((int)0xFF10151D),
            unchecked((int)0xFFA4AFBD),
            unchecked((int)0xFFFFFFFF)),
        new LogViewerTheme(
            "amber",
            "Amber",
            unchecked((int)0xFF1C1208),
            unchecked((int)0xFFFFD38B),
            unchecked((int)0xFF6D4B16)),
        new LogViewerTheme(
            "ice",
            "Ice",
            unchecked((int)0xFF0E1820),
            unchecked((int)0xFFD8F3FF),
            unchecked((int)0xFF24506A))
    };

    public static IReadOnlyList<LogViewerTheme> All => Themes;

    public static LogViewerTheme Get(string? id)
    {
        return Themes.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? Default;
    }
}
