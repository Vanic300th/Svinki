# YouTube TV и поиск музыки

Телевизор принимает обычные ссылки YouTube, а музыкальный автомат ищет треки ccMixter с лицензией CC BY по названию песни или исполнителя. FishNet/EOS сохраняют существующую архитектуру; медиа загружает каждый клиент самостоятельно. Браузерный компонент используется только для YouTube. Прямые MP4/WebM/OGV/MOV и MP3/OGG/WAV остаются доступны.

## Как пользоваться

1. Запустить игру из `Assets/Scenes/Lobby.unity` и войти в Friendslop.
2. Подойти к телевизору, нажать E, вставить ссылку `https://www.youtube.com/watch?v=...` или `https://youtu.be/...`, нажать PLAY URL. Поддерживаются также `/shorts/`, `/live/` и `/embed/` с корректным 11-символьным ID.
3. Если браузер блокирует автозапуск, нажать **CLICK TO PLAY** под изображением в панели TV. После начала воспроизведения эта кнопка меняется на PAUSE и тоже управляет общей паузой. Это клик в браузере; другим игрокам может понадобиться свой первый клик. Общие PLAY/PAUSE, SEEK +10 и STOP управляют состоянием для всей комнаты. VOLUME и MUTE изменяют только локальную громкость.
4. У музыкального автомата нажать E, в поле **Song or artist name** ввести запрос (например, `chill`) и нажать SEARCH; пустой запрос показывает свежие ремиксы. Нажать найденный трек, чтобы добавить его в общую очередь. Кнопка WEB открывает страницу оригинального источника во внешнем браузере. Также можно вставить ссылку на страницу трека ccMixter `/files/artist/ID` или прямой MP3/OGG/WAV; прежние ссылки Audius также поддерживаются.
5. SKIP запускает следующий трек; пустая очередь останавливается. Пока проигрыватели остановлены/на паузе, существующая фоновая музыка лобби возвращается.

ccMixter — независимые ремиксы, не база популярных песен. Официальный Query API не требует ключа: lic=by, reqtags=remix, до 12 записей с отображением шести подходящих полных MP3. Дополнительно проверяются точная CC BY лицензия (1.0/2.0/2.5/3.0/4.0), источник, тип аудио, размер до 32 MB, длительность 30 секунд–30 минут и NSFW flag. CC BY-NC/ND/SA и неизвестные лицензии отклоняются. Указываются автор/featuring, источник и ссылка на лицензию. Поиск имеет двухсекундный cooldown. Закрытие меню и перемещение по комнате не останавливают AudioSource.

В повторной проверке chill вернул треки; запросы «русский» и «Баста» — ноль результатов. Результаты russian не означают русский вокал. Наличие известных русских исполнителей не обещается.

## Сеть и проверка запросов

