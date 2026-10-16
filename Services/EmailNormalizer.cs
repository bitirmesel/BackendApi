namespace DktApi.Services;

/// <summary>
/// E-posta adresleri küçük harfle saklanır ve büyük/küçük harfe duyarsız karşılaştırılır (B23).
/// Karşılaştırmada DB tarafı da <c>ToLower()</c> ile (PostgreSQL <c>lower()</c>) indirgenir;
/// böylece bu değişiklikten önce karışık harfle yazılmış kayıtlar da bulunur.
/// </summary>
public static class EmailNormalizer
{
    /// <summary>
    /// Boşlukları kırpar, küçük harfe çevirir. <c>ToLowerInvariant</c> bilerek:
    /// Türkçe kültürde "I" → "ı" olur ve e-posta bozulur.
    /// </summary>
    public static string? Normalize(string? email) => email?.Trim().ToLowerInvariant();
}
