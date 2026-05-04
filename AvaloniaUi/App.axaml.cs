using Avalonia.Markup.Xaml;

namespace VlcShimDebugFr.AvaloniaUi;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
