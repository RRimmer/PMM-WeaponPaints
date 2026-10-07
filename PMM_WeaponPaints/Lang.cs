namespace PMM_WeaponPaints;

/// <summary>Server-side UI strings. Static labels in wp.xml are switched with the lang-ru class.</summary>
internal static class Lang
{
    private static readonly Dictionary<string, (string En, string Ru)> Strings = new(StringComparer.Ordinal)
    {
        ["title.home"] = ("WEAPON SKINS", "СКИНЫ ОРУЖИЯ"),
        ["crumb.home"] = ("LOADOUT", "СНАРЯЖЕНИЕ"),
        ["hint.home"] = ("Pick what to change", "Выберите, что изменить"),

        ["cat.knife"] = ("KNIFE", "НОЖ"),
        ["cat.gloves"] = ("GLOVES", "ПЕРЧАТКИ"),
        ["cat.agents"] = ("AGENTS", "АГЕНТЫ"),
        ["cat.music"] = ("MUSIC", "МУЗЫКА"),
        ["cat.pins"] = ("COINS", "ЗНАЧКИ"),
        ["cat.weapons"] = ("WEAPONS", "ОРУЖИЕ"),
        ["cat.skins"] = ("SKINS", "СКИНЫ"),
        ["cat.locker"] = ("LOCKER", "ЛОКЕР"),
        ["group.pistols"] = ("PISTOLS", "ПИСТОЛЕТЫ"),
        ["group.rifles"] = ("RIFLES", "ВИНТОВКИ"),
        ["group.snipers"] = ("SNIPERS", "СНАЙПЕРСКИЕ"),
        ["group.smg"] = ("SMG", "ПП"),
        ["group.heavy"] = ("HEAVY", "ТЯЖЁЛОЕ"),

        ["default"] = ("Default", "Стандарт"),
        ["none"] = ("None", "Нет"),
        ["stock"] = ("STOCK", "СТАНДАРТ"),
        ["vanilla"] = ("VANILLA", "ВАНИЛЬ"),
        ["finishes"] = ("{0} FINISHES", "СКИНОВ: {0}"),

        ["hint.knife"] = ("{0} knives  ·  pick one, then its finish", "Ножей: {0}  ·  выберите нож, затем скин"),
        ["hint.finishes"] = ("{0} finishes  ·  click one to equip", "Скинов: {0}  ·  нажмите, чтобы надеть"),
        ["hint.weapons"] = ("Pick a weapon, then its finish", "Выберите оружие, затем скин"),
        ["hint.equip"] = ("{0} items  ·  click one to equip", "Предметов: {0}  ·  нажмите, чтобы надеть"),
        ["hint.gloves"] = ("{0} glove types  ·  pick one, then its finish", "Типов перчаток: {0}  ·  выберите тип, затем скин"),
        ["hint.agents"] = ("{0} agents for {1}  ·  click one to equip", "Агентов за {1}: {0}  ·  нажмите, чтобы надеть"),
        ["hint.pins"] = ("{0} coins  ·  applied after a reconnect or map change", "Значков: {0}  ·  применяется после перезахода или смены карты"),
        ["crumb.finishes"] = ("{0}  /  FINISHES", "{0}  /  СКИНЫ"),
        ["crumb.weapons"] = ("WEAPONS  /  {0}", "ОРУЖИЕ  /  {0}"),
        ["crumb.gloves"] = ("GLOVES  /  {0}", "ПЕРЧАТКИ  /  {0}"),
        ["crumb.team"] = ("{0} SIDE", "СТОРОНА {0}"),

        ["chat.opened"] = (" [PMM] Locker", " [PMM] Локер"),
        ["chat.nodb"] = (" [PMM] The locker did not open: no connection to the WeaponPaints database.", " [PMM] Локер не открыт: нет соединения с базой WeaponPaints."),
        ["chat.panorama"] = (" [PMM] Mouse panorama is required: !menu → Select menu → Panorama (mouse). Then the locker opens.", " [PMM] Нужна панорама мышью: !menu → Выбор меню → Панорама (мышь). После этого локер откроется."),
        ["chat.team"] = (" [PMM] Join T or CT to open the locker.", " [PMM] Локер открывается только за T или CT. Зайдите за команду."),
        ["chat.layout"] = (" [PMM] The panorama was not created", " [PMM] Панорама не создалась"),
        ["chat.agent"] = (" [PMM] Agent: {0}", " [PMM] Агент: {0}"),
        ["chat.agent.later"] = (" [PMM] Agent saved: {0}. It applies on the next spawn.", " [PMM] Агент сохранён: {0}. Применится при следующем возрождении."),
        ["chat.agent.fail"] = (" [PMM] The agent was not saved: no connection to the WeaponPaints database.", " [PMM] Агент не сохранён: нет соединения с базой WeaponPaints."),
    };

    // CS2 rarity names, index 1..7.
    private static readonly string[] WeaponEn = { "", "CONSUMER GRADE", "INDUSTRIAL GRADE", "MIL-SPEC", "RESTRICTED", "CLASSIFIED", "COVERT", "CONTRABAND" };
    private static readonly string[] WeaponRu = { "", "ШИРПОТРЕБ", "ПРОМЫШЛЕННОЕ", "АРМЕЙСКОЕ", "ЗАПРЕЩЁННОЕ", "ЗАСЕКРЕЧЕННОЕ", "ТАЙНОЕ", "КОНТРАБАНДА" };
    private static readonly string[] AgentEn = { "", "", "", "DISTINGUISHED", "EXCEPTIONAL", "SUPERIOR", "MASTER", "MASTER" };
    private static readonly string[] AgentRu = { "", "", "", "ЗАСЛУЖЕННЫЙ", "ИСКЛЮЧИТЕЛЬНЫЙ", "ПРЕВОСХОДНЫЙ", "МАСТЕР", "МАСТЕР" };
    private static readonly string[] ItemEn = { "", "BASE GRADE", "BASE GRADE", "HIGH GRADE", "REMARKABLE", "EXOTIC", "EXTRAORDINARY", "CONTRABAND" };
    private static readonly string[] ItemRu = { "", "БАЗОВОГО КЛАССА", "БАЗОВОГО КЛАССА", "ВЫСШЕГО КЛАССА", "ЗАМЕЧАТЕЛЬНОГО ТИПА", "ЭКЗОТИЧНОГО ВИДА", "ЭКСТРАОРДИНАРНОГО ТИПА", "КОНТРАБАНДА" };

    public static bool IsRussian(string? language)
    {
        return language != null && language.Trim().StartsWith("ru", StringComparison.OrdinalIgnoreCase);
    }

    public static string Get(bool russian, string key, params object[] args)
    {
        if (!Strings.TryGetValue(key, out (string En, string Ru) pair))
        {
            return key;
        }

        string text = russian ? pair.Ru : pair.En;
        return args.Length == 0 ? text : string.Format(text, args);
    }

    public enum RarityKind
    {
        Weapon,
        Agent,
        Item
    }

    public static string Rarity(bool russian, RarityKind kind, int rarity)
    {
        if (rarity < 1 || rarity > 7)
        {
            return "";
        }

        string[] table = kind switch
        {
            RarityKind.Agent => russian ? AgentRu : AgentEn,
            RarityKind.Item => russian ? ItemRu : ItemEn,
            _ => russian ? WeaponRu : WeaponEn
        };
        return table[rarity];
    }
}
