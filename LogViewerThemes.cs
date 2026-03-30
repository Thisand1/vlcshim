namespace VlcShimDebugFr;

internal sealed record LogViewerTheme(
    string Id,
    string Label,
    int BackgroundArgb,
    int ForegroundArgb);

internal static class LogViewerThemes
{
    public static readonly LogViewerTheme Default = new(
        "matrix",
        "Matrix",
        unchecked((int)0xFF14161F),
        unchecked((int)0xFFC7FFD8));

    private static readonly IReadOnlyList<LogViewerTheme> Themes = new[]
    {
        Default,
        new LogViewerTheme(
            "tokyo-night-storm",
            "Tokyo Night Storm",
            unchecked((int)0xFF24283B),
            unchecked((int)0xFFC0CAF5)),
        new LogViewerTheme(
            "idx-dark",
            "Monospace IDX Dark",
            unchecked((int)0xFF1E1F22),
            unchecked((int)0xFFD7DCE2)),
        new LogViewerTheme("amber", "Amber", unchecked((int)0xFF1C1208), unchecked((int)0xFFFFD38B)),
        new LogViewerTheme("ice", "Ice", unchecked((int)0xFF0E1820), unchecked((int)0xFFD8F3FF))
    };

    public static IReadOnlyList<LogViewerTheme> All => Themes;

    public static LogViewerTheme Get(string? id)
    {
        return Themes.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? Default;
    }
}
