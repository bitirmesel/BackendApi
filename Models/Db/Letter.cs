namespace DktApi.Models.Db;

public class Letter
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;   // A, B, C...
    public string Description { get; set; } = string.Empty;
}
