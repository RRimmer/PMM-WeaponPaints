using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;

namespace PMM_WeaponPaints;

public partial class Plugin
{
	// ------------------------------------------------------------------ redraw

	private void Apply(CCSPlayerController player)
	{
		CCSCustomHudLayout? layout = _layout;
		if (layout == null || !layout.IsValid || player.Slot < 0 || player.Slot >= 64)
		{
			return;
		}
		int slot = player.Slot;
		int team = TeamOf(player);
		_teamIdx[slot] = team;
		int view = _view[slot];

		List<Card> cards = view switch
		{
			ViewKnife => KnifeCards(player),
			ViewAgents => AgentCards(player),
			ViewGloves => GloveTypeCards(player),
			ViewLive => LiveCards(player),
			_ => new List<Card>()
		};
		_cards[slot] = cards;
		int pages = Pages(slot);
		int page = Math.Clamp(_page[slot], 0, pages - 1);
		_page[slot] = page;

		for (int i = 0; i < Views.Length; i++)
		{
			SetClass(layout, player, "wp-root", Views[i], i == view);
		}
		int cat = Category(slot);
		for (int i = 0; i < Cats.Length; i++)
		{
			SetClass(layout, player, "wp-root", Cats[i], i == cat);
		}
		bool sideFirst = _sidePage[slot] == 0;
		SetClass(layout, player, "wp-root", "side-p0", sideFirst);
		SetClass(layout, player, "wp-root", "side-p1", !sideFirst);
		SetClass(layout, player, "wp-side-prev", "wp-dim", sideFirst);
		SetClass(layout, player, "wp-side-next", "wp-dim", !sideFirst);
		SetClass(layout, player, "wp-side-num-0", "is-cur", sideFirst);
		SetClass(layout, player, "wp-side-num-1", "is-cur", !sideFirst);
		SetClass(layout, player, "wp-root", "single-page", pages <= 1);
		SetClass(layout, player, "wp-root", "multi-page", pages > 1);
		SetClass(layout, player, "wp-root", "team-t", team == WpDatabase.TeamT);
		SetClass(layout, player, "wp-root", "team-ct", team == WpDatabase.TeamCt);
		SetClass(layout, player, "wp-root", "lang-ru", Ru);

		string active = _active[slot] ?? "home";
		foreach ((string id, string action) in _navButtons)
		{
			SetClass(layout, player, id, "is-cur", string.Equals(action, active, StringComparison.OrdinalIgnoreCase));
		}

		for (int c = CatKnife; c <= CatPins; c++)
		{
			(string label, string? art) = Summary(slot, team, c);
			string key = CatKeys[c];
			SetVar(layout, player, "wp-side-" + key, "side_" + key, label);
			SetVar(layout, player, "wp-home-" + key, "side_" + key, label);
			PaintArt(layout, player, "wp-sw-" + key, ref _swArt[slot, c], art);
			PaintArt(layout, player, "wp-tile-art-" + key, ref _tileArt[slot, c], art);
		}

		SetHeader(layout, player, cards.Count);
		// Rows hold five cards. Empty cells of the last row stay as invisible ghosts so the row keeps its widths.
		int onPage = Math.Clamp(cards.Count - page * LivePage, 0, LivePage);
		int lastRow = onPage > 0 ? (onPage - 1) / RowSize : -1;
		for (int i = 0; i < LivePage; i++)
		{
			int index = page * LivePage + i;
			Card? card = index < cards.Count ? cards[index] : null;
			RenderCard(layout, player, i, card, team, card == null && i / RowSize == lastRow);
		}
		FillPager(layout, player, page, pages);
	}

