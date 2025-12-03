namespace DktApi.Dtos;

public class CreateAssetSetRequest
{
    public int GameId { get; set; }
    public int LetterId { get; set; }
    // Buraya direkt JSON formatında string yapıştıracağız
    public string JsonData { get; set; } 
}