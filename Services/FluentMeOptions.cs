namespace DktApi.Services;

/// <summary>
/// thefluent.me hesap bilgileri. Değerler koda yazılmaz; ortam değişkenlerinden gelir:
///   FluentMe__User, FluentMe__Password, FluentMe__ApiKey
/// Yerelde: dotnet user-secrets set "FluentMe:User" "..."
/// </summary>
public class FluentMeOptions
{
    public const string SectionName = "FluentMe";

    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(User) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}
