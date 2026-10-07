using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PMM_WeaponPaints;

internal sealed class AgentEntry
{
    [JsonPropertyName("def")] public int Def { get; set; }
    [JsonPropertyName("team")] public int Team { get; set; }
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("en")] public string? En { get; set; }
    [JsonPropertyName("ru")] public string? Ru { get; set; }
    [JsonPropertyName("rar")] public int Rarity { get; set; }
    [JsonPropertyName("art")] public string? Art { get; set; }
    [JsonPropertyName("names")] public List<string>? Names { get; set; }
}

internal sealed class NamedEntry
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("en")] public string? En { get; set; }
    [JsonPropertyName("ru")] public string? Ru { get; set; }
    [JsonPropertyName("rar")] public int Rarity { get; set; } = 3;
    [JsonPropertyName("art")] public string? Art { get; set; }
    [JsonPropertyName("names")] public List<string>? Names { get; set; }
}

internal sealed class GloveEntry
{
    [JsonPropertyName("def")] public int Def { get; set; }
    [JsonPropertyName("paint")] public int Paint { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

internal sealed class GloveType
{
    [JsonPropertyName("def")] public int Def { get; set; }
    [JsonPropertyName("en")] public string? En { get; set; }
    [JsonPropertyName("ru")] public string? Ru { get; set; }
    [JsonPropertyName("art")] public string? Art { get; set; }
}

internal sealed class ItemFile
{
    [JsonPropertyName("skinRarity")] public Dictionary<string, int>? SkinRarity { get; set; }
    [JsonPropertyName("paints")] public Dictionary<string, string[]>? Paints { get; set; }
    [JsonPropertyName("agents")] public List<AgentEntry>? Agents { get; set; }
    [JsonPropertyName("music")] public List<NamedEntry>? Music { get; set; }
    [JsonPropertyName("pins")] public List<NamedEntry>? Pins { get; set; }
    [JsonPropertyName("gloves")] public List<GloveEntry>? Gloves { get; set; }
    [JsonPropertyName("gloveTypes")] public List<GloveType>? GloveTypes { get; set; }
}

/// <summary>
/// Item tables built from the CS2 item schema (pmm_items.json next to the DLL), plus the names
/// WeaponPaints puts into its menus (read from WeaponPaints/data at load, any language).
/// </summary>
internal sealed class ItemData
{
    private Dictionary<long, int> _skinRarity = new();
    private Dictionary<int, string[]> _paints = new();
    private readonly Dictionary<string, (int Def, int Paint)> _gloveByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(int Def, int Paint), string> _gloveName = new();
    private readonly Dictionary<string, int> _musicByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _pinByName = new(StringComparer.OrdinalIgnoreCase);

    public List<AgentEntry> Agents { get; private set; } = new();
    public Dictionary<int, NamedEntry> Music { get; private set; } = new();
    public Dictionary<int, NamedEntry> Pins { get; private set; } = new();
    public List<GloveType> GloveTypes { get; private set; } = new();

    public void Load(string moduleDirectory, ILogger logger)
    {
        string path = Path.Combine(moduleDirectory, "pmm_items.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("pmm_items.json was not found next to PMM_WeaponPaints.dll: no rarity, agents, music and pin images.");
            return;
        }

        try
        {
            ItemFile? file = JsonSerializer.Deserialize<ItemFile>(File.ReadAllText(path));
            if (file == null)
            {
                return;
            }

            _skinRarity = new Dictionary<long, int>();
            foreach ((string key, int rarity) in file.SkinRarity ?? new())
            {
                string[] parts = key.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int def) && int.TryParse(parts[1], out int paint))
                {
                    _skinRarity[Key(def, paint)] = rarity;
                }
            }

            _paints = new Dictionary<int, string[]>();
            foreach ((string key, string[] names) in file.Paints ?? new())
            {
                if (int.TryParse(key, out int paint) && names.Length > 0)
                {
                    _paints[paint] = names;
                }
            }

            Agents = file.Agents ?? new();
            Music = (file.Music ?? new()).GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());
            Pins = (file.Pins ?? new()).GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());
            GloveTypes = file.GloveTypes ?? new();

            foreach (GloveEntry glove in file.Gloves ?? new())
            {
                AddGlove(glove.Name, glove.Def, glove.Paint);
            }

            foreach (NamedEntry music in Music.Values)
            {
                AddNames(_musicByName, music);
            }

            foreach (NamedEntry pin in Pins.Values)
            {
                AddNames(_pinByName, pin);
            }

