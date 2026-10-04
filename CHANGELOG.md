Вы можете ознакомиться с более подробной информацией на сайте: https://genesis-cs.space/menuconstructor/index.html

PanoramaMenuManager_WeaponPaints 0.0.3
Проект: PMM_WeaponPaints
Сборка: CounterStrikeSharp 1.0.376
Нужен MenuManager 1.2.02

RU
Что нового в 0.0.3
- Левая колонка: винтовки, снайперские, ПП, тяжёлое и музыка. Ножи, перчатки, пистолеты и агенты на месте. Пинов в меню нет.
- Иконки разделов — картинки игры: AK-47, AWP, MAC-10, Nova, музкит.
- Колонка на двух страницах: стрелки и номера 1 и 2. Первая — Loadout, Knife, Gloves, Pistols, Rifles, Snipers. Вторая — SMG, Heavy, Agents, Music.
- На Loadout девять плиток 144×170, сетка 6×3 как у списка скинов: первый ряд шесть разделов, второй — тяжёлое, агенты, музыка.
- Экипировка читается из базы WeaponPaints при !pws и не пропадает после перезахода. Поля DatabaseHost, DatabasePort, DatabaseUser, DatabasePassword и DatabaseName — это база плагина WeaponPaints, не MenuManager. Плагин базу не пишет.
- Без соединения с этой базой локер не рисуется: ошибка в консоли сервера и в чате на !pws.
- !pws открывается только если в !menu выбрана панорама мышью. Иначе в чат пишется, как её включить: Выбор меню → Панорама (мышь).
- Выбранная отделка видна и в списке оружия, и внутри его скинов: картинка, имя и золотая EQUIPPED. Пока отделку в этом заходе не выбирали, карточка показывает базовую картинку.
- Скин пишется в команду, в которой игрок стоит. Список агентов тот, что отдал WeaponPaints.
- Конфиг plugins/PMM_WeaponPaints/PMM_WeaponPaints.json: Commands (по умолчанию ["pws"], в чате !pws), OpenDelay (пауза до меню, 0.2), InputDelay (пауза до мыши, 0.2).
- Панораму wp.xml и wp.css нужно заново собрать в Workshop Tools.

Что было в 0.0.2
- Оружие делится на группы pistols, rifles, snipers, smg, heavy. Кнопка с триггером weapons:группа открывает только эту группу, а не весь список css_skins.
- Кнопки конструктора ищутся в wp_triggers.json рядом с DLL. Без этого файла плагин знает штатные id wp-cat-knife и соседние.
- Клик по типу ножа сохраняет нож и открывает его отделки. Выбор отделки возвращает к списку, выбранный предмет золотой, с надписью EQUIPPED.
- Назад из списка скинов возвращает к тому списку, из которого пришли.
- Раздел пинов в локере не рисуется. Строка css_pins остаётся только запасным разбором заголовка.

Что было в 0.0.1
- Окно локера по центру. !pws открывает главное окно.
- !knife открывает сетку типов ножей. !gloves, !agents, !music и меню скинов открывают свой список. Клик вызывает исходный callback WeaponPaints.
- Своей копии MenuManager нет. Нужен MenuManager. В plugins/WeaponPaints не должно быть MenuManagerApi.dll.
- У игрока в !menu панорама мышью. В конфиге WeaponPaints MenuType: selectable.

EN
What is new in 0.0.3
- Left column: rifles, snipers, SMGs, heavy and music. Knives, gloves, pistols and agents stay. There is no pins section.
- Section icons are game images: AK-47, AWP, MAC-10, Nova and the music kit.
- The column has two pages, with arrows and numbers 1 and 2. Page 1 is Loadout, Knife, Gloves, Pistols, Rifles, Snipers. Page 2 is SMG, Heavy, Agents, Music.
- Loadout uses nine 144×170 tiles in the same 6×3 grid as the skin list. The first row has six sections. The second has heavy, agents and music.
- Equipped items are read from the WeaponPaints database on !pws and stay after a reconnect. DatabaseHost, DatabasePort, DatabaseUser, DatabasePassword and DatabaseName are the WeaponPaints database, not MenuManager. This plugin does not write to it.
- Without that database the locker does not draw: the server console and !pws in chat report the error.
- !pws opens only when !menu is set to mouse panorama. Otherwise chat explains: Select menu, then Panorama (mouse).
- A chosen finish shows on the weapon card and inside its skin list: image, name and a gold EQUIPPED frame. Until a finish is picked this session, the card keeps the base weapon image.
- A skin is stored for the team the player is on. The agent list is the one WeaponPaints opened.
- Config plugins/PMM_WeaponPaints/PMM_WeaponPaints.json: Commands (default ["pws"], chat !pws), OpenDelay (pause before the menu, 0.2), InputDelay (pause before the mouse, 0.2).
- Rebuild wp.xml and wp.css in Workshop Tools.

What was in 0.0.2
- Weapons are split into pistols, rifles, snipers, smg and heavy. A weapons:group trigger opens that group, not the whole css_skins list.
- Constructor buttons are resolved from wp_triggers.json next to the DLL. Without that file the plugin only knows the stock ids such as wp-cat-knife.
- A knife type saves the knife and opens its finishes. Picking a finish returns to the list. The equipped item is gold and labeled EQUIPPED.
- Back from a skin list returns to the list it came from.
- The locker does not draw a pins section. css_pins remains only as a fallback title match.

What was in 0.0.1
- Centered locker. !pws opens the home window.
- !knife opens the knife-type grid. !gloves, !agents, !music and the skin menu open their own lists. A click runs the original WeaponPaints callback.
- This archive has no MenuManager. Do not leave MenuManagerApi.dll in plugins/WeaponPaints.
- The player menu in !menu must be mouse panorama. WeaponPaints MenuType: selectable.
