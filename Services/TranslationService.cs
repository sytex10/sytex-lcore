using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;

namespace SytexLCore.Services;

public enum TranslationProvider
{
    GoogleGTX,
    Gemini,
    DeepL
}

public sealed class TranslationService : IDisposable
{
    private static readonly HttpClient _http;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    static TranslationService()
    {
        var handler = new HttpClientHandler
        {
            Proxy = null,
            UseProxy = false,
            AllowAutoRedirect = false
        };
        _http = new HttpClient(handler);
        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        _http.Timeout = TimeSpan.FromSeconds(8);
        
        // Connection Lease Timeout ayarı (latency azaltmak için)
        var sp = System.Net.ServicePointManager.FindServicePoint(new Uri("https://translate.googleapis.com"));
        sp.ConnectionLeaseTimeout = 60000; // 1 dk
    }

    public string TargetLanguage { get; set; } = "tr";
    public string SourceLanguage { get; set; } = "auto";
    
    public TranslationProvider Provider { get; set; } = TranslationProvider.GoogleGTX;
    public string? ApiKey { get; set; }
    public string AiModel { get; set; } = "gemini-1.5-flash";

    public async Task<string[]> TranslateBatchAsync(string[] texts)
    {
        if (texts.Length == 0) return Array.Empty<string>();

        // Caching mekanizması
        var results = new string[texts.Length];
        var toTranslate = new List<(int index, string text)>();

        for (int i = 0; i < texts.Length; i++)
        {
            if (_cache.TryGetValue(texts[i], out var cached))
                results[i] = cached;
            else
                toTranslate.Add((i, texts[i]));
        }

        if (toTranslate.Count == 0) return results;

        string[] newTranslations;
        string[] rawTexts = toTranslate.Select(t => t.text).ToArray();

        // Sağlayıcıya göre çeviri yap
        if (Provider == TranslationProvider.Gemini && !string.IsNullOrWhiteSpace(ApiKey))
        {
            newTranslations = await TranslateGeminiAsync(rawTexts);
        }
        else if (Provider == TranslationProvider.DeepL && !string.IsNullOrWhiteSpace(ApiKey))
        {
            newTranslations = await TranslateDeepLAsync(rawTexts);
        }
        else
        {
            newTranslations = await TranslateGtxAsync(rawTexts);
        }

        // Sonuçları yerleştir ve önbelleğe al
        for (int i = 0; i < toTranslate.Count; i++)
        {
            string original = toTranslate[i].text;
            string trans = i < newTranslations.Length ? newTranslations[i] : original;

            // AKILLI SİBER YERELLEŞTİRME VE DUYGUSAL POST-PROCESS
            trans = ApplyGamerLocalization(trans);

            results[toTranslate[i].index] = trans;
            _cache[original] = trans;
        }

        return results;
    }

