namespace SytexLCore.Services;

public enum AppLanguage { Turkish, English }

public static class Loc
{
    public static AppLanguage Current { get; private set; } = AppLanguage.Turkish;

    public static event Action? LanguageChanged;

    public static void Set(AppLanguage lang)
    {
        Current = lang;
        LanguageChanged?.Invoke();
    }

    public static void Toggle()
    {
        Set(Current == AppLanguage.Turkish ? AppLanguage.English : AppLanguage.Turkish);
    }

    // ── UI Strings ──────────────────────────────────────────────────────────
    public static string Title          => T("👾 SYTEX L-CORE ULTIMATE 🕹",        "👾 SYTEX L-CORE ULTIMATE 🕹");
    public static string ControlPanel   => T("🕹 KONTROL PANELİ",                   "🕹 CONTROL PANEL");
    public static string DetectedGame   => T("👾 Algılanan Oyun:",                  "👾 Detected Game:");
    public static string Waiting        => T("Bekleniyor...",                        "Waiting...");
    public static string StartBtn       => T("ÇEVİRİYİ BAŞLAT",                     "START TRANSLATION");
    public static string StopBtn        => T("ÇEVİRİYİ DURDUR",                     "STOP TRANSLATION");
    public static string BorderlessBtn  => T("Borderless (Penceresiz) Zorla",        "Force Borderless Window");
    public static string SettingsBtn    => T("Gelişmiş Ayarlar Menüsü",             "Advanced Settings");
    public static string EngineLabel    => T("Çeviri Motoru: Google GTX",           "Translation Engine: Google GTX");
    public static string OcrLabel       => T("OCR Motoru: Windows OCR",             "OCR Engine: Windows OCR");
    public static string TotalLabel     => T("Toplam Çeviri: ",                     "Total Translations: ");
    public static string RecentTitle    => T("░▒▓ SON ÇEVİRİLER (TERMİNAL) ▓▒░",  "░▒▓ RECENT TRANSLATIONS (LOG) ▓▒░");
    public static string StatusReady    => T("Hazır (F10 ile başlatın)",             "Ready (Press F10 to start)");
    public static string StatusRunning  => T("⬤ AKTIF - Çeviri Yapılıyor",          "⬤ ACTIVE - Translating");
    public static string StatusStopped  => T("Durduruldu",                           "Stopped");
    public static string SystemReady    => T("[SYSTEM] SYTEX L-Core Ultimate başlatıldı...\n[SYSTEM] Hazır! F10 tuşuyla otomatik çeviriyi açabilirsiniz.",
                                             "[SYSTEM] SYTEX L-Core Ultimate started...\n[SYSTEM] Ready! Press F10 to enable auto translation.");
    public static string StatusLabel    => T("👾 Durum: ",                           "👾 Status: ");

    // Bubble
    public static string BubbleScanning => T("OCR taranıyor ve çevriliyor...",      "Scanning and translating...");
    public static string BubbleNotFound => T("Metin bulunamadı.",                   "No text found.");
    public static string BubbleNotFoundMsg => T("Seçilen alanda herhangi bir yazı tespit edilemedi.", "No text was detected in the selected area.");
    public static string BubbleError    => T("Hata oluştu",                         "Error occurred");
    public static string BubbleHeader   => T("✦ SYTEX MANUAL SCAN",                "✦ SYTEX MANUAL SCAN");

    // MiniMenu
    public static string MiniAuto       => T("⚡ OTO ÇEVİRİ",                       "⚡ AUTO TRANSLATE");
    public static string MiniManual     => T("🎯 MANUEL",                           "🎯 MANUAL");
    public static string MiniSettings   => T("⚙ AYARLAR",                          "⚙ SETTINGS");

    // GameDetected
    public static string GameActive     => T("👾 OYUN AKTİF",                       "👾 GAME ACTIVE");

    private static string T(string tr, string en) => Current == AppLanguage.Turkish ? tr : en;
}
