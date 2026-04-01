using System.Drawing;
using System.Windows.Forms;

namespace VlcShimDebugFr;

internal sealed class ConfigForm : Form
{
    private readonly ComboBox _playerSelector;
    private readonly ComboBox _logThemeSelector;
    private readonly TextBox _customNameTextBox;
    private readonly TextBox _customAppIdTextBox;
    private readonly TextBox _vlcHttpPasswordTextBox;
    private readonly TextBox _vlcHttpPortsTextBox;
    private readonly TextBox _backgroundImagePathTextBox;
    private readonly NumericUpDown _backgroundOpacityNumeric;
    private readonly NumericUpDown _backgroundDimNumeric;
    private readonly CheckBox _showToastCheckBox;

    public ConfigForm(ShimConfig config)
    {
        Config = ConfigStore.Clone(config);

        Text = "VLC Shim Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 470);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 12
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 10; i++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            Text = "Player profile",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, 0);

        _playerSelector = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (var profile in PlayerIdentityProfiles.All)
        {
            _playerSelector.Items.Add(profile);
        }
        _playerSelector.DisplayMember = nameof(PlayerIdentityProfile.Label);
        _playerSelector.SelectedIndexChanged += (_, __) => UpdateCustomFieldState();
        root.Controls.Add(_playerSelector, 1, 0);

        root.Controls.Add(new Label
        {
            Text = "Custom name",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, 1);

        _customNameTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = Config.CustomPlayerDisplayName
        };
        root.Controls.Add(_customNameTextBox, 1, 1);

        root.Controls.Add(new Label
        {
            Text = "Custom AppUserModelID",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, 2);

        _customAppIdTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = Config.CustomAppUserModelId
        };
        root.Controls.Add(_customAppIdTextBox, 1, 2);

        root.Controls.Add(new Label
        {
            Text = "VLC HTTP password",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, 3);

        _vlcHttpPasswordTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            Text = Config.VlcHttpPassword
        };
        root.Controls.Add(_vlcHttpPasswordTextBox, 1, 3);

        root.Controls.Add(new Label
        {
            Text = "VLC HTTP ports",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, 4);

        _vlcHttpPortsTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = Config.VlcHttpPorts
        };
        root.Controls.Add(_vlcHttpPortsTextBox, 1, 4);

        root.Controls.Add(new Label
        {
            Text = "Log viewer theme",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, 5);

        _logThemeSelector = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (var theme in LogViewerThemes.All)
        {
            _logThemeSelector.Items.Add(theme);
        }
        _logThemeSelector.DisplayMember = nameof(LogViewerTheme.Label);
        root.Controls.Add(_logThemeSelector, 1, 5);

        root.Controls.Add(new Label
        {
            Text = "Log background",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, 6);

        var backgroundImagePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            AutoSize = true
        };
        backgroundImagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        backgroundImagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        backgroundImagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _backgroundImagePathTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = Config.LogViewerBackgroundImagePath
        };

        var browseBackgroundButton = new Button
        {
            Text = "Browse...",
            AutoSize = true
        };
        browseBackgroundButton.Click += (_, __) => BrowseForBackgroundImage();

        var clearBackgroundButton = new Button
        {
            Text = "Clear",
            AutoSize = true
        };
        clearBackgroundButton.Click += (_, __) => _backgroundImagePathTextBox.Text = string.Empty;

        backgroundImagePanel.Controls.Add(_backgroundImagePathTextBox, 0, 0);
        backgroundImagePanel.Controls.Add(browseBackgroundButton, 1, 0);
        backgroundImagePanel.Controls.Add(clearBackgroundButton, 2, 0);
        root.Controls.Add(backgroundImagePanel, 1, 6);

        root.Controls.Add(new Label
        {
            Text = "Image opacity",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, 7);

        _backgroundOpacityNumeric = new NumericUpDown
        {
            Dock = DockStyle.Left,
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(Config.LogViewerBackgroundOpacityPercent, 0, 100),
            Width = 80
        };
        root.Controls.Add(_backgroundOpacityNumeric, 1, 7);

        root.Controls.Add(new Label
        {
            Text = "Backdrop dim",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6)
        }, 0, 8);

