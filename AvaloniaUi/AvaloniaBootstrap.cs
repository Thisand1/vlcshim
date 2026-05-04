using Avalonia;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;

namespace VlcShimDebugFr.AvaloniaUi;

public static class AvaloniaBootstrap
{
    private static bool _initialized;
    private static readonly object Sync = new();

    public static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .SetupWithoutStarting();

            DisableAvaloniaDataAnnotationValidation();
            _initialized = true;
        }
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var plugins = BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in plugins)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