	private void RenderCard(CCSCustomHudLayout layout, CCSPlayerController player, int i, Card? card, int team, bool ghost)
	{
		int slot = player.Slot;
		string panel = "wp-live-" + i;
		SetClass(layout, player, panel, "wp-off", card == null && !ghost);
		SetClass(layout, player, panel, "wp-ghost", ghost);
		if (card == null)
		{
			return;
		}
		string artPanel = "wp-live-art-" + i;
		PaintArt(layout, player, artPanel, ref _icon[slot, i], card.Art);
		SetClass(layout, player, artPanel, "wp-noart", string.IsNullOrEmpty(card.Art));
		bool equipped = team == WpDatabase.TeamCt ? card.EqCt : card.EqT;
		SetClass(layout, player, panel, "is-eq", equipped);
		SetClass(layout, player, panel, "is-stock", card.Stock);
		SetClass(layout, player, panel, "on-t", card.EqT);
		SetClass(layout, player, panel, "on-ct", card.EqCt);
		PaintArt(layout, player, panel, ref _rarity[slot, i], card.Rarity > 0 && !card.Stock ? "rar-" + card.Rarity : null);
		SetVar(layout, player, panel, "live" + i, card.Name);
		SetVar(layout, player, panel, "livesub" + i, card.Sub);
	}

	private void SetHeader(CCSCustomHudLayout layout, CCSPlayerController player, int count)
	{
		int slot = player.Slot;
		int team = _teamIdx[slot];
		string title;
		string crumb;
		string hint;
		switch (_view[slot])
		{
			case ViewHome:
				title = T("title.home");
				crumb = T("crumb.home");
				hint = T("hint.home");
				break;
			case ViewKnife:
				title = T("cat.knife");
				crumb = Summary(slot, team, CatKnife).Label;
				hint = T("hint.knife", KnifeNames.Length);
				break;
			case ViewAgents:
				title = T("cat.agents");
				crumb = T("crumb.team", TeamName(team));
				hint = T("hint.agents", Math.Max(0, count - 1), TeamName(team));
				break;
			case ViewGloves:
				title = T("cat.gloves");
				crumb = Summary(slot, team, CatGloves).Label;
				hint = T("hint.gloves", _items.GloveTypes.Count);
				break;
			default:
				LiveHeader(slot, team, count, out title, out crumb, out hint);
				break;
		}
		SetVar(layout, player, "wp-title", "wp_title", title);
		SetVar(layout, player, "wp-crumb", "wp_crumb", crumb);
		SetVar(layout, player, "wp-hint", "wp_hint", hint);
	}

	private void LiveHeader(int slot, int team, int count, out string title, out string crumb, out string hint)
	{
		string text = Plain(_menu[slot]?.Title ?? "");
		if (IsSkinMenu(text))
		{
			string? knife = KnifeIn(text);
			string weapon = knife ?? WeaponIn(text) ?? text;
			title = weapon;
			crumb = T("crumb.finishes", knife != null ? T("cat.knife") : GroupTitleOf(weapon));
			hint = T("hint.finishes", count);
		}
		else if (IsWeaponList(text))
		{
			WeaponGroup? group = _filter[slot];
			title = group != null ? GroupTitle(group) : T("cat.weapons");
			crumb = T("crumb.weapons", title);
			hint = T("hint.weapons");
		}
		else if (IsGlovesMenu(text))
		{
			GloveType? type = _items.FindGloveType(_gloveType[slot]);
			title = type != null ? GloveTypeName(type) : T("cat.gloves");
			crumb = T("crumb.gloves", title);
			hint = T("hint.finishes", count);
		}
		else if (IsMusicMenu(text))
		{
			title = T("cat.music");
			crumb = Summary(slot, team, CatMusic).Label;
			hint = T("hint.equip", count);
		}
		else if (IsPinsMenu(text))
		{
			title = T("cat.pins");
			crumb = Summary(slot, team, CatPins).Label;
			hint = T("hint.pins", Math.Max(0, count - 1));
		}
		else
		{
			title = text;
			crumb = T("cat.locker");
			hint = T("hint.equip", count);
		}
	}

	private void FillPager(CCSCustomHudLayout layout, CCSPlayerController player, int page, int pages)
	{
		int slot = player.Slot;
		int start = pages > PagerButtons ? Math.Clamp(page - PagerButtons / 2, 0, pages - PagerButtons) : 0;
		_pagerStart[slot] = start;
		for (int i = 0; i < PagerButtons; i++)
		{
			int number = start + i;
			bool shown = pages > 1 && number < pages;
			string panel = "wp-num-" + i;
			SetClass(layout, player, panel, "wp-off", !shown);
			SetClass(layout, player, panel, "is-cur", shown && number == page);
			SetVar(layout, player, panel, "num" + i, (number + 1).ToString());
		}
		SetClass(layout, player, "wp-prev", "wp-dim", page <= 0);
		SetClass(layout, player, "wp-next", "wp-dim", page >= pages - 1);
	}

