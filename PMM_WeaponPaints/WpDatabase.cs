using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace PMM_WeaponPaints;

/// <summary>What one team (T or CT) of a player has equipped in WeaponPaints.</summary>
internal sealed class TeamData
{
    /// <summary>Knife display name ("Karambit"), null = Default Knife.</summary>
    public string? Knife { get; set; }

    /// <summary>weapon defindex -> paint id (guns, knives and gloves).</summary>
    public Dictionary<int, int> Paints { get; } = new();

    public int GloveDef { get; set; }
    public int Music { get; set; }
    public int Pin { get; set; }

    /// <summary>Agent model as WeaponPaints stores it ("tm_leet/tm_leet_variantg"), null = default.</summary>
    public string? Agent { get; set; }

    public int PaintOf(int def) => Paints.TryGetValue(def, out int paint) ? paint : 0;
}

internal sealed class WpDatabase
{
    public const int TeamT = 0;
    public const int TeamCt = 1;

    public static readonly Dictionary<string, int> DefByClass = new(StringComparer.OrdinalIgnoreCase)
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
        ["weapon_knife"] = 42, ["weapon_knife_t"] = 59,
        ["weapon_bayonet"] = 500, ["weapon_knife_css"] = 503, ["weapon_knife_flip"] = 505,
        ["weapon_knife_gut"] = 506, ["weapon_knife_karambit"] = 507, ["weapon_knife_m9_bayonet"] = 508,
        ["weapon_knife_tactical"] = 509, ["weapon_knife_falchion"] = 512, ["weapon_knife_survival_bowie"] = 514,
        ["weapon_knife_butterfly"] = 515, ["weapon_knife_push"] = 516, ["weapon_knife_cord"] = 517,
        ["weapon_knife_canis"] = 518, ["weapon_knife_ursus"] = 519, ["weapon_knife_gypsy_jackknife"] = 520,
        ["weapon_knife_outdoor"] = 521, ["weapon_knife_stiletto"] = 522, ["weapon_knife_widowmaker"] = 523,
        ["weapon_knife_skeleton"] = 525, ["weapon_knife_kukri"] = 526
    };

    public static readonly Dictionary<int, string> NameByDef = new()
    {
        [1] = "Desert Eagle", [2] = "Dual Berettas", [3] = "Five-SeveN", [4] = "Glock-18",
        [7] = "AK-47", [8] = "AUG", [9] = "AWP", [10] = "FAMAS", [11] = "G3SG1", [13] = "Galil AR",
        [14] = "M249", [16] = "M4A4", [17] = "MAC-10", [19] = "P90", [23] = "MP5-SD", [24] = "UMP-45",
        [25] = "XM1014", [26] = "PP-Bizon", [27] = "MAG-7", [28] = "Negev", [29] = "Sawed-Off",
        [30] = "Tec-9", [31] = "Zeus x27", [32] = "P2000", [33] = "MP7", [34] = "MP9", [35] = "Nova",
        [36] = "P250", [38] = "SCAR-20", [39] = "SG 553", [40] = "SSG 08", [60] = "M4A1-S",
        [61] = "USP-S", [63] = "CZ75-Auto", [64] = "R8 Revolver",
        [42] = "Default Knife", [59] = "Default Knife",
        [500] = "Bayonet", [503] = "Classic Knife", [505] = "Flip Knife", [506] = "Gut Knife",
        [507] = "Karambit", [508] = "M9 Bayonet", [509] = "Huntsman Knife", [512] = "Falchion Knife",
        [514] = "Bowie Knife", [515] = "Butterfly Knife", [516] = "Shadow Daggers", [517] = "Paracord Knife",
        [518] = "Survival Knife", [519] = "Ursus Knife", [520] = "Navaja Knife", [521] = "Nomad Knife",
        [522] = "Stiletto Knife", [523] = "Talon Knife", [525] = "Skeleton Knife", [526] = "Kukri Knife",
        [4725] = "Broken Fang Gloves", [5027] = "Bloodhound Gloves", [5030] = "Sport Gloves", [5031] = "Driver Gloves",
        [5032] = "Hand Wraps", [5033] = "Moto Gloves", [5034] = "Specialist Gloves", [5035] = "Hydra Gloves"
    };

    private static readonly Dictionary<string, int> DefByName = BuildDefByName();

    private readonly Dictionary<(int Def, int Paint), string> _icons = new();

    private static Dictionary<string, int> BuildDefByName()
    {
        Dictionary<string, int> map = new(StringComparer.OrdinalIgnoreCase);
        foreach ((int def, string name) in NameByDef)
        {
            map.TryAdd(name, def);
        }

        return map;
    }

    public static int DefOf(string? name)
    {
        return name != null && DefByName.TryGetValue(name.Trim(), out int def) ? def : 0;
    }

    /// <summary>Index icons.json keys ("AK-47 | Redline (282)") by (defindex, paint).</summary>
    public void IndexIcons(IReadOnlyDictionary<string, string> icons)
    {
        _icons.Clear();
        foreach (string key in icons.Keys)
        {
            if (!TryPaint(key, out int paint))
            {
                continue;
            }

            int bar = key.IndexOf('|');
            string head = (bar >= 0 ? key[..bar] : key).Replace("★", "").Replace("(", " ").Replace(")", " ").Trim();
            foreach ((int def, string name) in NameByDef)
            {
                if (def == 42 || def == 59)
                {
                    continue;
                }

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

    /// <summary>icons.json key for a weapon/knife/glove finish, or null.</summary>
    public string? IconKey(int def, int paint)
    {
        return paint > 0 && _icons.TryGetValue((def, paint), out string? key) ? key : null;
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

    /// <summary>Reads the loadout of both teams. Index 0 = T (weapon_team 2), 1 = CT (weapon_team 3).</summary>
    public TeamData[] Load(PluginConfig config, string steam, ILogger logger)
    {
        TeamData[] teams = { new(), new() };
        using MySqlConnection connection = Open(config);

        Query(connection, steam, logger, "SELECT weapon_team, weapon_defindex, weapon_paint_id FROM wp_player_skins WHERE steamid = @steam", reader =>
        {
            int team = TeamIndex(reader.GetInt32(0));
            if (team >= 0)
            {
                teams[team].Paints[reader.GetInt32(1)] = reader.GetInt32(2);
            }
        });

        Query(connection, steam, logger, "SELECT weapon_team, knife FROM wp_player_knife WHERE steamid = @steam", reader =>
        {
            int team = TeamIndex(reader.GetInt32(0));
            if (team >= 0 && !reader.IsDBNull(1) && DefByClass.TryGetValue(reader.GetString(1), out int def) && NameByDef.TryGetValue(def, out string? name))
            {
                teams[team].Knife = name;
            }
        });

        Query(connection, steam, logger, "SELECT weapon_team, weapon_defindex FROM wp_player_gloves WHERE steamid = @steam", reader =>
        {
            int team = TeamIndex(reader.GetInt32(0));
            if (team >= 0 && !reader.IsDBNull(1))
            {
                teams[team].GloveDef = reader.GetInt32(1);
            }
        });

        Query(connection, steam, logger, "SELECT weapon_team, music_id FROM wp_player_music WHERE steamid = @steam", reader =>
        {
            int team = TeamIndex(reader.GetInt32(0));
            if (team >= 0 && !reader.IsDBNull(1))
            {
                teams[team].Music = reader.GetInt32(1);
            }
        });

        Query(connection, steam, logger, "SELECT weapon_team, id FROM wp_player_pins WHERE steamid = @steam", reader =>
        {
            int team = TeamIndex(reader.GetInt32(0));
            if (team >= 0 && !reader.IsDBNull(1))
            {
                teams[team].Pin = reader.GetInt32(1);
            }
        });

        Query(connection, steam, logger, "SELECT agent_ct, agent_t FROM wp_player_agents WHERE steamid = @steam LIMIT 1", reader =>
        {
            teams[TeamCt].Agent = CleanAgent(reader.IsDBNull(0) ? null : reader.GetString(0));
            teams[TeamT].Agent = CleanAgent(reader.IsDBNull(1) ? null : reader.GetString(1));
        });

        return teams;
    }

    /// <summary>Writes the agent of one team, the same row WeaponPaints reads on connect.</summary>
    public void SaveAgent(PluginConfig config, string steam, int team, string? model)
    {
        using MySqlConnection connection = Open(config);
        using MySqlCommand command = connection.CreateCommand();
        string column = team == TeamCt ? "agent_ct" : "agent_t";
        command.CommandText = $"INSERT INTO wp_player_agents (steamid, {column}) VALUES (@steam, @model) ON DUPLICATE KEY UPDATE {column} = @model";
        command.Parameters.AddWithValue("@steam", steam);
        command.Parameters.AddWithValue("@model", string.IsNullOrEmpty(model) ? (object)DBNull.Value : model);
        command.ExecuteNonQuery();
    }

    private static string? CleanAgent(string? agent)
    {
        return string.IsNullOrWhiteSpace(agent) || agent.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : agent.Trim();
    }

    private static int TeamIndex(int weaponTeam)
    {
        return weaponTeam switch
        {
            2 => TeamT,
            3 => TeamCt,
            _ => -1
        };
    }

    // One query per table: an older WeaponPaints without wp_player_pins must not break the rest.
    private static void Query(MySqlConnection connection, string steam, ILogger logger, string sql, Action<MySqlDataReader> row)
    {
        try
        {
            using MySqlCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@steam", steam);
            using MySqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                row(reader);
            }
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.NoSuchTable)
        {
            logger.LogDebug("PMM_WeaponPaints: {Message}", ex.Message);
        }
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
