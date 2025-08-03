namespace OneShelf.OneDragon.Database.Model;

public class AiParameters
{
    public int Id { get; init; }

    public required string SystemMessage { get; set; }

    public required string GptVersion { get; set; }

    public required int DalleVersion { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public required string LyriaModel { get; set; }

    public required string SoraModel { get; set; }

    public required string VeoModel { get; set; }
}