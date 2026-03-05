using Newtonsoft.Json;

namespace DktApi.Models.Game
{
    [System.Serializable]
    public class FeedbackReq
    {
        // Flutter'dan json.encode({"feedback": "..."}) olarak gelecek
        [JsonProperty("feedback")]
        public string Feedback { get; set; } = string.Empty;

        // Flutter'da SharedPreferences'ta tuttuğumuz terapist ID'si
        [JsonProperty("therapist_id")]
        public long TherapistId { get; set; }
    }
}