            logger.LogInformation("pmm_items.json: {Skins} skin rarities, {Agents} agents, {Music} music kits, {Pins} pins.", _skinRarity.Count, Agents.Count, Music.Count, Pins.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning("pmm_items.json could not be read: {Message}", ex.Message);
        }
    }

    /// <summary>Adds the names WeaponPaints itself shows, whatever SkinsLanguage it runs with.</summary>
    public void LoadWeaponPaintsNames(string moduleDirectory, ILogger logger)
    {
        string data = Path.GetFullPath(Path.Combine(moduleDirectory, "..", "WeaponPaints", "data"));
        if (!Directory.Exists(data))
        {
            logger.LogInformation("WeaponPaints data folder not found at {Path}; using the bundled names.", data);
            return;
        }

        int read = 0;
        foreach (string file in Directory.GetFiles(data, "*.json"))
        {
            string name = Path.GetFileName(file);
            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                if (name.StartsWith("gloves_", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (JsonElement e in doc.RootElement.EnumerateArray())
                    {
                        AddGlove(Str(e, "paint_name"), Int(e, "weapon_defindex"), Int(e, "paint"));
                    }
                }
                else if (name.StartsWith("music_", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (JsonElement e in doc.RootElement.EnumerateArray())
                    {
                        string text = Norm(Str(e, "name"));
                        if (text.Length > 0)
                        {
                            _musicByName.TryAdd(text, Int(e, "id"));
                        }
                    }
                }
                else if (name.StartsWith("collectibles_", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (JsonElement e in doc.RootElement.EnumerateArray())
                    {
                        string text = Norm(Str(e, "name"));
                        if (text.Length > 0)
                        {
                            _pinByName.TryAdd(text, Int(e, "id"));
                        }
                    }
                }
                else
                {
                    continue;
                }

                read++;
            }
            catch (Exception ex)
            {
                logger.LogWarning("{File} could not be read: {Message}", name, ex.Message);
            }
        }

        logger.LogInformation("WeaponPaints data: {Count} name files read from {Path}.", read, data);
    }

    public int SkinRarity(int def, int paint)
    {
        return _skinRarity.TryGetValue(Key(def, paint), out int rarity) ? rarity : 0;
    }

    public string? PaintName(int paint, bool russian)
    {
        if (!_paints.TryGetValue(paint, out string[]? names))
        {
            return null;
        }

        return russian && names.Length > 1 && !string.IsNullOrEmpty(names[1]) ? names[1] : names[0];
    }

    public bool TryGlove(string text, out int def, out int paint)
    {
        if (_gloveByName.TryGetValue(Norm(text), out (int Def, int Paint) found))
        {
            def = found.Def;
            paint = found.Paint;
            return true;
        }

        def = 0;
        paint = -1;
        return false;
    }

    public string? GloveName(int def, int paint)
    {
        return _gloveName.TryGetValue((def, paint), out string? name) ? name : null;
    }

    public int MusicId(string text) => _musicByName.TryGetValue(Norm(text), out int id) ? id : -1;

    public int PinId(string text) => _pinByName.TryGetValue(Norm(text), out int id) ? id : -1;

    public AgentEntry? Agent(string? model)
    {
        if (string.IsNullOrEmpty(model))
        {
            return null;
        }

        return Agents.FirstOrDefault(a => a.Model.Equals(model, StringComparison.OrdinalIgnoreCase));
    }

    public GloveType? FindGloveType(int def) => GloveTypes.FirstOrDefault(t => t.Def == def);

    public static string Norm(string? text)
    {
        return Regex.Replace(text ?? "", @"\s+", " ").Trim();
    }

    private void AddGlove(string name, int def, int paint)
    {
        string text = Norm(name);
        if (text.Length == 0)
        {
            return;
        }

        _gloveByName.TryAdd(text, (def, paint));
        _gloveName.TryAdd((def, paint), name);
    }

    private static void AddNames(Dictionary<string, int> map, NamedEntry entry)
    {
        foreach (string name in (entry.Names ?? new()).Append(entry.En ?? "").Append(entry.Ru ?? ""))
        {
            string text = Norm(name);
            if (text.Length > 0)
            {
                map.TryAdd(text, entry.Id);
            }
        }
    }

    private static long Key(int def, int paint) => ((long)def << 32) | (uint)paint;

    private static string Str(JsonElement e, string name)
    {
        return e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    }

    private static int Int(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out JsonElement v))
        {
            return 0;
        }

        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n))
        {
            return n;
        }

        return v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out int s) ? s : 0;
    }
}
