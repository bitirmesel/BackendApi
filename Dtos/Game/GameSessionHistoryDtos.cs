namespace DktApi.Dtos.Game;

public class GameSessionHistoryItemDto
{
    public long Id { get; set; }
    public int OrderNo { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public int? Score { get; set; }
    public bool? IsCorrect { get; set; }
}

public class GameSessionFeedbackDto
{
    public long Id { get; set; }
    public long TherapistId { get; set; }
    public string TherapistName { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class GameSessionHistoryListDto
{
    public long Id { get; set; }
    public long PlayerId { get; set; }
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public long LetterId { get; set; }
    public string LetterCode { get; set; } = string.Empty;
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public int? DurationSec { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int ItemCount { get; set; }
    public int CorrectItemCount { get; set; }
    public string? LatestFeedback { get; set; }
}

public class GameSessionDetailDto
{
    public long Id { get; set; }
    public long PlayerId { get; set; }
    public long GameId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public long LetterId { get; set; }
    public string LetterCode { get; set; } = string.Empty;
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public int? DurationSec { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public List<GameSessionHistoryItemDto> Items { get; set; } = new();
    public List<GameSessionFeedbackDto> Feedbacks { get; set; } = new();
}