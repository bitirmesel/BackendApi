namespace DktApi.Models.Db;

public class Note
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public string Text { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty; // "dd.MM.yyyy HH:mm"
}
