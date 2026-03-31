using System.Drawing;

namespace VlcShimDebugFr;

internal static class PlayerIcons
{
    public static Icon LoadForProfile(ShimConfig config)
    {
        string iconPath = PlayerIconResolver.ResolveIconPath(config);

        try
        {
            if (File.Exists(iconPath))
            {
                if (string.Equals(Path.GetExtension(iconPath), ".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var icon = new Icon(iconPath);
                    return (Icon)icon.Clone();
                }

                using Icon? extracted = Icon.ExtractAssociatedIcon(iconPath);
                if (extracted is not null)
                {
                    return (Icon)extracted.Clone();
                }
            }
        }
        catch
        {
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