	// ------------------------------------------------------------------ cards

	private List<Card> KnifeCards(CCSPlayerController player)
	{
		int slot = player.Slot;
		TeamData[] teams = Teams(slot);
		int team = _teamIdx[slot];
		List<Card> cards = new();
		foreach (int k in KnifeOrder(slot))
		{
			string name = KnifeNames[k];
			bool stock = k == StockKnife;
			Card card = new()
			{
				Name = name,
				Stock = stock,
				Rarity = stock ? 0 : CovertRarity,
				EqT = KnifeOf(teams[WpDatabase.TeamT]) == name,
				EqCt = KnifeOf(teams[WpDatabase.TeamCt]) == name,
				Art = "knife-" + k
			};
			bool equipped = team == WpDatabase.TeamCt ? card.EqCt : card.EqT;
			if (stock)
			{
				card.Sub = T("stock");
			}
			else if (equipped)
			{
				int def = WpDatabase.DefOf(name);
				int paint = teams[team].PaintOf(def);
				card.Sub = paint > 0 ? (_items.PaintName(paint, Ru) ?? T("vanilla")) : T("vanilla");
				card.Art = paint > 0 ? (IconFor(def, paint) ?? card.Art) : card.Art;
			}
			else
			{
				card.Sub = T("finishes", KnifeFinishes[k]);
			}
			card.Click = p => ClickKnife(p, name);
			cards.Add(card);
		}
		return cards;
	}

	private List<Card> AgentCards(CCSPlayerController player)
	{
		int slot = player.Slot;
		TeamData[] teams = Teams(slot);
		int teamNum = _teamIdx[slot] == WpDatabase.TeamCt ? 3 : 2;
		List<Card> cards = new()
		{
			new Card
			{
				Name = T("default"),
				Sub = TeamName(_teamIdx[slot]),
				Art = _teamIdx[slot] == WpDatabase.TeamCt ? "ag-def-ct" : "ag-def-t",
				Stock = true,
				EqT = teams[WpDatabase.TeamT].Agent == null,
				EqCt = teams[WpDatabase.TeamCt].Agent == null,
				Click = p => ChooseAgent(p, null)
			}
		};
		foreach (AgentEntry agent in _items.Agents.Where(a => a.Team == teamNum))
		{
			string full = (Ru ? agent.Ru : agent.En) ?? agent.En ?? agent.Model;
			SplitBar(full, out string head, out string tail);
			AgentEntry captured = agent;
			cards.Add(new Card
			{
				Name = head,
				Sub = tail.Length > 0 ? tail : Lang.Rarity(Ru, Lang.RarityKind.Agent, agent.Rarity),
				Art = agent.Art,
				Rarity = agent.Rarity,
				EqT = string.Equals(teams[WpDatabase.TeamT].Agent, agent.Model, StringComparison.OrdinalIgnoreCase),
				EqCt = string.Equals(teams[WpDatabase.TeamCt].Agent, agent.Model, StringComparison.OrdinalIgnoreCase),
				Click = p => ChooseAgent(p, captured)
			});
		}
		return cards;
	}

