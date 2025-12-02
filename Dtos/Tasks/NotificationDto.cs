namespace DktApi.Dtos.Tasks
{
    public class NotificationDto
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty; // string, Flutter direkt gösteriyor
        public string Type { get; set; } = "info";       // success | warning | info | ...
        public bool Unread { get; set; } = true;
    }
}