TV сохраняет URL/ID, версию видео, Play/Pause, позицию, network time начала и автора в существующем `LobbyMediaState`/SyncVar. Jukebox сохраняет ту же временную шкалу и очередь SyncList. Запросы используют существующий owned-player ServerRpc с проверкой комнаты, живого игрока, расстояния, препятствий и частоты. Сервер дополнительно проверяет ccMixter/Audius metadata и повторяет проверку аватара/расстояния после ответа внешнего API. Клиент не передаёт доверенные название, длительность, права доступа или временный CDN-токен в очередь: сервер получает их сам. Для ccMixter в сети остаётся каноническая страница /files/artist/ID. Каждый клиент получает metadata по ID, повторно проверяет CC BY и выбирает допустимый HTTPS MP3 с домена ccMixter. Недоступные/сменившие лицензию треки не проигрываются; host пропускает их. Аудиозапрос ccMixter передаёт Referer проверенной страницы источника: в проверке сервер файла возвращал 403 без него и 206 audio/mpeg с ним. Для Audius остаётся стабильный /v1/tracks/ID/stream. Прямые /content/*.mp3 ссылки ccMixter нельзя вводить вместо страницы: это обходило бы проверку лицензии.

Максимум 12 треков в очереди, 3 на игрока вместе с текущим треком и незавершёнными metadata-запросами; двухсекундный cooldown добавления, запрет дубликатов текущего/стоящего в очереди трека. HTTP/HTTPS, абсолютный URL, максимум 2048 символов, отсутствие URL credentials; для сервисов — точный домен, ID и стандартный порт. `file://`, JS и похожие домены отклоняются. Metadata-запросы ограничены 12 секундами и 512 KB; аудио — 20 секундами и 32 MB по исходным Serialized Fields.

YouTube использует официальный IFrame API, не извлекает потоки и не скачивает видео. HTML берётся из единственного bundled-файла StreamingAssets через случайный localhost-маршрут. Сервер страницы слушает только loopback; не обслуживает файловую систему, чужой HTML или proxy. Его порты и browser IPC локальны и не связаны с FishNet/EOS. При Stop, смене источника, выходе из сцены и уничтожении TV браузер закрывается.

Поздний вход получает существующие SyncVar/SyncList, включая network time: локальный плеер вычисляет нужную позицию после загрузки. Коррекция YouTube выполняется локально раз в 0.5 секунды; texture выводится максимум 20 fps. Ничего из этого не отправляется в FishNet каждый кадр. Переключение между прямым видео и YouTube отменяет загрузку предыдущего источника.

**Текущая версия session protocol — 24. Все участники должны обновить проект/игру одновременно.** EOS authentication, lobby и transport не переделывались.

## Зависимости и платформы

В UPM установлены UnityWebBrowser/CEF **2.2.8** и движки Windows x64, macOS x64/arm64 через VoltUPR (`https://upr.voltstro.dev`). UniTask закреплён на официальном Git-теге **2.5.11**, исправляющем несовместимость старого Editor TreeView API с Unity 6. Native пакеты крупные: первый импорт всех трёх платформ занимает примерно 1 GB скачивания. Не нужно добавлять их содержимое из Library в git.

Ядро UnityWebBrowser находится в `Packages/dev.voltstro.unitywebbrowser` как embedded package с сохранённой MIT-лицензией. **Эту папку, Packages/manifest.json и packages-lock.json нужно передать через git.** В ядре исправлен выбор ОС: macOS Editor с Windows build target использует Mac-процесс; Windows player использует Windows-процесс. Кроме того, разрешена настройка Resolution до Init: TV запускается сразу в 640×360 без лишнего resize и full-HD buffers. Подробности в `FRIENDSLOP-PATCH.md`. Native engine binaries не менялись. UPM устанавливает их по зафиксированным версиям при открытии проекта; установка не требует Unity Asset Store или платного браузерного ассета.

Поддерживаемая конфигурация этой интеграции — desktop с Mono. IL2CPP, WebGL, мобильные платформы и Linux здесь не подключены/не проверены; browser RPC-зависимость требует отдельной проверки AOT перед сменой backend. Текущий Windows build target не менялся. Windows-сборка в этой работе не выполнялась.

Ограничения YouTube:

- Автор может запретить встраивание; приватное/возрастное/региональное видео может быть недоступно. Ошибки 101/150, 153 и другие ошибки плеера показываются локально.
- Штатные controls, branding и реклама сохраняются. Для рекламы, буферизации и первоначального разрешения автозапуска нельзя гарантировать покадровое совпадение клиентов.
- YouTube работает как видимый телевизор: вне поля зрения и без открытой панели он локально ставится на паузу. Это не источник фоновой музыки и не audio-only player.
- YouTube-звук выходит из native browser: применяется локальная громкость и затухание по расстоянию, но нет Unity AudioMixer routing, HRTF/направленного 3D-панорамирования. Direct TV, ccMixter и Audius используют прежние 3D AudioSources.
- В macOS CEF проверка кликов внутри cross-origin iframe не прошла: клики не меняли состояние видео даже после фокуса. Используйте рабочие общие кнопки панели TV и CLICK TO PLAY/PAUSE под изображением. Штатные элементы iframe остаются видимыми; их мышь, captions/fullscreen и реклама требуют отдельной проверки в Windows player. Browser security не отключалась ради обхода этой проблемы.
- Browser identity/Referer формируется локальной HTTP-страницей и application identifier; если YouTube меняет требования или выдаёт 153 в конкретном player build, нужен отдельный платформенный тест. API key для IFrame не требуется.

Публичные Audius read endpoints используются без ключа; их доступность и лимиты зависят от сервиса. Ошибка API отображается в UI, запрос имеет timeout. Архитектура `IMusicCatalogProvider` позволяет позже добавить другой официальный каталог или серверный API-adapter без замены очереди и FishNet.

## Проверка ccMixter

Tools/Unity/CcMixterCheck.cs проверяет URL/лицензии/некорректные metadata. OnlineMediaCheck.Music() теперь проверяет основной поиск ccMixter; новые отчёты называются ccmixter-*-validation.txt, прежние отчёты Audius сохранены как исторические. Пассивное воспроизведение проверяется после закрытия меню и ухода от автомата. Remote Client/Late Join и Windows playback требуют отдельного прогона.

На FishNet Host прошли: реальный поиск через UI, повторная серверная проверка CC BY, декодирование MP3 The Fade Out, название/автор/источник/лицензия, pause/resume/mute, запрет дубликатов, очередь/skip следующего реально декодированного трека, продолжение аудио после закрытия меню и ухода игрока, остановка пустой очереди и один EventSystem. Результат: ArtSource/OnlineMedia/ccmixter-host-validation.txt. Проверка некорректных metadata и лицензий: ccmixter-safety-validation.txt. Компиляция Unity 6000.6.2f1 прошла; сборки не выполнялись.

## Предыдущие проверки YouTube/Audius

`Tools/Unity/OnlineMediaCheck.cs` запускается через подключённый Editor без сборки игры. Для DirectVideo нужен `python3 Tools/OnlineMediaHttpFixture.py /tmp/svinki-social-media`: он переиспользует Range HTTP handler из старой multiplayer-проверки и обслуживает только loopback; MP4 создаётся через `Tools/SocialLobbyVideoFixture.swift`. Обычный Python http.server без byte ranges не подходит для AVFoundation seek. Результаты лежат в `ArtSource/OnlineMedia`:

- URL validation: канонизация YouTube/Audius, сохранение direct MP3/MP4, отклонение file/JS, credentials, неверных ID/портов/доменов и YouTube audio-only.
- Direct video Host: реальный локальный HTTP H.264 MP4 после YouTube, подготовка и продвижение кадра, длительность, pause, seek, resume, stop.
- Live music: реальный поиск через UI, host metadata validation, декодирование слышимого MP3, название/исполнитель/длительность, pause/resume, локальный mute, дубликаты и Skip.
- Live YouTube: официальный IFrame/CEF на RenderTexture, длительность и продвижение реального плеера, native footer CLICK TO PLAY/PAUSE, shared pause, seek/resume и Stop. Отдельный native-iframe-input.txt честно фиксирует ограничение мыши внутри iframe на macOS.

Offline и FishNet Host прошли для музыки и YouTube. Отчёты Host отдельно включают очередь/Skip на следующий реально декодированный трек и остановку native browser; наличие отчёта с PASS подтверждает выполненный прогон. Remote Client/Late Join, EOS internet session и Windows native playback этой новой интеграции пока требуют проверки на отдельных обновлённых экземплярах. Старые Host/Client/Late Join отчёты social lobby относятся к прямым media URLs, а не к новым провайдерам.

## Источники

- [YouTube IFrame API](https://developers.google.com/youtube/iframe_api_reference)
- [YouTube minimum player requirements](https://developers.google.com/youtube/terms/required-minimum-functionality)
- [YouTube developer policies](https://developers.google.com/youtube/terms/developer-policies)
- [Audius API](https://docs.audius.co/api/)
- [UnityWebBrowser](https://github.com/Voltstro-Studios/UnityWebBrowser)
- [UniTask 2.5.11](https://github.com/Cysharp/UniTask/releases/tag/2.5.11)

- [ccMixter Query API](https://ccmixter.org/query-api)
- [ccMixter API permission and licenses](https://ccmixter.org/about)
- [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/)