	private List<Card> GloveTypeCards(CCSPlayerController player)
	{
		int slot = player.Slot;
		TeamData[] teams = Teams(slot);
		int team = _teamIdx[slot];
		IMenu? menu = _menu[slot];
		List<Card> cards = new();
		if (menu == null)
		{
			return cards;
		}

		ChatMenuOption? stock = menu.MenuOptions.FirstOrDefault(o => _items.TryGlove(Plain(o.Text), out int def, out _) && def == 0)
			?? menu.MenuOptions.FirstOrDefault(o => Plain(o.Text).Contains("Default", StringComparison.OrdinalIgnoreCase));
		if (stock != null)
		{
			ChatMenuOption option = stock;
			cards.Add(new Card
			{
				Name = T("default"),
				Sub = T("stock"),
				Art = team == WpDatabase.TeamCt ? "gl-def-ct" : "gl-def-t",
				Stock = true,
				EqT = teams[WpDatabase.TeamT].GloveDef == 0,
				EqCt = teams[WpDatabase.TeamCt].GloveDef == 0,
				Click = p => Choose(p, option, t => t.GloveDef = 0)
			});
		}

		foreach (GloveType type in _items.GloveTypes)
		{
			int typeDef = type.Def;
			int count = menu.MenuOptions.Count(o => _items.TryGlove(Plain(o.Text), out int def, out _) && def == typeDef);
			if (count == 0)
			{
				continue;
			}
			Card card = new()
			{
				Name = GloveTypeName(type),
				Rarity = CovertRarity,
				Art = type.Art,
				EqT = teams[WpDatabase.TeamT].GloveDef == typeDef,
				EqCt = teams[WpDatabase.TeamCt].GloveDef == typeDef,
				Click = p =>
				{
					int s = p.Slot;
					_gloveType[s] = typeDef;
					_view[s] = ViewLive;
					_page[s] = 0;
					Apply(p);
				}
			};
			bool equipped = team == WpDatabase.TeamCt ? card.EqCt : card.EqT;
			int paint = teams[team].PaintOf(typeDef);
			if (equipped && paint > 0)
			{
				card.Sub = _items.PaintName(paint, Ru) ?? T("finishes", count);
				card.Art = GloveIcon(typeDef, paint) ?? card.Art;
			}
			else
			{
				card.Sub = T("finishes", count);
			}
			cards.Add(card);
		}
		return cards;
	}

