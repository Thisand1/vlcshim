using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Windows.Forms;
using SharpGen.Runtime;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct2D1.D2D1;
using static Vortice.DirectWrite.DWrite;
using DrawingColor = System.Drawing.Color;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingSize = System.Drawing.Size;
using D2DPixelFormat = Vortice.DCommon.PixelFormat;

namespace VlcShimDebugFr;

internal sealed class DirectWriteLogControl : ScrollableControl
{
    private const float FontSizeDip = 13.0f;
    private const float HorizontalPadding = 18.0f;
    private const float VerticalPadding = 14.0f;
    private const float GutterWidth = 14.0f;
    private const float GutterGap = 12.0f;
    private const float LineInnerPadding = 4.0f;
    private const float MarkerInset = 5.0f;
    private const int SelectionHitCapacity = 8;

    private readonly List<LogLine> _lines = new();
    private LogViewerTheme _theme;
    private readonly string _fontFamilyName;
    private string _backgroundImagePath;
    private float _backgroundOpacity;
    private float _backgroundDimOpacity;

    private ID2D1Factory? _d2dFactory;
    private IDWriteFactory? _dwriteFactory;
    private ID2D1HwndRenderTarget? _renderTarget;
    private IDWriteTextFormat? _textFormat;
    private ID2D1Bitmap? _backgroundBitmap;
    private ID2D1SolidColorBrush? _foregroundBrush;
    private ID2D1SolidColorBrush? _mutedBrush;
    private ID2D1SolidColorBrush? _accentBrush;
    private ID2D1SolidColorBrush? _backgroundDimBrush;
    private ID2D1SolidColorBrush? _infoBrush;
    private ID2D1SolidColorBrush? _warningBrush;
    private ID2D1SolidColorBrush? _errorBrush;
    private ID2D1SolidColorBrush? _surfaceBrush;
    private ID2D1SolidColorBrush? _stripeBrush;
    private ID2D1SolidColorBrush? _selectionBrush;

    private float _lineHeight = 24.0f;
    private float _maxLineWidth;
    private bool _selecting;
    private TextAnchor? _selectionStart;
    private TextAnchor? _selectionEnd;

