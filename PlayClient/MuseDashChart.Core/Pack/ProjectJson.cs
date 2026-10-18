using System.Text.Json;
using System.Text.Json.Serialization;

namespace MuseDashChart;

public static class ProjectJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ChartDocument Load(string path)
        => JsonSerializer.Deserialize<ChartDocument>(File.ReadAllText(path), Options)
           ?? throw new InvalidDataException("Invalid chart JSON.");

    public static void Save(ChartDocument document, string path)
    {
        document.Schema = ChartDocument.SchemaId;
        File.WriteAllText(path, JsonSerializer.Serialize(document, Options));
    }

    public static string Serialize(ChartDocument document)
    {
        document.Schema = ChartDocument.SchemaId;
        return JsonSerializer.Serialize(document, Options);
    }

    public static ChartDocument Deserialize(string json)
        => JsonSerializer.Deserialize<ChartDocument>(json, Options)
           ?? throw new InvalidDataException("Invalid chart JSON.");
}

public static class AlbumInfoJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AlbumInfoFile Load(string path)
        => JsonSerializer.Deserialize<AlbumInfoFile>(File.ReadAllText(path), Options)
           ?? throw new InvalidDataException("Invalid info.json.");

    public static void Save(AlbumInfoFile info, string path)
        => File.WriteAllText(path, JsonSerializer.Serialize(info, Options));
}

