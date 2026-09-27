# -*- coding: utf-8 -*-
"""Собирает два архива: для Thunderstore (он же для Hexium) и для Nexus.

    tools/venv/Scripts/python.exe mod/package.py

dist/ForgePlanner-<v>.zip — Thunderstore и Hexium:

    manifest.json        собирается здесь, из VERSION и store/short-description.txt
    icon.png             256x256, нарисован make_icon.py
    README.md            store/page.md, картинки — адресами из галереи Nexus
                         (store/pages.py); без адресов сборка откажется
    CHANGELOG.md         pack/CHANGELOG.md
    LICENSE
    plugins/Forgeplan.dll

dist/nexus/ForgePlanner-<v>.zip — Nexus: один файл, BepInEx/plugins/Forgeplan.dll,
чтобы архив распаковывался прямо в папку игры и Vortex знал, куда его класть.
Манифест Thunderstore там только путал бы скачавшего.

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
sys.path.insert(0, os.path.join(BASE, 'store'))
import pages  # noqa: E402

# Одна строка, не длиннее 250 знаков: описание в манифесте Thunderstore и
# summary на Nexus. Живёт в store/, чтобы не писать её дважды.
DESCRIPTION = open(os.path.join(BASE, 'store', 'short-description.txt'),
                   encoding='utf-8-sig').read().strip()


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

    readme = pages.readme()
    files = [
        ('CHANGELOG.md', os.path.join(BASE, 'pack', 'CHANGELOG.md')),
        ('LICENSE', os.path.join(BASE, 'LICENSE')),
        ('icon.png', icon),
        ('plugins/Forgeplan.dll', DLL),
    ]

    with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('manifest.json', json.dumps(manifest, ensure_ascii=False, indent=2))
        z.writestr('README.md', readme)
        for inside, path in files:
            if not os.path.exists(path):
                sys.exit(f'нет файла: {path}')
            z.write(path, inside)

    print(f'{out}  ({os.path.getsize(out)} байт)')
    with zipfile.ZipFile(out) as z:
        for info in z.infolist():
            print(f'  {info.file_size:8}  {info.filename}')

    nexus = os.path.join(DIST, 'nexus', f'{NAME}-{v}.zip')
    os.makedirs(os.path.dirname(nexus), exist_ok=True)
    with zipfile.ZipFile(nexus, 'w', zipfile.ZIP_DEFLATED) as z:
        z.write(DLL, 'BepInEx/plugins/Forgeplan.dll')
    print(f'{nexus}  ({os.path.getsize(nexus)} байт)')


if __name__ == '__main__':
    main()