    // ── Google GTX (Ücretsiz Toplu Çeviri - Paralel & Güvenli) ──────────────────
    private async Task<string[]> TranslateGtxAsync(string[] texts)
    {
        var tasks = texts.Select(async text =>
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            try
            {
                string src = SourceLanguage == "auto" ? "auto" : SourceLanguage;
                string encoded = HttpUtility.UrlEncode(text.Replace("\n", " ").Trim());
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={src}&tl={TargetLanguage}&dt=t&q={encoded}";

                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                var response = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var arr = doc.RootElement[0];
                var sb = new System.Text.StringBuilder();
                foreach (var part in arr.EnumerateArray())
                {
                    if (part.ValueKind == JsonValueKind.Array && part.GetArrayLength() > 0)
                    {
                        var strPart = part[0].GetString();
                        if (!string.IsNullOrEmpty(strPart))
                            sb.Append(strPart);
                    }
                }
                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GTX Single ERROR] {ex.Message}");
                return text;
            }
        });
        return await Task.WhenAll(tasks);
    }

    // ── Gemini AI (Bağlamsal Çeviri) ────────────────────────────────────────
    private async Task<string[]> TranslateGeminiAsync(string[] texts)
    {
        try
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{AiModel}:generateContent?key={ApiKey}";
            
            var promptArray = texts.Select((t, i) => $"[{i}] {t}").ToArray();
            string prompt;
            if (TargetLanguage == "tr")
            {
                prompt = "Sen, profesyonel bir oyun yerelleştirme (localization) ve çeviri uzmanısın. Görevin, sana iletilen İngilizce oyun altyazılarını/metinlerini en doğal, akıcı ve sahnenin ruhuna uygun şekilde Türkçe'ye çevirmektir.\n\n" +
                         "LÜTFEN ŞU KURALLARA KESİNLİKLE UY:\n\n" +
                         "BAĞLAMSAL VE DUYGULU ÇEVİRİ: Kelimesi kelimesine (literal) çeviri kesinlikle yapma. Karakterin o anki duygusunu (öfke, hüzün, korku, heyecan, alaycılık, çaresizlik), oyunun geçtiği dönemi ve atmosferi (vahşi batı, mitoloji, modern sokak vb.) hesaba katarak \"yaşayan\" bir Türkçe kullan. İngilizce kalıpları, deyimleri ve argoları Türkçe oyuncu kültürüne ve sokak jargonuna en uygun, en cuk oturan dinamik karşılıklarıyla değiştir.\n\n" +
                         "KOD GÜVENLİĞİ VE KESİN KURAL (SADECE ÇEVİRİ): Çıktı olarak ASLA açıklama yapma. Başına veya sonuna 'İşte çeviri:', 'Translation:', 'Tabii ki:' gibi ifadeler ekleme. Metni gereksiz tırnak işaretleri içine alma. Sadece ve sadece istenen çeviri formatını döndür. Kodum bu metni direkt ekrana basacağı için ekstra tek bir karakter bile arayüzü bozar.\n\n" +
                         "ALTYAZI DİNAMİĞİ (KISA VE ÖZ): Oyuncunun metni ekranda kalma süresi içinde rahatça okuyabilmesi için çeviriyi gereksiz yere uzatma. İngilizce metindeki vuruculuğu ve uzunluk dengesini Türkçe karşılığında da koru.\n\n" +
                         "FORMAT: Çevirileri sadece '[i] Çeviri' formatında, her satıra bir tane gelecek şekilde ver (örn: '[0] Çevrilmiş Metin').\n\n" +
                         string.Join("\n", promptArray);
            }
            else
            {
                prompt = "Translate the following game text from " + (SourceLanguage == "auto" ? "any language" : SourceLanguage) + 
                         " to " + TargetLanguage + ".\n" +
                         "IMPORTANT: Provide the translation ONLY. Keep the exact same format [0] translated text \\n [1] translated text. " +
                         "Adapt it to fit game context (dialogue, menus, items).\n\n" +
                         string.Join("\n", promptArray);
            }

            var payload = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new { temperature = 0.3 }
            };

            _http.DefaultRequestHeaders.Clear();
            var response = await _http.PostAsJsonAsync(url, payload);
            var resultStr = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(resultStr);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Gemini ERROR] {resultStr}. Falling back to Google GTX.");
                return await TranslateGtxAsync(texts);
            }

            var textResp = doc.RootElement.GetProperty("candidates")[0]
                              .GetProperty("content")
                              .GetProperty("parts")[0]
                              .GetProperty("text").GetString() ?? "";

            // Gelen metni [0], [1] vb taglara göre parçala
            var results = new string[texts.Length];
            var lines = textResp.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach(var line in lines)
            {
                if(line.TrimStart().StartsWith("["))
                {
                    int endIdx = line.IndexOf(']');
                    if(endIdx > 0 && int.TryParse(line.Substring(1, endIdx - 1), out int idx) && idx < results.Length)
                    {
                        results[idx] = line.Substring(endIdx + 1).Trim();
                    }
                }
            }

            // Çevirisi gelmeyen varsa yedeği koy
            for (int i = 0; i < texts.Length; i++)
                if (string.IsNullOrEmpty(results[i])) results[i] = texts[i];

            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gemini ERROR] {ex.Message}. Falling back to Google GTX.");
            return await TranslateGtxAsync(texts);
        }
    }

    // ── DeepL (Resmi API) ───────────────────────────────────────────────────
    private async Task<string[]> TranslateDeepLAsync(string[] texts)
    {
        try
        {
            // DeepL Free API veya Pro API
            string domain = ApiKey!.EndsWith(":fx") ? "api-free.deepl.com" : "api.deepl.com";
            string url = $"https://{domain}/v2/translate";

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {ApiKey}");

            var requestData = new List<KeyValuePair<string, string>>();
            foreach(var t in texts) requestData.Add(new("text", t));
            requestData.Add(new("target_lang", TargetLanguage.ToUpperInvariant() == "EN" ? "EN-US" : TargetLanguage.ToUpperInvariant()));
            if (SourceLanguage != "auto")
                requestData.Add(new("source_lang", SourceLanguage.ToUpperInvariant()));

            var response = await _http.PostAsync(url, new FormUrlEncodedContent(requestData));
            var resultStr = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[DeepL ERROR] {resultStr}");
                return texts;
            }

            using var doc = JsonDocument.Parse(resultStr);
            var translations = doc.RootElement.GetProperty("translations");
            var results = new string[texts.Length];
            
            for(int i = 0; i < texts.Length; i++)
            {
                results[i] = i < translations.GetArrayLength() 
                    ? translations[i].GetProperty("text").GetString() ?? texts[i]
                    : texts[i];
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeepL ERROR] {ex.Message}");
            return texts;
        }
    }

    private string ApplyGamerLocalization(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || TargetLanguage != "tr") return input;

        // Kuru/resmi kelime çevirilerini siber-glow oyun atmosferine uyduracak zengin duygusal Gamer sözlüğü
        var replacements = new Dictionary<string, string>
        {
            // God of War Özel & Ailevi Duygu Hitapları
            { "oğlan", "evlat" },
            { "oğlanı", "evladı" },
            { "oğlana", "evlada" },
            { "oğlandan", "evlattan" },
            { "oğlum", "evlat" },
            { "oğlumun", "evladımın" },
            { "çocuk", "evlat" }, // Kratos'un Atreus'a "Boy" hitabı
            
            // Kaba ve Resmi Olmayan Kalıplar (Gamer Yerelleştirmesi)
            { "lanet olsun", "kahretsin" },
            { "lanet", "kahretsin" },
            { "lanet olası", "kahrolası" },
            { "lanet olasıca", "kahrolası" },
            { "aman tanrım", "yok artık" },
            { "aman allah'ım", "hadi be" },
            { "cehennem hayır", "asla" },
            { "kapa çeneni", "kes sesini" },
            { "kapa o çeneni", "kes sesini" },
            { "çeneni kapat", "kes sesini" },
            { "sesini kes", "kes sesini" },
            { "git buradan", "defol git" },
            { "buradan git", "defol git" },
            { "bunu yapabiliriz", "başarabiliriz" },
            { "yapabiliriz", "başarabiliriz" },
            { "bebek", "dostum" },
            { "dikkat et", "siper al" },
            { "kendine dikkat et", "kendini kolla" },
            { "neler oluyor", "ne haltlar dönüyor" },
            { "ne oluyor", "ne haltlar dönüyor" },
            { "emin misin", "ciddi misin" },
            { "ciddi olamazsın", "hadi oradan" },
            { "kıçını", "arkanı" },
            { "kıçımı", "arkamı" },
            { "kıçını kurtar", "kıçını kurtar" },
            { "hemen git", "tüymeliyiz" },
            { "hemen kaç", "tüymeliyiz" },
            { "defol", "yıkıl karşımdan" },
            
            // Çatışma & Aksiyon Terimleri
            { "ateş et", "sık kafasına" },
            { "saldır", "çök üstüne" },
            { "öldür onu", "bitir işini" },
            { "onu öldür", "işini bitir" },
            { "seni öldüreceğim", "işini bitireceğim" },
            { "seni öldürecek", "işini bitirecek" },
            { "ölü bir adamsın", "işin bitti senin" },
            { "ölüceksin", "işin bitti" },
            
            // Dostane & Argo Hitaplar
            { "kardeş", "dostum" },
            { "adamım", "dostum" },
            { "ahbap", "dostum" },
            { "dost", "dostum" },
            { "kanka", "dostum" },
            { "efendi", "usta" },
            { "beyim", "usta" },
            
            // Diğer Duygusal Oyun Diyalogları
            { "lütfen yapma", "yalvarırım yapma" },
            { "lütfen", "yalvarırım" },
            { "özür dilerim", "affet beni" },
            { "beni affet", "affet beni" },
            { "bunu hak etmedin", "bunu hak etmedin" },
            { "güzel", "pekala" }, // Diyalog başlangıcı "Fine"
            { "iyi", "pekala" },
            { "tamam", "pekala" },
            { "her şey yolunda", "sorun yok" },
            { "hiçbir şey", "hiçbir şey" },
            { "bir şey yok", "sorun yok" }
        };

        string output = input;
        foreach (var r in replacements)
        {
            output = System.Text.RegularExpressions.Regex.Replace(
                output, 
                @"\b" + System.Text.RegularExpressions.Regex.Escape(r.Key) + @"\b", 
                r.Value, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return output;
    }

    public void ClearCache() => _cache.Clear();

    public void Dispose()
    {
        _http.Dispose();
        _cache.Clear();
    }
}
