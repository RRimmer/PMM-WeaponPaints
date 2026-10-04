using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace PMM_WeaponPaints;

internal sealed class WpLoadout
{
    public string? KnifeName { get; set; }
    public string? KnifePlain { get; set; }
    public string? KnifeSkin { get; set; }
    public Dictionary<string, string> WeaponSkins { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string?[] Side { get; } = new string?[11];
    public string?[] Art { get; } = new string?[11];
}

internal sealed class WpDatabase
{
    private const int CatGloves = 2;
    private const int CatPistols = 3;
    private const int CatRifles = 4;
    private const int CatSnipers = 5;
    private const int CatSmg = 6;
    private const int CatHeavy = 7;
    private const int CatAgents = 8;
    private const int CatMusic = 9;

    private static readonly Dictionary<string, int> DefByClass = new(StringComparer.OrdinalIgnoreCase)
    {
        ["weapon_deagle"] = 1, ["weapon_elite"] = 2, ["weapon_fiveseven"] = 3, ["weapon_glock"] = 4,
        ["weapon_ak47"] = 7, ["weapon_aug"] = 8, ["weapon_awp"] = 9, ["weapon_famas"] = 10,
        ["weapon_g3sg1"] = 11, ["weapon_galilar"] = 13, ["weapon_m249"] = 14, ["weapon_m4a1"] = 16,
        ["weapon_mac10"] = 17, ["weapon_p90"] = 19, ["weapon_mp5sd"] = 23, ["weapon_ump45"] = 24,
        ["weapon_xm1014"] = 25, ["weapon_bizon"] = 26, ["weapon_mag7"] = 27, ["weapon_negev"] = 28,
        ["weapon_sawedoff"] = 29, ["weapon_tec9"] = 30, ["weapon_taser"] = 31, ["weapon_hkp2000"] = 32,
        ["weapon_mp7"] = 33, ["weapon_mp9"] = 34, ["weapon_nova"] = 35, ["weapon_p250"] = 36,
        ["weapon_scar20"] = 38, ["weapon_sg556"] = 39, ["weapon_ssg08"] = 40, ["weapon_m4a1_silencer"] = 60,
        ["weapon_usp_silencer"] = 61, ["weapon_cz75a"] = 63, ["weapon_revolver"] = 64,
        ["weapon_bayonet"] = 500, ["weapon_knife_css"] = 503, ["weapon_knife_flip"] = 505,
        ["weapon_knife_gut"] = 506, ["weapon_knife_karambit"] = 507, ["weapon_knife_m9_bayonet"] = 508,
        ["weapon_knife_tactical"] = 509, ["weapon_knife_falchion"] = 512, ["weapon_knife_survival_bowie"] = 514,
        ["weapon_knife_butterfly"] = 515, ["weapon_knife_push"] = 516, ["weapon_knife_cord"] = 517,
        ["weapon_knife_canis"] = 518, ["weapon_knife_ursus"] = 519, ["weapon_knife_gypsy_jackknife"] = 520,
        ["weapon_knife_outdoor"] = 521, ["weapon_knife_stiletto"] = 522, ["weapon_knife_widowmaker"] = 523,
        ["weapon_knife_skeleton"] = 525, ["weapon_knife_kukri"] = 526
    };

    private static readonly Dictionary<int, string> NameByDef = new()
    {
        [1] = "Desert Eagle", [2] = "Dual Berettas", [3] = "Five-SeveN", [4] = "Glock-18",
        [7] = "AK-47", [8] = "AUG", [9] = "AWP", [10] = "FAMAS", [11] = "G3SG1", [13] = "Galil AR",
        [14] = "M249", [16] = "M4A4", [17] = "MAC-10", [19] = "P90", [23] = "MP5-SD", [24] = "UMP-45",
        [25] = "XM1014", [26] = "PP-Bizon", [27] = "MAG-7", [28] = "Negev", [29] = "Sawed-Off",
        [30] = "Tec-9", [31] = "Zeus x27", [32] = "P2000", [33] = "MP7", [34] = "MP9", [35] = "Nova",
        [36] = "P250", [38] = "SCAR-20", [39] = "SG 553", [40] = "SSG 08", [60] = "M4A1-S",
        [61] = "USP-S", [63] = "CZ75-Auto", [64] = "R8 Revolver",
        [500] = "Bayonet", [503] = "Classic Knife", [505] = "Flip Knife", [506] = "Gut Knife",
        [507] = "Karambit", [508] = "M9 Bayonet", [509] = "Huntsman Knife", [512] = "Falchion Knife",
        [514] = "Bowie Knife", [515] = "Butterfly Knife", [516] = "Shadow Daggers", [517] = "Paracord Knife",
        [518] = "Survival Knife", [519] = "Ursus Knife", [520] = "Navaja Knife", [521] = "Nomad Knife",
        [522] = "Stiletto Knife", [523] = "Talon Knife", [525] = "Skeleton Knife", [526] = "Kukri Knife",
        [5027] = "Bloodhound Gloves", [5030] = "Sport Gloves", [5031] = "Driver Gloves",
        [5032] = "Hand Wraps", [5033] = "Moto Gloves", [5034] = "Specialist Gloves", [5035] = "Hydra Gloves"
    };

