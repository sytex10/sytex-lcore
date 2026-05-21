using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SytexLCore.Models;
using SytexLCore.Services;
using WpfRect = System.Windows.Rect;

namespace SytexLCore;

public partial class OverlayWindow : Window, IDisposable
{
    // ── Win32 ──────────────────────────────────────────────────────────────────
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int idx, int val);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int idx);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insert, int x, int y, int w, int h, int flags);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const IntPtr HWND_TOPMOST = -1;
    private const int SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
    private const int HOTKEY_ID = 9001;

    // ── Servisler ──────────────────────────────────────────────────────────────
    private readonly OcrService _ocr = new();
    private readonly TranslationService _translator;
    private readonly GameProfile _profile;

    // ── Timer'lar ──────────────────────────────────────────────────────────────
    private DispatcherTimer? _topmostTimer;

    // ── Durum ─────────────────────────────────────────────────────────────────
    private bool _isRunning = true;
    private bool _disposed;
    private string _lastCapturedText = "";
    private string _lastStableText = "";
    private int _consecutiveEmptyFrames = 0;
    private DateTime _lastUpdate = DateTime.MinValue;

    private readonly System.Threading.Channels.Channel<List<OcrBlock>> _ocrChannel = 
        System.Threading.Channels.Channel.CreateBounded<List<OcrBlock>>(
            new System.Threading.Channels.BoundedChannelOptions(1) { FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest });

    // Aktif çeviri kutularının konumları (hariç tutulan alanlar için)
    private List<System.Windows.Rect> _activeRects = new();
    private readonly List<UIElement> _translationElements = new();

    public event Action? OnBack;
    public event Action<string, string>? OnTextTranslated;

    private readonly uint _selectedHotkey;

    public OverlayWindow(GameProfile profile, TranslationService translator, uint hotkey)
    {
        InitializeComponent();
        _profile = profile;
        _translator = translator;
        _selectedHotkey = hotkey;

        // OCR Motorunu ve Kaynak Dilini Seç
        _ocr.SelectedEngine = profile.SelectedOcrEngine;
        _ocr.SourceLanguage = profile.SourceLanguage;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        
        // Pencereyi tamamen click-through ve şeffaf yap
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED);

        var src = HwndSource.FromHwnd(hwnd);
        src?.AddHook(WndProc);
        
        // Hotkey kaydet
        RegisterHotKey(hwnd, HOTKEY_ID, 0, _selectedHotkey);

        // Pencereyi her zaman en üstte tut (Topmost zorlaması)
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _topmostTimer.Tick += (_, _) => SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        _topmostTimer.Start();

        StartPipeline();
    }

    private void StartPipeline()
    {
        // 1. OCR YAKALAMA DÖNGÜSÜ
        Task.Run(async () =>
        {
            while (!_disposed)
            {
                if (!_isRunning) { await Task.Delay(200); continue; }

                var exclusions = _activeRects.ToList();
                var scanRegion = _profile.OcrRegion?.ToRect();
                
                var blocks = await _ocr.RecognizeAsync(exclusions, scanRegion);

                await _ocrChannel.Writer.WriteAsync(blocks);
                
                await Task.Delay(100); // 100ms ultra hızlı tarama gecikmesi
            }
        });

        // 2. ÇEVİRİ VE RENDER DÖNGÜSÜ
        Task.Run(async () =>
        {
            while (!_disposed)
            {
                var blocks = await _ocrChannel.Reader.ReadAsync();
                
                string currentText = string.Join(" ", blocks.Select(b => b.Text.Trim())).Trim();

                if (string.IsNullOrEmpty(currentText))
                {
                    _consecutiveEmptyFrames++;
                    if (_consecutiveEmptyFrames >= 8) // ~800ms eşik (8 * 100ms)
                    {
                        _lastStableText = ""; // Ekran boşaldığında kararlı metin geçmişini sıfırla
                        await Dispatcher.InvokeAsync(() => 
                        {
                            RenderTranslations(new List<(OcrBlock, string)>());
                        });
                    }
                    continue;
                }
                else
                {
                    _consecutiveEmptyFrames = 0;
                }

                // OCR Karakter Titremesi ve Gürültü Sabitleyici (Text Stabilizer):
                // 1. Benzerlik Kontrolü: Eğer yeni okunan metin, en son kararlı metin ile %85'ten fazla benziyorsa, bunu bir OCR hatası kabul et ve koru!
                if (!string.IsNullOrEmpty(_lastStableText) && CalculateSimilarity(_lastStableText, currentText) > 0.85)
                {
                    currentText = _lastStableText;
                }
                // 2. Alt Küme (Solma Gürültüsü) Kontrolü: Eğer yeni okunan metin daha kısa ise ve kelimelerinin en az %60'ı 
                // en son kararlı metin ile uyuşuyorsa, bu solma esnasındaki yarım okumadır. Kararlı metni koru!
                else if (!string.IsNullOrEmpty(_lastStableText) && _lastStableText.Length > currentText.Length)
                {
                    var stableWords = _lastStableText.ToLowerInvariant().Split(new[] { ' ', '.', ',', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                    var currentWords = currentText.ToLowerInvariant().Split(new[] { ' ', '.', ',', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (currentWords.Length > 0)
                    {
                        int matchedWords = 0;
                        foreach (var cw in currentWords)
                        {
                            if (stableWords.Contains(cw) || stableWords.Any(sw => CalculateSimilarity(sw, cw) > 0.60))
                            {
                                matchedWords++;
                            }
                        }
                        
                        double matchRatio = (double)matchedWords / currentWords.Length;
                        if (matchRatio >= 0.60)
                        {
                            currentText = _lastStableText;
                        }
                        else
                        {
                            _lastStableText = currentText;
                        }
                    }
                    else
                    {
                        _lastStableText = currentText;
                    }
                }
                else
                {
                    _lastStableText = currentText;
                }

                // Levenshtein Karşılaştırması: Metin %90 aynıysa çevirme
                if (CalculateSimilarity(_lastCapturedText, currentText) > 0.90) continue;
                _lastCapturedText = currentText;

                try
                {
                    // 1. Tüm alt OCR bloklarını dikey konumuna göre sırala ve tek bir birleşik OcrBlock oluştur
                    var sortedBlocks = blocks.OrderBy(b => b.Rect.Y).ToList();
                    var minX = sortedBlocks.Min(b => b.Rect.X);
                    var minY = sortedBlocks.Min(b => b.Rect.Y);
                    var maxX = sortedBlocks.Max(b => b.Rect.Right);
                    var maxY = sortedBlocks.Max(b => b.Rect.Bottom);

                    var mergedBlock = new OcrBlock
                    {
                        Text = currentText,
                        Rect = new WpfRect(minX, minY, maxX - minX, maxY - minY),
                        Background = sortedBlocks[0].Background,
                        Foreground = sortedBlocks[0].Foreground
                    };

                    // 2. Birleşik tek metni çevir (Tam cümle bağlamını korur!)
                    string[] translations = await _translator.TranslateBatchAsync(new[] { currentText });
                    string translatedText = translations.Length > 0 ? translations[0] : currentText;

                    await Dispatcher.InvokeAsync(() => 
                    {
                        RenderTranslations(new List<(OcrBlock, string)> { (mergedBlock, translatedText) });
                        _lastUpdate = DateTime.Now;
                    });
                }
                catch (Exception ex) 
                { 
                    System.Diagnostics.Debug.WriteLine($"[Pipeline Error] {ex.Message}"); 
                }
                
                await Task.Delay(100); // 100ms ultra hızlı çeviri kuyruk gecikmesi
            }
        });

        // Belirli süre yazı algılanmazsa ekranı temizleme
        var clearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        clearTimer.Tick += (_, _) =>
        {
            if (_isRunning && (DateTime.Now - _lastUpdate).TotalSeconds > 10)
            {
                RenderTranslations(Array.Empty<(OcrBlock, string)>());
            }
        };
        clearTimer.Start();
    }

    private List<(OcrBlock block, string translated)> MergeBlocks(IEnumerable<(OcrBlock block, string translated)> inputs)
    {
        var inputList = inputs.ToList();
        if (inputList.Count <= 1) return inputList;

        // Altyazı satırlarını yukarıdan aşağıya sıralayıp tek bir anlamlı paragraf halinde birleştir
        var sorted = inputList.OrderBy(i => i.block.Rect.Y).ToList();

        var combinedText = string.Join(" ", sorted.Select(s => s.block.Text.Trim()));
        var combinedTrans = string.Join(" ", sorted.Select(s => s.translated.Trim()));

        var minX = sorted.Min(s => s.block.Rect.X);
        var minY = sorted.Min(s => s.block.Rect.Y);
        var maxX = sorted.Max(s => s.block.Rect.Right);
        var maxY = sorted.Max(s => s.block.Rect.Bottom);

        var mergedBlock = new OcrBlock
        {
            Text = combinedText,
            Rect = new WpfRect(minX, minY, maxX - minX, maxY - minY),
            Background = sorted[0].block.Background,
            Foreground = sorted[0].block.Foreground
        };

        return new List<(OcrBlock block, string translated)> { (mergedBlock, combinedTrans) };
    }

    private void RenderTranslations(IEnumerable<(OcrBlock block, string translated)> results)
    {
        var resultList = results.ToList();

        if (resultList.Any())
        {
            // Yeni bir çeviri geldiğinde, eski tüm balonları ve hayalet pencereleri ANINDA temizle.
            // Bu sayede pencerelerin üst üste binmesi veya ekranda birden fazla balon birikmesi kesinlikle engellenir!
            TranslationCanvas.Children.Clear();
            _translationElements.Clear();
        }
        else
        {
            // Sadece ekran boşaldığında (altyazı bittiğinde) son balonu yumuşakça 300ms soldurarak yok et (fade-out)
            foreach (var el in _translationElements)
            {
                var oldEl = el;
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                fadeOut.Completed += (s, e) =>
                {
                    try { TranslationCanvas.Children.Remove(oldEl); } catch { }
                };
                oldEl.BeginAnimation(OpacityProperty, fadeOut);
            }
            _translationElements.Clear();
        }
        _activeRects.Clear();

        // Renkleri profilden parse et
        Color bgColor = Colors.Black;
        Color textColor = Colors.White;
        Color borderColor = Color.FromRgb(0, 245, 255);

        try
        {
            bgColor = (Color)ColorConverter.ConvertFromString(_profile.BubbleBgColor);
            textColor = (Color)ColorConverter.ConvertFromString(_profile.BubbleTextColor);
            borderColor = (Color)ColorConverter.ConvertFromString(_profile.BubbleBorderColor);
        }
        catch { }

        // Opacity ayarla
        bgColor.A = (byte)(_profile.BackgroundOpacity * 255);

        foreach (var (block, translated) in resultList)
        {
            if (string.IsNullOrWhiteSpace(translated)) continue;

            // Çeviriyi ana panele de yansıt
            OnTextTranslated?.Invoke(block.Text, translated);

            bool isVertical = block.Rect.Height > block.Rect.Width * 1.5;
            double fontSize = _profile.FontSizeOverride > 0 
                ? _profile.FontSizeOverride 
                : Math.Clamp(block.Rect.Height * 0.45, 13, 24);

            // Premium Siber Balon Çerçevesi
            var outerBorder = new Border
            {
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(borderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(14, 10, 14, 10)
            };

            if (isVertical)
            {
                outerBorder.Width = Math.Max(block.Rect.Width * 1.5, 90);
                outerBorder.MinHeight = block.Rect.Height;
            }
            else
            {
                outerBorder.MaxWidth = Math.Clamp(block.Rect.Width * 1.5, 350, 800);
                outerBorder.MinWidth = block.Rect.Width;
            }

            var sp = new StackPanel();

            // Üst Etiket
            var labelPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            labelPanel.Children.Add(new TextBlock
            {
                Text = "⚡ SYTEX ULTIMATE",
                Foreground = new SolidColorBrush(borderColor),
                FontSize = 9,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold
            });
            sp.Children.Add(labelPanel);

            // Çevrilmiş Metin
            var transTb = new TextBlock
            {
                Text = translated,
                Foreground = new SolidColorBrush(textColor),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = fontSize,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = fontSize * 1.3
            };
            sp.Children.Add(transTb);

            // Orijinal Metin (Muted)
            var origTb = new TextBlock
            {
                Text = block.Text.Replace("\n", " "),
                Foreground = new SolidColorBrush(Color.FromRgb(138, 155, 181)), // Muted
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = fontSize * 0.72,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            sp.Children.Add(origTb);

            outerBorder.Child = sp;

            // Siber Glow DropShadow
            outerBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = borderColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.35
            };

            // WPF Elementini anında ölç (Measure layout pass)
            outerBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double desiredW = outerBorder.DesiredSize.Width;
            double desiredH = outerBorder.DesiredSize.Height;

            // Baloncuğu yatayda ekranın tam ortasına yerleştirerek sağa sola titremesini ve kaymasını engelle!
            double left = (SystemParameters.PrimaryScreenWidth - desiredW) / 2.0;
            
            // Dikey konumdaki milimetrik titreşimleri önlemek için Y koordinatını 20 piksele yuvarlayarak sabitle!
            double stableTop = Math.Round(block.Rect.Top / 20.0) * 20.0;
            double top = stableTop - desiredH - 16.0; // 16px boşluk

            // Ekran dışına taşmayı önle (Boundary Protection)
            if (left < 10) left = 10;
            if (left + desiredW > SystemParameters.PrimaryScreenWidth - 10)
                left = SystemParameters.PrimaryScreenWidth - desiredW - 10;

            if (top < 10) // Ekran dışına taşmayı dikeyde önle
                top = 10;

            Canvas.SetLeft(outerBorder, left);
            Canvas.SetTop(outerBorder, top);
            
            TranslationCanvas.Children.Add(outerBorder);
            _translationElements.Add(outerBorder);

            // Fade-in Animasyonu
            var anim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(200)
            };
            outerBorder.BeginAnimation(OpacityProperty, anim);

            // Dışlama bölgesi olarak altyazı alanını değil, sadece çeviri balonunun kendi kapladığı alanı ekle!
            // Böylece altyazı alanı serbest kalır ve yeni altyazılar sıfır gecikmeyle anında algılanıp çevrilir!
            var bubbleRect = new WpfRect(left, top, desiredW, desiredH);
            _activeRects.Add(bubbleRect);
        }
    }

    private void ToggleScan()
    {
        _isRunning = !_isRunning;
        if (!_isRunning)
        {
            RenderTranslations(Array.Empty<(OcrBlock, string)>());
            _lastCapturedText = "";
        }
    }

    private double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0;
        int stepsToSame = ComputeLevenshteinDistance(s1, s2);
        return 1.0 - ((double)stepsToSame / Math.Max(s1.Length, s2.Length));
    }

    private int ComputeLevenshteinDistance(string source, string target)
    {
        if (source == target) return 0;
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        int[] v0 = new int[target.Length + 1];
        int[] v1 = new int[target.Length + 1];

        for (int i = 0; i < v0.Length; i++) v0[i] = i;

        for (int i = 0; i < source.Length; i++)
        {
            v1[0] = i + 1;
            for (int j = 0; j < target.Length; j++)
            {
                int cost = (source[i] == target[j]) ? 0 : 1;
                v1[j + 1] = Math.Min(v1[j] + 1, Math.Min(v0[j + 1] + 1, v0[j] + cost));
            }
            for (int j = 0; j < v0.Length; j++) v0[j] = v1[j];
        }
        return v1[target.Length];
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == HOTKEY_ID) // WM_HOTKEY
        {
            Dispatcher.Invoke(ToggleScan);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _topmostTimer?.Stop();

        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, HOTKEY_ID);
        }
        catch { }

        _ocr.Dispose();
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }
}
