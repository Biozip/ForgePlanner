using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Forgeplan.Ui {

/// <summary>
/// Виджеты в стиле игры.
///
/// Тему не придумываем: шрифт, спрайты и палитру кнопки забираем у живого
/// интерфейса Valheim. Своя рамка означала бы окно, которое «почти как игра» —
/// а почти всегда заметно.
///
/// Интерфейс игры — гибрид: раскладка и кнопки из UnityEngine.UI, а весь текст
/// на TextMeshPro (у InventoryGui подписи объявлены как TMP_Text). Поэтому и
/// здесь Text не используется вовсе.
///
/// Ни один поиск не обязан удаться. Если ассет не нашёлся, берётся запасной
/// вариант, а Init() пишет в лог, что именно не нашлось: окно должно
/// открываться всегда, пусть и беднее на вид.
/// </summary>
public static class UiKit {
    public static bool Ready { get; private set; }

    /// <summary>Выкладывать в лог список найденных спрайтов. Ставит плагин из
    /// той же настройки, что включает самопроверку: подбор фона — единственное
    /// место, где мод угадывает по именам, и вслепую его не починить.</summary>
    public static bool Verbose;

    public static TMP_FontAsset Font;
    public static Sprite PanelSprite;      // деревянная панель под всё окно
    public static Sprite FieldSprite;      // тёмная плашка под строку и списки
    public static Sprite ButtonSprite;     // графика кнопки игры
    public static ColorBlock ButtonColors; // её же подсветка и нажатие
    static bool _hasButtonColors;

    /// <summary>Канва игры. Родителимся к ней, чтобы совпало масштабирование:
    /// своя канва со своим CanvasScaler разъедется с игрой на других
    /// разрешениях и при смене размера интерфейса в настройках.</summary>
    public static Transform CanvasRoot;

    public static readonly Color Ink = new Color(0.90f, 0.86f, 0.76f);
    public static readonly Color Dim = new Color(0.60f, 0.57f, 0.51f);
    public static readonly Color Gold = new Color(0.93f, 0.77f, 0.38f);
    public static readonly Color Line = new Color(1f, 1f, 1f, 0.10f);
    /// <summary>Недоступное. Тусклее Dim ровно настолько, чтобы читалось как
    /// «выключено», а не как «плохо видно».</summary>
    public static readonly Color Faint = new Color(0.42f, 0.40f, 0.37f, 0.55f);

    public static bool Init() {
        if (Ready) return true;
        var gui = InventoryGui.instance;
        if (gui == null) return false;              // интерфейс ещё не поднялся

        var canvas = gui.GetComponentInParent<Canvas>();
        if (canvas == null) return false;
        CanvasRoot = canvas.transform;

        // Шрифт: берём прямо у подписи рецепта. Так он совпадёт и с языком —
        // у кириллицы и латиницы в игре разные атласы.
        if (gui.m_recipeName != null) Font = gui.m_recipeName.font;
        if (Font == null) {
            var any = CanvasRoot.GetComponentInChildren<TMP_Text>(true);
            if (any != null) Font = any.font;
        }

        var images = CanvasRoot.GetComponentsInChildren<Image>(true);
        PanelSprite = Best(images, "woodpanel", "panel");
        FieldSprite = Best(images, "darken", "interior", "text_field", "bkg", "field");

        // Кнопку не клонируем, а разбираем: клон тащит за собой чужие
        // компоненты раскладки, подписчиков нажатия и собственные якоря —
        // именно от этого вкладки разделов разъехались в колонны во весь
        // столбец. Нужны только картинка и палитра, остальное собираем сами.
        if (gui.m_craftButton != null) {
            var bi = gui.m_craftButton.GetComponent<Image>();
            if (bi != null) ButtonSprite = bi.sprite;
            ButtonColors = gui.m_craftButton.colors;
            _hasButtonColors = true;
        }

        Ready = true;
        ForgeplanPlugin.Log.LogInfo(string.Format(
            "интерфейс: шрифт {0}, панель {1}, плашка {2}, кнопка {3}",
            Font != null ? Font.name : "НЕ НАЙДЕН",
            PanelSprite != null ? PanelSprite.name : "нет",
            FieldSprite != null ? FieldSprite.name : "нет",
            ButtonSprite != null ? ButtonSprite.name : "нет"));
        if (Verbose) DumpSprites(images);
        return true;
    }

    /// <summary>
    /// Подходящий спрайт-подложка.
    ///
    /// Три условия, и каждое куплено ошибкой. Рамка (<c>sprite.border</c>) —
    /// иначе в фон попадает разделительная черта и растягивается в полосу.
    /// Отсев масок — иначе побеждает <c>woodpanel_playerinventory_mask</c>,
    /// сплошная белая фигура с рваным краем, которой игра обрезает панель
    /// инвентаря: окно становится белым листом. Площадь — из двух настоящих
    /// панелей нужна та, что рисуется крупно, у мелкой другая пропорция рамки.
    /// </summary>
    static Sprite Best(Image[] all, params string[] prefer) {
        Sprite best = null;
        int bestRank = int.MaxValue;
        float bestArea = -1f;

        foreach (var img in all) {
            var s = img.sprite;
            if (s == null || s.border == Vector4.zero) continue;
            if (IsMask(img, s)) continue;

            var low = s.name.ToLowerInvariant();
            int rank = -1;
            for (int i = 0; i < prefer.Length; i++)
                if (low.Contains(prefer[i])) { rank = i; break; }
            if (rank < 0) continue;

            var r = img.rectTransform.rect;
            float area = Mathf.Abs(r.width * r.height);
            if (rank < bestRank || (rank == bestRank && area > bestArea)) {
                best = s; bestRank = rank; bestArea = area;
            }
        }
        return best;
    }

    static bool IsMask(Image img, Sprite s) {
        if (s.name.ToLowerInvariant().Contains("mask")) return true;
        if (img.name.ToLowerInvariant().Contains("mask")) return true;
        return img.GetComponent<Mask>() != null;
    }

    static void DumpSprites(Image[] all) {
        var names = new SortedSet<string>();
        foreach (var img in all) {
            var s = img.sprite;
            if (s == null || s.border == Vector4.zero) continue;
            names.Add(s.name + (IsMask(img, s) ? " (маска)" : ""));
        }
        var list = new List<string>(names);
        ForgeplanPlugin.Log.LogInfo("спрайты с рамкой (" + list.Count + "): "
                                    + string.Join(", ", list.ToArray()));
    }

    /* ————————————————————————— кирпичи ————————————————————————— */

    public static RectTransform Rect(Transform parent, string name) {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>Прямоугольник во всю ширину родителя, заданной высоты.</summary>
    public static RectTransform Row(Transform parent, string name, float height) {
        var rt = Rect(parent, name);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = height;
        le.flexibleHeight = 0;
        return rt;
    }

    /// <summary>
    /// Подложка во весь родитель.
    ///
    /// ignoreLayout обязателен: подложка лежит внутри того же объекта, что и
    /// содержимое, и без него VerticalLayoutGroup считает её обычной строкой и
    /// расплющивает в полоску.
    ///
    /// Порядок слоёв — обычный, по созданию. SetAsFirstSibling здесь был и
    /// оказался ловушкой: два вызова подряд меняли слои местами, и тёмный слой
    /// уходил ПОД деревянный вместо того, чтобы его притенять.
    /// </summary>
    public static Image Box(Transform parent, string name, Sprite sprite, Color tint) {
        var rt = Rect(parent, name);
        rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Stretch(rt);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Sliceable(sprite) ? Image.Type.Sliced : Image.Type.Simple;
        img.color = tint;
        img.raycastTarget = true;   // чтобы клики не проваливались в мир
        return img;
    }

    static bool Sliceable(Sprite s) {
        return s != null && s.border != Vector4.zero;
    }

    public static void Stretch(RectTransform rt) {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static TextMeshProUGUI Label(Transform parent, string name, string text,
                                        float size, Color color,
                                        TextAlignmentOptions align = TextAlignmentOptions.Left) {
        var rt = Rect(parent, name);
        // Объект гасится на время сборки не для красоты. TextMeshPro в своём
        // Awake хватает шрифт по умолчанию, а в сборке Valheim его нет — на
        // каждую подпись прилетало «LiberationSans SDF Font Asset was not
        // found». При каталоге в тысячу строк это четыре тысячи строк в лог,
        // записываемых на диск синхронно, прямо в кадре открытия окна.
        // Пока объект выключен, Awake не вызывается, и шрифт успевает встать.
        rt.gameObject.SetActive(false);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null) t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        rt.gameObject.SetActive(true);
        return t;
    }

    /// <summary>
    /// Кнопка. Картинка и палитра — от кнопки «Создать» из окна крафта, всё
    /// остальное своё, поэтому размер получается ровно тот, что попросили.
    ///
    /// minWidth намеренно меньше preferredWidth: строка переключателей не
    /// влезала в столбец и выпихивала его за край окна — Unity не ужимает
    /// ребёнка ниже минимума и молча позволяет ряду торчать наружу.
    /// </summary>
    public static Button Button(Transform parent, string text, float w, float h,
                                UnityAction onClick, float fontSize = 16f,
                                TextAlignmentOptions align = TextAlignmentOptions.Center) {
        var rt = Rect(parent, "button");
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = ButtonSprite;
        img.type = Sliceable(ButtonSprite) ? Image.Type.Sliced : Image.Type.Simple;
        img.color = ButtonSprite != null ? Color.white : new Color(0.17f, 0.15f, 0.12f, 0.96f);

        var label = Label(rt, "label", text, fontSize, Ink, align);
        var lrt = (RectTransform)label.transform;
        Stretch(lrt);
        // Отступ по выравниванию. Двенадцать точек нужны колонке разделов,
        // где подпись прижата влево; кнопке «+» шириной в 28 они не оставляли
        // на знак ровно ничего — квадрат выходил пустым.
        float pad = align == TextAlignmentOptions.Left ? 12f : 4f;
        lrt.offsetMin = new Vector2(pad, 0);
        lrt.offsetMax = new Vector2(-pad, 0);

        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.minWidth = Mathf.Min(w, 34f);
        le.minHeight = le.preferredHeight = h;
        le.flexibleWidth = le.flexibleHeight = 0;
        rt.sizeDelta = new Vector2(w, h);

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        if (_hasButtonColors) btn.colors = ButtonColors;
        // Без этого нажатая кнопка остаётся «выбранной», и игра рисует ей
        // рамку выделения — состояние переключателя показывает цвет текста.
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
        if (onClick != null) btn.onClick.AddListener(onClick);
        return btn;
    }

    /// <summary>
    /// Кнопка шириной по своей подписи.
    ///
    /// Нужна там, где ширину нельзя проставить числом: подписи биомов на двух
    /// языках разной длины, и «Deep North» с «Дальним севером» в одну колонку
    /// цифр не уложить. TMP умеет посчитать ширину строки заранее — берём у
    /// него.
    /// </summary>
    public static Button ButtonAuto(Transform parent, string text, float h,
                                    UnityAction onClick, float fontSize = 15f,
                                    float padding = 28f) {
        var btn = Button(parent, text, 10f, h, onClick, fontSize);
        var label = btn.GetComponentInChildren<TMP_Text>(true);
        float w = padding + (label != null ? label.GetPreferredValues(text).x : 70f);
        var le = btn.GetComponent<LayoutElement>();
        le.preferredWidth = w;
        le.minWidth = Mathf.Min(w, 44f);
        ((RectTransform)btn.transform).sizeDelta = new Vector2(w, h);
        return btn;
    }

    /// <summary>
    /// Кнопка с картинкой вместо подписи.
    ///
    /// Картинка белая, цвет задаётся здесь: так одна и та же иконка годится и
    /// для обычного вида, и для погашенного, и держать по файлу на состояние
    /// не нужно.
    /// </summary>
    public static Button IconButton(Transform parent, Sprite icon, float size,
                                    UnityAction onClick, Color tint) {
        var btn = Button(parent, "", size, size, onClick);
        var rt = Rect((RectTransform)btn.transform, "icon");
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size - 16f, size - 16f);
        rt.anchoredPosition = Vector2.zero;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;
        // Картинки нет — кнопка остаётся, но пустая: лучше дыра в ряду, чем
        // исключение при сборке окна.
        img.color = icon != null ? tint : new Color(0f, 0f, 0f, 0f);
        return btn;
    }

    /* ————————————————————————— флажки ————————————————————————— */

    /// <summary>
    /// Кнопка переключения языка с флажком.
    ///
    /// Флажок нарисован прямоугольниками, а не картинкой: своих ассетов мод не
    /// везёт вовсе, и заводить их ради двух флагов не стоит. «Юнион Джек»
    /// собирается из восьми полос, две из которых повёрнуты; лишнее срезает
    /// RectMask2D.
    ///
    /// Показывается флаг того языка, на который переключит, — как и подпись.
    /// </summary>
    public static Button FlagButton(Transform parent, bool english, string text,
                                    float w, float h, UnityAction onClick) {
        var btn = Button(parent, text, w, h, onClick, 15f);
        var rt = (RectTransform)btn.transform;

        var flag = Rect(rt, "flag");
        flag.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        flag.anchorMin = flag.anchorMax = new Vector2(0f, 0.5f);
        flag.pivot = new Vector2(0f, 0.5f);
        flag.sizeDelta = new Vector2(26f, 17f);
        flag.anchoredPosition = new Vector2(10f, 0f);
        flag.gameObject.AddComponent<RectMask2D>();
        if (english) UnionJack(flag); else Tricolour(flag);

        // Подпись уступает место флажку: без этого они наезжают друг на друга.
        var label = btn.GetComponentInChildren<TMP_Text>(true);
        if (label != null) {
            var lrt = (RectTransform)label.transform;
            lrt.offsetMin = new Vector2(42f, 0f);
            lrt.offsetMax = new Vector2(-8f, 0f);
        }
        return btn;
    }

    static Image Stripe(RectTransform parent, float w, float h, Color color,
                        float angle = 0f) {
        var rt = Rect(parent, "stripe");
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Image Band(RectTransform parent, float from, float to, Color color) {
        var rt = Rect(parent, "band");
        rt.anchorMin = new Vector2(0f, from);
        rt.anchorMax = new Vector2(1f, to);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static readonly Color FlagWhite = new Color(0.96f, 0.96f, 0.96f);
    static readonly Color RuBlue = new Color(0f, 0.224f, 0.651f);
    static readonly Color RuRed = new Color(0.835f, 0.169f, 0.118f);
    static readonly Color UkBlue = new Color(0.004f, 0.129f, 0.412f);
    static readonly Color UkRed = new Color(0.784f, 0.063f, 0.180f);

    static void Tricolour(RectTransform flag) {
        Band(flag, 0.667f, 1f, FlagWhite);
        Band(flag, 0.333f, 0.667f, RuBlue);
        Band(flag, 0f, 0.333f, RuRed);
    }

    static void UnionJack(RectTransform flag) {
        const float W = 26f, H = 17f;
        float diag = Mathf.Sqrt(W * W + H * H);
        float angle = Mathf.Atan2(H, W) * Mathf.Rad2Deg;

        Band(flag, 0f, 1f, UkBlue);
        Stripe(flag, diag, 5.4f, FlagWhite, angle);
        Stripe(flag, diag, 5.4f, FlagWhite, -angle);
        Stripe(flag, diag, 2.4f, UkRed, angle);
        Stripe(flag, diag, 2.4f, UkRed, -angle);
        Stripe(flag, W, 6.4f, FlagWhite);
        Stripe(flag, 6.4f, H, FlagWhite);
        Stripe(flag, W, 3.6f, UkRed);
        Stripe(flag, 3.6f, H, UkRed);
    }

    /// <summary>
    /// Тонкий контур по краю прямоугольника — четырьмя полосками.
    ///
    /// Не спрайтом: спрайта «только рамка» у игры нет, а девятислайсовые
    /// подложки рисуют заодно и заливку. Четыре Image дают ровно контур,
    /// любой толщины и прозрачности, и ничего не закрывают.
    /// </summary>
    public static void Frame(RectTransform rt, Color color, float thickness = 1f) {
        Edge(rt, "top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -thickness),
             Vector2.zero, color);
        Edge(rt, "bottom", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero,
             new Vector2(0, thickness), color);
        Edge(rt, "left", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero,
             new Vector2(thickness, 0), color);
        Edge(rt, "right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-thickness, 0),
             Vector2.zero, color);
    }

    static void Edge(RectTransform parent, string name, Vector2 aMin, Vector2 aMax,
                     Vector2 oMin, Vector2 oMax, Color color) {
        var rt = Rect(parent, name);
        rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    /// <summary>Мелкая подпись над блоком: «Предметы», «Поиск», «Биом».</summary>
    public static TextMeshProUGUI Caption(Transform parent, string text, float height = 20f) {
        var t = Label(parent, "caption", text, 14f, Dim, TextAlignmentOptions.BottomLeft);
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = height;
        le.flexibleHeight = 0;
        return t;
    }

    /* ————————————————————————— рамка со списком ————————————————————————— */

    const float Pad = 6f;
    const float BarWidth = 9f;

    /// <summary>
    /// Тёмная рамка с прокруткой и полосой справа. Возвращает содержимое, в
    /// которое складывают строки; высота считается сама.
    ///
    /// Полоса нужна не для красоты: в каталоге пять сотен строк, и без неё
    /// ничто не подсказывает, что список вообще прокручивается.
    /// </summary>
    public static RectTransform ScrollBox(RectTransform frame, float spacing = 2f) {
        Box(frame, "bg", FieldSprite, new Color(0f, 0f, 0f, 0.50f));

        var view = Rect(frame, "view");
        Stretch(view);
        view.offsetMin = new Vector2(Pad, Pad);
        view.offsetMax = new Vector2(-(Pad + BarWidth + 4f), -Pad);
        view.gameObject.AddComponent<RectMask2D>();

        // Прозрачная картинка прямо на объекте с ScrollRect. Без неё колесо
        // мыши над списком не работало вовсе: система событий отдаёт прокрутку
        // тому, во что попал луч, а попадать было не во что — подписи и иконки
        // лучи не ловят, а подложка лежит на соседнем объекте, не на предке.
        var catcher = view.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;

        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        // Три строки за щелчок колеса. Одна — ровно столько ставит Unity по
        // умолчанию — при списке в тысячу позиций превращает прокрутку в пытку.
        scroll.scrollSensitivity = 102f;

        var content = Rect(view, "content");
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = view;
        scroll.content = content;

        var bar = Rect(frame, "scrollbar");
        bar.anchorMin = new Vector2(1, 0);
        bar.anchorMax = new Vector2(1, 1);
        bar.pivot = new Vector2(1, 0.5f);
        bar.offsetMin = new Vector2(-(Pad + BarWidth), Pad);
        bar.offsetMax = new Vector2(-Pad, -Pad);
        var track = bar.gameObject.AddComponent<Image>();
        track.color = new Color(0f, 0f, 0f, 0.35f);

        var area = Rect(bar, "area");
        Stretch(area);
        var handle = Rect(area, "handle");
        // Обнулить обязательно. Scrollbar двигает ползунок якорями, а sizeDelta
        // не трогает — и свежий RectTransform со своими 100x100 превращался в
        // песочный прямоугольник поперёк окна.
        handle.sizeDelta = Vector2.zero;
        handle.offsetMin = handle.offsetMax = Vector2.zero;
        var hi = handle.gameObject.AddComponent<Image>();
        hi.color = new Color(0.58f, 0.48f, 0.30f, 0.90f);

        var sb = bar.gameObject.AddComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;
        sb.handleRect = handle;
        sb.targetGraphic = hi;
        scroll.verticalScrollbar = sb;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        return content;
    }

    /// <summary>Строка ввода. Готовой в интерфейсе игры нет — поиска там
    /// попросту негде, — поэтому собирается из частей.</summary>
    public static TMP_InputField Input(Transform parent, string placeholder, float height) {
        var rt = Row(parent, "search", height);
        var bg = rt.gameObject.AddComponent<Image>();
        bg.sprite = FieldSprite;
        bg.type = Sliceable(FieldSprite) ? Image.Type.Sliced : Image.Type.Simple;
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        var area = Rect(rt, "area");
        Stretch(area);
        area.offsetMin = new Vector2(10, 4);
        area.offsetMax = new Vector2(-10, -4);
        area.gameObject.AddComponent<RectMask2D>();

        var text = Label(area, "text", "", 17f, Ink);
        Stretch((RectTransform)text.transform);
        var hint = Label(area, "placeholder", placeholder, 17f, Dim);
        Stretch((RectTransform)hint.transform);

        var field = rt.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = hint;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.customCaretColor = true;
        field.caretColor = Gold;
        field.selectionColor = new Color(0.9f, 0.74f, 0.36f, 0.35f);
        field.restoreOriginalTextOnEscape = false;
        return field;
    }

    /// <summary>Тонкая разделительная черта.</summary>
    public static void Separator(Transform parent) {
        var rt = Row(parent, "sep", 1f);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = Line;
        img.raycastTarget = false;
    }

    /// <summary>Иконка предмета. Если её нет — прозрачное место того же
    /// размера, чтобы строки не прыгали.</summary>
    public static Image Icon(Transform parent, Sprite sprite, float size) {
        var rt = Rect(parent, "icon");
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = size;
        le.minHeight = le.preferredHeight = size;
        le.flexibleWidth = le.flexibleHeight = 0;
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = sprite != null ? Color.white : Color.clear;
        return img;
    }

    /// <summary>
    /// Раскладка в ряд. childForceExpandHeight выключен намеренно: включённый
    /// растягивает детей на всю высоту ряда, и кнопка в 26 точек становится
    /// колонной в двести. Высоту каждый задаёт себе сам, а по вертикали ряд их
    /// центрирует.
    /// </summary>
    public static HorizontalLayoutGroup Horizontal(RectTransform rt, float spacing,
                                                   int left = 0, int right = 0) {
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.padding = new RectOffset(left, right, 0, 0);
        return h;
    }
}

}
