using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using MenuManager;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;

namespace PMM_WeaponPaints;

public class PluginConfig : BasePluginConfig
{
	[JsonPropertyName("Commands")] public List<string> Commands { get; set; } = ["pws"];
	[JsonPropertyName("Language")] public string Language { get; set; } = "en";
	[JsonPropertyName("OpenDelay")] public float OpenDelay { get; set; } = 0.2f;
	[JsonPropertyName("InputDelay")] public float InputDelay { get; set; } = 0.2f;
	[JsonPropertyName("DatabaseHost")] public string DatabaseHost { get; set; } = "";
	[JsonPropertyName("DatabasePort")] public int DatabasePort { get; set; } = 3306;
	[JsonPropertyName("DatabaseUser")] public string DatabaseUser { get; set; } = "";
	[JsonPropertyName("DatabasePassword")] public string DatabasePassword { get; set; } = "";
	[JsonPropertyName("DatabaseName")] public string DatabaseName { get; set; } = "";
}

public partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>, IPmmMenuPainter
{
	private sealed record WeaponGroup(string Key, string Title, string[] Items);

	private sealed class WeaponGroupJson
	{
		public string? Title { get; set; }

		public string[]? Items { get; set; }
	}

	private sealed class TriggerFile
	{
		public int Version { get; set; }

		public Dictionary<string, string>? Buttons { get; set; }

		public Dictionary<string, WeaponGroupJson>? Groups { get; set; }
	}

	/// <summary>One cell of the grid, built fresh on every redraw.</summary>
	private sealed class Card
	{
		public string Name = "";
		public string Sub = "";
		public string? Art;
		public int Rarity;
		public bool Stock;
		public bool EqT;
		public bool EqCt;
		public Action<CCSPlayerController>? Click;
	}

	private const string LayoutPath = "panorama/layout/custom_game/wp.xml";

	private const int ViewHome = 0;
	private const int ViewKnife = 1;
	private const int ViewLive = 2;
	private const int ViewAgents = 3;
	private const int ViewGloves = 4;

	private const int LivePage = 120;
	private const int RowSize = 5;
	private const int PagerButtons = 7;

	private const int CatLoadout = 0;
	private const int CatKnife = 1;
	private const int CatGloves = 2;
	private const int CatPistols = 3;
	private const int CatAgents = 8;
	private const int CatMusic = 9;
	private const int CatPins = 10;
	private const int CatNone = 11;
	private const int CatCount = 12;

	private const int StockKnife = 1;
	private const int CovertRarity = 6;

	private static readonly string[] Views = { "view-home", "view-knife", "view-live", "view-agents", "view-gloves" };

	private static readonly string[] Cats =
	{
		"cat-loadout", "cat-knife", "cat-gloves", "cat-pistols", "cat-rifles", "cat-snipers", "cat-smg", "cat-heavy", "cat-agents", "cat-music",
		"cat-pins", "cat-none"
	};

	// Category index -> panel suffix used in wp.xml (wp-side-X, wp-home-X, wp-sw-X, wp-tile-art-X) and dialog variable side_X.
	private static readonly string[] CatKeys = { "loadout", "knife", "gloves", "pistols", "rifles", "snipers", "smg", "heavy", "agents", "music", "pins" };

	private static readonly string[] KnifeNames =
	{
		"Shadow Daggers", "Default Knife", "Bayonet", "Bowie Knife", "Butterfly Knife", "Classic Knife", "Falchion Knife", "Flip Knife", "Gut Knife", "Huntsman Knife",
		"Karambit", "Kukri Knife", "M9 Bayonet", "Navaja Knife", "Nomad Knife", "Paracord Knife", "Skeleton Knife", "Stiletto Knife", "Survival Knife", "Talon Knife",
		"Ursus Knife"
	};

	private static readonly int[] KnifeFinishes =
	{
		34, 0, 34, 34, 34, 12, 34, 34, 34, 34,
		34, 12, 34, 24, 24, 24, 24, 24, 24, 24,
		24
	};

	private static readonly int[] KnifeBase =
	{
		1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
		11, 12, 13, 14, 15, 0, 16, 17, 18, 19,
		20
	};

	private static readonly string[] Match =
	{
		"Knife Menu", "Меню Ножей", "Weapon Menu", "Меню Оружия", "Gloves Menu", "Меню Перчаток", "Agents Menu", "Меню Агентов", "Music Menu", "Меню Музыки",
		"Pins Menu", "Меню пинов", "Select skin", "Выберите скин"
	};

	private static readonly string[] Colors =
	{
		"Default", "White", "Darkred", "Green", "Lightyellow", "Lightblue", "Olive", "Lime", "Red", "Lightpurple",
		"Purple", "Grey", "Yellow", "Gold", "Silver", "Blue", "Darkblue", "Bluegrey", "Magenta", "Lightred",
		"Orange"
	};

	private static readonly Dictionary<string, string> DefaultTriggers = new(StringComparer.Ordinal)
	{
		["wp-close"] = "close",
		["wp-back"] = "back",
		["wp-cat-loadout"] = "home",
		["wp-cat-knife"] = "open:knife",
		["wp-tile-knife"] = "open:knife",
		["wp-cat-gloves"] = "open:gloves",
		["wp-tile-gloves"] = "open:gloves",
		["wp-cat-pistols"] = "weapons:pistols",
		["wp-tile-pistols"] = "weapons:pistols",
		["wp-cat-rifles"] = "weapons:rifles",
		["wp-tile-rifles"] = "weapons:rifles",
		["wp-cat-snipers"] = "weapons:snipers",
		["wp-tile-snipers"] = "weapons:snipers",
		["wp-cat-smg"] = "weapons:smg",
		["wp-tile-smg"] = "weapons:smg",
		["wp-cat-heavy"] = "weapons:heavy",
		["wp-tile-heavy"] = "weapons:heavy",
		["wp-cat-agents"] = "open:agents",
		["wp-tile-agents"] = "open:agents",
		["wp-cat-music"] = "open:music",
		["wp-tile-music"] = "open:music",
		["wp-cat-pins"] = "open:pins",
		["wp-tile-pins"] = "open:pins",
		["wp-prev"] = "page:prev",
		["wp-next"] = "page:next"
	};

	private static readonly Dictionary<string, WeaponGroup> DefaultGroups = new(StringComparer.OrdinalIgnoreCase)
	{
		["pistols"] = new WeaponGroup("pistols", "PISTOLS", new[]
		{
			"Glock-18", "USP-S", "P2000", "P250", "Dual Berettas", "Five-SeveN", "Tec-9", "CZ75-Auto", "Desert Eagle", "R8 Revolver",
			"Zeus x27"
		}),
		["rifles"] = new WeaponGroup("rifles", "RIFLES", new[] { "AK-47", "M4A4", "M4A1-S", "Galil AR", "FAMAS", "SG 553", "AUG" }),
		["snipers"] = new WeaponGroup("snipers", "SNIPERS", new[] { "AWP", "SSG 08", "SCAR-20", "G3SG1" }),
		["smg"] = new WeaponGroup("smg", "SMG", new[] { "MAC-10", "MP9", "MP7", "MP5-SD", "UMP-45", "P90", "PP-Bizon" }),
		["heavy"] = new WeaponGroup("heavy", "HEAVY", new[] { "Nova", "XM1014", "Sawed-Off", "MAG-7", "M249", "Negev" })
	};

	// Category 3..7 -> group key, and the weapon that represents the group on the home page when several have skins.
	private static readonly string[] CatGroup = { "", "", "", "pistols", "rifles", "snipers", "smg", "heavy" };
	private static readonly string[] CatPreferred = { "", "", "", "Desert Eagle", "AK-47", "AWP", "MAC-10", "Nova" };

	private readonly PluginCapability<IPmmPaintRegistry?> _paint = new("pmm:paint");
	private readonly PluginCapability<IMenuApi?> _menus = new("menu:nfcore");

	private readonly WpDatabase _wpDatabase = new();
	private readonly ItemData _items = new();

	private bool _dbReady;
	private bool _painterAdded;

	private readonly bool[] _open = new bool[64];
	private readonly int[] _token = new int[64];
	private readonly int[] _view = new int[64];
	private readonly int[] _page = new int[64];
	private readonly int[] _pagerStart = new int[64];
	private readonly int[] _teamIdx = new int[64];
	private readonly IMenu?[] _menu = new IMenu?[64];
	private readonly TeamData[]?[] _teams = new TeamData[]?[64];
	private readonly List<Card>?[] _cards = new List<Card>?[64];
	private readonly string?[,] _icon = new string?[64, LivePage];
	private readonly string?[,] _rarity = new string?[64, LivePage];
	private readonly string?[,] _swArt = new string?[64, CatCount];
	private readonly string?[,] _tileArt = new string?[64, CatCount];
	private readonly Dictionary<string, bool>?[] _sentClass = new Dictionary<string, bool>?[64];
	private readonly Dictionary<string, string>?[] _sentVar = new Dictionary<string, string>?[64];
	private readonly string?[] _parent = new string?[64];
	private readonly string?[] _drill = new string?[64];
	private readonly int[] _knifePage = new int[64];
	private readonly int[] _restorePage = new int[64];
	private readonly bool[] _fromKnife = new bool[64];
	private readonly int[] _gloveType = new int[64];
	private readonly bool[] _loading = new bool[64];
	private readonly string?[] _active = new string?[64];
	private readonly int[] _sidePage = new int[64];
	private readonly WeaponGroup?[] _filter = new WeaponGroup?[64];

	private const string TriggersFile = "wp_triggers.json";

	private Dictionary<string, string> _triggers = new(DefaultTriggers, StringComparer.Ordinal);
	private Dictionary<string, WeaponGroup> _groups = new(DefaultGroups, StringComparer.OrdinalIgnoreCase);
	private List<KeyValuePair<string, string>> _navButtons = new();

	private static bool _agentReflectWarned;

	private CCSCustomHudLayout? _layout;
	private IPmmPaintRegistry? _registry;

	private static Dictionary<string, string> Icons { get; set; } = new(StringComparer.Ordinal);

	public PluginConfig Config { get; set; } = new();

	public override string ModuleName => "PanoramaMenuManager_WeaponPaints";

	public override string ModuleVersion => "0.1.0";

	public override string ModuleAuthor => "Rimmer";

	public override string ModuleDescription => "Panorama locker. WeaponPaints menus opened through MenuManager call the original option callback.";

	private bool Ru => Lang.IsRussian(Config.Language);

	private string T(string key, params object[] args) => Lang.Get(Ru, key, args);

	public void OnConfigParsed(PluginConfig config)
	{
		config.Commands = (config.Commands ?? new List<string>())
			.Select(command => command.Trim().TrimStart('!').TrimStart('/'))
			.Select(command => command.StartsWith("css_", StringComparison.OrdinalIgnoreCase) ? command[4..] : command)
			.Where(command => command.Length > 0)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (config.Commands.Count == 0)
		{
			config.Commands.Add("pws");
		}
		if (config.OpenDelay < 0f)
		{
			config.OpenDelay = 0f;
		}
		if (config.InputDelay < 0f)
		{
			config.InputDelay = 0f;
		}
		config.Language = Lang.IsRussian(config.Language) ? "ru" : "en";
		Config = config;
	}

	public override void Load(bool hotReload)
	{
		Array.Fill(_restorePage, -1);
		Array.Fill(_gloveType, -1);
		LoadIcons();
		_wpDatabase.IndexIcons(Icons);
		_items.Load(ModuleDirectory, Logger);
		_items.LoadWeaponPaintsNames(ModuleDirectory, Logger);
		_dbReady = _wpDatabase.TryConnect(Config, Logger);
		LoadTriggers();
		foreach (string command in Config.Commands)
		{
			AddCommand("css_" + command, "Open the panorama locker", OnPws);
		}
		RegisterListener<OnCustomHudClicked>(OnClick);
		RegisterEventHandler<EventPlayerDisconnect>((@event, _) =>
		{
			CCSPlayerController? player = @event.Userid;
			if (player != null)
			{
				int slot = player.Slot;
				if (slot >= 0 && slot < 64)
				{
					ResetState(slot);
				}
				Hide(player);
			}
			return HookResult.Continue;
		});
		RegisterEventHandler<EventPlayerTeam>((@event, _) =>
		{
			CCSPlayerController? player = @event.Userid;
			if (player != null && player.IsValid && !player.IsBot)
			{
				int slot = player.Slot;
				if (slot >= 0 && slot < 64 && _open[slot])
				{
					// The loadout, the agents and the T/CT frame all belong to the old team.
					Server.NextFrame(() =>
					{
						if (player.IsValid)
						{
							Hide(player);
						}
					});
				}
			}
			return HookResult.Continue;
		});
	}

	public override void OnAllPluginsLoaded(bool hotReload)
	{
		if (_dbReady)
		{
			RegisterPainter();
		}
	}

	private void RegisterPainter()
	{
		if (_painterAdded)
		{
			return;
		}
		try
		{
			_registry = _paint.Get();
			if (_registry == null)
			{
				return;
			}
			_registry.Add(this);
			_painterAdded = true;
			Logger.LogInformation("PanoramaMenuManager_WeaponPaints registered on pmm:paint.");
		}
		catch (Exception exception)
		{
			Logger.LogError(exception, "MenuManagerCore is not loaded, so WeaponPaints menus stay on the normal list.");
		}
	}

	public override void Unload(bool hotReload)
	{
		_registry?.Remove(this);
		for (int i = 0; i < _open.Length; i++)
		{
			_token[i]++;
			_open[i] = false;
			_menu[i] = null;
		}
	}

	// ------------------------------------------------------------------ painter

	public bool TryPaint(CCSPlayerController player, IMenu menu)
	{
		if (!player.IsValid || player.IsBot || player.Slot < 0 || player.Slot >= 64 || !Matches(menu.Title))
		{
			return false;
		}
		if (!EnsureLayout())
		{
			Logger.LogWarning("Locker layout was not created. WeaponPaints uses the normal panorama list.");
			return false;
		}
		int slot = player.Slot;
		if (_drill[slot] != null && IsWeaponList(menu.Title))
		{
			ChatMenuOption? hit = Find(menu, _drill[slot]!);
			_drill[slot] = null;
			if (hit != null && !hit.Disabled)
			{
				_fromKnife[slot] = true;
				Server.NextFrame(() =>
				{
					if (player.IsValid)
					{
						InvokeSelect(player, hit);
					}
				});
				return true;
			}
		}
		EnsureTeams(player);
		string text = Plain(menu.Title);
		if (!IsSkinMenu(text))
		{
			string? parent = ParentFor(text);
			if (parent != null)
			{
				_parent[slot] = parent;
			}
		}
		bool same = _open[slot] && _menu[slot] != null && string.Equals(Plain(_menu[slot]!.Title), text, StringComparison.OrdinalIgnoreCase);
		int page = _restorePage[slot] >= 0 ? _restorePage[slot] : (same ? _page[slot] : 0);
		_restorePage[slot] = -1;
		int token = ++_token[slot];
		_open[slot] = true;
		_page[slot] = page;
		if (IsAgentsMenu(text))
		{
			// WeaponPaints only lists the agents its language file has. The locker uses the full CS2 list instead.
			_menu[slot] = null;
			_active[slot] = "open:agents";
			_view[slot] = ViewAgents;
		}
		else
		{
			_menu[slot] = menu;
			if (IsKnifeMenu(text))
			{
				_view[slot] = ViewKnife;
			}
			else if (IsGlovesMenu(text))
			{
				_view[slot] = _gloveType[slot] >= 0 ? ViewLive : ViewGloves;
			}
			else
			{
				_view[slot] = ViewLive;
			}
		}
		Later(Config.OpenDelay, () =>
		{
			if (_token[slot] == token && player.IsValid && _open[slot])
			{
				Show(player);
				CaptureInput(player, token);
			}
		});
		return true;
	}

	public void Close(CCSPlayerController player)
	{
		Hide(player);
	}

	public bool IsOpen(CCSPlayerController player)
	{
		int slot = player.Slot;
		return slot >= 0 && slot < 64 && _open[slot];
	}

	private void CaptureInput(CCSPlayerController player, int token)
	{
		int slot = player.Slot;
		Later(Config.InputDelay, () =>
		{
			CCSCustomHudLayout? layout = _layout;
			if (_token[slot] == token && layout != null && layout.IsValid && player.IsValid && _open[slot])
			{
				layout.SetInputCaptureEnabled(player, true);
			}
		});
	}

	// ------------------------------------------------------------------ !pws

	private void OnPws(CCSPlayerController? player, CommandInfo _)
	{
		if (player == null || !player.IsValid || player.IsBot || player.Slot < 0 || player.Slot >= 64)
		{
			return;
		}
		int slot = player.Slot;
		if (_open[slot])
		{
			Hide(player);
			return;
		}
		if (player.Team != CsTeam.Terrorist && player.Team != CsTeam.CounterTerrorist)
		{
			player.PrintToChat(T("chat.team"));
			return;
		}
		if (!_dbReady)
		{
			_dbReady = _wpDatabase.TryConnect(Config, Logger);
			if (!_dbReady)
			{
				player.PrintToChat(T("chat.nodb"));
				return;
			}
			RegisterPainter();
		}
		IMenuApi? menuApi = _menus.Get();
		if (menuApi == null || menuApi.GetSelectedMenu(player) != MenuType.PanoramaMenu)
		{
			player.PrintToChat(T("chat.panorama"));
			return;
		}
		string steam = player.SteamID.ToString(System.Globalization.CultureInfo.InvariantCulture);
		int token = ++_token[slot];
		_sidePage[slot] = 0;
		_open[slot] = true;
		_menu[slot] = null;
		_view[slot] = ViewHome;
		_page[slot] = 0;
		_parent[slot] = null;
		_fromKnife[slot] = false;
		_filter[slot] = null;
		_gloveType[slot] = -1;
		_active[slot] = "home";
		player.PrintToChat(T("chat.opened"));
		Task.Run(() =>
		{
			TeamData[]? teams = null;
			Exception? error = null;
			try
			{
				teams = _wpDatabase.Load(Config, steam, Logger);
			}
			catch (Exception ex)
			{
				error = ex;
			}
			Server.NextFrame(() =>
			{
				if (!player.IsValid || _token[slot] != token || !_open[slot])
				{
					return;
				}
				if (error != null || teams == null)
				{
					_dbReady = false;
					_open[slot] = false;
					Logger.LogError("PMM_WeaponPaints: нет соединения с базой WeaponPaints: {Message}", error?.Message);
					player.PrintToChat(T("chat.nodb"));
					return;
				}
				_teams[slot] = teams;
				Later(Config.OpenDelay, () =>
				{
					if (_token[slot] != token || !player.IsValid || !_open[slot])
					{
						return;
					}
					if (!EnsureLayout())
					{
						_open[slot] = false;
						player.PrintToChat(T("chat.layout"));
						Logger.LogError("PMM locker: custom_hud_layout was not created");
						return;
					}
					Show(player);
					CaptureInput(player, token);
				});
			});
		});
	}

	/// <summary>A WeaponPaints menu opened without !pws still needs the loadout for the frames.</summary>
	private void EnsureTeams(CCSPlayerController player)
	{
		int slot = player.Slot;
		if (_teams[slot] != null || _loading[slot] || !_dbReady)
		{
			if (_teams[slot] == null)
			{
				_teams[slot] = new[] { new TeamData(), new TeamData() };
			}
			return;
		}
		_teams[slot] = new[] { new TeamData(), new TeamData() };
		_loading[slot] = true;
		string steam = player.SteamID.ToString(System.Globalization.CultureInfo.InvariantCulture);
		Task.Run(() =>
		{
			TeamData[]? teams = null;
			try
			{
				teams = _wpDatabase.Load(Config, steam, Logger);
			}
			catch (Exception ex)
			{
				Logger.LogWarning("PMM_WeaponPaints: loadout was not read: {Message}", ex.Message);
			}
			Server.NextFrame(() =>
			{
				_loading[slot] = false;
				if (teams == null || !player.IsValid)
				{
					return;
				}
				_teams[slot] = teams;
				if (_open[slot])
				{
					Apply(player);
				}
			});
		});
	}

	private TeamData[] Teams(int slot)
	{
		return _teams[slot] ??= new[] { new TeamData(), new TeamData() };
	}

	private static int TeamOf(CCSPlayerController player)
	{
		return player.Team == CsTeam.CounterTerrorist ? WpDatabase.TeamCt : WpDatabase.TeamT;
	}

	// ------------------------------------------------------------------ clicks

	private void OnClick(CCSPlayerController player, CCSCustomHudLayout layout, string buttonId)
	{
		if (_layout == null || layout.Handle != _layout.Handle || player.Slot < 0 || player.Slot >= 64 || !_open[player.Slot])
		{
			return;
		}
		int slot = player.Slot;
		if (buttonId == "wp-side-prev" || buttonId == "wp-side-num-0")
		{
			_sidePage[slot] = 0;
			Apply(player);
			return;
		}
		if (buttonId == "wp-side-next" || buttonId == "wp-side-num-1")
		{
			_sidePage[slot] = 1;
			Apply(player);
			return;
		}
		if (_triggers.TryGetValue(buttonId, out string? action))
		{
			RunAction(player, action);
			return;
		}
		if (buttonId.StartsWith("wp-live-", StringComparison.Ordinal) && int.TryParse(buttonId["wp-live-".Length..], out int cell))
		{
			List<Card>? cards = _cards[slot];
			int index = _page[slot] * LivePage + cell;
			if (cards != null && index >= 0 && index < cards.Count)
			{
				cards[index].Click?.Invoke(player);
			}
			return;
		}
		if (buttonId.StartsWith("wp-num-", StringComparison.Ordinal) && int.TryParse(buttonId["wp-num-".Length..], out int num))
		{
			Turn(player, _pagerStart[slot] + num);
		}
	}

	private void RunAction(CCSPlayerController player, string action)
	{
		int slot = player.Slot;
		int colon = action.IndexOf(':');
		string verb = (colon < 0 ? action : action[..colon]).Trim().ToLowerInvariant();
		string arg = colon >= 0 ? action[(colon + 1)..].Trim() : "";
		switch (verb)
		{
			case "close":
				Hide(player);
				break;
			case "back":
				Back(player);
				break;
			case "home":
				_filter[slot] = null;
				_sidePage[slot] = 0;
				GoHome(player);
				break;
			case "side":
				_sidePage[slot] = arg == "next" || arg == "1" ? 1 : 0;
				Apply(player);
				break;
			case "open":
			{
				string section = arg.ToLowerInvariant();
				_filter[slot] = null;
				_active[slot] = "open:" + section;
				_fromKnife[slot] = false;
				_restorePage[slot] = 0;
				SyncSidePage(slot);
				if (section == "agents")
				{
					OpenAgents(player);
					break;
				}
				string? command = section switch
				{
					"knife" => "css_knife",
					"gloves" => "css_gloves",
					"skins" or "pistols" or "weapons" => "css_skins",
					"music" => "css_music",
					"pins" or "coins" => "css_pins",
					_ => null
				};
				if (command == null)
				{
					Logger.LogWarning("Unknown trigger section: {Action}", action);
					break;
				}
				if (section == "pins" || section == "coins")
				{
					_active[slot] = "open:pins";
				}
				if (section == "gloves")
				{
					_gloveType[slot] = -1;
				}
				OpenCategory(player, command);
				break;
			}
			case "weapons":
			{
				WeaponGroup? group = GroupFor(arg);
				if (group == null)
				{
					Logger.LogWarning("Unknown weapon group in trigger: {Action}", action);
					break;
				}
				_filter[slot] = group;
				_active[slot] = action;
				_fromKnife[slot] = false;
				_restorePage[slot] = 0;
				SyncSidePage(slot);
				OpenCategory(player, "css_skins");
				break;
			}
			case "page":
				if (_view[slot] != ViewHome)
				{
					Turn(player, _page[slot] + (arg.Equals("prev", StringComparison.OrdinalIgnoreCase) ? -1 : 1));
				}
				break;
			case "cmd":
				if (!IsSafeCommand(arg))
				{
					break;
				}
				Server.NextFrame(() =>
				{
					if (player.IsValid)
					{
						ClearCooldown(player.Slot);
						player.ExecuteClientCommandFromServer(arg);
					}
				});
				break;
			default:
				Logger.LogWarning("Unknown trigger: {Action}", action);
				break;
		}
	}

	private void Back(CCSPlayerController player)
	{
		int slot = player.Slot;
		IMenu? menu = _menu[slot];
		if (_view[slot] == ViewLive && menu != null && IsSkinMenu(menu.Title) && _parent[slot] != null)
		{
			_restorePage[slot] = _fromKnife[slot] ? _knifePage[slot] : 0;
			_fromKnife[slot] = false;
			ClearCooldown(slot);
			OpenCategory(player, _parent[slot]!);
		}
		else if (_view[slot] == ViewLive && menu != null && IsGlovesMenu(menu.Title) && _gloveType[slot] >= 0)
		{
			_gloveType[slot] = -1;
			_view[slot] = ViewGloves;
			_page[slot] = 0;
			Apply(player);
		}
		else if (_view[slot] != ViewHome)
		{
			_filter[slot] = null;
			GoHome(player);
		}
		else
		{
			Hide(player);
		}
	}

	private void Turn(CCSPlayerController player, int target)
	{
		int slot = player.Slot;
		if (_view[slot] != ViewHome)
		{
			_page[slot] = Math.Clamp(target, 0, Math.Max(0, Pages(slot) - 1));
			Apply(player);
		}
	}

	private int Pages(int slot)
	{
		int count = _cards[slot]?.Count ?? 0;
		return Math.Max(1, (count + LivePage - 1) / LivePage);
	}

	private WeaponGroup? GroupFor(string arg)
	{
		if (_groups.TryGetValue(arg, out WeaponGroup? group))
		{
			return group;
		}
		string[] items = arg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return items.Length == 0 ? null : new WeaponGroup("", "WEAPONS", items);
	}

	private static bool IsSafeCommand(string command)
	{
		if (!command.StartsWith("css_", StringComparison.OrdinalIgnoreCase) || command.Length > 64)
		{
			return false;
		}
		foreach (char c in command)
		{
			if (!char.IsAsciiLetterOrDigit(c) && c != '_' && c != ' ')
			{
				return false;
			}
		}
		return true;
	}

	private void LoadTriggers()
	{
		_triggers = new Dictionary<string, string>(DefaultTriggers, StringComparer.Ordinal);
		_groups = new Dictionary<string, WeaponGroup>(DefaultGroups, StringComparer.OrdinalIgnoreCase);
		_navButtons = new List<KeyValuePair<string, string>>();
		string path = Path.Combine(ModuleDirectory, TriggersFile);
		if (!File.Exists(path))
		{
			return;
		}
		try
		{
			TriggerFile? file = JsonSerializer.Deserialize<TriggerFile>(File.ReadAllText(path), new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			});
			if (file == null)
			{
				return;
			}
			foreach ((string key, WeaponGroupJson value) in file.Groups ?? new Dictionary<string, WeaponGroupJson>())
			{
				if (value.Items is { Length: > 0 })
				{
					_groups[key] = new WeaponGroup(key, string.IsNullOrWhiteSpace(value.Title) ? key.ToUpperInvariant() : value.Title!, value.Items);
				}
			}
			foreach ((string id, string action) in file.Buttons ?? new Dictionary<string, string>())
			{
				if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(action))
				{
					continue;
				}
				_triggers[id] = action.Trim();
				if (action.StartsWith("open:", StringComparison.OrdinalIgnoreCase) || action.StartsWith("weapons:", StringComparison.OrdinalIgnoreCase) || action.Equals("home", StringComparison.OrdinalIgnoreCase))
				{
					_navButtons.Add(new KeyValuePair<string, string>(id, action.Trim()));
				}
			}
			Logger.LogInformation("{File}: {Buttons} buttons, {Groups} weapon groups.", TriggersFile, file.Buttons?.Count ?? 0, _groups.Count);
		}
		catch (Exception ex)
		{
			Logger.LogWarning("{File} could not be read: {Message}", TriggersFile, ex.Message);
		}
	}

	private void GoHome(CCSPlayerController player)
	{
		int slot = player.Slot;
		_active[slot] = "home";
		_fromKnife[slot] = false;
		_menu[slot] = null;
		_parent[slot] = null;
		_gloveType[slot] = -1;
		_view[slot] = ViewHome;
		_page[slot] = 0;
		Apply(player);
	}

	private void OpenAgents(CCSPlayerController player)
	{
		int slot = player.Slot;
		_menu[slot] = null;
		_parent[slot] = null;
		_view[slot] = ViewAgents;
		_page[slot] = 0;
		Apply(player);
	}

	private void OpenCategory(CCSPlayerController player, string command)
	{
		Server.NextFrame(() =>
		{
			if (player.IsValid)
			{
				ClearCooldown(player.Slot);
				player.ExecuteClientCommandFromServer(command);
			}
		});
	}

	private void ClickKnife(CCSPlayerController player, string knifeName)
	{
		int slot = player.Slot;
		IMenu? menu = _menu[slot];
		if (menu == null)
		{
			return;
		}
		ChatMenuOption? option = Find(menu, knifeName);
		if (option != null && !option.Disabled)
		{
			OpenKnife(player, knifeName, option);
		}
	}

	private void OpenKnife(CCSPlayerController player, string knifeName, ChatMenuOption option)
	{
		int slot = player.Slot;
		bool stock = knifeName.Equals("Default Knife", StringComparison.OrdinalIgnoreCase);
		Teams(slot)[TeamOf(player)].Knife = stock ? null : knifeName;
		_knifePage[slot] = _page[slot];
		_parent[slot] = "css_knife";
		if (stock)
		{
			_fromKnife[slot] = false;
			_page[slot] = 0;
			Apply(player);
			Server.NextFrame(() =>
			{
				if (player.IsValid && _open[slot])
				{
					InvokeSelect(player, option);
					_page[slot] = 0;
					Apply(player);
				}
			});
			return;
		}
		_drill[slot] = knifeName;
		Apply(player);
		Server.NextFrame(() =>
		{
			if (player.IsValid && _open[slot])
			{
				InvokeSelect(player, option);
				ClearCooldown(slot);
				player.ExecuteClientCommandFromServer("css_skins");
			}
		});
	}

	/// <summary>Runs the WeaponPaints callback of a menu option and records what it equips.</summary>
	private void Choose(CCSPlayerController player, ChatMenuOption option, Action<TeamData>? equip)
	{
		int slot = player.Slot;
		string title = Plain(_menu[slot]?.Title ?? "");
		bool skin = IsSkinMenu(title);
		int page = _page[slot];
		if (equip != null && (player.Team == CsTeam.Terrorist || player.Team == CsTeam.CounterTerrorist))
		{
			equip(Teams(slot)[TeamOf(player)]);
		}
		Server.NextFrame(() =>
		{
			if (!player.IsValid || !_open[slot])
			{
				return;
			}
			InvokeSelect(player, option);
			if (!player.IsValid)
			{
				return;
			}
			if (skin)
			{
				_fromKnife[slot] = false;
				_restorePage[slot] = 0;
				_page[slot] = 0;
				Later(Config.OpenDelay, () =>
				{
					if (player.IsValid)
					{
						ClearCooldown(slot);
						player.ExecuteClientCommandFromServer(_parent[slot] ?? "css_skins");
					}
				});
			}
			else if (_open[slot] && _menu[slot] != null && string.Equals(Plain(_menu[slot]!.Title), title, StringComparison.OrdinalIgnoreCase))
			{
				_page[slot] = page;
				Apply(player);
			}
		});
	}

	private void ChooseAgent(CCSPlayerController player, AgentEntry? agent)
	{
		int slot = player.Slot;
		if (player.Team != CsTeam.Terrorist && player.Team != CsTeam.CounterTerrorist)
		{
			return;
		}
		int team = TeamOf(player);
		string? model = agent?.Model;
		Teams(slot)[team].Agent = model;
		Apply(player);

		bool live = SetWeaponPaintsAgent(player, team, model);
		string name = agent == null ? T("default") : AgentTitle(agent);
		player.PrintToChat(T(live && model != null ? "chat.agent" : "chat.agent.later", name));

		string steam = player.SteamID.ToString(System.Globalization.CultureInfo.InvariantCulture);
		Task.Run(() =>
		{
			try
			{
				_wpDatabase.SaveAgent(Config, steam, team, model);
			}
			catch (Exception ex)
			{
				Server.NextFrame(() =>
				{
					Logger.LogError("PMM_WeaponPaints: agent was not saved: {Message}", ex.Message);
					if (player.IsValid)
					{
						player.PrintToChat(T("chat.agent.fail"));
					}
				});
			}
		});
	}

	/// <summary>
	/// Puts the agent into WeaponPaints' own table (GPlayersAgent) so its spawn hook keeps it,
	/// and applies it right away when the pawn is alive. Returns true when the model was set now.
	/// </summary>
	private bool SetWeaponPaintsAgent(CCSPlayerController player, int team, string? model)
	{
		try
		{
			Type? type = WeaponPaintsType();
			object? table = type?.GetField("GPlayersAgent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
			if (type == null || table == null)
			{
				WarnAgentReflection("GPlayersAgent was not found in WeaponPaints");
				return false;
			}
			string? ct = null;
			string? t = null;
			object?[] args = { player.Slot, null };
			if (table.GetType().GetMethod("TryGetValue")?.Invoke(table, args) is true && args[1] is ITuple current && current.Length >= 2)
			{
				ct = current[0] as string;
				t = current[1] as string;
			}
			if (team == WpDatabase.TeamCt)
			{
				ct = model;
			}
			else
			{
				t = model;
			}
			table.GetType().GetProperty("Item")?.SetValue(table, (ct, t), new object[] { player.Slot });
			if (model == null || player.PlayerPawn.Value == null || !player.PawnIsAlive)
			{
				return false;
			}
			MethodInfo? give = type.GetMethod("GivePlayerAgent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (give == null)
			{
				WarnAgentReflection("GivePlayerAgent was not found in WeaponPaints");
				return false;
			}
			give.Invoke(null, new object[] { player });
			return true;
		}
		catch (Exception ex)
		{
			WarnAgentReflection(ex.Message);
			return false;
		}
	}

	private void WarnAgentReflection(string message)
	{
		if (_agentReflectWarned)
		{
			return;
		}
		_agentReflectWarned = true;
		Logger.LogWarning("PMM_WeaponPaints: the agent is saved to the database only ({Message}). It applies after a reconnect.", message);
	}

	private static Type? WeaponPaintsType()
	{
		foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			Type? type = assembly.GetType("WeaponPaints.WeaponPaints");
			if (type != null)
			{
				return type;
			}
		}
		return null;
	}

	private void Later(float seconds, Action callback)
	{
		if (seconds <= 0f)
		{
			Server.NextFrame(callback);
		}
		else
		{
			AddTimer(seconds, callback);
		}
	}

	private static void InvokeSelect(CCSPlayerController player, ChatMenuOption option)
	{
		option.OnSelect?.Invoke(player, option);
	}

	// ------------------------------------------------------------------ show / hide

	private void Show(CCSPlayerController player)
	{
		CCSCustomHudLayout? layout = _layout;
		if (layout != null && layout.IsValid)
		{
			Apply(player);
			layout.SetHasClassForPlayer(player, "wp-root", "wp-hidden", false);
		}
	}

	private void Hide(CCSPlayerController player)
	{
		int slot = player.Slot;
		if (slot >= 0 && slot < 64)
		{
			_token[slot]++;
			_open[slot] = false;
			_menu[slot] = null;
			_filter[slot] = null;
			_cards[slot] = null;
		}
		CCSCustomHudLayout? layout = _layout;
		if (layout != null && layout.IsValid && player.IsValid)
		{
			layout.SetInputCaptureEnabled(player, false);
			layout.SetHasClassForPlayer(player, "wp-root", "wp-hidden", true);
		}
	}

	private void ResetState(int slot)
	{
		_teams[slot] = null;
		_loading[slot] = false;
		_parent[slot] = null;
		_drill[slot] = null;
		_fromKnife[slot] = false;
		_restorePage[slot] = -1;
		_gloveType[slot] = -1;
		_filter[slot] = null;
		_active[slot] = null;
		_sidePage[slot] = 0;
		_cards[slot] = null;
		ForgetSent(slot);
	}

	/// <summary>
	/// Drops what was sent to this client, so the next redraw sends every class and text again.
	/// Only for a new layout entity or a new client: the panels keep their classes while the locker
	/// is hidden, and forgetting a picture class here would leave it on the panel next to the new one.
	/// </summary>
	private void ForgetSent(int slot)
	{
		_sentClass[slot] = null;
		_sentVar[slot] = null;
		for (int i = 0; i < LivePage; i++)
		{
			_icon[slot, i] = null;
			_rarity[slot, i] = null;
		}
		for (int i = 0; i < CatCount; i++)
		{
			_swArt[slot, i] = null;
			_tileArt[slot, i] = null;
		}
	}

	private bool EnsureLayout()
	{
		CCSCustomHudLayout? layout = _layout;
		if (layout != null && layout.IsValid)
		{
			return true;
		}
		CCSCustomHudLayout? created = Utilities.CreateEntityByName<CCSCustomHudLayout>("custom_hud_layout");
		if (created == null || created.Handle == IntPtr.Zero)
		{
			return false;
		}
		created.StrLayout = LayoutPath;
		created.DispatchSpawn();
		if (!created.IsValid)
		{
			return false;
		}
		_layout = created;
		for (int slot = 0; slot < 64; slot++)
		{
			ForgetSent(slot);
		}
		return true;
	}

	// ------------------------------------------------------------------ helpers

	private static ChatMenuOption? Find(IMenu menu, string name)
	{
		foreach (ChatMenuOption option in menu.MenuOptions)
		{
			if (string.Equals(Plain(option.Text ?? ""), name, StringComparison.OrdinalIgnoreCase))
			{
				return option;
			}
		}
		return null;
	}

	private static void ClearCooldown(int slot)
	{
		Type? type = WeaponPaintsType();
		if (type?.GetField("CommandsCooldown", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null) is Dictionary<int, DateTime> cooldown)
		{
			cooldown[slot] = DateTime.MinValue;
		}
	}

	private void LoadIcons()
	{
		try
		{
			string path = Path.Combine(ModuleDirectory, "icons.json");
			if (!File.Exists(path))
			{
				Logger.LogWarning("icons.json was not found next to PMM_WeaponPaints.dll.");
				return;
			}
			Dictionary<string, string>? icons = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
			if (icons != null)
			{
				Icons = new Dictionary<string, string>(icons, StringComparer.Ordinal);
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning("icons.json could not be read: {Message}", ex.Message);
		}
	}

	private static string? IconClass(string name)
	{
		return Icons.TryGetValue(name, out string? value) ? value : null;
	}

	private static string Plain(string? text)
	{
		string result = text ?? "";
		foreach (string color in Colors)
		{
			result = result.Replace("{ " + color + "}", "", StringComparison.OrdinalIgnoreCase);
			result = result.Replace("{" + color + "}", "", StringComparison.OrdinalIgnoreCase);
			result = result.Replace("[color:" + color + "]", "", StringComparison.OrdinalIgnoreCase);
		}
		return result.Replace("</font>", "", StringComparison.OrdinalIgnoreCase).Trim();
	}
}