	private List<Card> LiveCards(CCSPlayerController player)
	{
		int slot = player.Slot;
		IMenu? menu = _menu[slot];
		List<Card> cards = new();
		if (menu == null)
		{
			return cards;
		}
		TeamData[] teams = Teams(slot);
		int team = _teamIdx[slot];
		string title = Plain(menu.Title);

		if (IsSkinMenu(title))
		{
			string? knife = KnifeIn(title);
			int def = WpDatabase.DefOf(knife ?? WeaponIn(title));
			foreach (ChatMenuOption option in menu.MenuOptions)
			{
				string text = Plain(option.Text);
				int paint = TrailingPaint(text);
				Split(StripPrefix(text), out string name, out _);
				bool stock = paint <= 0;
				int rarity = stock ? 0 : knife != null ? CovertRarity : _items.SkinRarity(def, paint);
				if (!stock && rarity == 0)
				{
					rarity = 3;
				}
				string? art = IconClass(text) ?? (knife != null ? IconClass("★ " + knife + " | " + text) : null) ?? IconFor(def, paint);
				int p = paint;
				cards.Add(new Card
				{
					Name = name,
					Sub = stock ? T("stock") : (knife != null ? "★ " : "") + Lang.Rarity(Ru, Lang.RarityKind.Weapon, rarity),
					Art = art,
					Rarity = rarity,
					Stock = stock,
					EqT = SkinOn(teams[WpDatabase.TeamT], knife, def, p),
					EqCt = SkinOn(teams[WpDatabase.TeamCt], knife, def, p),
					Click = pl => Choose(pl, option, t =>
					{
						if (knife != null)
						{
							t.Knife = knife;
						}
						if (p > 0)
						{
							t.Paints[def] = p;
						}
						else
						{
							t.Paints.Remove(def);
						}
					})
				});
			}
			return cards;
		}

		if (IsWeaponList(title))
		{
			foreach (ChatMenuOption option in VisibleWeapons(slot, menu))
			{
				string text = Plain(option.Text);
				int def = WpDatabase.DefOf(text);
				int paint = teams[team].PaintOf(def);
				bool skinned = paint > 0;
				int rarity = skinned ? Math.Max(1, _items.SkinRarity(def, paint)) : 0;
				cards.Add(new Card
				{
					Name = text,
					Sub = skinned ? (_items.PaintName(paint, Ru) ?? Lang.Rarity(Ru, Lang.RarityKind.Weapon, rarity)) : T("default"),
					Art = (skinned ? IconFor(def, paint) : null) ?? IconClass(text),
					Rarity = rarity,
					Stock = !skinned,
					EqT = teams[WpDatabase.TeamT].PaintOf(def) > 0,
					EqCt = teams[WpDatabase.TeamCt].PaintOf(def) > 0,
					Click = pl => Choose(pl, option, null)
				});
			}
			return cards;
		}

		if (IsGlovesMenu(title))
		{
			int type = _gloveType[slot];
			foreach (ChatMenuOption option in menu.MenuOptions)
			{
				string text = Plain(option.Text);
				if (!_items.TryGlove(text, out int def, out int paint) || def == 0 || (type >= 0 && def != type))
				{
					continue;
				}
				SplitBar(text.Replace("★", "").Trim(), out string head, out string tail);
				int d = def;
				int p = paint;
				cards.Add(new Card
				{
					Name = tail.Length > 0 ? tail : head,
					Sub = "★ " + Lang.Rarity(Ru, Lang.RarityKind.Item, CovertRarity),
					Art = IconClass(text) ?? GloveIcon(def, paint),
					Rarity = CovertRarity,
					EqT = teams[WpDatabase.TeamT].GloveDef == d && teams[WpDatabase.TeamT].PaintOf(d) == p,
					EqCt = teams[WpDatabase.TeamCt].GloveDef == d && teams[WpDatabase.TeamCt].PaintOf(d) == p,
					Click = pl => Choose(pl, option, t =>
					{
						t.GloveDef = d;
						t.Paints[d] = p;
					})
				});
			}
			return cards;
		}

		if (IsMusicMenu(title))
		{
			foreach (ChatMenuOption option in menu.MenuOptions)
			{
				string text = Plain(option.Text);
				int id = _items.MusicId(text);
				if (id < 0 && IsNone(text))
				{
					id = 0;
				}
				_items.Music.TryGetValue(id, out NamedEntry? entry);
				string shown = id > 0 && entry != null ? ((Ru ? entry.Ru : entry.En) ?? text) : text;
				SplitMusic(shown, out string artist, out string song);
				int music = id;
				cards.Add(new Card
				{
					Name = id == 0 ? T("none") : song,
					Sub = id == 0 ? T("default") : artist,
					Art = id == 0 ? "mk-none" : entry?.Art,
					Rarity = id > 0 ? 3 : 0,
					Stock = id <= 0,
					EqT = music >= 0 && teams[WpDatabase.TeamT].Music == music,
					EqCt = music >= 0 && teams[WpDatabase.TeamCt].Music == music,
					Click = pl => Choose(pl, option, music >= 0 ? (Action<TeamData>)(t => t.Music = music) : null)
				});
			}
			return cards;
		}

		if (IsPinsMenu(title))
		{
			foreach (ChatMenuOption option in menu.MenuOptions)
			{
				string text = Plain(option.Text);
				int id = _items.PinId(text);
				if (id < 0 && IsNone(text))
				{
					id = 0;
				}
				_items.Pins.TryGetValue(id, out NamedEntry? entry);
				int pin = id;
				int rarity = id > 0 ? Math.Max(1, entry?.Rarity ?? 3) : 0;
				cards.Add(new Card
				{
					Name = id == 0 ? T("none") : text,
					Sub = id == 0 ? T("default") : Lang.Rarity(Ru, Lang.RarityKind.Item, rarity),
					Art = id == 0 ? "pn-none" : entry?.Art,
					Rarity = rarity,
					Stock = id <= 0,
					EqT = pin >= 0 && teams[WpDatabase.TeamT].Pin == pin,
					EqCt = pin >= 0 && teams[WpDatabase.TeamCt].Pin == pin,
					Click = pl => Choose(pl, option, pin >= 0 ? (Action<TeamData>)(t => t.Pin = pin) : null)
				});
			}
			return cards;
		}

		foreach (ChatMenuOption option in menu.MenuOptions)
		{
			string text = Plain(option.Text);
			Split(text, out string name, out string sub);
			cards.Add(new Card
			{
				Name = name,
				Sub = int.TryParse(sub, out _) ? "" : sub,
				Art = IconClass(text),
				Click = pl => Choose(pl, option, null)
			});
		}
		return cards;
	}

