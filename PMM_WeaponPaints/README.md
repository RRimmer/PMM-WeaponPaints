# PanoramaMenuManager_WeaponPaints

Проект: `PMM_WeaponPaints`. Полное имя модуля: `PanoramaMenuManager_WeaponPaints`.

`!pws` открывает главное окно (Loadout) только за T или CT. Разделы: Knife, Gloves (8 типов), Pistols, Rifles, Snipers, SMG, Heavy, Agents (свой список CS2), Music, Coins (`!pins`). Клик по карточке вызывает callback WeaponPaints.

Версия 0.1.0: язык `"Language": "ru"|"en"` в конфиге, рамки редкости, цвет команды у экипированного, кружки T/CT, сетка 5 в ряд, окно 65% × 70%. Данные редкостей, агентов, музыки и значков — `pmm_items.json` рядом с DLL. Полный список изменений — `../CHANGELOG.txt`.

## Как это работает (v0.1.0)

* Сетка 6 x 3 (18 карточек на странице), снизу пагинация `‹ 1 2 3 … ›`.
* Выбранный элемент: золотая рамка со свечением, золотое название, бейдж `EQUIPPED` в правом верхнем углу.
* Ховер: подсветка фона, светлая рамка, небольшое увеличение карточки (также у кнопок, категорий, пагинации).
* Ножи: сначала выбирается тип ножа. Откроется подменю со скинами этого ножа. После выбора скина
  подменю закрывается, окно возвращается на страницу 1, а выбранный нож стоит первым и показывает тип и скин.
* Кнопка BACK из подменю скинов возвращает в список, из которого оно открыто.
* Состояние «что выбрано» хранится в памяти плагина на время сессии игрока.

## Триггеры (v0.1.1, сборка 0.0.4)

Свой вид окна рисуется на сайте CSCreatePanorama (`menumanager/create/index.html?preset=weaponpaints`).
Сайт отдаёт ZIP: `wp.xml`, `wp.css`, `wp_layout.css` (клиент) и `wp_triggers.json` (сервер).

`wp_triggers.json` лежит рядом с DLL и говорит, что делает каждая кнопка (id панели → действие):

| Действие | Что делает |
| --- | --- |
| `close` | закрыть меню |
| `back` | назад: из скинов в список, из списка на главную, с главной закрыть |
| `home` | главная (Loadout) |
| `open:knife` / `open:gloves` / `open:skins` / `open:agents` / `open:music` / `open:pins` | открыть раздел WeaponPaints |
| `weapons:pistols` / `rifles` / `snipers` / `smg` / `heavy` | список оружия только этой группы |
| `weapons:AK-47,M4A4` | свой список оружия (имена как в меню WeaponPaints) |
| `page:prev` / `page:next` | листать страницы |
| `cmd:css_команда` | выполнить команду игрока (только `css_…`) |

Группы можно переопределить в `groups` (см. `wp_triggers.example.json`).
Кнопка, которая открывает раздел, получает класс `is-cur`, пока раздел открыт.
Без `wp_triggers.json` плагин работает как раньше со стандартным `wp.xml`.

Файл читается при загрузке плагина: `css_plugins reload PMM_WeaponPaints` или рестарт сервера.

Сервер: `addons/counterstrikesharp/plugins/PMM_WeaponPaints/PMM_WeaponPaints.dll` (рядом `icons.json`, `wp_triggers.json`).

Клиент, в тот же аддон, что уже собран в Workshop Tools:

`csgo_addons/<аддон>/panorama/layout/custom_game/wp.xml`

`csgo_addons/<аддон>/panorama/styles/custom_game/wp.css`

`csgo_addons/<аддон>/panorama/styles/custom_game/wp_layout.css` (только для вида с сайта)

После замены DLL сервер нужно полностью перезапустить. После сборки аддона игрок заходит заново.
