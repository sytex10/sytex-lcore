using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using SytexLCore.Services;
using SytexLCore.Models;
using System.Drawing;

namespace SytexLCore
{
    public static class TestProgram
    {
        [STAThread]
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("==================================================");
            Console.WriteLine("    SYTEX L-CORE ULTIMATE - KALİTE TEST ÇALIŞMASI  ");
            Console.WriteLine("==================================================");
            
            // --- TEST 1: ÇEVİRİ KALİTESİ VE GAMER SÖZLÜĞÜ TESTİ ---
            Console.WriteLine("\n[TEST 1] Çeviri ve Gamer Yerelleştirme Kalitesi Test Ediliyor...");
            using var translator = new TranslationService();
            translator.SourceLanguage = "en";
            translator.TargetLanguage = "tr";
            translator.Provider = TranslationProvider.GoogleGTX; // Google Translate GTX test edelim

            var testSentences = new Dictionary<string, string>
            {
                { "Kratos: Nature will take its course.", "Kratos: Doğa kendi yoluna gidecek." },
                { "Atreus: Okay, but... how could things be any worse than here?", "Atreus: Pekala ama... işler nasıl buradan daha kötü olabilir ki?" },
                { "Mimir: They say Fimbulwinter affects dreams, lad.", "Mimir: Fimbulwinter'ın bazı bölgeleri etkilediğini söylüyorlar evlat." },
                { "Boy, look out!", "Evlat, siper al!" },
                { "Oh damn, the storm is coming.", "Kahretsin, fırtına yaklaşıyor." },
                { "What the hell is that?", "Ne haltlar dönüyor orada?" },
                { "Shut up and help me.", "Kes sesini ve bana yardım et." },
                { "Yes, sir.", "Evet efendim." }
            };

            int successCount = 0;
            int totalCount = testSentences.Count;

            foreach (var test in testSentences)
            {
                string original = test.Key;
                string expected = test.Value;
                
                string[] result = await translator.TranslateBatchAsync(new[] { original });
                string translated = result[0];

                Console.WriteLine($"\n[İngilizce]: {original}");
                Console.WriteLine($"[Beklenen] : {expected}");
                Console.WriteLine($"[Çevrilen] : {translated}");

                // Doğruluk değerlendirmesi
                double similarity = CalculateSimilarity(translated.ToLower(), expected.ToLower());
                bool isCorrect = similarity > 0.75;
                if (isCorrect) successCount++;

                Console.WriteLine($"[Uyuşma Skoru]: %{similarity * 100:0.0} -> {(isCorrect ? "BAŞARILI" : "ZAYIF")}");
            }

            double translationAccuracy = (double)successCount / totalCount * 100.0;
            Console.WriteLine($"\n>> ÇEVİRİ DOĞRULUK ORANI: %{translationAccuracy:0.0}");

            // --- TEST 2: OCR VE GÖRÜNTÜ İŞLEME SİMÜLASYONU VE TESTİ ---
            Console.WriteLine("\n==================================================");
            Console.WriteLine("[TEST 2] Kaydedilmiş Ekran Görüntüleri Üzerinde OCR Testi...");
            
            string artifactsDir = @"C:\Users\mamo1\.gemini\antigravity\brain\fb2766ee-7f81-468a-8279-1ff436fced65";
            if (Directory.Exists(artifactsDir))
            {
                var pngFiles = Directory.GetFiles(artifactsDir, "media__*.png");
                Console.WriteLine($"Klasörde {pngFiles.Length} adet görsel bulundu.");

                using var ocr = new OcrService();
                ocr.SelectedEngine = "WindowsOCR";
                ocr.SourceLanguage = "en";

                foreach (var file in pngFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        // Çok küçük dosyaları (log ekran görüntülerini vb.) atlayıp sadece oyun görsellerini bulmaya çalışalım
                        if (fileInfo.Length < 10000) continue; 

                        Console.WriteLine($"\nTest Edilen Dosya: {Path.GetFileName(file)} ({fileInfo.Length / 1024} KB)");
                        
                        using var bmp = new Bitmap(file);
                        // Tüm resmi tarayalım
                        var blocks = await ocr.RecognizeAsync(new List<System.Windows.Rect>(), new System.Windows.Rect(0, 0, bmp.Width, bmp.Height));
                        
                        if (blocks.Count == 0)
                        {
                            Console.WriteLine("-> Bu görselde herhangi bir altyazı veya metin algılanamadı.");
                        }
                        else
                        {
                            Console.WriteLine("-> Algılanan Metinler:");
                            foreach (var b in blocks)
                            {
                                Console.WriteLine($"   [{b.Rect.X:0},{b.Rect.Y:0}] {b.Text}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Hata ({Path.GetFileName(file)}): {ex.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine("Hata: Artifacts klasörü bulunamadı.");
            }

            Console.WriteLine("\n==================================================");
            Console.WriteLine("Test çalışması tamamlandı. Çıkış için bir tuşa basın...");
        }

        // Levenshtein Benzerlik Ölçümü
        private static double CalculateSimilarity(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 1.0 : 0.0;
            if (string.IsNullOrEmpty(t)) return 0.0;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            double distance = d[n, m];
            double maxLen = Math.Max(n, m);
            return 1.0 - (distance / maxLen);
        }
    }
}
