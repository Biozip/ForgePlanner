# Nexus Mods

**Страница:** https://www.nexusmods.com/valheim/mods/3879
**Категория:** User Interface.

Вторая загрузка каждого выпуска, после Thunderstore; порядок целиком — в
[docs/RELEASING.md](../../../docs/RELEASING.md).

**Publish, Upload, Delete, галочки «I agree» и объявление разрешений нажимает
автор.**

## Что загружать

`dist/nexus/ForgePlanner-<версия>.zip` из `python package.py`: внутри один
`BepInEx/plugins/Forgeplan.dll`, архив распаковывается прямо в папку игры.
Не архив Thunderstore: манифест Nexus не нужен и только путает скачавшего.

## Обновление

1. Страница мода → **Manage** → **Files** → у текущего главного файла
   **Update**, новый архив. Номер — новый, галочку «Update mod version to
   match» оставить.
2. **Changelog** — строки этой версии из `pack/CHANGELOG.md`.
3. **Description** — целиком `description.bbcode`. Редактор — SCEditor:
   кнопка **source** (последняя на панели), выделить всё, вставить поверх,
   снова source, Save. Перезагрузить страницу и убедиться, что текст на месте.
4. **Summary** — `../short-description.txt`.
5. **Images** — по `../gallery.md`: jpg, png или gif, до 8 МБ, webp нельзя.
   Первая — `01-in-game.png`, она же обложка. После загрузки адреса картинок
   строк «README: да» вписать в `images.json` — без них `package.py` не
   соберёт архив для Thunderstore.

В отличие от Thunderstore, ничего здесь не живёт в архиве: описание, картинки
и теги меняются в любой момент без новой версии.
