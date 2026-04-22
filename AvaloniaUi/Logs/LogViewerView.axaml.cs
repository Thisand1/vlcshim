using Avalonia;
using Avalonia.Threading;

namespace VlcShimDebugFr.AvaloniaUi.Logs;

public partial class LogViewerView : Avalonia.Controls.UserControl
{
    public LogViewerView()
    {
        InitializeComponent();
    }

    public bool IsNearBottom()
    {
        double visibleBottom = EntriesScrollViewer.Offset.Y + EntriesScrollViewer.Viewport.Height;
        return visibleBottom >= EntriesScrollViewer.Extent.Height - 72.0;
    }

    public void ScrollToEnd()
    {
        Dispatcher.UIThread.Post(() =>
        {
            double y = Math.Max(0.0, EntriesScrollViewer.Extent.Height - EntriesScrollViewer.Viewport.Height);
            EntriesScrollViewer.Offset = new Vector(EntriesScrollViewer.Offset.X, y);
        }, DispatcherPriority.Background);
    }
}