    private static readonly Dictionary<int, int> Preferred = new()
    {
        [CatPistols] = 1, [CatRifles] = 7, [CatSnipers] = 9, [CatSmg] = 17, [CatHeavy] = 35
    };

    private readonly Dictionary<(int Def, int Paint), string> _icons = new();
    private readonly Dictionary<string, string> _iconClass = new(StringComparer.Ordinal);

    public void IndexIcons(IReadOnlyDictionary<string, string> icons)
    {
        _icons.Clear();
        _iconClass.Clear();
        foreach ((string key, string cls) in icons)
        {
            _iconClass[key] = cls;
            if (!TryPaint(key, out int paint))
            {
                continue;
            }

            foreach ((int def, string name) in NameByDef)
            {
                int bar = key.IndexOf('|');
                string head = (bar >= 0 ? key[..bar] : key).Replace("★", "").Trim();
                if (head.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    if (!_icons.TryGetValue((def, paint), out string? current) || key.Length < current.Length)
                    {
                        _icons[(def, paint)] = key;
                    }
                }
            }
        }
    }

    public bool TryConnect(PluginConfig config, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(config.DatabaseHost) || string.IsNullOrWhiteSpace(config.DatabaseName))
        {
            logger.LogError("PMM_WeaponPaints: не задана база WeaponPaints. Заполните DatabaseHost и DatabaseName в PMM_WeaponPaints.json.");
            return false;
        }

        try
        {
            using MySqlConnection connection = Open(config);
            using MySqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.ExecuteScalar();
            logger.LogInformation("PMM_WeaponPaints: соединение с базой WeaponPaints {Name} установлено.", config.DatabaseName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("PMM_WeaponPaints: нет соединения с базой WeaponPaints ({Host}/{Name}): {Message}", config.DatabaseHost, config.DatabaseName, ex.Message);
            return false;
        }
    }

