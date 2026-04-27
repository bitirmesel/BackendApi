namespace DktApi.Dtos.Therapist;

public record ChangePasswordRequest(string OldPassword, string NewPassword);
