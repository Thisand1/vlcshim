using System.Drawing;
using System.Windows.Forms;

namespace VlcShimDebugFr;

internal sealed class ConfigForm : Form
{
    private readonly ComboBox _playerSelector;
    private readonly TextBox _customNameTextBox;
    private readonly TextBox _customAppIdTextBox;
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
        ClientSize = new Size(440, 250);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 6
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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

        _showToastCheckBox = new CheckBox
        {
            Text = "Show startup warning toast",
            Checked = Config.ShowStartupToast,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        };
        root.SetColumnSpan(_showToastCheckBox, 2);
        root.Controls.Add(_showToastCheckBox, 0, 3);

        var noteLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = "Player identity changes apply on next launch. Selecting AIMP also enables the Rainmeter-compatible AIMP bridge on restart."
        };
        root.SetColumnSpan(noteLabel, 2);
        root.Controls.Add(noteLabel, 0, 4);

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
        root.Controls.Add(buttons, 0, 5);

        Controls.Add(root);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        _playerSelector.SelectedItem = PlayerIdentityProfiles.Get(Config.PlayerProfileId);
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

        Config.PlayerProfileId = profile.Id;
        Config.CustomPlayerDisplayName = _customNameTextBox.Text.Trim();
        Config.CustomAppUserModelId = _customAppIdTextBox.Text.Trim();
        Config.ShowStartupToast = _showToastCheckBox.Checked;

        DialogResult = DialogResult.OK;
        Close();
    }
}
