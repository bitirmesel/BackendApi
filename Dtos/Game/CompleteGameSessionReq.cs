namespace DktApi.Dtos.Game;

public class CompleteGameSessionReq
{
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public int? DurationSec { get; set; }
    public List<CompleteGameSessionItemReq> Items { get; set; } = new();
}

public class CompleteGameSessionItemReq
{
    public int OrderNo { get; set; }
    public string ItemType { get; set; } = "WORD";
    public string PromptText { get; set; } = string.Empty;
    public int? Score { get; set; }
    public bool? IsCorrect { get; set; }
}