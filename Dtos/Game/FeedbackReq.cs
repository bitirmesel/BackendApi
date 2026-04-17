using System.Text.Json.Serialization;

namespace DktApi.Models.Game
{
    public class FeedbackReq
    {
        [JsonPropertyName("feedback")]
        public string Feedback { get; set; } = string.Empty;

        [JsonPropertyName("therapistId")]
        public long TherapistId { get; set; }
    }
}