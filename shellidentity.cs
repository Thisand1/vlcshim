using System.Runtime.InteropServices;

namespace VlcShimDebugFr;

internal sealed record ShellIdentityResult(string DisplayName, string AppUserModelId, bool Applied, bool MatchedInstalledApp);

internal static class ShellIdentity
{
    private const string AppsFolderPath = "shell:::{4234d49b-0245-4df3-b780-3893943456e1}";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    public static ShellIdentityResult ApplyConfiguredIdentity(ShimConfig config)
    {
        var appId = Environment.GetEnvironmentVariable("VLC_SMTC_APPID");
        string displayName = PlayerIdentityProfiles.GetDisplayName(config);
        bool matchedInstalledApp = false;

        if (string.IsNullOrWhiteSpace(appId))
        {
            appId = TryResolveInstalledAppId(displayName);
            matchedInstalledApp = !string.IsNullOrWhiteSpace(appId);
        }

        if (string.IsNullOrWhiteSpace(appId))
        {
            appId = PlayerIdentityProfiles.GetFallbackAppUserModelId(config);
        }

        try
        {
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(appId));
            return new ShellIdentityResult(displayName, appId, true, matchedInstalledApp);
        }
        catch
        {
            // Best effort only. The bridge can still function if shell identity setup fails.
            return new ShellIdentityResult(displayName, appId, false, matchedInstalledApp);
        }
    }

    private static string? TryResolveInstalledAppId(string displayName)
    {
        object? shell = null;
        object? appsFolder = null;
        object? items = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
            {
                return null;
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return null;
            }

            dynamic shellApp = shell;
            appsFolder = shellApp.NameSpace(AppsFolderPath);
            if (appsFolder is null)
            {
                return null;
            }

            dynamic folder = appsFolder;
            items = folder.Items();
            if (items is null)
            {
                return null;
            }

            if (items is not System.Collections.IEnumerable enumerable)
            {
                return null;
            }

            foreach (dynamic item in enumerable)
            {
                string? name = item?.Name as string;
                if (!IsMatchingDisplayName(name, displayName))
                {
                    continue;
                }

                string? appId = item?.Path as string;
                if (!string.IsNullOrWhiteSpace(appId))
                {
                    return appId;
                }
            }
        }
        catch
        {
        }
        finally
        {
            ReleaseComObject(items);
            ReleaseComObject(appsFolder);
            ReleaseComObject(shell);
        }

        return null;
    }

    private static bool IsMatchingDisplayName(string? candidate, string expected)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return string.Equals(candidate, expected, StringComparison.OrdinalIgnoreCase) ||
               candidate.Contains(expected, StringComparison.OrdinalIgnoreCase) ||
               expected.Contains(candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }
}
