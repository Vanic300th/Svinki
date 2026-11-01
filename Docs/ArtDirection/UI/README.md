# Направление UI Project S.V.I.N. — эскизы

Дата: 21 октября 2026 года.

В проект подключено редактируемое оформление Unity. Ранние изображения, созданные встроенным ImageGen, сохранены как визуальные референсы. Исходники PSD/SVG и инструкция по редактированию находятся в `ArtSource/UI/ProjectSvin`; рабочая тема и prefab — в `Assets/Resources/UI`.

## Решения из обсуждения

- Главное меню сохраняется, кастомизация свинки справа.
- Пользователь выбрал отдельные кнопки: «Создать лобби», «Войти по коду», «Играть одному», «Как играть», «Настройки», «Выход».
- Одна система оформления для всей игры: меню, лобби, настройки, внешность, одежда, инвентарь, тележка, подсказки, фотоальбом, результаты и HUD.
- Пользователь выбрал название **Project S.V.I.N.** Точное написание сохраняет точки после каждой буквы сокращения. В логотипе «Project» стоит небольшой строкой над крупным «S.V.I.N.». Эскиз v2 отражает выбранное название; код игры этой правкой не переименован.
- Пользователь запросил перенос этого оформления в игру с возможностью отдельно менять текст, фон, цвета и графические элементы, а также PSD для Photoshop / Photopea.

## Изображения

- `project-svin-main-menu-concept-v2.png` — актуальный эскиз главного меню и кастомизации с названием Project S.V.I.N.
- `svinki-main-menu-concept-v1.png` — предыдущий эскиз с рабочим названием SVINKI, сохранён для истории.
- `svinki-ui-system-concept-v1.png` — настройки, инвентарь, пример игрового HUD.

## Визуальная система

| Роль | Цвет |
|---|---|
| Тёмная основа | `#241921` |
| Панели | `#3D2633` |
| Второстепенные кнопки | `#9F5C77` |
| Главный акцент и контуры | `#EDB3C5` |
| Основной светлый текст | `#F8E9E4` |

Матовые бордовые поверхности, умеренное скругление, тонкий розовый кант, крупный читаемый шрифт с кириллицей. Названия окон оформлены как тканевые бирки. Наклейки, вешалки, геймпады и следы копыт — редкие фоновые акценты. Пятачок с двумя ноздрями используется как бегунок ползунка. Выход имеет иконку копыта и текстовую подпись.

Главная кнопка светлая; второстепенные действия темнее. Выбор обозначается контуром, формой или значком вместе с цветом. Для игровой сцены — компактные элементы и меньше декоративных рамок. Окружение и модель персонажа остаются узнаваемыми; это направление UI, а не перекраска всего игрового мира.

## При последующей реализации

Изображения задают художественное направление, а не новую игровую механику. Следует сохранить текущие функции, условия видимости и доступности действий, локализацию и сохранения. «Продолжить с контрольной точки» остаётся доступным в подходящем состоянии; его размещение нужно включить в окончательный макет. Число доступных ударов в HUD соответствует существующей механике: два. Дополнительные пустые кружки на сгенерированном рисунке не задают новые значения здоровья. Представленные предметы одежды иллюстративные; реализация использует реальные предметы из каталога проекта. Набор настроек звука при реализации следует сверить с фактически поддерживаемыми настройками.

## Промпты для продолжения и воспроизведения направления

Основной использованный запрос нормализован ниже; точные запросы генерации также сохранены в истории этого чата.

### Главное меню

Исходный эскиз v1; для актуального названия поверх него применена точечная правка из `project-svin-logo-edit-prompt.txt`.

> High fidelity 16:9 PC game UI mockup for the existing SVINKI coop game. Preserve functional main menu on the left and actual pig customization preview on the right. Use the existing potato-shaped 3D pig with triangular ears, small snout, dark hoof hands and feet, round spectacles and moustache. Existing worn industrial locker room is a softly defocused background. Replace navy/cyan with matte warm plum #241921, berry panels #3D2633, dusty rose controls #9F5C77, pale blush #EDB3C5 and ivory #F8E9E4. Minimalism with restrained fabric clothing labels, stickers, thin blush keylines, soft corners, clear readable Cyrillic typography. Wordmark 'SVINKI'. Nickname field 'Никнейм', value 'Игрок'. Six buttons exactly 'Создать лобби', 'Войти по коду', 'Играть одному', 'Как играть', 'Настройки', 'Выход'. First action strongest; exit quiet with split hoof icon. Right panel title 'ТВОЯ СВИНКА', large full body actual pig, rotation arrows, tabs 'Внешность', 'Одежда', 'Аксессуары', skin swatches 'Цвет кожи' and pattern selector 'Узор', 'Без узора'. Small 'RU / EN' language choice. No browser chrome, no unrelated game mechanics, no neon, no generic website.

Референсы: `ArtSource/EnglishHud/menu.png`, `ArtSource/Pigs/v1/three_pigs_preview.png`, `ArtSource/Bugfix/native-lobby.png`.

### Продолжение системы

> Landscape 16:9 game UI continuation sheet matching the main menu image exactly: dark plum, burgundy, dusty rose, pale blush outlines, ivory Cyrillic type, matte garment labels and restrained stickers. Two large separate sample windows above, a shallow gameplay sample below. Left 'НАСТРОЙКИ' panel: tabs 'Звук', 'Игра', 'Управление'; sliders 'Общая громкость', 'Музыка', 'Голос друзей' with small oval two-nostril pig-snout thumbs; push-to-talk toggle and 'Готово'. Right 'ИНВЕНТАРЬ': actual pig preview wearing burgundy hoodie and cream trousers, 2 by 3 garment grid, category tabs 'Всё', 'Одежда', 'Аксессуары'; selection outline and small hoof stamp; 'Бордовое худи' and 'Надеть'. Bottom: actual industrial locker room with restrained compact burgundy HUD, pig portrait, remaining-hit indicators, simple minimap and short key prompts. Much less decoration in gameplay than in menus. No combat stats, currencies, battle passes, unrelated gear systems, navy/cyan panels or neon.

Референсы: `svinki-main-menu-concept-v1.png` и `ArtSource/Bugfix/native-lobby.png`.