    public DirectWriteLogControl(LogViewerTheme theme, ShimConfig config)
    {
        _theme = theme;
        _fontFamilyName = ChooseFontFamily();
        _backgroundImagePath = config.LogViewerBackgroundImagePath?.Trim() ?? string.Empty;
        _backgroundOpacity = Math.Clamp(config.LogViewerBackgroundOpacityPercent, 0, 100) / 100.0f;
        _backgroundDimOpacity = Math.Clamp(config.LogViewerBackgroundDimPercent, 0, 100) / 100.0f;

        AutoScroll = true;
        BackColor = DrawingColor.FromArgb(_theme.BackgroundArgb);
        DoubleBuffered = false;
        TabStop = true;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.Opaque |
            ControlStyles.Selectable |
            ControlStyles.ResizeRedraw,
            true);
    }

    public void LoadLines(IEnumerable<string> lines)
    {
        _lines.Clear();
        _maxLineWidth = 0.0f;

        foreach (string line in lines)
        {
            _lines.Add(ParseLine(line));
        }

        RefreshMeasurements();
        ClearSelection();
        ScrollToBottom();
        Invalidate();
    }

    public void AppendLine(string line)
    {
        bool wasNearBottom = IsNearBottom();
        _lines.Add(ParseLine(line));
        TrimLinesIfNeeded();
        RefreshMeasurements();

        if (wasNearBottom)
        {
            ScrollToBottom();
        }

        Invalidate();
    }

    public void ApplyConfig(LogViewerTheme theme, ShimConfig config)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action<LogViewerTheme, ShimConfig>(ApplyConfig), theme, config);
            }
            catch
            {
            }

            return;
        }

        _theme = theme;
        _backgroundImagePath = config.LogViewerBackgroundImagePath?.Trim() ?? string.Empty;
        _backgroundOpacity = Math.Clamp(config.LogViewerBackgroundOpacityPercent, 0, 100) / 100.0f;
        _backgroundDimOpacity = Math.Clamp(config.LogViewerBackgroundDimPercent, 0, 100) / 100.0f;
        BackColor = DrawingColor.FromArgb(_theme.BackgroundArgb);
        CreateDeviceResources();
        RefreshMeasurements();
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        CreateDeviceResources();
        RefreshMeasurements();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DisposeDeviceResources();
        base.OnHandleDestroyed(e);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ResizeRenderTarget();
        UpdateScrollArea();
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Render();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _selecting = true;
        TextAnchor anchor = HitTestAnchor(e.Location);
        _selectionStart = anchor;
        _selectionEnd = anchor;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_selecting)
        {
            return;
        }

        _selectionEnd = HitTestAnchor(e.Location);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left)
        {
            _selecting = false;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Invalidate();
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData switch
        {
            Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageDown or Keys.PageUp => true,
            _ => base.IsInputKey(keyData)
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Control && e.KeyCode == Keys.A)
        {
            if (_lines.Count > 0)
            {
                _selectionStart = new TextAnchor(0, 0);
                _selectionEnd = new TextAnchor(_lines.Count - 1, _lines[^1].Text.Length);
                Invalidate();
            }

            e.SuppressKeyPress = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.C)
        {
            string? selected = GetSelectedText();
            if (!string.IsNullOrEmpty(selected))
            {
                Clipboard.SetText(selected);
            }

            e.SuppressKeyPress = true;
            return;
        }

        int verticalStep = Math.Max(1, (int)MathF.Ceiling(_lineHeight));
        int pageStep = Math.Max(verticalStep, ClientSize.Height - verticalStep);
        int horizontalStep = 32;

        switch (e.KeyCode)
        {
            case Keys.Up:
                ScrollBy(0, -verticalStep);
                e.SuppressKeyPress = true;
                break;
            case Keys.Down:
                ScrollBy(0, verticalStep);
                e.SuppressKeyPress = true;
                break;
            case Keys.PageUp:
                ScrollBy(0, -pageStep);
                e.SuppressKeyPress = true;
                break;
            case Keys.PageDown:
                ScrollBy(0, pageStep);
                e.SuppressKeyPress = true;
                break;
            case Keys.Home:
                AutoScrollPosition = new Point(-AutoScrollPosition.X, 0);
                Invalidate();
                e.SuppressKeyPress = true;
                break;
            case Keys.End:
                ScrollToBottom();
                Invalidate();
                e.SuppressKeyPress = true;
                break;
            case Keys.Left:
                ScrollBy(-horizontalStep, 0);
                e.SuppressKeyPress = true;
                break;
            case Keys.Right:
                ScrollBy(horizontalStep, 0);
                e.SuppressKeyPress = true;
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeDeviceResources();
        }

        base.Dispose(disposing);
    }

    private void TrimLinesIfNeeded()
    {
        int totalChars = _lines.Sum(line => line.Text.Length + Environment.NewLine.Length);

        while (totalChars > LogViewerForm.MaxDisplayedChars && _lines.Count > 0)
        {
            LogLine removed = _lines[0];
            totalChars -= removed.Text.Length + Environment.NewLine.Length;
            _lines.RemoveAt(0);
            ShiftSelectionAfterTrim();
        }
    }

    private void ShiftSelectionAfterTrim()
    {
        if (_selectionStart is TextAnchor start)
        {
            _selectionStart = start.Line > 0 ? start with { Line = start.Line - 1 } : new TextAnchor(0, 0);
        }

        if (_selectionEnd is TextAnchor end)
        {
            _selectionEnd = end.Line > 0 ? end with { Line = end.Line - 1 } : new TextAnchor(0, 0);
        }

        if (_lines.Count == 0)
        {
            ClearSelection();
        }
    }

    private void ClearSelection()
    {
        _selectionStart = null;
        _selectionEnd = null;
    }

    private void CreateDeviceResources()
    {
        DisposeDeviceResources();

        if (!IsHandleCreated || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        _d2dFactory = D2D1CreateFactory<ID2D1Factory>(Vortice.Direct2D1.FactoryType.SingleThreaded);
        _dwriteFactory = DWriteCreateFactory<IDWriteFactory>(Vortice.DirectWrite.FactoryType.Shared);

        var renderTargetProperties = new RenderTargetProperties(
            RenderTargetType.Default,
            new D2DPixelFormat(Format.Unknown, Vortice.DCommon.AlphaMode.Ignore),
            0.0f,
            0.0f,
            RenderTargetUsage.None,
            FeatureLevel.Default);

        var hwndProperties = new HwndRenderTargetProperties
        {
            Hwnd = Handle,
            PixelSize = new SizeI(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height)),
            PresentOptions = PresentOptions.Immediately
        };

        _renderTarget = _d2dFactory.CreateHwndRenderTarget(renderTargetProperties, hwndProperties);
        _renderTarget.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Cleartype;

        _foregroundBrush = _renderTarget.CreateSolidColorBrush(ToColor4(DrawingColor.FromArgb(_theme.ForegroundArgb)));
        _mutedBrush = _renderTarget.CreateSolidColorBrush(ToColor4(DrawingColor.FromArgb(_theme.MutedArgb)));
        _accentBrush = _renderTarget.CreateSolidColorBrush(ToColor4(DrawingColor.FromArgb(_theme.AccentArgb)));
        _backgroundDimBrush = _renderTarget.CreateSolidColorBrush(ToColor4(WithOpacity(DrawingColor.FromArgb(_theme.BackgroundArgb), _backgroundDimOpacity)));
        _infoBrush = _renderTarget.CreateSolidColorBrush(ToColor4(DrawingColor.FromArgb(_theme.InfoArgb)));
        _warningBrush = _renderTarget.CreateSolidColorBrush(ToColor4(DrawingColor.FromArgb(_theme.WarningArgb)));
        _errorBrush = _renderTarget.CreateSolidColorBrush(ToColor4(DrawingColor.FromArgb(_theme.ErrorArgb)));
        _surfaceBrush = _renderTarget.CreateSolidColorBrush(ToColor4(DrawingColor.FromArgb(_theme.SurfaceArgb)));
        _stripeBrush = _renderTarget.CreateSolidColorBrush(ToColor4(DrawingColor.FromArgb(_theme.StripeArgb)));
        _selectionBrush = _renderTarget.CreateSolidColorBrush(ToColor4(DrawingColor.FromArgb(_theme.SelectionArgb)));
        _backgroundBitmap = CreateBackgroundBitmap();

        _textFormat = _dwriteFactory.CreateTextFormat(
            _fontFamilyName,
            null,
            FontWeight.Normal,
            Vortice.DirectWrite.FontStyle.Normal,
            FontStretch.Normal,
            FontSizeDip,
            CultureInfo.CurrentCulture.Name);
        _textFormat.WordWrapping = WordWrapping.NoWrap;
        _textFormat.TextAlignment = TextAlignment.Leading;
        _textFormat.ParagraphAlignment = ParagraphAlignment.Near;
    }

    private void DisposeDeviceResources()
    {
        _backgroundBitmap?.Dispose();
        _backgroundBitmap = null;
        _backgroundDimBrush?.Dispose();
        _backgroundDimBrush = null;
        _selectionBrush?.Dispose();
        _selectionBrush = null;
        _stripeBrush?.Dispose();
        _stripeBrush = null;
        _surfaceBrush?.Dispose();
        _surfaceBrush = null;
        _errorBrush?.Dispose();
        _errorBrush = null;
        _warningBrush?.Dispose();
        _warningBrush = null;
        _infoBrush?.Dispose();
        _infoBrush = null;
        _accentBrush?.Dispose();
        _accentBrush = null;
        _mutedBrush?.Dispose();
        _mutedBrush = null;
        _foregroundBrush?.Dispose();
        _foregroundBrush = null;
        _textFormat?.Dispose();
        _textFormat = null;
        _renderTarget?.Dispose();
        _renderTarget = null;
        _dwriteFactory?.Dispose();
        _dwriteFactory = null;
        _d2dFactory?.Dispose();
        _d2dFactory = null;
    }

    private void ResizeRenderTarget()
    {
        if (_renderTarget is null || !IsHandleCreated)
        {
            return;
        }

        _renderTarget.Resize(new SizeI(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height)));
    }

    private void RefreshMeasurements()
    {
        if (_dwriteFactory is null || _textFormat is null)
        {
            UpdateScrollArea();
            return;
        }

        float measuredHeight = MeasureTextHeight("Ag");
        if (measuredHeight > 0.0f)
        {
            _lineHeight = measuredHeight + (LineInnerPadding * 2.0f);
        }

        _maxLineWidth = 0.0f;

        foreach (LogLine line in _lines)
        {
            float totalWidth = 0.0f;

            foreach (LogSegment segment in line.Segments)
            {
                segment.Width = MeasureTextWidth(segment.Text);
                totalWidth += segment.Width;
            }

            line.Width = totalWidth;
            _maxLineWidth = MathF.Max(_maxLineWidth, totalWidth);
        }

        UpdateScrollArea();
    }

    private void UpdateScrollArea()
    {
        float leftContentOffset = HorizontalPadding + GutterWidth + GutterGap;
        int width = Math.Max(ClientSize.Width, (int)Math.Ceiling(_maxLineWidth + leftContentOffset + HorizontalPadding));
        int height = Math.Max(ClientSize.Height, (int)Math.Ceiling((_lines.Count * _lineHeight) + (VerticalPadding * 2.0f)));
        AutoScrollMinSize = new DrawingSize(width, height);
    }

    private void ScrollToBottom()
    {
        int y = Math.Max(0, AutoScrollMinSize.Height - ClientSize.Height);
        int x = Math.Max(0, -AutoScrollPosition.X);
        AutoScrollPosition = new Point(x, y);
    }

    private void ScrollBy(int deltaX, int deltaY)
    {
        int x = Math.Max(0, -AutoScrollPosition.X + deltaX);
        int y = Math.Max(0, -AutoScrollPosition.Y + deltaY);
        AutoScrollPosition = new Point(x, y);
        Invalidate();
    }

    private bool IsNearBottom()
    {
        int scrollY = -AutoScrollPosition.Y;
        int visibleBottom = scrollY + ClientSize.Height;
        return visibleBottom >= AutoScrollMinSize.Height - Math.Max((int)Math.Ceiling(_lineHeight * 2.0f), 32);
    }

    private float MeasureTextHeight(string text)
    {
        using IDWriteTextLayout? layout = CreateLayout(text);
        return layout?.Metrics.Height ?? (_lineHeight - (LineInnerPadding * 2.0f));
    }

    private float MeasureTextWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0.0f;
        }

        using IDWriteTextLayout? layout = CreateLayout(text);
        return layout?.Metrics.WidthIncludingTrailingWhitespace ?? 0.0f;
    }

    private IDWriteTextLayout? CreateLayout(string text)
    {
        if (_dwriteFactory is null || _textFormat is null)
        {
            return null;
        }

        return _dwriteFactory.CreateTextLayout(text, _textFormat, 100000.0f, Math.Max(64.0f, _lineHeight * 2.0f));
    }

    private void Render()
    {
        if (_renderTarget is null || _textFormat is null || _foregroundBrush is null || _surfaceBrush is null)
        {
            CreateDeviceResources();
            RefreshMeasurements();

            if (_renderTarget is null || _textFormat is null || _foregroundBrush is null || _surfaceBrush is null)
            {
                return;
            }
        }

        try
        {
            _renderTarget.BeginDraw();
            _renderTarget.Clear(ToColor4(DrawingColor.FromArgb(_theme.BackgroundArgb)));

            DrawBackdrop();

            float gutterRight = HorizontalPadding + GutterWidth;
            _renderTarget.FillRectangle(new Rect(0, 0, gutterRight + 6.0f, ClientSize.Height), _surfaceBrush);

            var selection = GetNormalizedSelection();
            float textOriginX = AutoScrollPosition.X + HorizontalPadding + GutterWidth + GutterGap;
            float rowsOriginY = AutoScrollPosition.Y + VerticalPadding;
            int firstVisibleLine = Math.Max(0, (int)Math.Floor((-AutoScrollPosition.Y - VerticalPadding) / _lineHeight));
            int visibleCount = Math.Max(1, (int)Math.Ceiling(ClientSize.Height / _lineHeight) + 2);
            int lastVisibleLine = Math.Min(_lines.Count - 1, firstVisibleLine + visibleCount);

            for (int lineIndex = firstVisibleLine; lineIndex <= lastVisibleLine; lineIndex++)
            {
                LogLine line = _lines[lineIndex];
                float rowTop = rowsOriginY + (lineIndex * _lineHeight);
                float textY = rowTop + LineInnerPadding;

                if ((lineIndex & 1) == 1 && _stripeBrush is not null)
                {
                    _renderTarget.FillRectangle(new Rect(0, rowTop, ClientSize.Width, _lineHeight), _stripeBrush);
                }

                DrawMarker(line.Level, rowTop);

                using IDWriteTextLayout? fullLayout = CreateLayout(line.Text);
                if (fullLayout is null)
                {
                    continue;
                }

                if (selection is SelectionRange selectionRange)
                {
                    DrawSelectionForLine(lineIndex, line.Text.Length, selectionRange, fullLayout, textOriginX, textY);
                }

                DrawStyledLine(line, textOriginX, textY);
            }

            if (Focused)
            {
                _renderTarget.DrawRectangle(
                    new Rect(0.5f, 0.5f, ClientSize.Width - 1.0f, ClientSize.Height - 1.0f),
                    _accentBrush ?? _foregroundBrush,
                    1.0f);
            }

            _renderTarget.EndDraw();
        }
        catch
        {
            DisposeDeviceResources();
        }
    }

    private ID2D1Bitmap? CreateBackgroundBitmap()
    {
        if (_renderTarget is null || string.IsNullOrWhiteSpace(_backgroundImagePath) || !File.Exists(_backgroundImagePath))
        {
            return null;
        }

        try
        {
            using var sourceBitmap = new Bitmap(_backgroundImagePath);
            using var workingBitmap = new Bitmap(sourceBitmap.Width, sourceBitmap.Height, DrawingPixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(workingBitmap))
            {
                graphics.Clear(DrawingColor.Transparent);
                graphics.DrawImage(sourceBitmap, 0, 0, sourceBitmap.Width, sourceBitmap.Height);
            }

            var bounds = new Rectangle(0, 0, workingBitmap.Width, workingBitmap.Height);
            BitmapData data = workingBitmap.LockBits(bounds, ImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppPArgb);
            try
            {
                var properties = new BitmapProperties(
                    new D2DPixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    96.0f,
                    96.0f);

                return _renderTarget.CreateBitmap(
                    new SizeI(workingBitmap.Width, workingBitmap.Height),
                    data.Scan0,
                    (uint)data.Stride,
                    properties);
            }
            finally
            {
                workingBitmap.UnlockBits(data);
            }
        }
        catch
        {
            return null;
        }
    }

    private void DrawBackdrop()
    {
        if (_renderTarget is null)
        {
            return;
        }

        if (_backgroundBitmap is not null && _backgroundOpacity > 0.0f)
        {
            _renderTarget.DrawBitmap(
                _backgroundBitmap,
                _backgroundOpacity,
                BitmapInterpolationMode.Linear,
                GetCoverRect(_backgroundBitmap.PixelSize.Width, _backgroundBitmap.PixelSize.Height));
        }

        if (_backgroundDimBrush is not null && _backgroundDimOpacity > 0.0f)
        {
            _renderTarget.FillRectangle(new Rect(0, 0, ClientSize.Width, ClientSize.Height), _backgroundDimBrush);
        }
    }

    private Rect GetCoverRect(float sourceWidth, float sourceHeight)
    {
        if (sourceWidth <= 0.0f || sourceHeight <= 0.0f || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return new Rect(0, 0, ClientSize.Width, ClientSize.Height);
        }

        float scale = MathF.Max(ClientSize.Width / sourceWidth, ClientSize.Height / sourceHeight);
        float drawWidth = sourceWidth * scale;
        float drawHeight = sourceHeight * scale;
        float x = (ClientSize.Width - drawWidth) * 0.5f;
        float y = (ClientSize.Height - drawHeight) * 0.5f;
        return new Rect(x, y, drawWidth, drawHeight);
    }

    private void DrawStyledLine(LogLine line, float textOriginX, float textOriginY)
    {
        if (_renderTarget is null)
        {
            return;
        }

        float segmentX = textOriginX;

        foreach (LogSegment segment in line.Segments)
        {
            if (string.IsNullOrEmpty(segment.Text))
            {
                continue;
            }

            using IDWriteTextLayout? layout = CreateLayout(segment.Text);
            if (layout is null)
            {
                continue;
            }

            _renderTarget.DrawTextLayout(
                new Vector2(segmentX, textOriginY),
                layout,
                GetBrush(segment.BrushKind),
                DrawTextOptions.EnableColorFont | DrawTextOptions.NoSnap);

            segmentX += segment.Width;
        }
    }

    private void DrawMarker(LogLevelKind level, float rowTop)
    {
        if (_renderTarget is null)
        {
            return;
        }

        float left = HorizontalPadding + MarkerInset;
        float top = rowTop + MarkerInset;
        float width = 4.0f;
        float height = _lineHeight - (MarkerInset * 2.0f);
        _renderTarget.FillRectangle(new Rect(left, top, width, height), GetLevelBrush(level));
    }

    private ID2D1Brush GetBrush(LogBrushKind kind)
    {
        return kind switch
        {
            LogBrushKind.Muted => _mutedBrush ?? _foregroundBrush!,
            LogBrushKind.Accent => _accentBrush ?? _foregroundBrush!,
            LogBrushKind.Info => _infoBrush ?? _foregroundBrush!,
            LogBrushKind.Warning => _warningBrush ?? _foregroundBrush!,
            LogBrushKind.Error => _errorBrush ?? _foregroundBrush!,
            _ => _foregroundBrush!
        };
    }

    private ID2D1Brush GetLevelBrush(LogLevelKind level)
    {
        return level switch
        {
            LogLevelKind.Info => _infoBrush ?? _foregroundBrush!,
            LogLevelKind.Warning => _warningBrush ?? _foregroundBrush!,
            LogLevelKind.Error => _errorBrush ?? _foregroundBrush!,
            _ => _accentBrush ?? _foregroundBrush!
        };
    }

    private void DrawSelectionForLine(
        int lineIndex,
        int lineLength,
        SelectionRange selection,
        IDWriteTextLayout layout,
        float originX,
        float originY)
    {
        if (_renderTarget is null || _selectionBrush is null)
        {
            return;
        }

        if (!TryGetSelectionRangeForLine(lineIndex, lineLength, selection, out int start, out int length) || length <= 0)
        {
            return;
        }

        var metrics = new HitTestMetrics[SelectionHitCapacity];
        layout.HitTestTextRange((uint)start, (uint)length, originX, originY, metrics, out uint actualCount);

        if (actualCount > metrics.Length)
        {
            metrics = new HitTestMetrics[actualCount];
            layout.HitTestTextRange((uint)start, (uint)length, originX, originY, metrics, out actualCount);
        }

        for (int i = 0; i < actualCount; i++)
        {
            HitTestMetrics metric = metrics[i];
            _renderTarget.FillRectangle(
                new Rect(metric.Left, metric.Top, metric.Width, metric.Height),
                _selectionBrush);
        }
    }

    private bool TryGetSelectionRangeForLine(int lineIndex, int lineLength, SelectionRange selection, out int start, out int length)
    {
        start = 0;
        length = 0;

        if (lineIndex < selection.Start.Line || lineIndex > selection.End.Line)
        {
            return false;
        }

        int selectionStart = lineIndex == selection.Start.Line ? selection.Start.Character : 0;
        int selectionEnd = lineIndex == selection.End.Line ? selection.End.Character : lineLength;

        selectionStart = Math.Clamp(selectionStart, 0, lineLength);
        selectionEnd = Math.Clamp(selectionEnd, 0, lineLength);

        if (selectionEnd <= selectionStart)
        {
            return false;
        }

        start = selectionStart;
        length = selectionEnd - selectionStart;
        return true;
    }

    private TextAnchor HitTestAnchor(Point point)
    {
        if (_lines.Count == 0)
        {
            return new TextAnchor(0, 0);
        }

        float contentY = point.Y - AutoScrollPosition.Y - VerticalPadding;
        int lineIndex = Math.Clamp((int)Math.Floor(contentY / _lineHeight), 0, _lines.Count - 1);
        string text = _lines[lineIndex].Text;

        using IDWriteTextLayout? layout = CreateLayout(text);
        if (layout is null)
        {
            return new TextAnchor(lineIndex, text.Length);
        }

        float localX = point.X - AutoScrollPosition.X - HorizontalPadding - GutterWidth - GutterGap;
        float localY = point.Y - AutoScrollPosition.Y - VerticalPadding - (lineIndex * _lineHeight) - LineInnerPadding;

        layout.HitTestPoint(localX, localY, out RawBool isTrailingHit, out RawBool isInside, out HitTestMetrics metrics);

        int character = isInside
            ? (int)metrics.TextPosition + (isTrailingHit ? (int)metrics.Length : 0)
            : localX <= 0 ? 0 : text.Length;

        return new TextAnchor(lineIndex, Math.Clamp(character, 0, text.Length));
    }

    private SelectionRange? GetNormalizedSelection()
    {
        if (_selectionStart is not TextAnchor start || _selectionEnd is not TextAnchor end)
        {
            return null;
        }

        return CompareAnchors(start, end) <= 0
            ? new SelectionRange(start, end)
            : new SelectionRange(end, start);
    }

    private string? GetSelectedText()
    {
        SelectionRange? selection = GetNormalizedSelection();
        if (selection is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (int lineIndex = selection.Value.Start.Line; lineIndex <= selection.Value.End.Line; lineIndex++)
        {
            string text = _lines[lineIndex].Text;
            int start = lineIndex == selection.Value.Start.Line ? selection.Value.Start.Character : 0;
            int end = lineIndex == selection.Value.End.Line ? selection.Value.End.Character : text.Length;

            start = Math.Clamp(start, 0, text.Length);
            end = Math.Clamp(end, 0, text.Length);

            if (end > start)
            {
                builder.Append(text.AsSpan(start, end - start));
            }

            if (lineIndex < selection.Value.End.Line)
            {
                builder.AppendLine();
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static LogLine ParseLine(string text)
    {
        var segments = new List<LogSegment>();
        int cursor = 0;
        LogLevelKind level = LogLevelKind.Neutral;

        int firstToken = text.IndexOf(" [", StringComparison.Ordinal);
        if (firstToken > 0)
        {
            segments.Add(new LogSegment(text[..(firstToken + 1)], LogBrushKind.Muted));
            cursor = firstToken + 1;
        }

        int tokenIndex = 0;
        while (TryReadBracketSegment(text, ref cursor, out string segmentText, out string tokenText))
        {
            LogBrushKind brushKind = tokenIndex switch
            {
                0 => ToBrushKind(ParseLevel(tokenText)),
                1 => LogBrushKind.Accent,
                _ => LogBrushKind.Muted
            };

            if (tokenIndex == 0)
            {
                level = ParseLevel(tokenText);
            }

            segments.Add(new LogSegment(segmentText, brushKind));
            tokenIndex++;
        }

        if (cursor < text.Length)
        {
            segments.Add(new LogSegment(text[cursor..], ToMessageBrush(level)));
        }

        if (segments.Count == 0)
        {
            segments.Add(new LogSegment(text, LogBrushKind.Foreground));
        }

        return new LogLine(text, level, segments);
    }

    private static bool TryReadBracketSegment(string text, ref int cursor, out string segmentText, out string tokenText)
    {
        segmentText = string.Empty;
        tokenText = string.Empty;

        if (cursor < 0 || cursor >= text.Length || text[cursor] != '[')
        {
            return false;
        }

        int end = text.IndexOf(']', cursor);
        if (end < 0)
        {
            return false;
        }

        int next = end + 1;
        if (next < text.Length && text[next] == ' ')
        {
            next++;
        }

        segmentText = text.Substring(cursor, next - cursor);
        tokenText = text.Substring(cursor + 1, end - cursor - 1);
        cursor = next;
        return true;
    }

    private static LogLevelKind ParseLevel(string tokenText)
    {
        return tokenText.ToUpperInvariant() switch
        {
            "INFO" => LogLevelKind.Info,
            "WARN" or "WARNING" => LogLevelKind.Warning,
            "ERROR" => LogLevelKind.Error,
            _ => LogLevelKind.Neutral
        };
    }

    private static LogBrushKind ToBrushKind(LogLevelKind level)
    {
        return level switch
        {
            LogLevelKind.Info => LogBrushKind.Info,
            LogLevelKind.Warning => LogBrushKind.Warning,
            LogLevelKind.Error => LogBrushKind.Error,
            _ => LogBrushKind.Accent
        };
    }

    private static LogBrushKind ToMessageBrush(LogLevelKind level)
    {
        return level switch
        {
            LogLevelKind.Warning => LogBrushKind.Warning,
            LogLevelKind.Error => LogBrushKind.Error,
            _ => LogBrushKind.Foreground
        };
    }

    private static int CompareAnchors(TextAnchor left, TextAnchor right)
    {
        int lineCompare = left.Line.CompareTo(right.Line);
        return lineCompare != 0 ? lineCompare : left.Character.CompareTo(right.Character);
    }

    private static string ChooseFontFamily()
    {
        using var fonts = new InstalledFontCollection();
        var installed = fonts.Families.Select(family => family.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string candidate in new[] { "Cascadia Mono", "Cascadia Code", "Consolas" })
        {
            if (installed.Contains(candidate))
            {
                return candidate;
            }
        }

        return (SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont).FontFamily.Name;
    }

    private static Color4 ToColor4(DrawingColor color)
    {
        return new Color4(
            color.R / 255.0f,
            color.G / 255.0f,
            color.B / 255.0f,
            color.A / 255.0f);
    }

    private static DrawingColor WithOpacity(DrawingColor color, float opacity)
    {
        int alpha = Math.Clamp((int)MathF.Round(Math.Clamp(opacity, 0.0f, 1.0f) * 255.0f), 0, 255);
        return DrawingColor.FromArgb(alpha, color);
    }

    private sealed class LogLine
    {
        public LogLine(string text, LogLevelKind level, List<LogSegment> segments)
        {
            Text = text;
            Level = level;
            Segments = segments;
        }

        public string Text { get; }

        public LogLevelKind Level { get; }

        public List<LogSegment> Segments { get; }

        public float Width { get; set; }
    }

    private sealed class LogSegment
    {
        public LogSegment(string text, LogBrushKind brushKind)
        {
            Text = text;
            BrushKind = brushKind;
        }

        public string Text { get; }

        public LogBrushKind BrushKind { get; }

        public float Width { get; set; }
    }

    private enum LogBrushKind
    {
        Foreground,
        Muted,
        Accent,
        Info,
        Warning,
        Error
    }

    private enum LogLevelKind
    {
        Neutral,
        Info,
        Warning,
        Error
    }

    private readonly record struct TextAnchor(int Line, int Character);

    private readonly record struct SelectionRange(TextAnchor Start, TextAnchor End);
}
