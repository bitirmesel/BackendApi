namespace DktApi.Dtos.Therapist;

public record UpdateTherapistRequest(
    string? Name,
    string? Email,
    string? PhoneNumber,
    string? LicenseNumber,
    string? ProfileImageUrl,
    string? ClinicName,
    string? City
);
