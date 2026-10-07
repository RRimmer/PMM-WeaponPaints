Вы можете ознакомиться с более подробной информацией на сайте: https://genesis-cs.space/menuconstructor/index.html

PanoramaMenuManager_WeaponPaints 0.1.0
Проект: PMM_WeaponPaints
Сборка: CounterStrikeSharp 1.0.376
Нужен MenuManager 1.2.02

RU
Исправления в 0.1.0 (после первого теста в игре)
- Фантомные картинки: у ножа Navaja, перчаток «Гидра» и Five-SeveN часть карточек показывала картинку от другого предмета (дробовик Nova, нож Gut). Причина: при каждом открытии !pws плагин «забывал», какой класс картинки уже висит на карточке, и не снимал его. Теперь старый класс снимается всегда, кэш сбрасывается только при новом окне панорамы или перезаходе игрока.
- Стандартные предметы получили картинки: стандартные перчатки T и CT (из игры), стандартный агент T и CT (из игры), «Нет» у музыки и значков — свои картинки (panorama/images/custom_game/pmm_music_none.png и pmm_coin_none.png).
- Клик по «Стандарт» в Перчатках сразу надевает стандартные перчатки, подменю нет.
- Карточка без картинки больше не схлопывается: название и полоса стоят на своём месте и не наезжают на кружки T/CT.
- Снаряжение (главная): плитки выходили за нижний край окна. Теперь два ряда по 272px — ровно по высоте окна на 16:9, 16:10 и 4:3.

Что нового в 0.1.0 (по сравнению с 0.0.3, включая промежуточные сборки 0.0.4–0.0.5)

Команды
- Рамка экипированного предмета теперь цвета команды, за которую вы стоите: T — золотая, CT — синяя. Бейдж ЭКИПИРОВАНО и название карточки того же цвета.
- В левом верхнем углу каждой карточки два кружка T и CT. Залитый кружок — предмет надет за эту команду, пустой — нет. Локер читает из базы WeaponPaints снаряжение обеих команд сразу.
- !pws открывается только за T или CT. В наблюдателях в чат пишется, что нужно зайти за команду. Открытый локер закрывается при смене команды.

Язык
- Новый параметр конфига "Language": "ru" или "en" (по умолчанию "en"). Переводится весь интерфейс: заголовки, разделы, кнопки, подсказки, бейдж, редкости и сообщения в чат.

Агенты
- Свой полный список агентов CS2 (63 модели) с картинками и редкостью. Список WeaponPaints больше не нужен: в его русском файле агентов нет.
- Показываются только агенты вашей команды: за T — агенты T, за CT — агенты CT. Первая карточка — стандартный агент.
- Выбор пишется в таблицу wp_player_agents (только колонка вашей команды) и в память WeaponPaints. Если вы живы, модель меняется сразу, иначе при следующем возрождении. Это единственное, что плагин пишет в базу.
- В левой колонке и на плитке Agents вместо имени файла модели (ctm_gendarmerie_variantc) показывается имя агента и его картинка.

Музыка
- У наборов музыки появились картинки. Крупно — название трека, ниже — исполнитель.
- Выбранный набор виден в левой колонке и на плитке Music с картинкой и названием.

Значки (Coins)
- Новый раздел COINS / ЗНАЧКИ в левой колонке (вторая страница) и на главной. Открывает меню пинов WeaponPaints (css_pins).
- Над сеткой в рамке предупреждение: «Внимание! После применения медали необходимо перезайти на сервер или дождаться смены карты».
- Картинки и редкость у всех 603 значков. Больше 120 значков — внизу страницы 1, 2, 3 …

Редкость
- Рамка, полоска и низ карточки окрашены по редкости CS2: Ширпотреб, Промышленное, Армейское, Запрещённое, Засекреченное, Тайное, Контрабанда. Под названием скина — название редкости.
- Ножи и перчатки — ★ Тайное / Экстраординарное. Агенты: Заслуженный … Мастер. Значки и музыка — по данным игры.
- Экипированный предмет всегда в цвете команды, а не редкости.
- Редкости собраны из items_game.txt игры в файл pmm_items.json (лежит рядом с DLL).

Перчатки
- Gloves сначала показывает 8 типов: Спортивные перчатки, Перчатки спецназа, Водительские перчатки, Мотоциклетные перчатки, Обмотки рук, Перчатки «Бладхаунд», Перчатки «Гидра», Перчатки «Сломанный клык» — и карточку «Стандарт».
- Клик по типу открывает только его скины. BACK возвращает к типам.

Сетка и размеры
- В строке ровно 5 карточек. Ширина карточек резиновая: делят ширину колонки поровну. Неполный последний ряд не растягивается.
- Главная (Loadout): 10 плиток, 2 ряда по 5, растянуты на всю высоту окна.
- Окно .wp-root: width 65%, height 70% — правильно встаёт на 16:9, 16:10 и 4:3. Левая колонка 23% ширины.
- Шрифты крупнее: название скина 16px, редкость 13px, подписи в колонке 14px, бейдж 11–12px, подсказка 16px.
- Картинки скинов вписаны в рамку 4:3 (112×84) без обрезки. На странице Knife — ванильные модели ножей вместо градиента.

