using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Local;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using WpfRect = System.Windows.Rect;
using CvRect = OpenCvSharp.Rect;

namespace SytexLCore.Services;

public sealed class OcrBlock
{
    public string Text { get; init; } = "";
    public WpfRect Rect { get; init; }
    public Color Background { get; init; }
    public Color Foreground { get; init; }
}

public sealed class OcrService : IDisposable
{
    // ── Static Lazy PaddleOCR Engine ──
    private static readonly Lazy<PaddleOcrAll> _paddleEngine = new(() =>
    {
        try
        {
            var model = LocalFullModels.ChineseV4; 
            return new PaddleOcrAll(model, PaddleDevice.Mkldnn())
            {
                AllowRotateDetection = false,
                Enable180Classification = false
            };
        }
        catch
        {
            return null!;
        }
    }, isThreadSafe: true);

    private static PaddleOcrAll PaddleEngine => _paddleEngine.Value;

    private readonly Dictionary<string, WpfRect> _lockedPositions = new();
    private readonly Dictionary<string, DateTime> _seenTexts = new(StringComparer.OrdinalIgnoreCase);
    
    private static readonly HashSet<string> _uiKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "hp", "mp", "exp", "lvl", "level", "gold", "fps", "ping", "inventory", "skills", "quests", "quest", "journal", "map", "menu", "options", "back", "save", "load", "quit", "exit"
    };

    private const double AnchorThreshold = 20.0;
    private bool _disposed;

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int n);

    public string SelectedEngine { get; set; } = "WindowsOCR"; // WindowsOCR veya PaddleOCR
    public string SourceLanguage { get; set; } = "en"; // Varsayılan kaynak dili (ingilizce)

    public async Task<List<OcrBlock>> RecognizeAsync(
        List<WpfRect> exclusionZones,
        WpfRect? scanRegion = null)
    {
        var results = new List<OcrBlock>();
        if (_disposed) return results;

        bool isDefaultScan = (scanRegion == null);

        // EĞER KULLANICI ÖZEL BÖLGE SEÇMEDİYSE: Ekranın sadece alt-orta kısmını (alytazı bölgesini) tara.
        // Bu sayede can barı, minimap, menü yazıları gibi "garip garip şeyler" çevrilmez.
        if (scanRegion == null)
        {
            int sw = GetSystemMetrics(0);
            int sh = GetSystemMetrics(1);
            scanRegion = new WpfRect(sw * 0.15, sh * 0.70, sw * 0.70, sh * 0.22);
        }

        var list = await Task.Run(async () =>
        {
            try
            {
                using var bmp = CaptureScreen(scanRegion);
                if (bmp == null) return results;

                // 1. Windows.Media.Ocr (Yerleşik, Hızlı, Bağımsız)
                if (SelectedEngine == "WindowsOCR")
                {
                    var blocks = await RunWindowsOcrAsync(bmp, exclusionZones, scanRegion);
                    return blocks;
                }

                // 2. PaddleOCR (AI Tabanlı Gelişmiş Motor)
                if (PaddleEngine != null)
                {
                    using var mat = BitmapConverter.ToMat(bmp);
                    
                    // Exclusion Zone Boyama
                    using (var g_dpi = Graphics.FromImage(bmp))
                    {
                        float dpiScale = g_dpi.DpiX / 96.0f;
                        if (exclusionZones != null && exclusionZones.Count > 0)
                        {
                            using var brush = new SolidBrush(System.Drawing.Color.Black);
                            foreach (var z in exclusionZones)
                            {
                                float px = (float)(z.X * dpiScale - (scanRegion?.X * dpiScale ?? 0));
                                float py = (float)(z.Y * dpiScale - (scanRegion?.Y * dpiScale ?? 0));
                                float pw = (float)(z.Width * dpiScale);
                                float ph = (float)(z.Height * dpiScale);
                                g_dpi.FillRectangle(brush, px, py, pw, ph);
                            }
                        }
                    }

                    using var gray = new Mat();
                    Cv2.CvtColor(mat, gray, ColorConversionCodes.BGRA2GRAY);
                    
                    using var binary = new Mat();
                    Cv2.AdaptiveThreshold(gray, binary, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 11, 2);

                    using var resized = new Mat();
                    Cv2.Resize(binary, resized, new OpenCvSharp.Size(binary.Width * 1.5, binary.Height * 1.5), 0, 0, InterpolationFlags.Linear);

                    using var ocrInput = new Mat();
                    Cv2.CvtColor(resized, ocrInput, ColorConversionCodes.GRAY2BGR);

                    var ocrResult = PaddleEngine.Run(ocrInput);
                    
                    double dpiX = 96.0, dpiY = 96.0;
                    try
                    {
                        using var g = Graphics.FromHwnd(IntPtr.Zero);
                        dpiX = g.DpiX;
                        dpiY = g.DpiY;
                    }
                    catch { }

                    double scaleX = (96.0 / dpiX) / 1.5;
                    double scaleY = (96.0 / dpiY) / 1.5;

                    foreach (var region in ocrResult.Regions)
                    {
                        if (region.Score < 0.5f) continue;

                        string text = region.Text?.Trim() ?? "";
                        text = System.Text.RegularExpressions.Regex.Replace(text, @"[^a-zA-Z0-9\söçşığüÖÇŞİĞÜ\.\!\?\:\,]", "");
                        text = System.Text.RegularExpressions.Regex.Replace(text, @"^\d+\s*", "");
                        text = CorrectGamingTerms(text);
                        
                        // Gürültü filtrelerini otomatik modda çalıştır, manuel modda esnet
                        if (scanRegion == null)
                        {
                            if (text.Length < 3) continue;
                            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+$")) continue;
                            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[^a-zA-ZöçşığüÖÇŞİĞÜ]+$")) continue;
                        }
                        else
                        {
                            if (text.Length < 1) continue;
                            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+$")) continue;
                        }

                        var raw = region.Rect.BoundingRect();
                        var wpfRect = new WpfRect(
                            raw.X * scaleX + (scanRegion?.X ?? 0),
                            raw.Y * scaleY + (scanRegion?.Y ?? 0),
                            raw.Width * scaleX,
                            raw.Height * scaleY);

                        if (scanRegion == null && IsExcluded(wpfRect, exclusionZones)) continue;
                        wpfRect = Anchor(text, wpfRect);
                        
                        var (bg, fg) = SampleColor(bmp, new CvRect((int)(raw.X / 1.5), (int)(raw.Y / 1.5), (int)(raw.Width / 1.5), (int)(raw.Height / 1.5)), scaleX * 1.5, scaleY * 1.5);

                        results.Add(new OcrBlock { Text = text, Rect = wpfRect, Background = bg, Foreground = fg });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OCR ERROR] {ex.Message}");
            }
            return results;
        });

        if (isDefaultScan)
        {
            return FilterSubtitles(list);
        }
        return list;
    }

    private async Task<List<OcrBlock>> RunWindowsOcrAsync(Bitmap bmp, List<WpfRect> exclusionZones, WpfRect? scanRegion)
    {
        var list = new List<OcrBlock>();
        try
        {
            // 2x Ölçekleme & Netleştirme Ön-İşlemi (Cubic Interpolation ile Yazı Kalitesini Uçur!)
            using var mat = BitmapConverter.ToMat(bmp);
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGRA2GRAY);
            
            using var resized = new Mat();
            Cv2.Resize(gray, resized, new OpenCvSharp.Size(gray.Width * 2.0, gray.Height * 2.0), 0, 0, InterpolationFlags.Cubic);
            
            using var processedMat = new Mat();
            Bitmap processedBmp;
            
            if (scanRegion != null)
            {
                // Manuel seçim için: Ham gri tonlamalı 2x büyütülmüş resmi kullan.
                // Windows OCR bu şekilde harika okur çünkü yapay kontrast germe yazıyı bozabilir.
                processedBmp = BitmapConverter.ToBitmap(resized);
            }
            else
            {
                // Otomatik altyazı modu için: Kontrast germe uygula.
                using var floatMat = new Mat();
                resized.ConvertTo(floatMat, MatType.CV_32F);
                
                using var temp = floatMat - new Scalar(110.0);
                using var stretchedExpr = temp * (255.0 / 145.0);
                using var stretched = stretchedExpr.ToMat();
                
                stretched.ConvertTo(processedMat, MatType.CV_8U);
                processedBmp = BitmapConverter.ToBitmap(processedMat);
            }
            
            using var ms = new MemoryStream();
            using (processedBmp)
            {
                processedBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            }
            ms.Position = 0;

            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            string bcpTag = GetBcp47Tag(SourceLanguage);
            var lang = new Windows.Globalization.Language(bcpTag);
            
            Windows.Media.Ocr.OcrEngine? ocrEngine = null;
            if (Windows.Media.Ocr.OcrEngine.IsLanguageSupported(lang))
            {
                ocrEngine = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(lang);
            }
            
            if (ocrEngine == null)
            {
                ocrEngine = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
            }

            if (ocrEngine == null) return list;

            var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
            
            double dpiX = 96.0, dpiY = 96.0;
            try
            {
                using var g = Graphics.FromHwnd(IntPtr.Zero);
                dpiX = g.DpiX;
                dpiY = g.DpiY;
            }
            catch { }

            double scaleX = 96.0 / dpiX;
            double scaleY = 96.0 / dpiY;

            foreach (var line in ocrResult.Lines)
            {
                string text = line.Text.Trim();
                text = System.Text.RegularExpressions.Regex.Replace(text, @"[^a-zA-Z0-9\söçşığüÖÇŞİĞÜ\.\!\?\:\,]", "");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"^\d+\s*", "");
                text = CorrectGamingTerms(text);
                
                // Gürültü filtrelerini otomatik modda çalıştır, manuel modda esnet
                if (scanRegion == null)
                {
                    if (text.Length < 3) continue;
                    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+$")) continue;
                    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[^a-zA-ZöçşığüÖÇŞİĞÜ]+$")) continue;
                }
                else
                {
                    if (text.Length < 1) continue;
                    if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+$")) continue;
                }

                var bounds = line.Words.Select(w => w.BoundingRect).ToList();
                if (bounds.Count == 0) continue;

                double minX = bounds.Min(b => b.X) / 2.0;
                double minY = bounds.Min(b => b.Y) / 2.0;
                double maxX = bounds.Max(b => b.X + b.Width) / 2.0;
                double maxY = bounds.Max(b => b.Y + b.Height) / 2.0;

                var wpfRect = new WpfRect(
                    minX * scaleX + (scanRegion?.X ?? 0),
                    minY * scaleY + (scanRegion?.Y ?? 0),
                    (maxX - minX) * scaleX,
                    (maxY - minY) * scaleY);

                // Manuel seçimde mini menü veya dışlama bölgesi engeline takılmasını önle
                if (scanRegion == null && IsExcluded(wpfRect, exclusionZones)) continue;
                wpfRect = Anchor(text, wpfRect);

                var (bg, fg) = SampleColor(bmp, new CvRect((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY)), scaleX, scaleY);
                list.Add(new OcrBlock { Text = text, Rect = wpfRect, Background = bg, Foreground = fg });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsOCR ERROR] {ex.Message}");
        }
        return list;
    }

    private WpfRect Anchor(string key, WpfRect incoming)
    {
        if (_lockedPositions.TryGetValue(key, out var locked))
        {
            double dx = Math.Abs(incoming.X - locked.X);
            double dy = Math.Abs(incoming.Y - locked.Y);
            if (dx < AnchorThreshold && dy < AnchorThreshold)
                return locked;
        }
        _lockedPositions[key] = incoming;
        return incoming;
    }

    private static bool IsExcluded(WpfRect r, List<WpfRect> zones)
    {
        if (r.X < 280 && r.Y < 120) return true; // Sol üst mini menüyü asla tarama
        foreach (var z in zones)
        {
            var inter = WpfRect.Intersect(r, z);
            if (inter.Width > 0 && (inter.Width * inter.Height) / (r.Width * r.Height) > 0.4)
                return true;
        }
        return false;
    }

    private static (Color bg, Color fg) SampleColor(Bitmap bmp, CvRect raw, double sx, double sy)
    {
        try
        {
            int px = Math.Clamp(raw.X + raw.Width / 2, 0, bmp.Width - 1);
            int py = Math.Clamp(raw.Y - 3, 0, bmp.Height - 1);
            var c = bmp.GetPixel(px, py);
            double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
            var bg = Color.FromArgb(220, c.R, c.G, c.B);
            var fg = lum > 0.5 ? Colors.Black : Colors.White;
            return (bg, fg);
        }
        catch { return (Color.FromArgb(220, 6, 8, 19), Colors.White); }
    }

    private static Bitmap CaptureScreen(WpfRect? region)
    {
        int sw = GetSystemMetrics(0), sh = GetSystemMetrics(1);
        int x = 0, y = 0, w = sw, h = sh;
        if (region.HasValue)
        {
            x = (int)region.Value.X; y = (int)region.Value.Y;
            w = (int)region.Value.Width; h = (int)region.Value.Height;
        }
        if (w <= 0 || h <= 0) return null!;
        var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h), CopyPixelOperation.SourceCopy);
        return bmp;
    }

    private double? _autoLockedY = null;
    private double? _autoLockedHeight = null;
    private int _lockConfidence = 0;

    private List<OcrBlock> FilterSubtitles(List<OcrBlock> blocks)
    {
        if (blocks.Count == 0) return blocks;

        int sw = GetSystemMetrics(0);
        int sh = GetSystemMetrics(1);

        // Altyazı kriterleri:
        // 1. Ekranın yatay merkezine yakın olmalı (merkezi sw * 0.15 ile sw * 0.85 arasında)
        // 2. Çok kısa kelimeler dahil (karakter sayısı > 2, örn: "Yes", "No", "Run")
        // 3. Ekranın alt yarısında olmalı (Y > sh * 0.40, pencere modları ve siyah barlar için çok güvenli)
        var potentialSubtitles = blocks.Where(b => 
            b.Rect.X + b.Rect.Width / 2.0 > sw * 0.15 && 
            b.Rect.X + b.Rect.Width / 2.0 < sw * 0.85 && 
            b.Rect.Y > sh * 0.40 &&
            b.Text.Length > 2
        ).ToList();

        if (potentialSubtitles.Count == 0)
        {
            return new List<OcrBlock>(); // Altyazı kriterlerine uyan metin yoksa hiçbir şey çevirme!
        }

        // GELİŞMİŞ LİNGUİSTİK HUD & GÜRÜLTÜ FİLTRESİ
        // Ekranda sabit duran arayüz yazılarını, can barlarını, quest tracker'ları ve butonları eler.
        var filteredList = new List<OcrBlock>();

        foreach (var block in potentialSubtitles)
        {
            string cleanText = block.Text.Trim();
            string lowerText = cleanText.ToLowerInvariant();

            // 1. Sayısal, Oran ve Sayaç Kontrolleri (Can barları, XP göstergeleri, süreler vb.)
            if (System.Text.RegularExpressions.Regex.IsMatch(cleanText, @"^[\d\s\/\+\-%:\.,gG]+$")) continue; // "100/100", "+50XP", "60 FPS" vb.

            // 2. Sıradışı Karakterler ve Tuş İkonları (örn: "[E]", "(Y)", "F1", "TAB")
            if (System.Text.RegularExpressions.Regex.IsMatch(cleanText, @"^[\[\(\{]?[a-zA-Z0-9]{1,2}[\]\)\}]?$")) continue; // "E", "[F]", "(X)", "L1", "R2"

            // 3. Ünlü Harf (Vowel) Kuralı: İçinde ünlü harf yoksa bu kesinlikle anlamsız OCR gürültüsüdür!
            // Türkçe ve İngilizce ünlü harfler: a, e, i, o, u, ö, ü, ı, y
            bool hasVowel = System.Text.RegularExpressions.Regex.IsMatch(lowerText, @"[aeıoöuüyæøå]");
            if (!hasVowel) continue; // Ünlü harf içermeyen metinleri (örn: "JJDthfrj3", "FPS", "HP", "XP", "LVL") doğrudan ele!

            // 4. Keyword Arayüz Verisi Kontrolü
            if (_uiKeywords.Contains(cleanText)) continue;

            filteredList.Add(block);
        }

        if (filteredList.Count == 0)
        {
            return new List<OcrBlock>();
        }

        // En çok altyazıya benzeyen (en alttaki ve merkeze en yakın olan) bloğu seçelim
        var bestSub = filteredList.OrderByDescending(b => b.Rect.Y).First();

        // Altyazı genellikle 1 veya en fazla 2 satırdır ve bunlar birbirine dikey olarak çok yakındır.
        // En alttaki ana altyazı bloğunun Y koordinatına çok yakın olan (en fazla 70px veya yüksekliğinin 2.5 katı toleranslı)
        // diğer satırları da altyazının bir parçası olarak kabul et.
        double toleranceY = Math.Max(70.0, bestSub.Rect.Height * 2.5);
        
        var finalSubtitles = filteredList.Where(b => 
            Math.Abs(b.Rect.Y - bestSub.Rect.Y) < toleranceY
        ).ToList();

        return finalSubtitles;
    }

    private static readonly Dictionary<string, string> _ocrCorrections = new(StringComparer.OrdinalIgnoreCase)
    {
        { "speji", "Speki" },
        { "spej", "Speki" },
        { "speki", "Speki" },
        { "svanna", "Svanna" },
        { "vanna", "Svanna" },
        { "atrele", "Atreus" },
        { "treus", "Atreus" },
        { "reus", "Atreus" },
        { "Kytos", "Kratos" },
        { "Krotos", "Kratos" },
        { "Kratas", "Kratos" },
        { "Krtos", "Kratos" },
        { "Atres", "Atreus" },
        { "Atreos", "Atreus" },
        { "Gerat", "Geralt" },
        { "Gerlt", "Geralt" },
        { "Yenifer", "Yennefer" },
        { "Yenefer", "Yennefer" },
        { "Cire", "Ciri" },
        { "Artur", "Arthur" },
        { "Dutc", "Dutch" },
        { "Silvehand", "Silverhand" },
        { "Jhon", "John" },
        { "Eley", "Ellie" },
        { "Elie", "Ellie" },
        { "Jole", "Joel" }
    };

    private static string CorrectGamingTerms(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        
        foreach (var pair in _ocrCorrections)
        {
            text = System.Text.RegularExpressions.Regex.Replace(
                text, 
                @"\b" + System.Text.RegularExpressions.Regex.Escape(pair.Key) + @"\b", 
                pair.Value, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }
        return text;
    }

    public void ResetAnchors()
    {
        _lockedPositions.Clear();
        _autoLockedY = null;
        _autoLockedHeight = null;
        _lockConfidence = 0;
    }

    private static string GetBcp47Tag(string langCode)
    {
        if (string.IsNullOrEmpty(langCode)) return "en-US";
        langCode = langCode.ToLowerInvariant().Trim();
        if (langCode == "auto") return "en-US";
        
        return langCode switch
        {
            "en" => "en-US",
            "ja" => "ja-JP",
            "ko" => "ko-KR",
            "zh-cn" => "zh-Hans-CN",
            "zh-tw" => "zh-Hant-TW",
            "de" => "de-DE",
            "fr" => "fr-FR",
            "es" => "es-ES",
            "tr" => "tr-TR",
            "ru" => "ru-RU",
            _ => langCode.Contains('-') ? langCode : $"{langCode}-{langCode.ToUpperInvariant()}"
        };
    }

    public void Dispose()
    {
        _disposed = true;
        _lockedPositions.Clear();
    }
}