        _backgroundDimNumeric = new NumericUpDown
        {
            Dock = DockStyle.Left,
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(Config.LogViewerBackgroundDimPercent, 0, 100),
            Width = 80
        };
        root.Controls.Add(_backgroundDimNumeric, 1, 8);

        _showToastCheckBox = new CheckBox
        {
            Text = "Show startup warning toast",
            Checked = Config.ShowStartupToast,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        };
        root.SetColumnSpan(_showToastCheckBox, 2);
        root.Controls.Add(_showToastCheckBox, 0, 9);

        var noteLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = "Settings apply immediately after Save. CLI flags still override the config, and blank password/ports fall back to the environment/default probe ports."
        };
        root.SetColumnSpan(noteLabel, 2);
        root.Controls.Add(noteLabel, 0, 10);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        var saveButton = new Button
        {
            Text = "Save",
            AutoSize = true
        };
        saveButton.Click += (_, __) => SaveAndClose();

        var cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            DialogResult = DialogResult.Cancel
        };

        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        root.SetColumnSpan(buttons, 2);
        root.Controls.Add(buttons, 0, 11);

        Controls.Add(root);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        _playerSelector.SelectedItem = PlayerIdentityProfiles.Get(Config.PlayerProfileId);
        _logThemeSelector.SelectedItem = LogViewerThemes.Get(Config.LogViewerThemeId);
        UpdateCustomFieldState();
    }

    public ShimConfig Config { get; }

    private void UpdateCustomFieldState()
    {
        bool isCustom = (_playerSelector.SelectedItem as PlayerIdentityProfile)?.IsCustom == true;
        _customNameTextBox.Enabled = isCustom;
        _customAppIdTextBox.Enabled = isCustom;
    }

    private void SaveAndClose()
    {
        var profile = _playerSelector.SelectedItem as PlayerIdentityProfile ?? PlayerIdentityProfiles.Default;
        if (!TryNormalizePortList(_vlcHttpPortsTextBox.Text, out string normalizedPorts))
        {
            MessageBox.Show(
                this,
                "VLC HTTP ports must be a comma-separated list of valid TCP ports, for example: 8080,4212",
                "Invalid VLC HTTP ports",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Config.PlayerProfileId = profile.Id;
        Config.LogViewerThemeId = (_logThemeSelector.SelectedItem as LogViewerTheme ?? LogViewerThemes.Default).Id;
        Config.CustomPlayerDisplayName = _customNameTextBox.Text.Trim();
        Config.CustomAppUserModelId = _customAppIdTextBox.Text.Trim();
        Config.VlcHttpPassword = string.IsNullOrWhiteSpace(_vlcHttpPasswordTextBox.Text) ? string.Empty : _vlcHttpPasswordTextBox.Text;
        Config.VlcHttpPorts = normalizedPorts;
        Config.LogViewerBackgroundImagePath = _backgroundImagePathTextBox.Text.Trim();
        Config.LogViewerBackgroundOpacityPercent = Decimal.ToInt32(_backgroundOpacityNumeric.Value);
        Config.LogViewerBackgroundDimPercent = Decimal.ToInt32(_backgroundDimNumeric.Value);
        Config.ShowStartupToast = _showToastCheckBox.Checked;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void BrowseForBackgroundImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose log background image",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
            CheckFileExists = true,
            RestoreDirectory = true
        };

        if (!string.IsNullOrWhiteSpace(_backgroundImagePathTextBox.Text))
        {
            try
            {
                dialog.InitialDirectory = Path.GetDirectoryName(_backgroundImagePathTextBox.Text);
            }
            catch
            {
            }
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _backgroundImagePathTextBox.Text = dialog.FileName;
        }
    }

    private static bool TryNormalizePortList(string rawValue, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        var ports = new List<int>();
        foreach (string rawPort in rawValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(rawPort, out int port) || port < 1 || port > 65535)
            {
                return false;
            }

            if (!ports.Contains(port))
            {
                ports.Add(port);
            }
        }

        normalized = string.Join(",", ports);
        return true;
    }
}