	private List<ChatMenuOption> VisibleWeapons(int slot, IMenu menu)
	{
		WeaponGroup? group = _filter[slot];
		if (group == null)
		{
			return menu.MenuOptions.ToList();
		}
		List<ChatMenuOption> list = new(group.Items.Length);
		foreach (string item in group.Items)
		{
			ChatMenuOption? option = Find(menu, item);
			if (option != null && !list.Contains(option))
			{
				list.Add(option);
			}
		}
		return list;
	}

	private static bool SkinOn(TeamData team, string? knife, int def, int paint)
	{
		if (knife != null && KnifeOf(team) != knife)
		{
			return false;
		}
		int current = team.PaintOf(def);
		return paint > 0 ? current == paint : current <= 0;
	}

	// ------------------------------------------------------------------ home page / left column

	/// <summary>The text and picture a category shows on the left column and the home tiles.</summary>
	private (string Label, string? Art) Summary(int slot, int team, int cat)
	{
		TeamData data = Teams(slot)[team];
		switch (cat)
		{
			case CatKnife:
			{
				string name = KnifeOf(data);
				int index = Array.IndexOf(KnifeNames, name);
				int def = WpDatabase.DefOf(name);
				int paint = index == StockKnife ? 0 : data.PaintOf(def);
				string? finish = paint > 0 ? _items.PaintName(paint, Ru) : null;
				string art = (paint > 0 ? IconFor(def, paint) : null) ?? "knife-" + Math.Max(0, index);
				return (finish != null ? name + "  |  " + finish : name, art);
			}
			case CatGloves:
			{
				if (data.GloveDef == 0)
				{
					return (T("default"), team == WpDatabase.TeamCt ? "gl-def-ct" : "gl-def-t");
				}
				GloveType? type = _items.FindGloveType(data.GloveDef);
				string name = type != null ? GloveTypeName(type) : (WpDatabase.NameByDef.TryGetValue(data.GloveDef, out string? n) ? n : T("cat.gloves"));
				int paint = data.PaintOf(data.GloveDef);
				string? finish = paint > 0 ? _items.PaintName(paint, Ru) : null;
				return (finish != null ? name + "  |  " + finish : name, (paint > 0 ? GloveIcon(data.GloveDef, paint) : null) ?? type?.Art);
			}
			case CatAgents:
			{
				AgentEntry? agent = _items.Agent(data.Agent);
				if (agent == null)
				{
					return data.Agent == null
						? (T("default"), team == WpDatabase.TeamCt ? "ag-def-ct" : "ag-def-t")
						: (data.Agent[(data.Agent.LastIndexOf('/') + 1)..], null);
				}
				return (AgentTitle(agent), agent.Art);
			}
			case CatMusic:
			{
				if (data.Music <= 0)
				{
					return (T("default"), null);
				}
				if (_items.Music.TryGetValue(data.Music, out NamedEntry? entry))
				{
					return ((Ru ? entry.Ru : entry.En) ?? entry.En ?? "#" + data.Music, entry.Art);
				}
				return ("#" + data.Music, null);
			}
			case CatPins:
			{
				if (data.Pin <= 0)
				{
					return (T("none"), null);
				}
				if (_items.Pins.TryGetValue(data.Pin, out NamedEntry? entry))
				{
					return ((Ru ? entry.Ru : entry.En) ?? entry.En ?? "#" + data.Pin, entry.Art);
				}
				return ("#" + data.Pin, null);
			}
			default:
			{
				if (cat < 3 || cat >= CatGroup.Length || !DefaultGroups.TryGetValue(CatGroup[cat], out WeaponGroup? group))
				{
					return (T("default"), null);
				}
				string? best = null;
				foreach (string weapon in group.Items)
				{
					if (data.PaintOf(WpDatabase.DefOf(weapon)) > 0 && (best == null || weapon == CatPreferred[cat]))
					{
						best = weapon;
					}
				}
				if (best == null)
				{
					return (T("default"), null);
				}
				int def = WpDatabase.DefOf(best);
				int paint = data.PaintOf(def);
				string? finish = _items.PaintName(paint, Ru);
				return (finish != null ? best + "  |  " + finish : best, IconFor(def, paint) ?? IconClass(best));
			}
		}
	}

	private string? IconFor(int def, int paint)
	{
		string? key = _wpDatabase.IconKey(def, paint);
		return key != null ? IconClass(key) : null;
	}

