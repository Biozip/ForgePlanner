# -*- coding: utf-8 -*-
"""Иконка мода для Thunderstore: icon.png, ровно 256x256.

Рисуется кодом, а не берётся из игры. Причина прагматичная: мод намеренно не
везёт ни одного ассета Iron Gate — ни шрифта, ни спрайтов, — и иконка не должна
быть единственным исключением. Наковальня нарисована примитивами, цвета взяты
из палитры окна (уголь и золото), так что пакет и окно выглядят одинаково.

Рисуем в четырёхкратном размере и уменьшаем: PIL не умеет сглаживать края
многоугольников, а уменьшение с LANCZOS делает это за него.

    tools/venv/Scripts/python.exe mod/make_icon.py
"""
import os
from PIL import Image, ImageDraw

SIZE = 256
SS = 4                      # коэффициент передискретизации
S = SIZE * SS

COAL_DARK = (26, 24, 21)
COAL = (43, 40, 35)
GOLD = (214, 166, 76)
GOLD_DIM = (150, 115, 52)
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'icon.png')


def k(*xs):
    """Координаты задаём в 256, рисуем в S."""
    return tuple(x * SS for x in xs)


def rounded(draw, box, radius, fill, outline=None, width=0):
    draw.rounded_rectangle(k(*box), radius=radius * SS, fill=fill,
                           outline=outline, width=width * SS)


def main():
    img = Image.new('RGB', (S, S), COAL_DARK)
    d = ImageDraw.Draw(img)

    # Подложка: угольный квадрат со скруглением и золотой рамкой.
    rounded(d, (6, 6, 249, 249), 34, COAL, GOLD_DIM, 3)

    # Наковальня. Четыре части снизу вверх: опора, талия, тело, рог.
    # Опора — трапеция: у настоящей наковальни низ шире верха.
    d.polygon(k(70, 214, 186, 214, 172, 186, 84, 186), fill=GOLD)
    # Талия.
    d.polygon(k(104, 186, 152, 186, 146, 140, 110, 140), fill=GOLD)
    # Тело со ступенькой под верхней плитой.
    d.polygon(k(76, 140, 180, 140, 190, 124, 66, 124), fill=GOLD)
    # Верхняя плита.
    rounded(d, (56, 96, 196, 126), 5, GOLD)
    # Рог: треугольник вправо, чуть ниже плоскости плиты.
    d.polygon(k(194, 98, 238, 112, 194, 126), fill=GOLD)

    # Искра над наковальней — чтобы силуэт не читался как «утюг».
    d.polygon(k(128, 40, 136, 66, 128, 78, 120, 66), fill=GOLD_DIM)

    img = img.resize((SIZE, SIZE), Image.LANCZOS)
    img.save(OUT, 'PNG', optimize=True)
    print('готово:', OUT, img.size, os.path.getsize(OUT), 'байт')


if __name__ == '__main__':
    main()
