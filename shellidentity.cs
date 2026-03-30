using System.Runtime.InteropServices;

namespace VlcShimDebugFr;

internal static class ShellIdentity
{
    private const string AppsFolderPath = "shell:::{4234d49b-0245-4df3-b780-3893943456e1}";
    private const string FallbackVlcAppId = "VideoLAN.VLC";
    private const string VlcDisplayName = "VLC media player";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    public static void ApplyVlcIdentity()
    {
        var appId = Environment.GetEnvironmentVariable("VLC_SMTC_APPID");

        if (string.IsNullOrWhiteSpace(appId))
        {
            appId = TryResolveInstalledVlcAppId() ?? FallbackVlcAppId;
        }

        try
        {
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(appId));
        }
        catch
        {
            // Best effort only. The bridge can still function if shell identity setup fails.
        }
    }

    private static string? TryResolveInstalledVlcAppId()
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
                if (!string.Equals(name, VlcDisplayName, StringComparison.OrdinalIgnoreCase))
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

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }
}
