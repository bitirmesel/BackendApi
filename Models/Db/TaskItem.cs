namespace DktApi.Models.Db;

public class TaskItem
{
    public int Id { get; set; }

    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public string Title { get; set; } = string.Empty;

    // Flutter "dd.MM.yyyy" formatında string gönderiyor, string saklıyoruz
    public string Date { get; set; } = string.Empty;

    // "Tamamlandı" / "Devam Ediyor" / "Bekliyor"
    public string Status { get; set; } = "Bekliyor";
}
