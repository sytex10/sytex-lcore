using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SytexLCore.Models;

namespace SytexLCore;

public partial class SettingsWindow : Window
{
    private readonly GameProfile _profile;
    private Button? _capturingButton;

    // Geçici kısayol değişkenleri
    private uint _tempManuel;
    private uint _tempAuto;
    private uint _tempSettings;

    public SettingsWindow(GameProfile profile)
    {
        InitializeComponent();
        _profile = profile;

        _tempManuel = profile.ManuelHotkey;
        _tempAuto = profile.AutoHotkey;
        _tempSettings = profile.SettingsHotkey;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BgColorBox.Text = _profile.BubbleBgColor;
        TextColorBox.Text = _profile.BubbleTextColor;
        BorderColorBox.Text = _profile.BubbleBorderColor;

        ManuelHotkeyBtn.Content = KeyInterop.KeyFromVirtualKey((int)_tempManuel).ToString();
        AutoHotkeyBtn.Content = KeyInterop.KeyFromVirtualKey((int)_tempAuto).ToString();
        SettingsHotkeyBtn.Content = KeyInterop.KeyFromVirtualKey((int)_tempSettings).ToString();

        MinimizeOnCloseCheck.IsChecked = _profile.MinimizeOnClose;

        UpdateLivePreview();
    }

    private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateLivePreview();
    }

    private void UpdateLivePreview()
    {
        if (PreviewBubbleBorder == null) return;
        try
        {
            // Background
            var bgCol = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(BgColorBox.Text);
            bgCol.A = 220; // Siber yarı şeffaflık
            PreviewBubbleBorder.Background = new System.Windows.Media.SolidColorBrush(bgCol);

            // Text Color
            var txtCol = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(TextColorBox.Text);
            PreviewBubbleText.Foreground = new System.Windows.Media.SolidColorBrush(txtCol);

            // Border Color & Header
            var bdrCol = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(BorderColorBox.Text);
            PreviewBubbleBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(bdrCol);
            PreviewBubbleHeader.Foreground = new System.Windows.Media.SolidColorBrush(bdrCol);

            if (PreviewBubbleGlow != null)
            {
                PreviewBubbleGlow.Color = bdrCol;
            }
        }
        catch { }
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void SelectBgColor_Click(object sender, RoutedEventArgs e)
    {
        var color = ShowColorDialog(BgColorBox.Text);
        if (color != null) BgColorBox.Text = color;
    }

    private void SelectTextColor_Click(object sender, RoutedEventArgs e)
    {
        var color = ShowColorDialog(TextColorBox.Text);
        if (color != null) TextColorBox.Text = color;
    }

    private void SelectBorderColor_Click(object sender, RoutedEventArgs e)
    {
        var color = ShowColorDialog(BorderColorBox.Text);
        if (color != null) BorderColorBox.Text = color;
    }

    private string? ShowColorDialog(string defaultHex)
    {
        var dialog = new System.Windows.Forms.ColorDialog();
        try
        {
            var systemCol = System.Drawing.ColorTranslator.FromHtml(defaultHex);
            dialog.Color = systemCol;
        }
        catch { }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            return System.Drawing.ColorTranslator.ToHtml(dialog.Color);
        }
        return null;
    }

    private void ResetColors_Click(object sender, RoutedEventArgs e)
    {
        BgColorBox.Text = "#0A0D17";
        TextColorBox.Text = "#FFFFFF";
        BorderColorBox.Text = "#00F5FF";
    }

    private void CaptureHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_capturingButton != null)
        {
            ResetCapturingButton();
        }

        _capturingButton = sender as Button;
        if (_capturingButton != null)
        {
            _capturingButton.Content = "[Tuş Bekleniyor...]";
            _capturingButton.Foreground = System.Windows.Media.Brushes.Yellow;
            KeyDown += OnHotkeyKeyDown;
        }
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturingButton == null) return;

        // Escape tuşu iptal eder
        if (e.Key == Key.Escape)
        {
            ResetCapturingButton();
            return;
        }

        int vk = KeyInterop.VirtualKeyFromKey(e.Key);
        if (vk > 0)
        {
            string type = _capturingButton.Tag.ToString() ?? "";
            if (type == "Manuel") _tempManuel = (uint)vk;
            else if (type == "Auto") _tempAuto = (uint)vk;
            else if (type == "Settings") _tempSettings = (uint)vk;

            _capturingButton.Content = e.Key.ToString();
        }

        ResetCapturingButton();
        e.Handled = true;
    }

    private void ResetCapturingButton()
    {
        if (_capturingButton != null)
        {
            _capturingButton.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 85));
            string type = _capturingButton.Tag.ToString() ?? "";
            uint val = type == "Manuel" ? _tempManuel : (type == "Auto" ? _tempAuto : _tempSettings);
            _capturingButton.Content = KeyInterop.KeyFromVirtualKey((int)val).ToString();
        }
        KeyDown -= OnHotkeyKeyDown;
        _capturingButton = null;
    }

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        _profile.BubbleBgColor = BgColorBox.Text;
        _profile.BubbleTextColor = TextColorBox.Text;
        _profile.BubbleBorderColor = BorderColorBox.Text;

        _profile.ManuelHotkey = _tempManuel;
        _profile.AutoHotkey = _tempAuto;
        _profile.SettingsHotkey = _tempSettings;

        _profile.MinimizeOnClose = MinimizeOnCloseCheck.IsChecked ?? false;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
