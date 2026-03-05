using Newtonsoft.Json;
namespace DktApi.Dtos.Game;

public class FeedbackResponseDto
{
    public string Comment { get; set; } = string.Empty;
    public string TargetWord { get; set; } = string.Empty;
    public int Score { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TherapistName { get; set; } = string.Empty;
}