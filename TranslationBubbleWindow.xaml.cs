using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SytexLCore.Models;
using SytexLCore.Services;
using WpfRect = System.Windows.Rect;

namespace SytexLCore;

public partial class TranslationBubbleWindow : Window
{
    private readonly WpfRect _region;
    private readonly GameProfile _profile;
    private readonly TranslationService _translator;
    private readonly OcrService _ocr = new();
    private DispatcherTimer? _autoCloseTimer;

    public TranslationBubbleWindow(WpfRect region, GameProfile profile, TranslationService translator)
    {
        InitializeComponent();
        _region = region;
        _profile = profile;
        _translator = translator;

        _ocr.SelectedEngine = profile.SelectedOcrEngine;
        _ocr.SourceLanguage = profile.SourceLanguage;

        Opacity = 0;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 1. İlk olarak balon penceresini kabaca konumlandır
        PositionWindow();

        // Giriş animasyonunu oynat
        if (Resources["FadeIn"] is Storyboard fadeIn)
        {
            fadeIn.Begin(this);
        }

        // Ekranın güncellenmesi ve kararma efektinin (CropWindow) kaybolması için 150ms bekle!
        // Aksi takdirde karartılmış ekranın ekran görüntüsünü alıp OCR yapmaya çalışırız ve yazı okunamaz!
        await Task.Delay(150);

        // 2. OCR ve Çeviriyi arka planda başlat
        try
        {
            var blocks = await _ocr.RecognizeAsync(new List<WpfRect>(), _region);
            if (blocks == null || blocks.Count == 0)
            {
                ShowText("Metin bulunamadı.", "Seçilen alanda herhangi bir yazı tespit edilemedi.");
            }
            else
            {
                List<string> lines = new();
                foreach (var b in blocks)
                {
                    if (!string.IsNullOrWhiteSpace(b.Text))
                        lines.Add(b.Text);
                }
                string originalText = string.Join(" ", lines);

                if (string.IsNullOrWhiteSpace(originalText))
                {
                    ShowText("Metin bulunamadı.", "Seçilen alanda herhangi bir yazı tespit edilemedi.");
                }
                else
                {
                    _translator.SourceLanguage = _profile.SourceLanguage;
                    _translator.TargetLanguage = _profile.TargetLanguage;
                    _translator.Provider = TranslationProvider.GoogleGTX;

                    var translateResults = await _translator.TranslateBatchAsync(new string[] { originalText });
                    string translatedText = translateResults.Length > 0 ? translateResults[0] : originalText;
                    ShowText(originalText, translatedText);
                }
            }
        }
        catch (Exception ex)
        {
            ShowText("Hata oluştu", $"Çeviri yapılırken hata oluştu: {ex.Message}");
        }

        // 3. Otomatik kapanma timer'ını başlat (10 saniye sonra kapansın)
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _autoCloseTimer.Tick += AutoCloseTimer_Tick;
        _autoCloseTimer.Start();
    }

    private void PositionWindow()
    {
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        UpdateLayout();
        double bubbleWidth = MainBorder.ActualWidth + 20; 
        double bubbleHeight = MainBorder.ActualHeight + 20;

        if (double.IsNaN(bubbleWidth) || bubbleWidth < 100) bubbleWidth = 350;
        if (double.IsNaN(bubbleHeight) || bubbleHeight < 50) bubbleHeight = 120;

        // Bölgenin tam ortasının üstünde hizala
        double targetLeft = _region.Left + (_region.Width - bubbleWidth) / 2;
        double targetTop = _region.Top - bubbleHeight - 10;

        if (targetLeft < 10) targetLeft = 10;
        if (targetLeft + bubbleWidth > screenWidth - 10) targetLeft = screenWidth - bubbleWidth - 10;

        if (targetTop < 10) // Eğer ekranın en üstündeyse, seçimin altına yerleştir!
        {
            targetTop = _region.Bottom + 10;
        }

        Left = targetLeft;
        Top = targetTop;
    }

    private void ShowText(string original, string translated)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Visible;

        OriginalTxt.Text = original;
        TranslatedTxt.Text = translated;

        // Boyutlar değiştiği için konumu yeniden ince ayarla
        PositionWindow();
    }

    private void AutoCloseTimer_Tick(object? sender, EventArgs e)
    {
        CloseWithAnimation();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    private void CloseWithAnimation()
    {
        _autoCloseTimer?.Stop();
        if (Resources["FadeOut"] is Storyboard fadeOut)
        {
            fadeOut.Completed += (s, e) => Close();
            fadeOut.Begin(this);
        }
        else
        {
            Close();
        }
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