	private string? GloveIcon(int def, int paint)
	{
		string? name = _items.GloveName(def, paint);
		return name != null ? IconClass(name) : null;
	}

	private string GloveTypeName(GloveType type)
	{
		return (Ru ? type.Ru : type.En) ?? type.En ?? "";
	}

	private string AgentTitle(AgentEntry agent)
	{
		string full = (Ru ? agent.Ru : agent.En) ?? agent.En ?? agent.Model;
		SplitBar(full, out string head, out _);
		return head;
	}

	private string GroupTitle(WeaponGroup group)
	{
		return group.Key.Length > 0 && DefaultGroups.ContainsKey(group.Key) && string.Equals(group.Title, DefaultGroups[group.Key].Title, StringComparison.Ordinal)
			? T("group." + group.Key.ToLowerInvariant())
			: group.Title;
	}

	private string GroupTitleOf(string weapon)
	{
		foreach (WeaponGroup group in DefaultGroups.Values)
		{
			if (group.Items.Contains(weapon, StringComparer.OrdinalIgnoreCase))
			{
				return T("group." + group.Key);
			}
		}
		return T("cat.skins");
	}

	private static string TeamName(int team) => team == WpDatabase.TeamCt ? "CT" : "T";

	private static string KnifeOf(TeamData team) => team.Knife ?? "Default Knife";

	private int[] KnifeOrder(int slot)
	{
		string equipped = KnifeOf(Teams(slot)[_teamIdx[slot]]);
		int first = Math.Max(0, Array.IndexOf(KnifeNames, equipped));
		List<int> order = new(KnifeBase.Length) { first };
		order.AddRange(KnifeBase.Where(k => k != first));
		return order.ToArray();
	}

	private void SyncSidePage(int slot)
	{
		_sidePage[slot] = (_active[slot] ?? "") switch
		{
			"weapons:smg" or "weapons:heavy" or "open:agents" or "open:music" or "open:pins" => 1,
			_ => 0
		};
	}

	private int Category(int slot)
	{
		return _view[slot] switch
		{
			ViewHome => CatLoadout,
			ViewKnife => CatKnife,
			ViewAgents => CatAgents,
			ViewGloves => CatGloves,
			_ => (_active[slot] ?? "") switch
			{
				"weapons:pistols" => CatPistols,
				"weapons:rifles" => 4,
				"weapons:snipers" => 5,
				"weapons:smg" => 6,
				"weapons:heavy" => 7,
				"open:music" => CatMusic,
				"open:agents" => CatAgents,
				"open:pins" => CatPins,
				"open:knife" => CatKnife,
				"open:gloves" => CatGloves,
				_ => CategoryOf(_parent[slot])
			}
		};
	}

	private static int CategoryOf(string? command)
	{
		return command switch
		{
			"css_knife" => CatKnife,
			"css_gloves" => CatGloves,
			"css_skins" => CatPistols,
			"css_agents" => CatAgents,
			"css_music" => CatMusic,
			"css_pins" => CatPins,
			_ => CatNone
		};
	}

	// ------------------------------------------------------------------ cached sends

	private void SetClass(CCSCustomHudLayout layout, CCSPlayerController player, string panel, string cls, bool on)
	{
		Dictionary<string, bool> sent = _sentClass[player.Slot] ??= new Dictionary<string, bool>(StringComparer.Ordinal);
		string key = panel + "\u0001" + cls;
		if (sent.TryGetValue(key, out bool current) && current == on)
		{
			return;
		}
		sent[key] = on;
		layout.SetHasClassForPlayer(player, panel, cls, on);
	}

	private void SetVar(CCSCustomHudLayout layout, CCSPlayerController player, string panel, string name, string value)
	{
		Dictionary<string, string> sent = _sentVar[player.Slot] ??= new Dictionary<string, string>(StringComparer.Ordinal);
		string key = panel + "\u0001" + name;
		if (sent.TryGetValue(key, out string? current) && current == value)
		{
			return;
		}
		sent[key] = value;
		layout.SetDialogVariableStringForPlayer(player, panel, name, value);
	}

