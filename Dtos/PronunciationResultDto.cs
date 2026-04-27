using System.Text.Json.Serialization;

namespace DktApi.Dtos;

/// <summary>
/// Flutter'a döndürülecek zenginleştirilmiş telaffuz sonucu.
/// FluentMe ham skoruna ek olarak kendi değerlendirme katmanımızı içerir.
/// </summary>
public class PronunciationResultDto
{
    /// <summary>Hedef metin (Flutter'dan gelen orijinal text)</summary>
    [JsonPropertyName("target_text")]
    public string TargetText { get; set; } = string.Empty;

    /// <summary>ASR'nin algıladığı transkript</summary>
    [JsonPropertyName("transcript")]
    public string? Transcript { get; set; }

    /// <summary>FluentMe API'nin verdiği ham skor (0-100)</summary>
    [JsonPropertyName("api_score")]
    public double ApiScore { get; set; }

    /// <summary>Bizim hesapladığımız nihai skor (0-100)</summary>
    [JsonPropertyName("score")]
    public int Score { get; set; }

    /// <summary>Skor >= 70 ise true</summary>
    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    /// <summary>Değerlendirme güven oranı (0.0 - 1.0)</summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Eşleşme tipi:
    /// exact_match, length_variant, contains, mismatch_short, api_score, no_audio
    /// Flutter bu key'e göre kullanıcıya mesaj gösterir.
    /// </summary>
    [JsonPropertyName("feedback_key")]
    public string FeedbackKey { get; set; } = string.Empty;

    /// <summary>FluentMe'nin oluşturduğu yapay okuma sesi URL'si</summary>
    [JsonPropertyName("ai_reading_url")]
    public string? AiReadingUrl { get; set; }

    /// <summary>Kayıt süresi (saniye)</summary>
    [JsonPropertyName("recording_duration_sec")]
    public double RecordingDurationSec { get; set; }

    /// <summary>İstek süreleri (ms cinsinden)</summary>
    [JsonPropertyName("timing")]
    public PronunciationTimingDto? Timing { get; set; }
}

public class PronunciationTimingDto
{
    [JsonPropertyName("token_ms")]
    public long TokenMs { get; set; }

    [JsonPropertyName("post_ms")]
    public long PostMs { get; set; }

    [JsonPropertyName("decode_ms")]
    public long DecodeMs { get; set; }

    [JsonPropertyName("upload_ms")]
    public long UploadMs { get; set; }

    [JsonPropertyName("inference_ms")]
    public long InferenceMs { get; set; }

    [JsonPropertyName("total_ms")]
    public long TotalMs { get; set; }
}
