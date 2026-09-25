using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Forgeplan.Ui {

/// <summary>
/// Закреплённый список поверх игры.
///
/// Это первая часть мода, которая живёт при закрытом окне, и от этого все её
/// особенности.
///
/// <para>Клики сквозь неё проходят. У всего, что здесь рисуется,
/// raycastTarget выключен — включая подложку, которой UiKit.Box по умолчанию
/// ставит его в true. Внутри окна это правильно: там подложка ловит
/// промахи, чтобы они не улетали в мир. Здесь ровно наоборот: панель висит
/// поверх игры, в которую в этот момент играют, и проглоченный удар киркой
/// хуже любой подсказки.</para>
///
/// <para>Пересчёт по таймеру, а не каждый кадр. Stock.Count обходит сцену в
/// поисках сундуков, и в кадре ему не место. Раз в секунду достаточно: руда
/// падает в рюкзак не чаще, а глазу разница незаметна.</para>
///
/// <para>Положение вынесено в конфиг по той же причине, что у кнопки
/// у станка: это ремонт, а не вкусовщина. Свободное место на экране зависит от
/// размера интерфейса и от того, что игрок себе включил, и угадать за всех
/// нельзя. По умолчанию — правый верх со сдвигом вниз, под миникарту.</para>
/// </summary>
public static class PinHud {
    public enum Corner { TopRight, TopLeft, BottomLeft, BottomRight }

    /// <summary>Показывать ли подсказку вообще. Ставит плагин из конфига.</summary>
    public static bool Enabled = true;
    public static Corner Where = Corner.TopRight;
    /// <summary>Отступ от угла экрана в точках канвы. Умолчание подобрано в
    /// игре, а не на глаз: при 230 панель касалась нижнего края миникарты.</summary>
    public static Vector2 Offset = new Vector2(42f, 268f);
    /// <summary>Как часто пересчитывать остаток, в секундах.</summary>
    public static float Interval = 1f;
    /// <summary>Сколько строк показывать; остальное сворачивается в «ещё N».</summary>
    public static int MaxRows = 8;

    const float Width = 252f;

    /* Фон темнее и прозрачнее, чем у окна: окно читают, а мимо этой панели
     * смотрят на мир. Суммарно около 0,62 — подписи держатся и на снегу, и на
     * ночном небе, но панель не выглядит заплаткой на экране. */
    static readonly Color Back = new Color(0.10f, 0.09f, 0.08f, 0.62f);

    static GameObject _root;
    static RectTransform _panel;
    static TextMeshProUGUI _title, _empty;
    static RectTransform _list;
    static readonly List<Row> _rows = new List<Row>();
    static TextMeshProUGUI _more;

    static Image _bg;
    static TextMeshProUGUI _move;
    static bool _dragMode;

    static float _due;
    static bool _dirty = true;

    /// <summary>Панель перетащили — плагину пора сохранить положение в конфиг.
    /// Передаётся тот же Offset, что читается при сборке.</summary>
    public static System.Action<Vector2> OnMoved;

    /// <summary>Список закреплённого поменялся — пересчитать, не дожидаясь
    /// таймера. Подписывается плагин при запуске.</summary>
    public static void Invalidate() { _dirty = true; }

    /// <summary>
    /// Зовётся каждый кадр из плагина. Внутри дешёвые проверки; дорогое —
    /// только по таймеру и только когда панель видна.
    /// </summary>
    public static void Tick(bool plannerVisible) {
        if (!Show(plannerVisible)) {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
            return;
        }
        if (_root == null) {
            if (!Build()) return;
        }
        if (!_root.activeSelf) {
            _root.SetActive(true);
            _dirty = true;                  // пока прятались, запас мог измениться
        }

        // Меню Esc — единственный момент, когда панель можно взять мышью.
        // Курсор в это время свободен, игра ввод не слушает, и перетаскивание
        // ничего не отнимает у игры. В остальное время панель кликов не
        // ловит вовсе, поэтому таскать её нечем и незачем.
        bool drag = Menu.IsVisible();
        if (drag != _dragMode) SetDragMode(drag);

        _due -= Time.unscaledDeltaTime;
        if (!_dirty && _due > 0f) return;
        _due = Mathf.Max(0.1f, Interval);
        _dirty = false;
        Refresh();
    }

    /// <summary>
    /// Включить или выключить «панель можно двигать».
    ///
    /// Сводится к одному флагу: ловит ли подложка лучи. Пока не ловит, мышь
    /// сквозь панель попадает в мир — это её обычное состояние и главное
    /// требование к ней. Пока ловит, работает DragMove, и панель едет за
    /// курсором.
    /// </summary>
    static void SetDragMode(bool on) {
        _dragMode = on;
        if (_bg != null) _bg.raycastTarget = on;
        if (_move != null) _move.gameObject.SetActive(on);
        // Меню Esc — чужое окно на той же канве, и нарисовано оно может быть
        // поверх нашего. Тогда до панели не дотянуться мышью: лучи упрутся в
        // меню. Поэтому на время перетаскивания поднимаем себя наверх.
        if (on && _root != null) _root.transform.SetAsLastSibling();
        if (!on) Remember();
    }

    /// <summary>Перевести положение панели обратно в отступ от угла и отдать
    /// плагину. Формула — обратная той, что в Build.</summary>
    static void Remember() {
        if (_panel == null) return;
        bool left = Where == Corner.TopLeft || Where == Corner.BottomLeft;
        bool top = Where == Corner.TopLeft || Where == Corner.TopRight;
        var p = _panel.anchoredPosition;
        var now = new Vector2(left ? p.x : -p.x, top ? -p.y : p.y);

        // Утащить панель за край экрана можно, а достать обратно уже нечем:
        // она там и кликов не ловит, и не видна. Поэтому край держим.
        var area = ((RectTransform)_root.transform).rect;
        now.x = Mathf.Clamp(now.x, 0f, Mathf.Max(0f, area.width - 60f));
        now.y = Mathf.Clamp(now.y, 0f, Mathf.Max(0f, area.height - 30f));
        _panel.anchoredPosition = new Vector2(left ? now.x : -now.x,
                                              top ? -now.y : now.y);

        if ((now - Offset).sqrMagnitude < 1f) return;   // не двигали
        Offset = now;
        if (OnMoved != null) OnMoved(now);
    }

    /// <summary>Надо ли вообще что-то рисовать. Только дешёвые проверки:
    /// метод зовётся каждый кадр.</summary>
    static bool Show(bool plannerVisible) {
        if (!Enabled || !Pin.Any) return false;
        // Окно показывает те же числа крупнее и подробнее. Две копии одного
        // ответа на экране — это не вдвое полезнее, это мельтешение.
        if (plannerVisible) return false;
        var player = Player.m_localPlayer;
        if (player == null || player.IsDead()) return false;
        // Инвентарь закрывает собой пол-экрана, и подсказка под ним всё равно
        // нечитаема. А вот меню Esc панель переживает намеренно: это
        // единственный момент, когда её можно подвинуть мышью.
        if (InventoryGui.IsVisible()) return false;
        return true;
    }

    /// <summary>Убрать всё построенное. Зовётся при выходе из мира: канва
    /// игры уходит вместе с ним, и держать ссылки на её детей нельзя.</summary>
    public static void Drop() {
        if (_root != null) Object.Destroy(_root);
        _root = null;
        _panel = null;
        _bg = null;
        _title = _empty = _more = _move = null;
        _list = null;
        _rows.Clear();
        _dragMode = false;
        _dirty = true;
    }

    static bool Build() {
        if (!UiKit.Init()) return false;

        _root = new GameObject("Forgeplan-pin", typeof(RectTransform));
        _root.transform.SetParent(UiKit.CanvasRoot, false);
        UiKit.Stretch((RectTransform)_root.transform);

        bool left = Where == Corner.TopLeft || Where == Corner.BottomLeft;
        bool top = Where == Corner.TopLeft || Where == Corner.TopRight;

        _panel = UiKit.Rect(_root.transform, "panel");
        _panel.anchorMin = _panel.anchorMax = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
        _panel.pivot = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
        _panel.anchoredPosition = new Vector2(left ? Offset.x : -Offset.x,
                                              top ? -Offset.y : Offset.y);
        _panel.sizeDelta = new Vector2(Width, 0f);

        _bg = UiKit.Box(_panel, "bg", UiKit.PanelSprite, Back);
        _bg.raycastTarget = false;
        // Таскается подложка, а двигается панель: у подложки нет своей
        // раскладки, и ухватить её можно в любом месте, не попадая в строки.
        _bg.gameObject.AddComponent<DragMove>().Target = _panel;

        var col = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
        col.padding = new RectOffset(10, 10, 8, 9);
        col.spacing = 3;
        col.childForceExpandHeight = false;
        col.childForceExpandWidth = true;
        col.childControlHeight = true;
        col.childControlWidth = true;

        // Высота по содержимому: строк то три, то восемь, и жёсткий размер
        // означал бы либо пустой хвост, либо обрезанный список.
        var fit = _panel.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _title = UiKit.Label(_panel, "title", L.Title, 14f, UiKit.Gold);
        UiKit.Row(_panel, "gap", 2f);

        _list = UiKit.Rect(_panel, "list");
        var lc = _list.gameObject.AddComponent<VerticalLayoutGroup>();
        lc.spacing = 2;
        lc.childForceExpandHeight = false;
        lc.childForceExpandWidth = true;
        lc.childControlHeight = true;
        lc.childControlWidth = true;
        // ContentSizeFitter здесь не нужен и вреден: список лежит внутри
        // раскладки родителя, а та при childControlHeight сама спрашивает у
        // него желаемую высоту. Второй счетовод на том же объекте — верный
        // способ получить дёргающуюся панель и ругань Unity в логе.

        _empty = UiKit.Label(_panel, "empty", "", 13f, UiKit.Dim);
        _more = UiKit.Label(_panel, "more", "", 12f, UiKit.Faint);
        _move = UiKit.Label(_panel, "move", L.PinDrag, 12f, UiKit.Gold);
        _move.gameObject.SetActive(false);

        ForgeplanPlugin.Log.LogInfo("закреплённый список поставлен: "
                                    + Where + " " + Offset);
        return true;
    }

    static void Refresh() {
        _title.text = L.Pinned + " · " + Pin.Items.Count;

        // Сверху то, чего не хватает больше всего: с этим и идти копать.
        // Собранное опускается вниз, но не исчезает — иначе список тает на
        // глазах и по нему не видно, из чего он вообще состоял. При равных
        // числах сортируем по имени, чтобы строки не перетасовывались сами
        // между пересчётами.
        var rows = Pin.Rows()
                      .OrderBy(r => r.Done)
                      .ThenByDescending(r => r.Need - r.Have)
                      .ThenBy(r => GameData.NameOf(r.Id))
                      .ToList();

        bool all = rows.Count > 0 && rows.All(r => r.Done);
        _empty.gameObject.SetActive(rows.Count == 0 || all);
        if (rows.Count == 0 || all) _empty.text = L.PinDone;

        int shown = Mathf.Min(rows.Count, Mathf.Max(1, MaxRows));
        for (int i = 0; i < shown; i++) {
            if (i >= _rows.Count) _rows.Add(new Row(_list));
            _rows[i].Show(rows[i]);
        }
        for (int i = shown; i < _rows.Count; i++) _rows[i].Hide();

        int rest = rows.Count - shown;
        _more.gameObject.SetActive(rest > 0);
        if (rest > 0) _more.text = L.PinMore + rest;
    }

    /// <summary>Строка «иконка — название — сколько ещё».</summary>
    class Row {
        readonly GameObject _go;
        readonly Image _icon;
        readonly TextMeshProUGUI _name, _need;

        public Row(Transform parent) {
            var rt = UiKit.Row(parent, "row", 20f);
            _go = rt.gameObject;
            UiKit.Horizontal(rt, 6);
            _icon = UiKit.Icon(rt, null, 18f);
            _name = UiKit.Label(rt, "name", "", 13f, UiKit.Ink);
            _name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            _need = UiKit.Label(rt, "need", "", 13f, UiKit.Gold,
                                TextAlignmentOptions.Right);
            // Под «10 / 156» с запасом: раньше тут стояло одно число.
            var le = _need.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 74;
            le.minWidth = 0;
        }

        public void Show(Pin.MatRow r) {
            var it = GameData.Get(r.Id);
            _icon.sprite = it != null ? it.Icon : null;
            _icon.color = _icon.sprite != null ? Color.white : Color.clear;
            _name.text = GameData.NameOf(r.Id);
            _need.text = r.Have + " / " + r.Need;
            // Собранное гаснет целиком, вместе с названием: строка должна
            // читаться как вычеркнутая, а не как «тут что-то не так с числом».
            _need.color = r.Done ? UiKit.Faint : UiKit.Gold;
            _name.color = r.Done ? UiKit.Faint : UiKit.Ink;
            if (!_go.activeSelf) _go.SetActive(true);
        }

        public void Hide() { if (_go.activeSelf) _go.SetActive(false); }
    }
}

}
