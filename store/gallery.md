# Скриншоты: что где стоит

Один набор на все площадки. Файлы лежат в `mod/media/`, снимались на 1.3.0.
Подписи английские: страницы на Nexus, Thunderstore и Hexium английские.

| Файл | Подпись (Nexus: title) | Nexus | Hexium | README |
| --- | --- | --- | --- | --- |
| `01-in-game.png` | The planner open in game, F7 from anywhere | 1, главная | 1 | да, первым |
| `02-planner.png` | A plan of four items: quality, quantity, what to gather, which stations and how far | 2 | 2 | да |
| `03-card-compare.png` | Hover a row: the item card against what you wear, with the recipe below | 3 | 3 | да |
| `13-card-weapon.png` | Weapons too: damage, block, parry, knockback, stamina per attack | 4 | 4 | — |
| `04-card-set-bonus.png` | Hold Alt: the set bonus in full, and how much of the set you wear | 5 | 5 | да |
| `05-pinned.png` | Pin the plan: it stays next to the minimap and counts what you carry | 6 | 6 | да |
| `06-recipes-mode.png` | Recipes mode: what the station asks for, plus the kiln for the coal | 7 | 7 | — |
| `07-settings.png` | Settings: spoiler-free catalogue, nearby chests, item card | 8 | 8 | да |
| `09-station-button.png` | The ForgePlanner button on every crafting station | 9 | 9 | да |
| `08-about.png` | Everything comes from your copy of the game | 10 | 10 | — |
| `10-russian.png` | Russian interface, switched in one click | 11 | 11 | — |
| `11-russian-card.png` | The item card in Russian | 12 | 12 | — |
| `12-russian-settings.png` | Settings in Russian | 13 | 13 | — |
| `14-planbuild-button.png` | With PlanBuild installed: the + Totem button above the plan | 14 | 14 | — |
| `15-planbuild-totem.png` | A PlanBuild totem as one row: what it still needs, piece by piece | 15 | 15 | да |
| `16-planbuild-pinned.png` | The totem's list pinned next to the minimap while you build | 16 | 16 | — |

Кадры PlanBuild (14–16) сняты на 1.5.0 и добавлены в конец, после русских:
так старые не пришлось переставлять.

Главная картинка на Nexus — `01-in-game.png`: 1919×1067, уменьшенная копия
в списке модов читается. Счётчик FPS в левом верхнем углу срезан.

## Порядок замены

1. **Nexus** (галерея правится в любой момент): удалить старые картинки,
   загрузить новые по таблице, первую сделать главной. Кнопки Upload и
   Delete нажимает автор.
2. **README для Thunderstore и Hexium** собирается из `store/page.md`, а
   картинки в нём — ссылки на галерею Nexus. После шага 1 открыть каждую
   картинку строки «да» в галерее, скопировать адрес
   (`staticdelivery.nexusmods.com/...`) и вписать в `store/nexus/images.json`.
   `package.py` без них не соберёт архив. На площадки попадёт с архивом 1.3.0.
3. **Hexium**: галерея отдельная, порядок перетаскиванием. Загрузить те же
   файлы в том же порядке.
4. **Руководство Steam** берёт `10-russian.png` и `11-russian-card.png`,
   английское — `02-planner.png` и `03-card-compare.png`. Ждёт стабильной
   версии, как и числа в нём.

## Как снимать заново

- Интерфейс мода английский, кроме трёх русских кадров в конце.
- Бронзовый набор в плане: Bronze Plate Leggings, Tunic, Helmet, Axe — те
  же числа на `01` и `02`.
- Счётчик FPS Steam выключить или срезать.
- `04` снят с зажатым Alt: описание Sneaky на месте рецепта. Остальные кадры
  карточки сняты до подсказки «Hold Alt» в заголовке рецепта — автор решил их
  не менять: подсказка есть только у вещей с бонусом, на `03`, `11`, `13` её
  и не было бы (бронзовый шлем и булава без бонуса).
- Названия эффектов и бонусов комплекта (`Sneaky (4)`) приходят из игры и
  идут на её языке, а не на языке окна: своей русской таблицы для них нет.
  Поэтому русская карточка — шлем, у которого бонуса комплекта нет.
- Во вкладке «All» первой строкой не должно стоять ничего в квадратных
  скобках: с 1.3.0 такие вещи в каталог не попадают.
- Снимки Windows (Win+Shift+S) лежат в `Pictures/Screenshots` без сжатия;
  брать оттуда, а не из пересланных копий.
