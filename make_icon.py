# -*- coding: utf-8 -*-
"""Иконка мода для Thunderstore: icon.png, ровно 256x256.

Знак берётся из icon.svg — того же файла, из которого сайт растрирует свои
иконки и обложку. Раньше здесь была своя наковальня, нарисованная примитивами,
и мод оказывался единственным местом, где логотип отличался от остальных.
Опознаваемость важнее: в мод-менеджере пакет стоит в списке из сотен строк, и
человек, пришедший с сайта, должен узнать значок с первого взгляда.

Копия icon.svg лежит здесь, а не читается из проекта сайта, потому что мод —
отдельный репозиторий и должен собираться сам по себе. Файл маленький и свой,
так что дублирование дешевле связанности. Если знак поменяется на сайте,
скопируйте icon.svg сюда заново и перезапустите этот скрипт.

Ассетов Valheim в иконке по-прежнему нет: знак нарисован автором.

Pillow не умеет SVG, а тащить cairosvg ради семи путей не хочется, поэтому
разбор свой и намеренно узкий — ровно под наш файл: один <rect> фона, одна
группа с translate/scale и <path> с явным fill. Всё остальное — ошибка, чтобы
молча не нарисовать ерунду.

    tools/venv/Scripts/python.exe mod/make_icon.py
"""
import io
import os
import re

from PIL import Image, ImageDraw

SIZE = 256
SS = 4                      # рисуем крупнее и уменьшаем: так сглаживаются края
BASE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(BASE, 'icon.svg')
OUT = os.path.join(BASE, 'icon.png')

TOKEN = re.compile(r'[MmLlHhVvCcZz]|-?\d*\.?\d+(?:[eE]-?\d+)?')


def bezier(p0, p1, p2, p3, steps=24):
    out = []
    for i in range(1, steps + 1):
        t = i / steps
        u = 1 - t
        out.append((u**3*p0[0] + 3*u*u*t*p1[0] + 3*u*t*t*p2[0] + t**3*p3[0],
                    u**3*p0[1] + 3*u*u*t*p1[1] + 3*u*t*t*p2[1] + t**3*p3[1]))
    return out


def parse_path(d):
    """Данные пути -> список контуров (каждый — список точек).

    Понимает M L H V C Z в обоих регистрах: больше в знаке ничего нет.
    """
    toks = TOKEN.findall(d)
    i, cmd = 0, None
    cur = start = (0.0, 0.0)
    contours, pts = [], []

    def num():
        nonlocal i
        v = float(toks[i])
        i += 1
        return v

    while i < len(toks):
        if TOKEN.match(toks[i]) and toks[i].isalpha():
            cmd = toks[i]
            i += 1
        rel = cmd.islower()
        c = cmd.upper()
        if c == 'M':
            if pts:
                contours.append(pts)
            x, y = num(), num()
            cur = start = (cur[0] + x, cur[1] + y) if rel else (x, y)
            pts = [cur]
            cmd = 'l' if rel else 'L'
        elif c == 'L':
            x, y = num(), num()
            cur = (cur[0] + x, cur[1] + y) if rel else (x, y)
            pts.append(cur)
        elif c == 'H':
            x = num()
            cur = (cur[0] + x, cur[1]) if rel else (x, cur[1])
            pts.append(cur)
        elif c == 'V':
            y = num()
            cur = (cur[0], cur[1] + y) if rel else (cur[0], y)
            pts.append(cur)
        elif c == 'C':
            a = (num(), num())
            b = (num(), num())
            e = (num(), num())
            if rel:
                a = (cur[0] + a[0], cur[1] + a[1])
                b = (cur[0] + b[0], cur[1] + b[1])
                e = (cur[0] + e[0], cur[1] + e[1])
            pts.extend(bezier(cur, a, b, e))
            cur = e
        elif c == 'Z':
            if pts:
                pts.append(start)
                contours.append(pts)
                pts = []
            cur = start
        else:
            raise ValueError('в знаке команда ' + c + ', разбор её не умеет')
    if pts:
        contours.append(pts)
    return contours


def read_icon():
    """icon.svg -> (цвет фона, сдвиг, масштаб, [(цвет, данные пути), ...])."""
    svg = io.open(SRC, encoding='utf-8').read()
    bg = re.search(r'<rect[^>]*fill="(#[0-9a-fA-F]{6})"', svg)
    g = re.search(r'<g transform="translate\(([-\d.]+) ([-\d.]+)\) scale\(([\d.]+)\)"', svg)
    if not bg or not g:
        raise ValueError('icon.svg изменился: нет фона или группы с transform')
    paths = re.findall(r'<path fill="(#[0-9a-fA-F]{6})" d="([^"]+)"', svg)
    if len(paths) < 2:
        raise ValueError('icon.svg: у путей должен быть явный fill')
    return bg.group(1), (float(g.group(1)), float(g.group(2))), float(g.group(3)), paths


def rgb(h):
    return (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16), 255)


def draw_mark(size):
    bg_hex, (tx, ty), sc, paths = read_icon()
    k = size * SS / 512.0                    # icon.svg нарисован в поле 512x512
    img = Image.new('RGBA', (size*SS, size*SS), rgb(bg_hex))
    d = ImageDraw.Draw(img)
    for fill, data in paths:
        for contour in parse_path(data):
            if len(contour) > 2:
                d.polygon([((tx + x*sc) * k, (ty + y*sc) * k) for x, y in contour],
                          fill=rgb(fill))
    return img.resize((size, size), Image.LANCZOS)


def main():
    draw_mark(SIZE).convert('RGB').save(OUT)
    print('icon.png  %dx%d  %d байт' % (SIZE, SIZE, os.path.getsize(OUT)))


if __name__ == '__main__':
    main()
