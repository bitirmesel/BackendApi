namespace DktApi.Services;

/// <summary>
/// FluentMe API skorunu zenginleştiren telaffuz değerlendirme servisi.
/// Özellikle kısa Türkçe heceler (ah, ha, ke, ek vb.) için
/// FluentMe'nin ASR transkriptini hedef metinle karşılaştırarak skor üretir.
/// </summary>
public class PronunciationScoringService
{
    /// <summary>
    /// Telaffuz değerlendirmesi yapar.
    /// </summary>
    /// <param name="target">Hedef metin (Flutter'dan gelen, ör: "ah", "kedi")</param>
    /// <param name="transcript">ASR transkripti (FluentMe'nin algıladığı, ör: "ah")</param>
    /// <param name="apiScore">FluentMe'nin verdiği ham skor (0-100)</param>
    /// <returns>Değerlendirme sonucu</returns>
    public PronunciationEvaluation Evaluate(string target, string? transcript, double apiScore)
    {
        var t = NormalizeTurkish(target);
        var h = NormalizeTurkish(transcript ?? "");

        // 1. Ses algılanmadı
        if (string.IsNullOrWhiteSpace(h))
        {
            return new PronunciationEvaluation(0, 0.0, "no_audio");
        }

        // 2. Tam eşleşme
        if (h == t)
        {
            return new PronunciationEvaluation(100, 1.0, "exact_match");
        }

        // 3. Uzatma varyantı (aah → ah, haa → ha, ahh → ah)
        if (IsLengthVariant(h, t))
        {
            return new PronunciationEvaluation(85, 0.9, "length_variant");
        }

        // 4. Transkript hedefi içeriyor veya hedef transkripti içeriyor
        if (h.Contains(t) || t.Contains(h))
        {
            return new PronunciationEvaluation(70, 0.7, "contains");
        }

        // 5. Kısa kelimeler (3 karakter veya altı) — API skoruna güvenme
        if (target.Trim().Length <= 3)
        {
            return new PronunciationEvaluation(0, 1.0, "mismatch_short");
        }

        // 6. Uzun kelimeler — FluentMe API skorunu kullan
        return new PronunciationEvaluation((int)Math.Round(apiScore), 0.8, "api_score");
    }

    /// <summary>
    /// İki stringin "uzatma varyantı" olup olmadığını kontrol eder.
    /// Örn: "aah" ve "ah" → true (fazladan harf tekrarı var)
    ///      "haa" ve "ha" → true
    ///      "kedi" ve "kodu" → false
    /// </summary>
    private static bool IsLengthVariant(string a, string b)
    {
        // İki string de farklı uzunluktaysa kontrol et
        if (a == b) return false;

        var shorter = a.Length <= b.Length ? a : b;
        var longer = a.Length <= b.Length ? b : a;

        // Çok uzunluk farkı varsa varyant değildir
        if (longer.Length - shorter.Length > shorter.Length)
            return false;

        // Kısa stringin tüm karakterleri sırayla uzun stringde bulunmalı
        int i = 0, j = 0;
        while (i < shorter.Length && j < longer.Length)
        {
            if (shorter[i] == longer[j])
            {
                i++;
                j++;
            }
            else if (j > 0 && longer[j] == longer[j - 1])
            {
                // Tekrarlanan karakter — atla
                j++;
            }
            else
            {
                return false;
            }
        }

        // Kalan karakterler tekrar mı kontrol et
        while (j < longer.Length)
        {
            if (longer[j] != longer[j - 1])
                return false;
            j++;
        }

        return i == shorter.Length;
    }

    /// <summary>
    /// Türkçe metin normalizasyonu — fonetik karşılaştırma için.
    /// Büyük/küçük harf, Türkçe karakter, boşluk temizleme.
    /// </summary>
    private static string NormalizeTurkish(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        return s
            .ToLowerInvariant()
            .Trim()
            .Replace(" ", "")
            .Replace("ı", "i")
            .Replace("ö", "o")
            .Replace("ü", "u")
            .Replace("ç", "c")
            .Replace("ş", "s")
            .Replace("ğ", "g");
    }
}

/// <summary>
/// Telaffuz değerlendirme sonucu.
/// </summary>
public class PronunciationEvaluation
{
    public int Score { get; }
    public double Confidence { get; }
    public string FeedbackKey { get; }

    public PronunciationEvaluation(int score, double confidence, string feedbackKey)
    {
        Score = Math.Clamp(score, 0, 100);
        Confidence = Math.Clamp(confidence, 0.0, 1.0);
        FeedbackKey = feedbackKey;
    }
}
