using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
	[JsonPropertyName("OpenDelay")] public float OpenDelay { get; set; } = 0.2f;
	[JsonPropertyName("InputDelay")] public float InputDelay { get; set; } = 0.2f;
	[JsonPropertyName("DatabaseHost")] public string DatabaseHost { get; set; } = "";
	[JsonPropertyName("DatabasePort")] public int DatabasePort { get; set; } = 3306;
	[JsonPropertyName("DatabaseUser")] public string DatabaseUser { get; set; } = "";
	[JsonPropertyName("DatabasePassword")] public string DatabasePassword { get; set; } = "";
	[JsonPropertyName("DatabaseName")] public string DatabaseName { get; set; } = "";
}

public class Plugin : BasePlugin, IPluginConfig<PluginConfig>, IPmmMenuPainter
{
	private sealed class LoadoutSwap(List<Action> undo) : IDisposable
	{
		public void Dispose()
		{
			for (int num = undo.Count - 1; num >= 0; num--)
			{
				undo[num]();
			}
		}
	}

	private sealed record WeaponGroup(string Title, string[] Items);

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

	private const string LayoutPath = "panorama/layout/custom_game/wp.xml";

	private const int ViewHome = 0;

	private const int ViewKnife = 1;

	private const int ViewLive = 2;

	private const int LivePage = 18;

	private const int PagerButtons = 7;

	private const int CatLoadout = 0;

	private const int CatKnife = 1;

	private const int CatGloves = 2;

	private const int CatPistols = 3;

	private const int CatRifles = 4;

	private const int CatSnipers = 5;

	private const int CatSmg = 6;

	private const int CatHeavy = 7;

	private const int CatAgents = 8;

	private const int CatMusic = 9;

	private const int CatNone = 10;

	private const int StockKnife = 1;

	private static readonly string[] Views = new string[3] { "view-home", "view-knife", "view-live" };

	private static readonly string[] Cats = new string[11]
	{
		"cat-loadout", "cat-knife", "cat-gloves", "cat-pistols", "cat-rifles", "cat-snipers", "cat-smg", "cat-heavy", "cat-agents", "cat-music",
		"cat-none"
	};

	private static readonly string[] KnifeNames = new string[21]
	{
		"Shadow Daggers", "Default Knife", "Bayonet", "Bowie Knife", "Butterfly Knife", "Classic Knife", "Falchion Knife", "Flip Knife", "Gut Knife", "Huntsman Knife",
		"Karambit", "Kukri Knife", "M9 Bayonet", "Navaja Knife", "Nomad Knife", "Paracord Knife", "Skeleton Knife", "Stiletto Knife", "Survival Knife", "Talon Knife",
		"Ursus Knife"
	};

	private static readonly int[] KnifeFinishes = new int[21]
	{
		34, 0, 34, 34, 34, 12, 34, 34, 34, 34,
		34, 12, 34, 24, 24, 24, 24, 24, 24, 24,
		24
	};

	private static readonly int[] KnifeBase = new int[21]
	{
		1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
		11, 12, 13, 14, 15, 0, 16, 17, 18, 19,
		20
	};

	private static readonly string[] Match = new string[14]
	{
		"Knife Menu", "Меню Ножей", "Weapon Menu", "Меню Оружия", "Gloves Menu", "Меню Перчаток", "Agents Menu", "Меню Агентов", "Music Menu", "Меню Музыки",
		"Pins Menu", "Меню пинов", "Select skin", "Выберите скин"
	};

	private static readonly string[] Colors = new string[21]
	{
		"Default", "White", "Darkred", "Green", "Lightyellow", "Lightblue", "Olive", "Lime", "Red", "Lightpurple",
		"Purple", "Grey", "Yellow", "Gold", "Silver", "Blue", "Darkblue", "Bluegrey", "Magenta", "Lightred",
		"Orange"
	};

	private readonly PluginCapability<IPmmPaintRegistry?> _paint = new PluginCapability<IPmmPaintRegistry>("pmm:paint");

	private readonly PluginCapability<IMenuApi?> _menus = new PluginCapability<IMenuApi>("menu:nfcore");

	private readonly WpDatabase _wpDatabase = new WpDatabase();

	private bool _dbReady;

	private bool _painterAdded;

	private readonly bool[] _open = new bool[64];

	private readonly int[] _token = new int[64];

	private readonly int[] _view = new int[64];

	private readonly int[] _page = new int[64];

	private readonly int[] _pagerStart = new int[64];

	private readonly IMenu?[] _menu = new IMenu[64];

	private readonly string?[,] _icon = new string[64, 18];

	private readonly string?[] _swatchKnifeArt = new string[64];

	private readonly string?[] _tileKnifeArt = new string[64];

	private readonly string?[] _equipKnife = new string[64];

	private readonly string?[] _equipSkin = new string[64];

	private readonly string?[] _knifePlain = new string[64];

	private readonly string?[,] _side = new string[64, 11];

	private readonly string?[] _typePick = new string[64];

	private readonly Dictionary<string, string>?[] _pickedBy = new Dictionary<string, string>[64];

	private readonly string?[] _parent = new string[64];

	private readonly string?[] _drill = new string[64];

	private readonly int[] _knifePage = new int[64];

	private readonly int[] _restorePage = new int[64];

	private readonly bool[] _fromKnife = new bool[64];

	private const string TriggersFile = "wp_triggers.json";

