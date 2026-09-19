# -*- coding: utf-8 -*-
"""Собирает zip для Thunderstore.

    tools/venv/Scripts/python.exe mod/package.py

Что кладётся в архив:

    manifest.json        собирается здесь, из VERSION
    icon.png             256x256, нарисован make_icon.py
    README.md            pack/README.md, английский — площадка международная
    CHANGELOG.md         pack/CHANGELOG.md
    LICENSE
    plugins/Forgeplan.dll

Номер версии нигде не дублируется руками: он живёт в VERSION, оттуда попадает в
[BepInPlugin] при сборке и в manifest.json здесь. Как и сборка, упаковка
отказывается идти, если VERSION разошёлся с верхней записью Versions.md — гита
в проекте нет, и выпуск без строчки в истории через месяц уже не опознать.

Строка зависимости — точное имя пакета BepInExPack на Thunderstore. Неверная
ломает валидацию при загрузке, поэтому проверена запросом к их API, а не взята
по памяти.
"""
import json
import os
import re
import sys
import zipfile

BASE = os.path.dirname(os.path.abspath(__file__))
DIST = os.path.join(BASE, 'dist')
DLL = os.path.join(BASE, 'bin', 'Release', 'Forgeplan.dll')

NAME = 'ForgePlanner'
SITE = 'https://forgeplanner.pages.dev'
BEPINEX = 'denikson-BepInExPack_Valheim-5.4.2350'
# Thunderstore: одна строка, не длиннее 250 знаков.
DESCRIPTION = (
    'In-game crafting planner. Press F7, build a shopping list, and see the '
    'ore, wood, coal and smelting time it takes. Reads recipes, upgrade costs '
    'and names from your running game, so the numbers survive patches.'
)


def read(path):
    with open(path, encoding='utf-8-sig') as f:
        return f.read()


def version():
    v = read(os.path.join(BASE, 'VERSION')).strip()
    if not re.fullmatch(r'\d+\.\d+\.\d+', v):
        sys.exit(f'VERSION должен быть вида 1.2.3, а там: {v!r}')
    history = read(os.path.join(BASE, 'Versions.md'))
    top = re.search(r'^## (\d+\.\d+\.\d+)', history, re.M)
    if not top:
        sys.exit('в Versions.md нет ни одной записи о выпуске')
    if top.group(1) != v:
        sys.exit(f'VERSION = {v}, а верхняя запись Versions.md — {top.group(1)}.\n'
                 'Допишите историю выпуска или поправьте номер.')
    return v


def main():
    v = version()

    icon = os.path.join(BASE, 'icon.png')
    for path in (DLL, icon):
        if not os.path.exists(path):
            sys.exit(f'нет файла: {path}\n'
                     'Соберите мод (dotnet build -c Release) и иконку '
                     '(python mod/make_icon.py).')

    manifest = {
        'name': NAME,
        'version_number': v,
        'website_url': SITE,
        'description': DESCRIPTION,
        'dependencies': [BEPINEX],
    }
    if len(DESCRIPTION) > 250:
        sys.exit(f'описание длиннее 250 знаков: {len(DESCRIPTION)}')

    os.makedirs(DIST, exist_ok=True)
    out = os.path.join(DIST, f'{NAME}-{v}.zip')

    files = [
        ('README.md', os.path.join(BASE, 'pack', 'README.md')),
        ('CHANGELOG.md', os.path.join(BASE, 'pack', 'CHANGELOG.md')),
        ('LICENSE', os.path.join(BASE, 'LICENSE')),
        ('icon.png', icon),
        ('plugins/Forgeplan.dll', DLL),
    ]

    with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('manifest.json', json.dumps(manifest, ensure_ascii=False, indent=2))
        for inside, path in files:
            if not os.path.exists(path):
                sys.exit(f'нет файла: {path}')
            z.write(path, inside)

    print(f'{out}  ({os.path.getsize(out)} байт)')
    with zipfile.ZipFile(out) as z:
        for info in z.infolist():
            print(f'  {info.file_size:8}  {info.filename}')


if __name__ == '__main__':
    main()
