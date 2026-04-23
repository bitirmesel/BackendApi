using System.Text.Json.Serialization;

namespace DktApi.Models.FluentMe;

/// <summary>
/// FluentMe API'nin /score endpoint'inden dönen JSON array'ini parse etmek için kullanılır.
/// Array 3 elemanlıdır: [provided_data, overall_result_data, word_result_data]
/// </summary>

// --- 1. Eleman: provided_data ---
public class FluentMeProvidedDataWrapper
{
    [JsonPropertyName("provided_data")]
    public List<FluentMeProvidedData>? ProvidedData { get; set; }
}

public class FluentMeProvidedData
{
    [JsonPropertyName("audio_provided")]
    public string? AudioProvided { get; set; }

    [JsonPropertyName("post_provided")]
    public string? PostProvided { get; set; }
}

// --- 2. Eleman: overall_result_data ---
public class FluentMeOverallResultWrapper
{
    [JsonPropertyName("overall_result_data")]
    public List<FluentMeOverallResult>? OverallResultData { get; set; }
}

public class FluentMeOverallResult
{
    [JsonPropertyName("ai_reading")]
    public string? AiReading { get; set; }

    [JsonPropertyName("length_of_recording_in_sec")]
    public double LengthOfRecordingInSec { get; set; }

    [JsonPropertyName("number_of_recognized_words")]
    public int NumberOfRecognizedWords { get; set; }

    [JsonPropertyName("number_of_words_in_post")]
    public int NumberOfWordsInPost { get; set; }

    [JsonPropertyName("overall_points")]
    public double OverallPoints { get; set; }

    [JsonPropertyName("post_language_id")]
    public int PostLanguageId { get; set; }

    [JsonPropertyName("post_language_name")]
    public string? PostLanguageName { get; set; }

    [JsonPropertyName("score_id")]
    public string? ScoreId { get; set; }

    [JsonPropertyName("user_recording_transcript")]
    public string? UserRecordingTranscript { get; set; }
}

// --- 3. Eleman: word_result_data ---
public class FluentMeWordResultWrapper
{
    [JsonPropertyName("word_result_data")]
    public List<FluentMeWordResult>? WordResultData { get; set; }
}

public class FluentMeWordResult
{
    [JsonPropertyName("points")]
    public string? Points { get; set; }

    [JsonPropertyName("speed")]
    public string? Speed { get; set; }

    [JsonPropertyName("word")]
    public string? Word { get; set; }
}
