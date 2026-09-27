# -*- coding: utf-8 -*-
"""Страницы мода на площадках — из одного текста.

    python mod/store/pages.py          пересобрать nexus/description.bbcode
                                       и показать, каких адресов картинок нет

Источник один: `store/page.md`. Из него получаются

    README.md в архиве   Thunderstore и Hexium: тот же Markdown, только
                         картинки `media/…` заменены адресами из галереи Nexus
                         (`nexus/images.json`) — площадки берут картинки лишь
                         ссылками. Собирает package.py, вызывая readme().
    description.bbcode   описание на Nexus: тот же текст в BBCode, без
                         картинок — у Nexus для них своя галерея.

Раньше описание на Nexus набиралось прямо в форме на сайте и за три выпуска
разошлось с README: там не было ни карточки предмета, ни закреплённого плана,
зато осталась ссылка на GitHub. Текст, существующий только в веб-форме,
переписывается к следующему выпуску по памяти и каждый раз чуть иначе.

Разметка page.md намеренно узкая — ровно то, что переводится в BBCode
без потерь: заголовки `#`/`##`, абзацы, списки `-` и `1.`, **жирный**,
`код`, ссылки, картинки отдельной строкой и одна таблица настроек.
"""
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PAGE = os.path.join(HERE, 'page.md')
IMAGES = os.path.join(HERE, 'nexus', 'images.json')
BBCODE = os.path.join(HERE, 'nexus', 'description.bbcode')

IMG = re.compile(r'^!\[([^\]]*)\]\((media/[^)]+)\)$')


def read(path):
    with io.open(path, encoding='utf-8-sig') as f:
        return f.read()


def write(path, text):
    with io.open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)


def images():
    return json.loads(read(IMAGES)) if os.path.exists(IMAGES) else {}


def missing_images(text=None):
    """Файлы из media/, для которых в images.json ещё нет адреса."""
    text = text or read(PAGE)
    have = images()
    need = [m.group(2)[len('media/'):] for m in
            (IMG.match(l.strip()) for l in text.splitlines()) if m]
    return [n for n in need if not have.get(n)]


def readme(text=None):
    """README.md для архива Thunderstore. Без адреса картинки — отказ:
    ссылка `media/…` на площадке превратилась бы в битую картинку."""
    text = text or read(PAGE)
    lost = missing_images(text)
    if lost:
        raise SystemExit('нет адресов картинок в store/nexus/images.json: '
                         + ', '.join(lost)
                         + '\nЗагрузите их в галерею Nexus и впишите адреса.')
    have = images()

    def sub(line):
        m = IMG.match(line.strip())
        if not m:
            return line
        return '![%s](%s)' % (m.group(1), have[m.group(2)[len('media/'):]])
    return '\n'.join(sub(l) for l in text.splitlines()) + '\n'


# ——— BBCode ———

def inline(s):
    s = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'[url=\2]\1[/url]', s)
    s = re.sub(r'\*\*(.+?)\*\*', r'[b]\1[/b]', s)
    # Код — жирным: моноширинный шрифт Nexus в тексте смотрится чужим, а
    # `F7` или `1 / 10` читателю важно заметить, не более.
    s = re.sub(r'`([^`]+)`', r'[b]\1[/b]', s)
    return s


def blocks(text):
    cur = []
    for line in text.splitlines():
        if line.strip():
            cur.append(line)
        elif cur:
            yield cur
            cur = []
    if cur:
        yield cur


def list_items(block):
    items = []
    for line in block:
        if re.match(r'^(- |\d+\. )', line):
            items.append(re.sub(r'^(- |\d+\. )', '', line).strip())
        else:
            items[-1] += ' ' + line.strip()
    return items


def table(block):
    """Таблица настроек — блоком [code], сгруппированным по разделам, как в
    самом .cfg: у Nexus нет таблиц, а выровненный текст читается так же."""
    # Внутри [code] разметка не работает — обратные кавычки убираем целиком.
    rows = [[c.strip().replace('`', '') for c in l.strip().strip('|').split('|')]
            for l in block]
    rows = [r for r in rows[1:] if not set(''.join(r)) <= set('- ')]
    kw = max(len(r[1]) for r in rows) + 2
    dw = max(len(r[2]) for r in rows) + 2
    out, section = [], None
    for sec, key, default, meaning in rows:
        if sec != section:
            if section is not None:
                out.append('')
            out.append('[%s]' % sec)
            section = sec
        out.append('  ' + key.ljust(kw) + default.ljust(dw) + meaning)
    return '[code]\n' + '\n'.join(out) + '[/code]'


def bbcode(text=None):
    text = text or read(PAGE)
    out = []
    for b in blocks(text):
        first = b[0]
        if first.startswith('# '):
            out.append('[size=5][b]%s[/b][/size]' % first[2:].strip())
        elif first.startswith('## '):
            out.append('[line]')
            out.append('[size=4][b]%s[/b][/size]' % first[3:].strip())
        elif IMG.match(first.strip()):
            continue
        elif first.startswith('|'):
            out.append(table(b))
        elif re.match(r'^- ', first):
            out.append('[list]\n' + '\n'.join('[*]%s[/*]' % inline(i)
                                               for i in list_items(b)) + '\n[/list]')
        elif re.match(r'^\d+\. ', first):
            out.append('[list=1]\n' + '\n'.join('[*]%s[/*]' % inline(i)
                                                 for i in list_items(b)) + '\n[/list]')
        else:
            out.append(inline(' '.join(l.strip() for l in b)))
    return '\n\n'.join(out) + '\n'


def main():
    text = read(PAGE)
    write(BBCODE, bbcode(text))
    print('записано:', os.path.relpath(BBCODE, os.path.dirname(HERE)),
          '(%d знаков)' % len(read(BBCODE)))
    lost = missing_images(text)
    if lost:
        print('нет адресов картинок (package.py откажется собирать):')
        for n in lost:
            print('  ', n)
    else:
        print('все адреса картинок на месте')


if __name__ == '__main__':
    sys.exit(main())