Прочее
- Новый файл pmm_items.json рядом с DLL: редкости, агенты, музыка, значки, типы перчаток. Без него локер работает, но без рамок редкости, агентов и картинок музыки/значков.
- Имена из WeaponPaints/data/*.json читаются при загрузке, поэтому картинки музыки, значков и перчаток находятся на любом SkinsLanguage WeaponPaints.
- Повторные классы и тексты не отправляются клиенту второй раз — меньше сетевых сообщений при перерисовке.
- wp-layout.css удалён, клиенту нужны только wp.xml и wp.css.
- Панораму wp.xml и wp.css нужно заново собрать в Workshop Tools.

Что было в 0.0.3
- Левая колонка: винтовки, снайперские, ПП, тяжёлое и музыка. Колонка на двух страницах.
- Экипировка читается из базы WeaponPaints при !pws. Конфиг: Commands, OpenDelay, InputDelay, Database*.
- !pws открывается только при панораме мышью.

Что было в 0.0.2
- Группы оружия pistols, rifles, snipers, smg, heavy. Кнопки конструктора из wp_triggers.json.

Что было в 0.0.1
- Окно локера по центру. !pws открывает главное окно. Клик вызывает исходный callback WeaponPaints.

EN
Fixes in 0.1.0 (after the first in-game test)
- Phantom pictures: some cards (Navaja Knife, Hydra Gloves, Five-SeveN) showed another item's picture (Nova shotgun, Gut Knife). Every !pws forgot which picture class a card already had and never removed it. Old classes are now always removed; the cache resets only for a new panorama entity or a reconnect.
- Default items have pictures: default T/CT gloves and default T/CT agent (game images), and own pictures for "None" music and coin (panorama/images/custom_game/pmm_music_none.png, pmm_coin_none.png).
- Clicking Default in Gloves equips the default gloves at once, there is no sub-menu.
- A card without a picture keeps its layout, the name no longer overlaps the T/CT dots.
- Loadout home page: tiles went past the bottom edge. Now two 272px rows fill the window exactly on 16:9, 16:10 and 4:3.

What is new in 0.1.0 (since 0.0.3, including the interim 0.0.4–0.0.5 builds)

Teams
- The frame of an equipped item takes the colour of the team you stand in: T gold, CT blue. The EQUIPPED badge and the card name use the same colour.
- Two dots, T and CT, in the top-left corner of every card. A filled dot means the item is equipped for that team. The locker reads both teams from the WeaponPaints database.
- !pws opens only for T or CT. Spectators get a chat message. An open locker closes when the player changes team.

Language
- New config key "Language": "ru" or "en" (default "en"). It covers the whole UI: titles, sections, buttons, hints, badge, rarity names and chat messages.

Agents
- Own full CS2 agent list (63 models) with pictures and rarity, independent of the WeaponPaints language file (its Russian file has no agents).
- Only agents of your team are listed. The first card is the default agent.
- A pick is written to wp_player_agents (only your team's column) and into WeaponPaints memory. Alive players get the model at once, otherwise on the next spawn. This is the only thing the plugin writes to the database.
- The left column and the Agents tile show the agent name and picture instead of the model file name.

Music
- Music kits have pictures. The track title is large, the artist below.

Coins
- New COINS section in the left column (page 2) and on the home page. It opens the WeaponPaints pins menu (css_pins).
- A framed warning above the grid: after applying a medal, reconnect or wait for the map change.
- Pictures and rarity for all 603 coins. More than 120 items get pages 1, 2, 3 … at the bottom.

Rarity
- Frame, bar and bottom tint follow CS2 rarity, from Consumer Grade to Contraband. The rarity name is shown under the skin name.
- Knives and gloves are ★ Covert / Extraordinary. Agents Distinguished … Master. Coins and music from game data.
- An equipped item always uses the team colour instead of the rarity colour.
- Rarities come from the game's items_game.txt, stored in pmm_items.json next to the DLL.

Gloves
- Gloves first shows 8 types (Sport, Specialist, Driver, Moto, Hand Wraps, Bloodhound, Hydra, Broken Fang) plus Default. A type opens only its finishes. BACK returns to the types.

Grid and sizes
- Exactly 5 cards per row, fluid width. An incomplete last row keeps the same card width.
- Home: 10 tiles in 2 rows of 5 that fill the window height.
- .wp-root is width 65%, height 70%, so it fits 16:9, 16:10 and 4:3. Left column is 23% wide.
- Bigger fonts. Skin pictures fit a 4:3 box (112×84) without cropping. The Knife page shows vanilla knife models.

Other
- New file pmm_items.json next to the DLL. Without it the locker works without rarity frames, agents and music/coin pictures.
- Names from WeaponPaints/data/*.json are read at load, so any WeaponPaints SkinsLanguage works.
- Unchanged classes and texts are not sent again on redraw.
- wp-layout.css removed. The client needs wp.xml and wp.css only.
- Rebuild wp.xml and wp.css in Workshop Tools.

What was in 0.0.3, 0.0.2, 0.0.1: see the Russian section above.
