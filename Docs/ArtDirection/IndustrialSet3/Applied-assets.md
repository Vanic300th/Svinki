# Реальные ассеты в Friendslop

Сцены `Assets/_Project/Scenes/Lobby/FriendslopLobby.unity` и `Assets/_Project/Scenes/Gameplay/SampleScene.unity` оформлены реальными моделями и текстурами из бесплатных CC0-паков. Концепты остаются ориентиром для следующего прохода по окружению, а не обещанием точного совпадения с текущим уровнем.

## Что установлено

| Объект | Источник | Применение |
| --- | --- | --- |
| Потёртый кожаный диван | [Poly Haven — Sofa 02](https://polyhaven.com/a/sofa_02) | Диван и адаптированное кресло в зоне отдыха |
| Журнальный стол | [Industrial Coffee Table](https://polyhaven.com/a/industrial_coffee_table) | Центральный стол зоны отдыха |
| CRT-телевизор | [Television 01](https://polyhaven.com/a/Television_01) | Корпус с сохранённым экраном RenderTexture и VideoPlayer |
| Трубы и вентиль | [Modular Industrial Pipes](https://polyhaven.com/a/modular_industrial_pipes_01) | Лобби и завод |
| Воздуховоды | [Modular Airduct Rectangular](https://polyhaven.com/a/modular_airduct_rectangular_01) | Потолок лобби |
| Рабочий ящик | [Metal Tool Chest](https://polyhaven.com/a/metal_tool_chest) | Лобби, подставка проигрывателя, заводские рабочие столы |
| Настенные светильники | [Industrial Wall Sconce](https://polyhaven.com/a/industrial_wall_sconce) | Декоративные корпуса в обеих сценах |
| Магнитола | [Boombox](https://polyhaven.com/a/boombox) | Временный корпус существующего сетевого музыкального проигрывателя |
| Двери и ручки шкафчиков | [Innerscene — Locker Bank](https://www.innerscene.com/tools/library/3d-parts/lock-3-office-locker-bank-c5ce4b13) | Адаптированы под шесть существующих персональных шкафчиков |
| Кольцо, щит и сетка | [Innerscene — Wall Basketball Hoop](https://www.innerscene.com/tools/library/3d-parts/wall-basketball-hoop-sports-equipment-7cfb56b9) | Баскетбол с существующими триггерами и коллайдерами |
| Баскетбольные мячи | [OpenGameArt — Basketballs](https://opengameart.org/content/basketballs), digitaln8m4r3 | Три существующих сетевых мяча |
| Мишень | [Poly Haven — Dartboard](https://polyhaven.com/a/dartboard) | Дартс; сектор 20 сверху, радиус подсчёта согласован с новой моделью |
| Дротики | [OpenGameArt — Dartboard Set](https://opengameart.org/content/dartboard-set), RayB2 | Четыре существующих сетевых дротика |
| Банки | [Can Rusted](https://polyhaven.com/a/can_rusted) | Два небольших бросаемых предмета |
| Бутылка очистителя | [Multi Cleaner Bottle](https://polyhaven.com/a/multi_cleaner_bottle) | Бросаемый предмет |
| Коробки | [Cardboard Box 01](https://polyhaven.com/a/cardboard_box_01) | Бросаемая коробка и декор |
| Подушка | [Throw Pillows 01](https://polyhaven.com/a/throw_pillows_01) | Бросаемая подушка |
| Резиновая утка | [Rubber Duck Toy](https://polyhaven.com/a/rubber_duck_toy) | Бросаемый предмет |
| Переносной инструментальный ящик | [Metal Toolbox](https://polyhaven.com/a/metal_toolbox) | Бросаемый предмет |
| Пластиковая канистра | [Plastic Bottle Gallon](https://polyhaven.com/a/plastic_bottle_gallon) | Бросаемый предмет |
| Трещотка | [Ratchet Wrench](https://polyhaven.com/a/ratchet_wrench) | Бросаемый инструмент |
| Мешок цемента | [Cement Bag](https://polyhaven.com/a/cement_bag) | Декор комнаты |

Покрытия: [Concrete Floor Worn 001](https://polyhaven.com/a/concrete_floor_worn_001), [Worn Corrugated Iron](https://polyhaven.com/a/worn_corrugated_iron), [Rusty Painted Metal](https://polyhaven.com/a/rusty_painted_metal), [Dirty Carpet](https://polyhaven.com/a/dirty_carpet).

Модели, текстуры, URP-материалы и визуальные префабы находятся в `Assets/_Project/Content/Environment/IndustrialCC0`. У каждого источника есть `source.json`; исходные FBX и необходимые текстуры включены в проект. Исходные Blender-файлы находятся в `ArtSource/IndustrialCC0`. Все перечисленные источники обозначены CC0; [политика Poly Haven](https://polyhaven.com/license), [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). Ссылки и авторы сохранены для происхождения ассетов.

## Что сохранено

- FishNet, EOS, контроллер игрока и существующая логика взаимодействий.
- 245 корневых NetworkObject и их SceneId в двух игровых сценах.
- Шесть назначаемых шкафчиков, их петли дверей, таблички и 36 Photo Slots.
- 17 сетевых физических предметов, их Rigidbody, Pickup/Throw и reset. Визуал заменён у 16; шайба аэрохоккея осталась прежней.
- Система очков баскетбола и дартса. Новые визуальные модели не добавляют сетевых компонентов или физики.
- Состояние TV/музыки, очередь, локальная загрузка медиа и пространственный звук.
- Планировка завода, коллайдеры, навигация, одежда, манекены и оба перехода через лифт.

В лобби добавлены мягкие источники света без теней. Большие статические поверхности обеих сцен получили более плотную визуальную сетку для существующего освещения по вершинам; коллайдеры и настройки URP не менялись. Текстуры ограничены 1K, общие материалы и префабы переиспользуются.

## Ограничения этого прохода

Industrial Set 3.0 **не импортирован**: он отсутствует в локальном кэше, Unity Editor и Asset Store не авторизованы. Для установки нужно войти в Unity, добавить [пак в My Assets](https://assetstore.unity.com/packages/3d/environments/industrial/rpg-fps-game-assets-for-pc-mobile-industrial-set-v3-0-101429) и скачать его через Package Manager. Ангары, контейнеры и мостки из этого пака пока не заменяют архитектуру завода.

Ранее выбранный автомат BlendSwap требует входа. Вместо него пока используется реальная магнитола на инструментальном ящике, привязанная к существующему LobbyJukebox. Дартс заменён доступной альтернативой без аккаунта. Стол аэрохоккея, доска и корпуса лифтов остаются модульными прототипами.

## Повторное применение и проверка

В чистом Edit Mode:

```sh
unity command run_script --file Tools/Unity/FriendslopArtSetup.cs --entry FriendslopArtSetup.Apply --caller plugin --skill unity-cli --format json --timeout_ms 60000 --timeout 70
```

Authoring-проход сохраняет обе сцены и проверяет сохранность сетевых корней и SceneId. Помощник `Assets/_Project/Editor/IndustrialArtAssets.cs` создаёт только визуальные префабы и URP-материалы. Он не включается в runtime-сборку.

Проверки: `FriendslopSceneCheck.Check` — состав сцен, сетевые ID и маршруты к лифтам; `PhotoRoundCheck.Spawns` — 64 варианта появления предметов и манекенов на заводе; `FriendslopArtPlayCheck.Check` — нормальная offline-сессия, подбор/бросок/reset заменённых предметов и реальные физические попадания. Последнюю запускают только после входа в offline-лобби, вне сетевой комнаты.

Скриншоты из Unity сохраняет `FriendslopArtPreview.Capture` в `ArtSource/IndustrialCC0/Previews`. Это рендеры игровых сцен, а не генерация изображения. Windows-билд не выполнялся; проверка Host/Client/Late Join с этими моделями остаётся отдельным ручным прогоном.

Текущий результат: все перечисленные проверки пройдены, включая реальные триггеры кольца, столкновение дротика, возврат предметов и рабочую привязку экрана телевизора. Проверка размещения заводского содержимого охватила 64 seed и 7360 размещений. Служебные отчёты удалены при очистке проекта.

При открытии завода Unity также сообщает о ранее отсутствовавшем префабе `hoodie1` (GUID `7c5f534ed257b4eaf99b19575c37029b`). Этот проход по окружению не исправляет и не удаляет исходную ссылку на одежду.
