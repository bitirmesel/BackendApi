namespace DktApi.Models.Db;

public class Notification
{
    public int Id { get; set; }

    public int TherapistId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    // "success" / "warning" / "info" / "person" / "report"
    public string Type { get; set; } = "info";

    public string Time { get; set; } = string.Empty; // "3 dk önce" gibi, string tutuyoruz
    public bool Unread { get; set; } = true;
}
