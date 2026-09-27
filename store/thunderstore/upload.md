# Thunderstore

**Страница:** https://thunderstore.io/c/valheim/p/Biozip/ForgePlanner/
**Команда:** Biozip.

Первая загрузка каждого выпуска: отсюда ставят мод-менеджеры.

**Вход (через Discord, GitHub или Overwolf), галочки и Submit — клики автора.**

## Что загружать

`dist/ForgePlanner-<версия>.zip` из `python package.py`:

```
manifest.json          из VERSION и ../short-description.txt
icon.png               256×256, make_icon.py
README.md              ../page.md, картинки — адресами из ../nexus/images.json
CHANGELOG.md           pack/CHANGELOG.md
LICENSE
plugins/Forgeplan.dll
```

Страница, иконка и история едут **внутри** архива: на сайте их не поправить
без новой версии.

## Обновление

1. **Upload** → команда Biozip → сообщество Valheim, бросить архив. Имя,
   версия, описание и зависимость от BepInEx читаются из манифеста.
2. Категории — как сейчас (ниже). NSFW — No. Submit.
3. Открыть страницу пакета, проверить версию; в **Manage Package** проверить,
   что категории не слетели.

## Категории

Сейчас (по API, 2026-09-27): Mods · Crafting · Client-side · Utility.
У RoutePlanner сверх того стоят «AI Generated» и «Deep North Update» — см.
`docs/platforms.md`, строка «Пометка про ИИ».