	private void PaintArt(CCSCustomHudLayout layout, CCSPlayerController player, string panel, ref string? previous, string? cls)
	{
		if (!string.IsNullOrEmpty(previous) && previous != cls)
		{
			SetClass(layout, player, panel, previous, false);
		}
		if (!string.IsNullOrEmpty(cls))
		{
			SetClass(layout, player, panel, cls, true);
		}
		previous = cls;
	}

	// ------------------------------------------------------------------ menu titles and option text

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
		if (IsGlovesMenu(text))
		{
			return "css_gloves";
		}
		if (IsAgentsMenu(text))
		{
			return "css_agents";
		}
		if (IsMusicMenu(text))
		{
			return "css_music";
		}
		if (IsPinsMenu(text))
		{
			return "css_pins";
		}
		return null;
	}

	private static bool HasAny(string? text, string first, string second)
	{
		string plain = Plain(text);
		return plain.Contains(first, StringComparison.OrdinalIgnoreCase) || plain.Contains(second, StringComparison.OrdinalIgnoreCase);
	}

	private static bool Matches(string title)
	{
		string text = Plain(title);
		return Match.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsWeaponList(string? title) => HasAny(title, "weapon menu", "меню оружия");

	private static bool IsSkinMenu(string? title) => HasAny(title, "select skin", "выберите скин");

	private static bool IsGlovesMenu(string? title) => !IsSkinMenu(title) && HasAny(title, "gloves menu", "меню перчаток");

	private static bool IsAgentsMenu(string? title) => !IsSkinMenu(title) && HasAny(title, "agents menu", "меню агентов");

	private static bool IsMusicMenu(string? title) => !IsSkinMenu(title) && HasAny(title, "music menu", "меню музыки");

	private static bool IsPinsMenu(string? title) => !IsSkinMenu(title) && HasAny(title, "pins menu", "меню пинов");

	private static bool IsKnifeMenu(string? title) => !IsSkinMenu(title) && HasAny(title, "knife menu", "меню нож");

	private static bool IsNone(string text)
	{
		string plain = text.Trim();
		return plain.Equals("None", StringComparison.OrdinalIgnoreCase) || plain.Equals("Нет", StringComparison.OrdinalIgnoreCase);
	}

	private static string? KnifeIn(string? title)
	{
		string text = Plain(title);
		string? found = null;
		foreach (string name in KnifeNames)
		{
			if (text.Contains(name, StringComparison.OrdinalIgnoreCase) && (found == null || name.Length > found.Length))
			{
				found = name;
			}
		}
		return found;
	}

	private static string? WeaponIn(string? title)
	{
		string plain = Plain(title);
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

	private static string StripPrefix(string text)
	{
		int bar = text.IndexOf(" | ", StringComparison.Ordinal);
		return bar < 0 ? text : text[(bar + 3)..].Trim();
	}

	private static void Split(string text, out string name, out string sub)
	{
		int open = text.LastIndexOf('(');
		if (open > 0 && text.EndsWith(')'))
		{
			name = text[..open].Trim();
			sub = text.Substring(open + 1, text.Length - open - 2).Trim();
		}
		else
		{
			name = text;
			sub = "";
		}
	}

	/// <summary>"Osiris | Elite Crew" -> head "Osiris", tail "Elite Crew".</summary>
	private static void SplitBar(string text, out string head, out string tail)
	{
		int bar = text.IndexOf('|');
		if (bar < 0)
		{
			head = text.Trim();
			tail = "";
			return;
		}
		head = text[..bar].Trim();
		tail = text[(bar + 1)..].Trim();
	}

	/// <summary>"Music Kit | Daniel Sadowski, Crimson Assault" -> artist "Daniel Sadowski", song "Crimson Assault".</summary>
	private static void SplitMusic(string text, out string artist, out string song)
	{
		string body = text.Contains('|') ? text[(text.IndexOf('|') + 1)..].Trim() : text.Trim();
		foreach (string separator in new[] { " — ", " – ", " - ", ", " })
		{
			int at = body.IndexOf(separator, StringComparison.Ordinal);
			if (at > 0)
			{
				artist = body[..at].Trim();
				song = body[(at + separator.Length)..].Trim();
				return;
			}
		}
		artist = "";
		song = body;
	}
}
