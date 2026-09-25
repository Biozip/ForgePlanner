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
    /// <summary>Отступ от угла экрана в точках канвы.</summary>
    public static Vector2 Offset = new Vector2(16f, 230f);
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

    static float _due;
    static bool _dirty = true;

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

        _due -= Time.unscaledDeltaTime;
        if (!_dirty && _due > 0f) return;
        _due = Mathf.Max(0.1f, Interval);
        _dirty = false;
        Refresh();
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
        // Инвентарь и меню Esc закрывают собой пол-экрана, и подсказка под
        // ними всё равно нечитаема.
        if (Menu.IsVisible() || InventoryGui.IsVisible()) return false;
        return true;
    }

    /// <summary>Убрать всё построенное. Зовётся при выходе из мира: канва
    /// игры уходит вместе с ним, и держать ссылки на её детей нельзя.</summary>
    public static void Drop() {
        if (_root != null) Object.Destroy(_root);
        _root = null;
        _panel = null;
        _title = _empty = _more = null;
        _list = null;
        _rows.Clear();
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

        var bg = UiKit.Box(_panel, "bg", UiKit.PanelSprite, Back);
        bg.raycastTarget = false;

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

        ForgeplanPlugin.Log.LogInfo("закреплённый список поставлен: "
                                    + Where + " " + Offset);
        return true;
    }

    static void Refresh() {
        var left = Pin.Left();
        _title.text = L.Pinned + " · " + Pin.Items.Count;

        // Сначала то, чего не хватает больше всего: с этим и идти копать.
        // При равных числах — по имени, чтобы список не перетасовывался сам
        // по себе между пересчётами.
        var rows = left.Where(kv => kv.Value > 0)
                       .OrderByDescending(kv => kv.Value)
                       .ThenBy(kv => GameData.NameOf(kv.Key))
                       .ToList();

        _empty.gameObject.SetActive(rows.Count == 0);
        if (rows.Count == 0) _empty.text = L.PinDone;

        int shown = Mathf.Min(rows.Count, Mathf.Max(1, MaxRows));
        for (int i = 0; i < shown; i++) {
            if (i >= _rows.Count) _rows.Add(new Row(_list));
            _rows[i].Show(rows[i].Key, rows[i].Value);
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
            var le = _need.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 48;
            le.minWidth = 0;
        }

        public void Show(string id, int n) {
            var it = GameData.Get(id);
            _icon.sprite = it != null ? it.Icon : null;
            _icon.color = _icon.sprite != null ? Color.white : Color.clear;
            _name.text = GameData.NameOf(id);
            _need.text = n.ToString();
            if (!_go.activeSelf) _go.SetActive(true);
        }

        public void Hide() { if (_go.activeSelf) _go.SetActive(false); }
    }
}

}
