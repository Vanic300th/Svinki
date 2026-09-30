# Простой грейбокс в SampleScene

Открой `Assets/Scenes/SampleScene.unity` и нажми **Play**. Если клавиши не реагируют, щёлкни по вкладке **Game**.

Управление в **Game**: **WASD** или стрелки — движение, **мышь** — поворот взгляда, **Space** — прыжок, **Escape** — освободить курсор. Чтобы снова захватить мышь, щёлкни по окну Game.

Для свободного осмотра уровня **вне игры** переключись с вкладки **Game** на **Scene**. В Scene удерживай правую кнопку мыши и используй **WASD** для полёта по сцене; **Q/E** — вниз/вверх, **Shift** — быстрее. Это камера редактора, она не привязана к Player.

## Что находится в сцене

- `Graybox Level/Floor` — большой куб размером `22 × 0.5 × 22`. Его верхняя поверхность на `Y = 0`.
- `Graybox Level/* Wall` — четыре стены. `Block A/B/C` — препятствия. У всех этих кубов есть `Box Collider`, который Unity добавляет при создании примитива.
- `Directional Light` — один источник света из исходной сцены. В Inspector можно менять его Rotation, Intensity и Shadows.
- `Player` — пустой объект с `Character Controller` и `Graybox Player Controller`. Дочерний `Cylinder Visual` показывает персонажа в Scene. У визуального цилиндра удалён собственный коллайдер: за столкновения отвечает только `Character Controller`.
- `Main Camera` — исходная камера с `Graybox First Person Camera`. Поле `Target` указывает на `Player`. Камера стоит на высоте глаз и поворачивается мышью. Слой `Ignore Raycast`, на котором находится цилиндр, исключён из изображения этой камеры, поэтому он не закрывает вид.

Исходный объект `Cube`, который уже был в открытой сцене, сохранён отдельно от созданного грейбокса.

## Как повторить вручную

1. Создай `GameObject > 3D Object > Cube`, назови `Floor`, задай Position `(0, -0.25, 0)` и Scale `(22, 0.5, 22)`. Оставь `Box Collider` включённым.
2. Дублируй кубы для стен и препятствий. Стены поставь по краям пола; не удаляй их `Box Collider`.
3. Оставь один `Directional Light` и поверни его так, чтобы стены и персонаж отбрасывали читаемые тени.
4. Создай пустой объект `Player` в `(0, 0.05, -3)`. Добавь `Character Controller`: Height `2`, Radius `0.45`, Center `(0, 1, 0)`, Step Offset `0.3`.
5. Добавь в `Player` примитив `Cylinder` как дочерний объект. Установи Local Position `(0, 1, 0)`, Scale `(0.8, 1, 0.8)` и удали с него `Capsule Collider`. Поставь `Player` и цилиндру Layer `Ignore Raycast`, чтобы проверка стен камерой не попадала в персонажа.
6. Добавь на `Player` скрипт `GrayboxPlayerController`. В поле `Camera Transform` перетащи `Main Camera`.
7. Добавь на `Main Camera` скрипт `GrayboxFirstPersonCamera`. В поле `Target` перетащи `Player`. Поставь камеру на высоту глаз (`Player Position + (0, 1.65, 0)`) и исключи слой `Ignore Raycast` из её Culling Mask. Камера должна иметь тег `MainCamera`.
8. Сохрани сцену (`Cmd+S`) и нажми **Play**.

Скрипт движения: `Assets/Scripts/GrayboxPlayerController.cs`. Скрипт камеры: `Assets/Scripts/GrayboxFirstPersonCamera.cs`. Они используют установленный в проекте **Input System**. Скорость движения, высота прыжка, высота глаз и чувствительность мыши меняются в Inspector.

Команда **Tools > Graybox > Build Demo Scene** собирает этот пример заново. Она пересоздаёт `Graybox Level` и `Player`, поэтому после ручных изменений этих объектов повторно запускай её только если хочешь восстановить исходный пример. Команда **Tools > Graybox > Set First Person Camera** настраивает камеру в уже открытой сцене без пересоздания уровня.
