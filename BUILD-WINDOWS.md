# Собрать игру для Windows

В Unity открой **File → Build Profiles**, выбери профиль **Windows** и нажми **Switch Profile**, если профиль ещё не активен. Платформа должна быть **Windows**, архитектура — **Intel 64-bit**, Scripting Backend — **Mono**. Нажми обычную кнопку **Build** и выбери папку для игры. Результат — `.exe` и файлы игры рядом с ним.

Если профиль Windows уже помечен активным, но Unity предлагает сохранить `.app`, переключись на другой профиль и обратно на Windows: фактическая платформа редактора должна быть Windows, а не macOS.

Для сборки с готовым ZIP также доступно меню **Svinki → Сборка Windows → Собрать EXE и ZIP**.

Если запущена игра, сборщик сам остановит Play Mode. Если сцены изменены, Unity предложит их сохранить. После завершения откроется папка `Builds`.

- **`Builds/Windows/Svinki.exe`** — готовая игра для Windows 64 bit.
- **`Builds/Svinki-Windows.zip`** — архив, который можно отправить друзьям.

Другу нужно распаковать **весь архив** и запустить `Svinki.exe`. Все файлы и папка `Svinki_Data` должны оставаться рядом с EXE.

После изменений в проекте просто повтори ту же команду: игра и ZIP обновятся. При ошибке самой сборки предыдущая готовая версия останется на месте. Пункт **Открыть папку сборок** открывает результаты в любой момент.

## Что сборщик делает автоматически

- Собирает обычную release-версию Windows x64 с Mono, включая новые изменения в скриптах и ассетах.
- Ставит `Lobby` первой сценой, включает `SampleScene` и остальные включённые сцены из Build Settings.
- Упаковывает игру вместе с EOS для онлайна и создаёт ZIP без служебной папки резервных символов Unity.
- После успешной сборки оставляет Windows x64 и Mono активными, чтобы следующая обычная сборка через Unity тоже создавала EXE. При ошибке возвращает прежнюю платформу и scripting backend.

## Если сборка не запускается

**Нет модуля Windows:** в Unity Hub открой **Installs → шестерёнка у версии редактора → Add modules → Windows Build Support (Mono)**. На Windows поддержка Windows обычно уже входит в редактор; на Mac нужен соответствующий модуль. Используй версию редактора из `ProjectSettings/ProjectVersion.txt`.

**Нет EOS-конфигурации:** настрой онлайн по `MULTIPLAYER.md`. Нужен рабочий `Assets/StreamingAssets/svinki-eos.json` и настройки EOS Plugin. В этом проекте они уже настроены локально; при переносе проекта на другой компьютер нужно перенести и эти локальные настройки.

**Включён `EOS_DISABLE`:** убери этот символ из **Player Settings → Other Settings → Scripting Define Symbols** для настольной платформы. Он используется только для локальных тестов без EOS.

Остальные ошибки Unity показывает в **Window → General → Console**. Исправь ошибку и повтори сборку.

Справка Unity: [Build Profiles](https://docs.unity.com/en-us/engine/6000.6/manual/building-and-publishing/build-settings/create-build-profile), [скрипты сборки](https://docs.unity.com/en-us/engine/6000.6/manual/building-and-publishing/build-customize-build-pipeline/build-script-build).