	private static readonly Dictionary<string, string> DefaultTriggers = new Dictionary<string, string>(StringComparer.Ordinal)
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
		["wp-prev"] = "page:prev",
		["wp-next"] = "page:next"
	};

	private static readonly Dictionary<string, WeaponGroup> DefaultGroups = new Dictionary<string, WeaponGroup>(StringComparer.OrdinalIgnoreCase)
	{
		["pistols"] = new WeaponGroup("PISTOLS", new string[11]
		{
			"Glock-18", "USP-S", "P2000", "P250", "Dual Berettas", "Five-SeveN", "Tec-9", "CZ75-Auto", "Desert Eagle", "R8 Revolver",
			"Zeus x27"
		}),
		["rifles"] = new WeaponGroup("RIFLES", new string[7] { "AK-47", "M4A4", "M4A1-S", "Galil AR", "FAMAS", "SG 553", "AUG" }),
		["snipers"] = new WeaponGroup("SNIPERS", new string[4] { "AWP", "SSG 08", "SCAR-20", "G3SG1" }),
		["smg"] = new WeaponGroup("SMG", new string[7] { "MAC-10", "MP9", "MP7", "MP5-SD", "UMP-45", "P90", "PP-Bizon" }),
		["heavy"] = new WeaponGroup("HEAVY", new string[6] { "Nova", "XM1014", "Sawed-Off", "MAG-7", "M249", "Negev" })
	};

	private Dictionary<string, string> _triggers = new Dictionary<string, string>(DefaultTriggers, StringComparer.Ordinal);

	private Dictionary<string, WeaponGroup> _groups = new Dictionary<string, WeaponGroup>(DefaultGroups, StringComparer.OrdinalIgnoreCase);

	private List<KeyValuePair<string, string>> _navButtons = new List<KeyValuePair<string, string>>();

	private readonly string?[] _active = new string[64];

	private readonly bool[] _wantTerrorist = new bool[64];

	private readonly int[] _sidePage = new int[64];

	private readonly Dictionary<string, string>?[] _weaponSkin = new Dictionary<string, string>?[64];

	private readonly string?[,] _catArt = new string?[64, 11];

	private readonly string?[,] _tileArt = new string?[64, 11];

	private static bool _loadoutWarned;

	private static bool _agentsWarned;

	private static bool _loadoutBound;

	private static readonly List<FieldInfo> _teamTables = new List<FieldInfo>();

	private static readonly HashSet<string> AgentsT = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Sir Bloody Darryl Royale", "Sir Bloody Miami Darryl", "Sir Bloody Silent Darryl", "Sir Bloody Skullhead Darryl", "Sir Bloody Loudmouth Darryl", "Safecracker Voltzmann", "Little Kev", "Number K", "Getaway Sally", "Street Soldier",
		"Elite Trapper Solman", "Medium Rare Crasswater", "The Elite Mr. Muhlik", "Bloody Darryl The Strapped", "Blackwolf", "Rezan the Ready", "Dragomir", "Maximus", "Osiris", "Slingshot",
		"Ground Rebel", "Jungle Rebel", "Professional", "Soldier", "Enforcer", "Separatist", "Balkan", "Phoenix", "Guerrilla", "Pirate"
	};

	private static readonly HashSet<string> AgentsCt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Special Agent Ava", "Chem-Haz Capitaine", "Cmdr. Mae Jamison", "1st Lieutenant Farlow", "John 'Van Healen' Kask", "Bio-Haz Specialist", "Sergeant Bombson", "Chem-Haz Specialist", "Lieutenant 'Tree Hugger' Farlow", "Cmdr. Davida 'Goggles' Fernandez",
		"Cmdr. Frank 'Wet Sox' Baroud", "Lieutenant Rex Krikey", "Sous-Lieutenant Medic", "Primeiro Tenente", "D Squadron Officer", "B Squadron Officer", "Seal Team 6 Soldier", "Buckshot", "Two Times McCoy", "Ricksaw",
		"Operator", "Markus Delrow", "Michael Syfers", "FBI", "GIGN", "GSG-9", "SAS", "SEAL", "SWAT", "IDF",
		"KSK", "NZSAS", "TACP"
	};

	private readonly WeaponGroup?[] _filter = new WeaponGroup[64];

	private CCSCustomHudLayout? _layout;

	private IPmmPaintRegistry? _registry;

	private static Dictionary<string, string> Icons { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

	public PluginConfig Config { get; set; } = new PluginConfig();

	public override string ModuleName => "PanoramaMenuManager_WeaponPaints";

	public override string ModuleVersion => "0.0.3";

	public override string ModuleAuthor => "Rimmer";

	public override string ModuleDescription => "Panorama locker. WeaponPaints menus opened through MenuManager call the original option callback.";

	public void OnConfigParsed(PluginConfig config)
	{
		config.Commands = (from command in config.Commands ?? new List<string>()
			select command.Trim().TrimStart('!').TrimStart('/') into command
			select (!command.StartsWith("css_", StringComparison.OrdinalIgnoreCase)) ? command : command.Substring(4, command.Length - 4) into command
			where command.Length > 0
			select command).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
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
		Config = config;
	}

	public override void Load(bool hotReload)
	{
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		Array.Fill(_restorePage, -1);
		LoadIcons();
		_wpDatabase.IndexIcons(Icons);
		_dbReady = _wpDatabase.TryConnect(Config, base.Logger);
		LoadTriggers();
		foreach (string command in Config.Commands)
		{
			AddCommand("css_" + command, "Open the panorama locker", OnPws);
		}
		base.RegisterListener<OnCustomHudClicked>(new OnCustomHudClicked(OnClick));
		RegisterEventHandler(delegate(EventPlayerDisconnect @event, GameEventInfo _)
		{
			if (@event.Userid != null)
			{
				int slot = @event.Userid.Slot;
				if (slot >= 0 && slot < 64)
				{
					ResetState(slot);
				}
				Hide(@event.Userid);
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
			base.Logger.LogInformation("PanoramaMenuManager_WeaponPaints registered on pmm:paint.");
		}
		catch (Exception exception)
		{
			base.Logger.LogError(exception, "MenuManagerCore is not loaded, so WeaponPaints menus stay on the normal list.");
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

	public bool TryPaint(CCSPlayerController player, IMenu menu)
	{
		bool flag = !player.IsValid || player.IsBot;
		if (!flag)
		{
			int slot = player.Slot;
			bool flag2 = ((slot < 0 || slot >= 64) ? true : false);
			flag = flag2;
		}
		if (flag || !Matches(menu.Title))
		{
			return false;
		}
		if (!EnsureLayout())
		{
			base.Logger.LogWarning("Locker layout was not created. WeaponPaints uses the normal panorama list.");
			return false;
		}
		int slot2 = player.Slot;
		if (_drill[slot2] != null && IsWeaponList(menu.Title))
		{
			ChatMenuOption hit = Find(menu, _drill[slot2]);
			_drill[slot2] = null;
			if (hit != null && !hit.Disabled)
			{
				_fromKnife[slot2] = true;
				Server.NextFrame(delegate
				{
					if (player.IsValid)
					{
						InvokeSelect(player, hit);
					}
				});
				return true;
			}
		}
		string text = Plain(menu.Title);
		if (!IsSkinMenu(text))
		{
			string text2 = ParentFor(text);
			if (text2 != null)
			{
				_parent[slot2] = text2;
			}
		}
		bool flag3 = _open[slot2] && _menu[slot2] != null && string.Equals(Plain(_menu[slot2].Title), text, StringComparison.OrdinalIgnoreCase);
		int num = ((_restorePage[slot2] >= 0) ? _restorePage[slot2] : (flag3 ? _page[slot2] : 0));
		_restorePage[slot2] = -1;
		int token = ++_token[slot2];
		_open[slot2] = true;
		_menu[slot2] = menu;
		_page[slot2] = num;
		_view[slot2] = (IsKnifeMenu(menu.Title) ? 1 : 2);
		Later(Config.OpenDelay, delegate
		{
			if (_token[slot2] == token && player.IsValid && _open[slot2])
			{
				Show(player);
				Later(Config.InputDelay, delegate
				{
					if (_token[slot2] == token)
					{
						CCSCustomHudLayout layout = _layout;
						if (layout != null && ((CEntityInstance)(object)layout).IsValid && player.IsValid && _open[slot2])
						{
							CCSCustomHudLayoutExtensions.SetInputCaptureEnabled(_layout, player, true);
						}
					}
				});
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
		if (slot >= 0 && slot < 64)
		{
			return _open[player.Slot];
		}
		return false;
	}

	private void OnPws(CCSPlayerController? player, CommandInfo _)
	{
		bool flag = player == null || !player.IsValid || player.IsBot;
		if (!flag)
		{
			int slot = player.Slot;
			bool flag2 = ((slot < 0 || slot >= 64) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return;
		}
		if (!_dbReady)
		{
			_dbReady = _wpDatabase.TryConnect(Config, base.Logger);
			if (!_dbReady)
			{
				player.PrintToChat(" [PMM] Локер не открыт: нет соединения с базой WeaponPaints.");
				return;
			}
			RegisterPainter();
		}
		IMenuApi? menuApi = _menus.Get();
		if (menuApi == null || menuApi.GetSelectedMenu(player) != MenuType.PanoramaMenu)
		{
			player.PrintToChat(" [PMM] Нужна панорама мышью: !menu → Выбор меню → Панорама (мышь). После этого локер откроется.");
			return;
		}
		int slot2 = player.Slot;
		if (_open[slot2])
		{
			Hide(player);
			return;
		}
		string steam = player.SteamID.ToString(System.Globalization.CultureInfo.InvariantCulture);
		int team = player.Team == CsTeam.CounterTerrorist ? 3 : 2;
		int token = ++_token[slot2];
		_sidePage[slot2] = 0;
		_open[slot2] = true;
		_menu[slot2] = null;
		_view[slot2] = 0;
		_page[slot2] = 0;
		_parent[slot2] = null;
		_fromKnife[slot2] = false;
		_filter[slot2] = null;
		_active[slot2] = "home";
		player.PrintToChat(" [PMM] Локер");
		Task.Run(delegate
		{
			WpLoadout? loadout = null;
			Exception? error = null;
			try
			{
				loadout = _wpDatabase.Load(Config, steam, team);
			}
			catch (Exception ex)
			{
				error = ex;
			}
			Server.NextFrame(delegate
			{
				if (!player.IsValid || _token[slot2] != token || !_open[slot2])
				{
					return;
				}
				if (error != null || loadout == null)
				{
					_dbReady = false;
					_open[slot2] = false;
					base.Logger.LogError("PMM_WeaponPaints: нет соединения с базой WeaponPaints: {Message}", error?.Message);
					player.PrintToChat(" [PMM] Локер не открыт: нет соединения с базой WeaponPaints.");
					return;
				}
				ApplyDatabase(slot2, loadout);
				Later(Config.OpenDelay, delegate
				{
					if (_token[slot2] == token && player.IsValid && _open[slot2])
					{
						if (!EnsureLayout())
						{
							_open[slot2] = false;
							player.PrintToChat(" [PMM] Панорама не создалась");
							base.Logger.LogError("PMM locker: custom_hud_layout was not created");
						}
						else
						{
							Show(player);
							Later(Config.InputDelay, delegate
							{
								if (_token[slot2] == token)
								{
									CCSCustomHudLayout layout = _layout;
									if (layout != null && ((CEntityInstance)(object)layout).IsValid && player.IsValid && _open[slot2])
									{
										CCSCustomHudLayoutExtensions.SetInputCaptureEnabled(_layout, player, true);
									}
								}
							});
						}
					}
				});
			});
		});
	}

	private void OnClick(CCSPlayerController player, CCSCustomHudLayout layout, string buttonId)
	{
		bool flag = (CEntityInstance?)(object)_layout == null || ((NativeEntity)(object)layout).Handle != ((NativeEntity)(object)_layout).Handle;
		if (!flag)
		{
			int slot = player.Slot;
			bool flag2 = ((slot < 0 || slot >= 64) ? true : false);
			flag = flag2;
		}
		if (flag || !_open[player.Slot])
		{
			return;
		}
		int slot2 = player.Slot;
		if (buttonId == "wp-side-prev" || buttonId == "wp-side-num-0")
		{
			_sidePage[slot2] = 0;
			Apply(player);
			return;
		}
		if (buttonId == "wp-side-next" || buttonId == "wp-side-num-1")
		{
			_sidePage[slot2] = 1;
			Apply(player);
			return;
		}
		if (_triggers.TryGetValue(buttonId, out string value))
		{
			RunAction(player, value);
			return;
		}
		if (buttonId.StartsWith("wp-live-", StringComparison.Ordinal))
		{
			string text = buttonId;
			int slot = "wp-live-".Length;
			if (int.TryParse(text.Substring(slot, text.Length - slot), out var result))
			{
				if (_view[slot2] == 1)
				{
					ClickKnifeCard(player, result);
				}
				else if (_view[slot2] == 2)
				{
					ClickLiveCard(player, result);
				}
				return;
			}
		}
		if (buttonId.StartsWith("wp-num-", StringComparison.Ordinal))
		{
			string text = buttonId;
			int slot = "wp-num-".Length;
			if (int.TryParse(text.Substring(slot, text.Length - slot), out var result2))
			{
				Turn(player, _pagerStart[slot2] + result2);
			}
		}
	}

	private void RunAction(CCSPlayerController player, string action)
	{
		int slot = player.Slot;
		int num = action.IndexOf(':');
		string text = ((num < 0) ? action : action.Substring(0, num)).Trim().ToLowerInvariant();
		object obj;
		if (num >= 0)
		{
			string text2 = action;
			int num2 = num + 1;
			obj = text2.Substring(num2, text2.Length - num2).Trim();
		}
		else
		{
			obj = "";
		}
		string arg = (string)obj;
		switch (text)
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
			if (arg == "next")
			{
				_sidePage[slot] = 1;
			}
			else if (arg == "prev")
			{
				_sidePage[slot] = 0;
			}
			else
			{
				_sidePage[slot] = arg == "1" ? 1 : 0;
			}
			Apply(player);
			break;
		case "open":
		{
			string text2;
			switch (arg.ToLowerInvariant())
			{
			case "knife":
				text2 = "css_knife";
				break;
			case "gloves":
				text2 = "css_gloves";
				break;
			case "skins":
			case "pistols":
			case "weapons":
				text2 = "css_skins";
				break;
			case "agents":
				text2 = "css_agents";
				break;
			case "music":
				text2 = "css_music";
				break;
			case "pins":
				text2 = "css_pins";
				break;
			default:
				text2 = null;
				break;
			}
			string text3 = text2;
			if (text3 == null)
			{
				base.Logger.LogWarning("Unknown trigger section: {Action}", action);
				break;
			}
			_filter[slot] = null;
			_active[slot] = action;
			_fromKnife[slot] = false;
			_restorePage[slot] = 0;
			SyncSidePage(slot);
			OpenCategory(player, text3);
			break;
		}
		case "weapons":
		{
			WeaponGroup weaponGroup = GroupFor(arg);
			if (weaponGroup == null)
			{
				base.Logger.LogWarning("Unknown weapon group in trigger: {Action}", action);
				break;
			}
			_filter[slot] = weaponGroup;
			_active[slot] = action;
			_fromKnife[slot] = false;
			_restorePage[slot] = 0;
			SyncSidePage(slot);
			OpenCategory(player, "css_skins");
			break;
		}
		case "page":
			if (_view[slot] != 0)
			{
				Turn(player, _page[slot] + ((!arg.Equals("prev", StringComparison.OrdinalIgnoreCase)) ? 1 : (-1)));
			}
			break;
		case "cmd":
			if (!IsSafeCommand(arg))
			{
				break;
			}
			Server.NextFrame(delegate
			{
				if (player.IsValid)
				{
					ClearCooldown(player.Slot);
					player.ExecuteClientCommandFromServer(arg);
				}
			});
			break;
		default:
			base.Logger.LogWarning("Unknown trigger: {Action}", action);
			break;
		}
	}

	private void Back(CCSPlayerController player)
	{
		int slot = player.Slot;
		if (_menu[slot] != null && IsSkinMenu(_menu[slot].Title) && _parent[slot] != null)
		{
			_restorePage[slot] = (_fromKnife[slot] ? _knifePage[slot] : 0);
			_fromKnife[slot] = false;
			ClearCooldown(slot);
			OpenCategory(player, _parent[slot]);
		}
		else if (_menu[slot] != null || _view[slot] != 0)
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
		if (_view[slot] != 0)
		{
			_page[slot] = Math.Clamp(target, 0, Pages(slot) - 1);
			Apply(player);
		}
	}

	private WeaponGroup? GroupFor(string arg)
	{
		if (_groups.TryGetValue(arg, out WeaponGroup value))
		{
			return value;
		}
		string[] array = arg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (array.Length == 0)
		{
			return null;
		}
		return new WeaponGroup("WEAPONS", array);
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
		string path = Path.Combine(base.ModuleDirectory, "wp_triggers.json");
		if (!File.Exists(path))
		{
			return;
		}
		try
		{
			TriggerFile triggerFile = JsonSerializer.Deserialize<TriggerFile>(File.ReadAllText(path), new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			});
			if (triggerFile == null)
			{
				return;
			}
			string key;
			foreach (KeyValuePair<string, WeaponGroupJson> item in triggerFile.Groups ?? new Dictionary<string, WeaponGroupJson>())
			{
				item.Deconstruct(out key, out var value);
				string text = key;
				WeaponGroupJson weaponGroupJson = value;
				string[] items = weaponGroupJson.Items;
				if (items != null && items.Length > 0)
				{
					_groups[text] = new WeaponGroup(string.IsNullOrWhiteSpace(weaponGroupJson.Title) ? text.ToUpperInvariant() : weaponGroupJson.Title, weaponGroupJson.Items);
				}
			}
			foreach (KeyValuePair<string, string> item2 in triggerFile.Buttons ?? new Dictionary<string, string>())
			{
				item2.Deconstruct(out key, out var value2);
				string text2 = key;
				string text3 = value2;
				if (!string.IsNullOrWhiteSpace(text2) && !string.IsNullOrWhiteSpace(text3))
				{
					_triggers[text2] = text3.Trim();
					if (text3.StartsWith("open:", StringComparison.OrdinalIgnoreCase) || text3.StartsWith("weapons:", StringComparison.OrdinalIgnoreCase) || text3.Equals("home", StringComparison.OrdinalIgnoreCase))
					{
						_navButtons.Add(new KeyValuePair<string, string>(text2, text3.Trim()));
					}
				}
			}
			base.Logger.LogInformation("{File}: {Buttons} buttons, {Groups} weapon groups.", "wp_triggers.json", triggerFile.Buttons?.Count ?? 0, _groups.Count);
		}
		catch (Exception ex)
		{
			base.Logger.LogWarning("{File} could not be read: {Message}", "wp_triggers.json", ex.Message);
		}
	}

	private void ClickKnifeCard(CCSPlayerController player, int cell)
	{
		int slot = player.Slot;
		if (_menu[slot] == null)
		{
			return;
		}
		int[] array = KnifeOrder(slot);
		int num = _page[slot] * 18 + cell;
		if (num >= 0 && num < array.Length)
		{
			string text = KnifeNames[array[num]];
			ChatMenuOption chatMenuOption = Find(_menu[slot], text);
			if (chatMenuOption != null && !chatMenuOption.Disabled)
			{
				OpenKnife(player, text, chatMenuOption);
			}
		}
	}

	private void ClickLiveCard(CCSPlayerController player, int cell)
	{
		int slot = player.Slot;
		if (_menu[slot] == null)
		{
			return;
		}
		List<int> list = Visible(slot, _menu[slot]);
		int num = _page[slot] * 18 + cell;
		if (num >= 0 && num < list.Count)
		{
			ChatMenuOption chatMenuOption = _menu[slot].MenuOptions[list[num]];
			if (!chatMenuOption.Disabled)
			{
				Choose(player, chatMenuOption);
			}
		}
	}

	private void GoHome(CCSPlayerController player)
	{
		int slot = player.Slot;
		_active[slot] = "home";
		_fromKnife[slot] = false;
		_menu[slot] = null;
		_parent[slot] = null;
		_view[slot] = 0;
		_page[slot] = 0;
		Apply(player);
	}

	private void OpenCategory(CCSPlayerController player, string command)
	{
		Server.NextFrame(delegate
		{
			if (player.IsValid)
			{
				ClearCooldown(player.Slot);
				player.ExecuteClientCommandFromServer(command);
			}
		});
	}

	private void OpenKnife(CCSPlayerController player, string knifeName, ChatMenuOption option)
	{
		int slot = player.Slot;
		if (!string.Equals(EquippedKnife(slot), knifeName, StringComparison.OrdinalIgnoreCase))
		{
			_equipSkin[slot] = "";
			_knifePlain[slot] = null;
		}
		_equipKnife[slot] = knifeName;
		_knifePage[slot] = _page[slot];
		_parent[slot] = "css_knife";
		if (knifeName.Equals("Default Knife", StringComparison.OrdinalIgnoreCase))
		{
			_equipSkin[slot] = "";
			_knifePlain[slot] = null;
			_fromKnife[slot] = false;
			_page[slot] = 0;
			Apply(player);
			Server.NextFrame(delegate
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
		Server.NextFrame(delegate
		{
			if (player.IsValid && _open[slot])
			{
				InvokeSelect(player, option);
				ClearCooldown(slot);
				player.ExecuteClientCommandFromServer("css_skins");
			}
		});
	}

	private void Choose(CCSPlayerController player, ChatMenuOption option)
	{
		int slot = player.Slot;
		string plain = Plain(option.Text ?? "");
		string title = Plain(_menu[slot]?.Title ?? "");
		bool skin = IsSkinMenu(title);
		Remember(slot, title, plain);
		int page = _page[slot];
		Server.NextFrame(delegate
		{
			if (player.IsValid && _open[slot])
			{
				InvokeSelect(player, option);
				if (player.IsValid)
				{
					if (skin)
					{
						_fromKnife[slot] = false;
						_restorePage[slot] = 0;
						_page[slot] = 0;
						Later(Config.OpenDelay, delegate
						{
							if (player.IsValid)
							{
								ClearCooldown(slot);
								player.ExecuteClientCommandFromServer(_parent[slot] ?? "css_skins");
							}
						});
					}
					else if (_open[slot] && _menu[slot] != null && string.Equals(Plain(_menu[slot].Title), title, StringComparison.OrdinalIgnoreCase))
					{
						_page[slot] = page;
						Apply(player);
					}
				}
			}
		});
	}

	private void ApplyDatabase(int slot, WpLoadout loadout)
	{
		if (!string.IsNullOrEmpty(loadout.KnifeName))
		{
			_equipKnife[slot] = loadout.KnifeName;
		}
		if (!string.IsNullOrEmpty(loadout.KnifePlain))
		{
			_knifePlain[slot] = loadout.KnifePlain;
		}
		if (!string.IsNullOrEmpty(loadout.KnifeSkin))
		{
			_equipSkin[slot] = loadout.KnifeSkin;
		}
		_weaponSkin[slot] = loadout.WeaponSkins;
		for (int cat = 2; cat <= 9; cat++)
		{
			if (!string.IsNullOrEmpty(loadout.Side[cat]))
			{
				_side[slot, cat] = loadout.Side[cat];
			}
			if (!string.IsNullOrEmpty(loadout.Art[cat]))
			{
				_catArt[slot, cat] = loadout.Art[cat];
			}
		}
	}

	private void Remember(int slot, string? title, string plain)
	{
		Dictionary<string, string>[] pickedBy = _pickedBy;
		if (pickedBy[slot] == null)
		{
			pickedBy[slot] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}
		_pickedBy[slot][title ?? ""] = plain;
		int num = Category(slot);
		if (IsSkinMenu(title))
		{
			string text = SkinLabel(plain);
			string text2 = KnifeIn(title);
			if (text2 != null)
			{
				_equipKnife[slot] = text2;
				_equipSkin[slot] = FinishName(plain);
				_knifePlain[slot] = plain;
			}
			else if (num >= 2 && num <= 9)
			{
				string weapon = WeaponIn(title);
				if (weapon != null)
				{
					if (_weaponSkin[slot] == null)
					{
						_weaponSkin[slot] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					}
					_weaponSkin[slot][weapon] = plain;
					_side[slot, num] = weapon + "  |  " + text;
				}
				else
				{
					string text3 = _typePick[slot];
					_side[slot, num] = (string.IsNullOrEmpty(text3) ? text : (text3 + "  |  " + text));
				}
				_catArt[slot, num] = IconClass(plain);
			}
		}
		else if (num >= 2 && num <= 9)
		{
			Split(plain, out string name, out string _);
			_typePick[slot] = name;
			_side[slot, num] = name;
			_catArt[slot, num] = IconClass(plain);
		}
	}

	private int Pages(int slot)
	{
		if (_view[slot] == 1)
		{
			return Math.Max(1, (int)Math.Ceiling((double)KnifeNames.Length / 18.0));
		}
		IMenu menu = _menu[slot];
		if (menu == null || _view[slot] != 2)
		{
			return 1;
		}
		return Math.Max(1, (int)Math.Ceiling((double)Visible(slot, menu).Count / 18.0));
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

	private void SetSide(CCSPlayerController player, bool terrorist)
	{
		int slot = player.Slot;
		_wantTerrorist[slot] = terrorist;
		if (_open[slot] && string.Equals(_active[slot], "open:agents", StringComparison.OrdinalIgnoreCase))
		{
			OpenCategory(player, "css_agents");
		}
		else
		{
			Apply(player);
		}
	}

	private void InvokeSelect(CCSPlayerController player, ChatMenuOption option)
	{
		option.OnSelect?.Invoke(player, option);
	}

	private bool WantsOtherTeam(CCSPlayerController player)
	{
		CsTeam team = player.Team;
		if (team - 2 > CsTeam.Spectator)
		{
			return false;
		}
		return _wantTerrorist[player.Slot] != (player.Team == CsTeam.Terrorist);
	}

	private IDisposable? SwapLoadout(int slot)
	{
		BindLoadout();
		if (_teamTables.Count == 0)
		{
			if (!_loadoutWarned)
			{
				_loadoutWarned = true;
				base.Logger.LogWarning("WeaponPaints не отдаёт лоадаут отдельно для T и КТ. Свитч фильтрует агентов, скины пишутся в текущую команду.");
			}
			return null;
		}
		List<Action> list = new List<Action>();
		foreach (FieldInfo teamTable in _teamTables)
		{
			object value = teamTable.GetValue(null);
			if (value != null)
			{
				SwapTeamKeys(value, slot);
				object captured = value;
				list.Add(delegate
				{
					SwapTeamKeys(captured, slot);
				});
			}
		}
		return new LoadoutSwap(list);
	}

	private static void BindLoadout()
	{
		if (_loadoutBound)
		{
			return;
		}
		_loadoutBound = true;
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (int i = 0; i < assemblies.Length; i++)
		{
			Type type = assemblies[i].GetType("WeaponPaints.WeaponPaints");
			if (type == null)
			{
				continue;
			}
			string[] array = new string[4] { "GPlayerWeaponsInfo", "GPlayersKnife", "GPlayersGlove", "GPlayersMusic" };
			foreach (string name in array)
			{
				FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null)
				{
					_teamTables.Add(field);
				}
			}
			break;
		}
	}

	private static bool SwapTeamKeys(object table, int slot)
	{
		if (!TryGetValue(table, slot, out object value) || value == null)
		{
			return false;
		}
		object value2;
		bool num = TryRemove(value, CsTeam.Terrorist, out value2);
		object value3;
		bool flag = TryRemove(value, CsTeam.CounterTerrorist, out value3);
		if (num)
		{
			TryAdd(value, CsTeam.CounterTerrorist, value2);
		}
		if (flag)
		{
			TryAdd(value, CsTeam.Terrorist, value3);
		}
		return num || flag;
	}

	private static bool TryGetValue(object dictionary, object key, out object? value)
	{
		object[] array = new object[2] { key, null };
		MethodInfo method = dictionary.GetType().GetMethod("TryGetValue");
		int result;
		if (method != null)
		{
			object obj = method.Invoke(dictionary, array);
			result = ((obj is bool && (bool)obj) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		value = array[1];
		return (byte)result != 0;
	}

	private static bool TryRemove(object dictionary, object key, out object? value)
	{
		object[] array = new object[2] { key, null };
		MethodInfo method = dictionary.GetType().GetMethod("TryRemove");
		int result;
		if (method != null)
		{
			object obj = method.Invoke(dictionary, array);
			result = ((obj is bool && (bool)obj) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		value = array[1];
		return (byte)result != 0;
	}

	private static void TryAdd(object dictionary, object key, object? value)
	{
		dictionary.GetType().GetMethod("TryAdd")?.Invoke(dictionary, new object[2] { key, value });
	}

	private static ChatMenuOption? TeamChoice(IMenu menu, bool terrorist)
	{
		int count = menu.MenuOptions.Count;
		if ((count < 2 || count > 6) ? true : false)
		{
			return null;
		}
		ChatMenuOption result = null;
		int num = 0;
		foreach (ChatMenuOption menuOption in menu.MenuOptions)
		{
			int num2 = TeamSide(menuOption.Text ?? "");
			if (num2 == 0)
			{
				return null;
			}
			num |= num2;
			if (num2 == (terrorist ? 1 : 2))
			{
				result = menuOption;
			}
		}
		if (num != 3)
		{
			return null;
		}
		return result;
	}

	private static int TeamSide(string text)
	{
		string text2 = Plain(text).ToLowerInvariant();
		bool flag = text2.Contains("counter") || text2.Contains("спецназ") || text2.Contains("контр");
		if (!flag)
		{
			bool flag2 = ((text2 == "ct" || text2 == "кт") ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return 2;
		}
		if (text2.Contains("terror") || text2.Contains("террор") || text2 == "t")
		{
			return 1;
		}
		return 0;
	}

	private List<int> VisibleAgents(int slot, IMenu menu, List<int> all)
	{
		if (TeamChoice(menu, _wantTerrorist[slot]) != null)
		{
			return all.Where((int index) => TeamSide(menu.MenuOptions[index].Text ?? "") == (_wantTerrorist[slot] ? 1 : 2)).ToList();
		}
		int num = (_wantTerrorist[slot] ? 1 : 2);
		List<int> list = new List<int>();
		List<int> list2 = new List<int>();
		foreach (int item in all)
		{
			int num2 = AgentSide(menu.MenuOptions[item].Text ?? "");
			if (num2 == num)
			{
				list.Add(item);
			}
			else if (num2 != 0)
			{
				list2.Add(item);
			}
		}
		if (list.Count > 0 && list2.Count > 0)
		{
			return list;
		}
		if (list.Count == 0 && list2.Count > 0 && !_agentsWarned)
		{
			_agentsWarned = true;
			base.Logger.LogWarning("WeaponPaints собрал агентов по команде в матче. Свитч T/КТ их не пересобирает.");
		}
		return all;
	}

	private static int AgentSide(string text)
	{
		string text2 = Plain(text);
		if (AgentsT.Contains(text2))
		{
			return 1;
		}
		if (AgentsCt.Contains(text2))
		{
			return 2;
		}
		return TeamSide(text2);
	}

	private List<int> Visible(int slot, IMenu menu)
	{
		List<int> list = Enumerable.Range(0, menu.MenuOptions.Count).ToList();
		WeaponGroup weaponGroup = _filter[slot];
		if (weaponGroup == null || !IsWeaponList(menu.Title))
		{
			return list;
		}
		List<int> list2 = new List<int>(weaponGroup.Items.Length);
		string[] items = weaponGroup.Items;
		foreach (string item in items)
		{
			int num = list.FindIndex((int index) => string.Equals(Plain(menu.MenuOptions[index].Text ?? ""), item, StringComparison.OrdinalIgnoreCase));
			if (num >= 0 && !list2.Contains(num))
			{
				list2.Add(num);
			}
		}
		return list2;
	}

	private void Show(CCSPlayerController player)
	{
		CCSCustomHudLayout layout = _layout;
		if (layout != null && ((CEntityInstance)(object)layout).IsValid)
		{
			Apply(player);
			CCSCustomHudLayoutExtensions.SetHasClassForPlayer(_layout, player, "wp-root", "wp-hidden", false);
			base.Logger.LogInformation("PMM locker shown to {Name}", player.PlayerName);
		}
	}

	private void Apply(CCSPlayerController player)
	{
		bool flag = !(((CEntityInstance)(object)_layout)?.IsValid ?? false);
		if (!flag)
		{
			int slot = player.Slot;
			bool flag2 = ((slot < 0 || slot >= 64) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return;
		}
		CCSCustomHudLayout layout = _layout;
		int slot2 = player.Slot;
		int num = _view[slot2];
		int num2 = Pages(slot2);
		int num3 = Math.Clamp(_page[slot2], 0, num2 - 1);
		_page[slot2] = num3;
		for (int i = 0; i < Views.Length; i++)
		{
			CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, "wp-root", Views[i], i == num);
		}
		int num4 = Category(slot2);
		for (int j = 0; j < Cats.Length; j++)
		{
			CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, "wp-root", Cats[j], j == num4);
		}
		bool sideFirst = _sidePage[slot2] == 0;
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, "wp-root", "side-p0", sideFirst);
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, "wp-root", "side-p1", !sideFirst);
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, "wp-side-prev", "wp-dim", sideFirst);
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, "wp-side-next", "wp-dim", !sideFirst);
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, "wp-side-num-0", "is-cur", sideFirst);
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, "wp-side-num-1", "is-cur", !sideFirst);
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, "wp-root", "single-page", num2 <= 1);
		string b = _active[slot2] ?? "home";
		foreach (var (text3, a) in _navButtons)
		{
			CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, text3, "is-cur", string.Equals(a, b, StringComparison.OrdinalIgnoreCase));
		}
		string text4 = KnifeLine(slot2);
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-side-knife", "side_knife", text4);
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-home-knife", "side_knife", text4);
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-side-gloves", "side_gloves", SideText(slot2, 2));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-home-gloves", "side_gloves", SideText(slot2, 2));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-side-pistols", "side_pistols", SideText(slot2, 3));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-home-pistols", "side_pistols", SideText(slot2, 3));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-side-agents", "side_agents", SideText(slot2, 8));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-home-agents", "side_agents", SideText(slot2, 8));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-side-rifles", "side_rifles", SideText(slot2, 4));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-home-rifles", "side_rifles", SideText(slot2, 4));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-side-snipers", "side_snipers", SideText(slot2, 5));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-home-snipers", "side_snipers", SideText(slot2, 5));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-side-smg", "side_smg", SideText(slot2, 6));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-home-smg", "side_smg", SideText(slot2, 6));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-side-heavy", "side_heavy", SideText(slot2, 7));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-home-heavy", "side_heavy", SideText(slot2, 7));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-side-music", "side_music", SideText(slot2, 9));
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-home-music", "side_music", SideText(slot2, 9));
		string cls = KnifeArtClass(slot2);
		PaintArt(layout, player, "wp-sw-knife", ref _swatchKnifeArt[slot2], cls);
		PaintArt(layout, player, "wp-tile-art-knife", ref _tileKnifeArt[slot2], cls);
		PaintArt(layout, player, "wp-tile-art-gloves", ref _tileArt[slot2, 2], _catArt[slot2, 2]);
		PaintArt(layout, player, "wp-tile-art-pistols", ref _tileArt[slot2, 3], _catArt[slot2, 3]);
		PaintArt(layout, player, "wp-tile-art-rifles", ref _tileArt[slot2, 4], _catArt[slot2, 4]);
		PaintArt(layout, player, "wp-tile-art-snipers", ref _tileArt[slot2, 5], _catArt[slot2, 5]);
		PaintArt(layout, player, "wp-tile-art-smg", ref _tileArt[slot2, 6], _catArt[slot2, 6]);
		PaintArt(layout, player, "wp-tile-art-heavy", ref _tileArt[slot2, 7], _catArt[slot2, 7]);
		PaintArt(layout, player, "wp-tile-art-agents", ref _tileArt[slot2, 8], _catArt[slot2, 8]);
		PaintArt(layout, player, "wp-tile-art-music", ref _tileArt[slot2, 9], _catArt[slot2, 9]);
		switch (num)
		{
		case 0:
			SetHead(layout, player, "WEAPON SKINS", "LOADOUT", "Pick what to change");
			break;
		case 1:
			FillKnife(layout, player);
			break;
		default:
			FillLive(layout, player);
			break;
		}
		FillPager(layout, player, num3, num2);
	}

	private static void SetHead(CCSCustomHudLayout layout, CCSPlayerController player, string title, string crumb, string hint)
	{
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-title", "wp_title", title);
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-crumb", "wp_crumb", crumb);
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, "wp-hint", "wp_hint", hint);
	}

	private void FillPager(CCSCustomHudLayout layout, CCSPlayerController player, int page, int pages)
	{
		int slot = player.Slot;
		int num = 0;
		if (pages > 7)
		{
			num = Math.Clamp(page - 3, 0, pages - 7);
		}
		_pagerStart[slot] = num;
		for (int i = 0; i < 7; i++)
		{
			int num2 = num + i;
			bool flag = pages > 1 && num2 < pages;
			string text = $"wp-num-{i}";
			CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, text, "wp-off", !flag);
			CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, text, "is-cur", flag && num2 == page);
			CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, text, $"num{i}", (num2 + 1).ToString());
		}
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, "wp-prev", "wp-dim", page <= 0);
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, "wp-next", "wp-dim", page >= pages - 1);
	}

	private void FillKnife(CCSCustomHudLayout layout, CCSPlayerController player)
	{
		int slot = player.Slot;
		int[] array = KnifeOrder(slot);
		int num = array[0];
		int num2 = _page[slot];
		SetHead(layout, player, "KNIFE", KnifeLine(slot), $"{KnifeNames.Length} knives  ·  pick one, then its finish");
		for (int i = 0; i < 18; i++)
		{
			int num3 = num2 * 18 + i;
			bool flag = num3 < array.Length;
			string name = "";
			string sub = "";
			string art = null;
			bool flag2 = false;
			bool flag3 = false;
			if (flag)
			{
				int num4 = array[num3];
				flag2 = num4 == num;
				flag3 = num4 == 1;
				name = KnifeNames[num4];
				art = (flag2 ? KnifeArtClass(slot) : ("knife-" + num4));
				sub = (flag3 ? "STOCK" : (flag2 ? KnifeSub(slot) : $"{KnifeFinishes[num4]} FINISHES"));
			}
			ApplyCard(layout, player, i, flag, flag2, flag3, art, name, sub);
		}
	}

	private void FillLive(CCSCustomHudLayout layout, CCSPlayerController player)
	{
		int slot = player.Slot;
		IMenu menu = _menu[slot];
		if (menu == null)
		{
			return;
		}
		int num = _page[slot];
		string text = Plain(menu.Title);
		bool flag = IsSkinMenu(text);
		bool flag2 = IsWeaponList(text);
		string text2 = (flag ? KnifeIn(text) : null);
		List<int> list = Visible(slot, menu);
		int count = list.Count;
		WeaponGroup weaponGroup = (flag2 ? _filter[slot] : null);
		if (text2 != null)
		{
			SetHead(layout, player, text2, "KNIFE  /  FINISHES", $"{count} finishes  ·  click one to equip");
		}
		else if (flag)
		{
			SetHead(layout, player, text, "SKINS", $"{count} finishes  ·  click one to equip");
		}
		else if (weaponGroup != null)
		{
			SetHead(layout, player, weaponGroup.Title, "WEAPONS  /  " + weaponGroup.Title, "Pick a weapon, then its finish");
		}
		else if (flag2)
		{
			SetHead(layout, player, text, "WEAPONS", "Pick a weapon, then its finish");
		}
		else
		{
			SetHead(layout, player, text, "LOCKER", "Click to equip");
		}
		string text3 = null;
		string value;
		if (flag && text2 != null)
		{
			if (string.Equals(text2, _equipKnife[slot], StringComparison.OrdinalIgnoreCase))
			{
				text3 = _knifePlain[slot];
			}
		}
		else if (!flag2 && _pickedBy[slot] != null && _pickedBy[slot].TryGetValue(text, out value))
		{
			text3 = value;
		}
		for (int i = 0; i < 18; i++)
		{
			int num2 = num * 18 + i;
			bool flag3 = num2 < count;
			string text4 = "";
			string name = "";
			string sub = "";
			string art = null;
			if (flag3)
			{
				text4 = Plain(menu.MenuOptions[list[num2]].Text ?? "");
				Split(flag ? StripPrefix(text4) : text4, out name, out sub);
				if (int.TryParse(sub, out var _))
				{
					sub = "";
				}
				art = IconClass(text4) ?? ((text2 != null) ? IconClass("★ " + text2 + " | " + text4) : null);
				if (flag2 && _weaponSkin[slot] != null && _weaponSkin[slot].TryGetValue(text4, out string saved))
				{
					sub = SkinLabel(saved);
					art = IconClass(saved) ?? art;
					text3 = text4;
				}
			}
			bool equipped = flag3 && !string.IsNullOrEmpty(text3) && string.Equals(text4, text3, StringComparison.OrdinalIgnoreCase);
			ApplyCard(layout, player, i, flag3, equipped, stock: false, art, name, sub);
		}
	}

	private void ApplyCard(CCSCustomHudLayout layout, CCSPlayerController player, int i, bool shown, bool equipped, bool stock, string? art, string name, string sub)
	{
		int slot = player.Slot;
		string text = $"wp-live-{i}";
		string text2 = $"wp-live-art-{i}";
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, text, "wp-off", !shown);
		PaintArt(layout, player, text2, ref _icon[slot, i], shown ? art : null);
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, text2, "wp-noart", !shown || string.IsNullOrEmpty(art));
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, text, "is-eq", shown && equipped);
		CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, text, "is-stock", shown && stock);
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, text, $"live{i}", name);
		CCSCustomHudLayoutExtensions.SetDialogVariableStringForPlayer(layout, player, text, $"livesub{i}", sub);
	}

	private static void PaintArt(CCSCustomHudLayout layout, CCSPlayerController player, string panel, ref string? previous, string? cls)
	{
		if (!string.IsNullOrEmpty(previous) && previous != cls)
		{
			CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, panel, previous, false);
		}
		if (!string.IsNullOrEmpty(cls))
		{
			CCSCustomHudLayoutExtensions.SetHasClassForPlayer(layout, player, panel, cls, true);
		}
		previous = cls;
	}

	private void Hide(CCSPlayerController player)
	{
		int slot = player.Slot;
		if (slot >= 0 && slot < 64)
		{
			_token[player.Slot]++;
			_open[player.Slot] = false;
			_menu[player.Slot] = null;
			_filter[player.Slot] = null;
		}
		CCSCustomHudLayout layout = _layout;
		if (layout != null && ((CEntityInstance)(object)layout).IsValid && player.IsValid)
		{
			CCSCustomHudLayoutExtensions.SetInputCaptureEnabled(_layout, player, false);
			CCSCustomHudLayoutExtensions.SetHasClassForPlayer(_layout, player, "wp-root", "wp-hidden", true);
		}
	}

	private void ResetState(int slot)
	{
		_equipKnife[slot] = null;
		_equipSkin[slot] = null;
		_knifePlain[slot] = null;
		_typePick[slot] = null;
		_pickedBy[slot] = null;
		_parent[slot] = null;
		_drill[slot] = null;
		_swatchKnifeArt[slot] = null;
		_tileKnifeArt[slot] = null;
		_fromKnife[slot] = false;
		_restorePage[slot] = -1;
		_filter[slot] = null;
		_active[slot] = null;
		_weaponSkin[slot] = null;
		_sidePage[slot] = 0;
		for (int i = 0; i < 11; i++)
		{
			_side[slot, i] = null;
			_catArt[slot, i] = null;
			_tileArt[slot, i] = null;
		}
		for (int j = 0; j < 18; j++)
		{
			_icon[slot, j] = null;
		}
	}

	private bool EnsureLayout()
	{
		CCSCustomHudLayout layout = _layout;
		if (layout != null && ((CEntityInstance)(object)layout).IsValid)
		{
			return true;
		}
		CCSCustomHudLayout val = Utilities.CreateEntityByName<CCSCustomHudLayout>("custom_hud_layout");
		if ((CEntityInstance?)(object)val == null || ((NativeEntity)(object)val).Handle == IntPtr.Zero)
		{
			return false;
		}
		val.StrLayout = "panorama/layout/custom_game/wp.xml";
		((CBaseEntity)(object)val).DispatchSpawn();
		if (!((CEntityInstance)(object)val).IsValid)
		{
			return false;
		}
		_layout = val;
		return true;
	}

	private string EquippedKnife(int slot)
	{
		if (!string.IsNullOrEmpty(_equipKnife[slot]))
		{
			return _equipKnife[slot];
		}
		return "Default Knife";
	}

	private int EquippedKnifeIndex(int slot)
	{
		string value = EquippedKnife(slot);
		for (int i = 0; i < KnifeNames.Length; i++)
		{
			if (KnifeNames[i].Equals(value, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return 1;
	}

	private int[] KnifeOrder(int slot)
	{
		int num = EquippedKnifeIndex(slot);
		List<int> list = new List<int>(KnifeBase.Length) { num };
		int[] knifeBase = KnifeBase;
		foreach (int num2 in knifeBase)
		{
			if (num2 != num)
			{
				list.Add(num2);
			}
		}
		return list.ToArray();
	}

	private string KnifeLine(int slot)
	{
		string text = EquippedKnife(slot);
		string text2 = _equipSkin[slot] ?? "";
		if (!string.IsNullOrEmpty(text2))
		{
			return text + "  |  " + text2;
		}
		return text;
	}

	private string KnifeSub(int slot)
	{
		if (!string.IsNullOrEmpty(_equipSkin[slot]))
		{
			return _equipSkin[slot];
		}
		return "VANILLA";
	}

	private string KnifeArtClass(int slot)
	{
		int num = EquippedKnifeIndex(slot);
		string text = _knifePlain[slot];
		if (num != 1 && !string.IsNullOrEmpty(text) && KnifeIcon(KnifeNames[num], text, out string cls))
		{
			return cls;
		}
		return "knife-" + num;
	}

	// Menu text is "★ Butterfly Knife | Fade (38)". icons.json may differ by spaces, so match the knife name and the paint id.
	private static bool KnifeIcon(string knifeName, string plain, out string cls)
	{
		cls = "";
		if (Icons.TryGetValue(plain, out string exact))
		{
			cls = exact;
			return true;
		}
		int paint = TrailingPaint(plain);
		string? bestKey = null;
		foreach (KeyValuePair<string, string> pair in Icons)
		{
			if (!pair.Key.Contains(knifeName, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (paint >= 0 && !pair.Key.Contains("(" + paint + ")", StringComparison.Ordinal))
			{
				continue;
			}
			if (paint < 0 && !pair.Key.Contains(plain, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			if (bestKey == null || pair.Key.Length < bestKey.Length)
			{
				bestKey = pair.Key;
				cls = pair.Value;
			}
		}
		return bestKey != null;
	}

	private static string FinishName(string plain)
	{
		int bar = plain.LastIndexOf('|');
		string finish = bar >= 0 ? plain.Substring(bar + 1).Trim() : plain;
		int open = finish.LastIndexOf('(');
		if (open > 0 && finish.EndsWith(')'))
		{
			finish = finish.Substring(0, open).Trim();
		}
		return finish.Length == 0 ? plain : finish;
	}

	private static int TrailingPaint(string text)
	{
		int open = text.LastIndexOf('(');
		if (open < 0 || !text.EndsWith(')'))
		{
			return -1;
		}
		string inner = text.Substring(open + 1, text.Length - open - 2).Trim();
		return int.TryParse(inner, out int paint) ? paint : -1;
	}

	private string SideText(int slot, int cat)
	{
		if (!string.IsNullOrEmpty(_side[slot, cat]))
		{
			return _side[slot, cat];
		}
		return "Default";
	}

	private void SyncSidePage(int slot)
	{
		int cat = (_active[slot] ?? "") switch
		{
			"weapons:smg" or "weapons:heavy" or "open:agents" or "open:music" => 1,
			_ => 0
		};
		_sidePage[slot] = cat;
	}

	private static string? WeaponIn(string? title)
	{
		string plain = Plain(title ?? "");
		string? found = null;
		foreach (WeaponGroup group in DefaultGroups.Values)
		{
			foreach (string name in group.Items)
			{
				if (plain.Contains(name, StringComparison.OrdinalIgnoreCase) && (found == null || name.Length > found.Length))
				{
					found = name;
				}
			}
		}
		return found;
	}

	private int Category(int slot)
	{
		if (_view[slot] == 0)
		{
			return 0;
		}
		return (_active[slot] ?? "") switch
		{
			"weapons:rifles" => 4, 
			"weapons:snipers" => 5, 
			"weapons:smg" => 6, 
			"weapons:heavy" => 7, 
			"weapons:pistols" => 3, 
			"open:music" => 9, 
			"open:agents" => 8, 
			"open:knife" => 1, 
			"open:gloves" => 2, 
			_ => CategoryOf(_parent[slot]), 
		};
	}

	private static int CategoryOf(string? command)
	{
		return command switch
		{
			"css_knife" => 1, 
			"css_gloves" => 2, 
			"css_skins" => 3, 
			"css_agents" => 8, 
			"css_music" => 9, 
			_ => 10, 
		};
	}

	private static string? ParentFor(string title)
	{
		string text = Plain(title);
		if (IsKnifeMenu(text))
		{
			return "css_knife";
		}
		if (IsWeaponList(text))
		{
			return "css_skins";
		}
		if (HasAny(text, "gloves menu", "меню перчаток"))
		{
			return "css_gloves";
		}
		if (HasAny(text, "agents menu", "меню агентов"))
		{
			return "css_agents";
		}
		if (HasAny(text, "music menu", "меню музыки"))
		{
			return "css_music";
		}
		if (HasAny(text, "pins menu", "меню пинов"))
		{
			return "css_pins";
		}
		return null;
	}

	private static bool HasAny(string text, string first, string second)
	{
		if (!text.Contains(first, StringComparison.OrdinalIgnoreCase))
		{
			return text.Contains(second, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool Matches(string title)
	{
		string text = Plain(title);
		string[] match = Match;
		foreach (string value in match)
		{
			if (text.Contains(value, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private void LoadIcons()
	{
		try
		{
			string path = Path.Combine(base.ModuleDirectory, "icons.json");
			if (!File.Exists(path))
			{
				base.Logger.LogWarning("icons.json was not found next to PMM_WeaponPaints.dll.");
				return;
			}
			Dictionary<string, string> dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
			if (dictionary != null)
			{
				Icons = new Dictionary<string, string>(dictionary, StringComparer.Ordinal);
			}
		}
		catch (Exception ex)
		{
			base.Logger.LogWarning("icons.json could not be read: {Message}", ex.Message);
		}
	}

	private static string? IconClass(string name)
	{
		if (!Icons.TryGetValue(name, out string value))
		{
			return null;
		}
		return value;
	}

	private static bool IsWeaponList(string? title)
	{
		string text = Plain(title ?? "");
		if (!text.Contains("weapon menu", StringComparison.OrdinalIgnoreCase))
		{
			return text.Contains("меню оружия", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool IsSkinMenu(string? title)
	{
		string text = Plain(title ?? "");
		if (!text.Contains("select skin", StringComparison.OrdinalIgnoreCase))
		{
			return text.Contains("выберите скин", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool IsKnifeMenu(string title)
	{
		if (IsSkinMenu(title))
		{
			return false;
		}
		string text = Plain(title);
		if (!text.Contains("knife menu", StringComparison.OrdinalIgnoreCase))
		{
			return text.Contains("меню нож", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static string? KnifeIn(string? title)
	{
		string text = Plain(title ?? "");
		string[] knifeNames = KnifeNames;
		foreach (string text2 in knifeNames)
		{
			if (text.Contains(text2, StringComparison.OrdinalIgnoreCase))
			{
				return text2;
			}
		}
		return null;
	}

	private static void ClearCooldown(int slot)
	{
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (int i = 0; i < assemblies.Length; i++)
		{
			Type type = assemblies[i].GetType("WeaponPaints.WeaponPaints");
			if (!(type == null))
			{
				if (type.GetField("CommandsCooldown", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is Dictionary<int, DateTime> dictionary)
				{
					dictionary[slot] = DateTime.MinValue;
				}
				break;
			}
		}
	}

	private static ChatMenuOption? Find(IMenu menu, string name)
	{
		foreach (ChatMenuOption menuOption in menu.MenuOptions)
		{
			if (string.Equals(Plain(menuOption.Text ?? ""), name, StringComparison.OrdinalIgnoreCase))
			{
				return menuOption;
			}
		}
		return null;
	}

	private static string StripPrefix(string text)
	{
		int num = text.IndexOf(" | ", StringComparison.Ordinal);
		if (num < 0)
		{
			return text;
		}
		int num2 = num + 3;
		return text.Substring(num2, text.Length - num2).Trim();
	}

	private static string SkinLabel(string plain)
	{
		string text = StripPrefix(plain);
		Split(text, out string name, out string sub);
		if (!string.IsNullOrEmpty(sub) && !int.TryParse(sub, out var _))
		{
			return text;
		}
		return name;
	}

	private static void Split(string text, out string name, out string sub)
	{
		int num = text.LastIndexOf('(');
		if (num > 0 && text.EndsWith(')'))
		{
			name = text.Substring(0, num).Trim();
			int num2 = num + 1;
			sub = text.Substring(num2, text.Length - 1 - num2).Trim();
		}
		else
		{
			name = text;
			sub = "";
		}
	}

	private static string Plain(string text)
	{
		string text2 = text ?? "";
		string[] colors = Colors;
		foreach (string text3 in colors)
		{
			text2 = text2.Replace("{ " + text3 + "}", "", StringComparison.OrdinalIgnoreCase);
			text2 = text2.Replace("{" + text3 + "}", "", StringComparison.OrdinalIgnoreCase);
			text2 = text2.Replace("[color:" + text3 + "]", "", StringComparison.OrdinalIgnoreCase);
		}
		return text2.Replace("</font>", "", StringComparison.OrdinalIgnoreCase).Trim();
	}
}
