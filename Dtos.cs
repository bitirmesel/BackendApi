namespace DktApi;

public record AddNoteRequest(string Text);
public record AddBadgeRequest(string Title, string Icon);
public record CreateTaskRequest(int StudentId, string Title, string Date, string Status);

public record LoginRequestDto(string Email, string Password);
public record LoginResponseDto(string Token, int TherapistId, string Name);

public record StudentStatsDto(
    int ProgressPercentage,
    int CompletedTasks,
    int BadgeCount,
    List<int> WeeklyProgress,
    Dictionary<string, double> Skills
);