    public WpLoadout Load(PluginConfig config, string steam, int team)
    {
        WpLoadout loadout = new();
        Dictionary<int, int> paints = new();
        string? knifeClass = null;
        int gloveDef = 0;
        int musicId = 0;
        string? agent = null;

        using MySqlConnection connection = Open(config);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT weapon_defindex, weapon_paint_id FROM wp_player_skins WHERE steamid = @steam AND weapon_team = @team;
            SELECT knife FROM wp_player_knife WHERE steamid = @steam AND weapon_team = @team LIMIT 1;
            SELECT weapon_defindex FROM wp_player_gloves WHERE steamid = @steam AND weapon_team = @team LIMIT 1;
            SELECT music_id FROM wp_player_music WHERE steamid = @steam AND weapon_team = @team LIMIT 1;
            SELECT agent_ct, agent_t FROM wp_player_agents WHERE steamid = @steam LIMIT 1;
            """;
        command.Parameters.AddWithValue("@steam", steam);
        command.Parameters.AddWithValue("@team", team);
        using (MySqlDataReader reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                paints[reader.GetInt32(0)] = reader.GetInt32(1);
            }

            if (reader.NextResult() && reader.Read() && !reader.IsDBNull(0))
            {
                knifeClass = reader.GetString(0);
            }

            if (reader.NextResult() && reader.Read() && !reader.IsDBNull(0))
            {
                gloveDef = reader.GetInt32(0);
            }

            if (reader.NextResult() && reader.Read() && !reader.IsDBNull(0))
            {
                musicId = reader.GetInt32(0);
            }

            if (reader.NextResult() && reader.Read())
            {
                int column = team == 3 ? 0 : 1;
                if (!reader.IsDBNull(column))
                {
                    agent = reader.GetString(column);
                }
            }
        }

        Dictionary<int, (int Def, string Key, string Label)> best = new();
        foreach ((int def, int paint) in paints)
        {
            if (paint <= 0 || !NameByDef.TryGetValue(def, out string? weapon))
            {
                continue;
            }

            string key = _icons.TryGetValue((def, paint), out string? found) ? found : weapon + " | #" + paint;
            loadout.WeaponSkins[weapon] = key;
            int cat = CategoryOf(def);
            if (cat < 0)
            {
                continue;
            }

            string label = LabelOf(weapon, key);
            if (!best.TryGetValue(cat, out (int Def, string Key, string Label) current) || Better(cat, def, current.Def))
            {
                best[cat] = (def, key, label);
            }
        }

        foreach ((int cat, (int _, string key, string label)) in best)
        {
            loadout.Side[cat] = label;
            if (_iconClass.TryGetValue(key, out string? art))
            {
                loadout.Art[cat] = art;
            }
        }

        if (!string.IsNullOrEmpty(knifeClass) && DefByClass.TryGetValue(knifeClass, out int knifeDef) && NameByDef.TryGetValue(knifeDef, out string? knifeName))
        {
            loadout.KnifeName = knifeName;
            if (paints.TryGetValue(knifeDef, out int knifePaint) && knifePaint > 0)
            {
                string key = _icons.TryGetValue((knifeDef, knifePaint), out string? found) ? found : knifeName + " | #" + knifePaint;
                loadout.KnifePlain = key;
                loadout.KnifeSkin = FinishOf(key);
            }
        }

        if (gloveDef > 0 && NameByDef.TryGetValue(gloveDef, out string? gloveName))
        {
            string label = gloveName;
            string? key = null;
            if (paints.TryGetValue(gloveDef, out int glovePaint) && glovePaint > 0 && _icons.TryGetValue((gloveDef, glovePaint), out string? found))
            {
                key = found;
                label = LabelOf(gloveName, found);
            }

            loadout.Side[CatGloves] = label;
            if (key != null && _iconClass.TryGetValue(key, out string? art))
            {
                loadout.Art[CatGloves] = art;
            }
        }

        if (musicId > 0)
        {
            loadout.Side[CatMusic] = "Kit " + musicId;
        }

        if (!string.IsNullOrWhiteSpace(agent) && !agent.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            int slash = agent.LastIndexOf('/');
            loadout.Side[CatAgents] = slash >= 0 ? agent[(slash + 1)..] : agent;
        }

        return loadout;
    }

    private static bool Better(int cat, int candidate, int current)
    {
        if (Preferred.TryGetValue(cat, out int preferred))
        {
            if (candidate == preferred)
            {
                return true;
            }

            if (current == preferred)
            {
                return false;
            }
        }

        return candidate < current;
    }

    private static int CategoryOf(int def)
    {
        return def switch
        {
            1 or 2 or 3 or 4 or 30 or 31 or 32 or 36 or 61 or 63 or 64 => CatPistols,
            7 or 8 or 10 or 13 or 16 or 39 or 60 => CatRifles,
            9 or 11 or 38 or 40 => CatSnipers,
            17 or 19 or 23 or 24 or 26 or 33 or 34 => CatSmg,
            14 or 25 or 27 or 28 or 29 or 35 => CatHeavy,
            _ => -1
        };
    }

    private static string LabelOf(string weapon, string key)
    {
        string finish = FinishOf(key);
        return finish.Length == 0 ? weapon : weapon + "  |  " + finish;
    }

    private static string FinishOf(string key)
    {
        int bar = key.LastIndexOf('|');
        string finish = bar >= 0 ? key[(bar + 1)..].Trim() : key;
        int paren = finish.LastIndexOf('(');
        if (paren > 0 && finish.EndsWith(')'))
        {
            finish = finish[..paren].Trim();
        }

        return finish.StartsWith('#') ? "" : finish;
    }

    private static bool TryPaint(string key, out int paint)
    {
        paint = 0;
        Match match = Regex.Match(key, @"\((\d+)\)\s*$");
        return match.Success && int.TryParse(match.Groups[1].Value, out paint);
    }

    private static MySqlConnection Open(PluginConfig config)
    {
        string cs = $"Server={config.DatabaseHost};Port={config.DatabasePort};User ID={config.DatabaseUser};Password={config.DatabasePassword};Database={config.DatabaseName};SslMode=None;Connection Timeout=3";
        MySqlConnection connection = new(cs);
        connection.Open();
        return connection;
    }
}
