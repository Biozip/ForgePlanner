using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Forgeplan.Ui {

/// <summary>Курсор вошёл в строку или ушёл с неё.</summary>
public class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public System.Action Enter, Exit;
    public void OnPointerEnter(PointerEventData e) { if (Enter != null) Enter(); }
    public void OnPointerExit(PointerEventData e) { if (Exit != null) Exit(); }
}

/// <summary>
/// Карточка предмета рядом с курсором.
///
/// Окно отвечает «сколько ресурсов нужно», но не отвечает на вопрос, который
/// задают раньше: «а стоит ли это делать». Чтобы понять, лучше ли бронзовая
/// кираса надетой кожаной, приходилось закрывать окно и идти к кузнице.
///
/// Слева — создаваемая вещь, справа — надетая в тот же слот, строки стоят
/// друг напротив друга. Лучше — зелёным и «+», хуже — красным и «−», поровну —
/// «=». Под ними рецепт с тем, что уже в рюкзаке. У вещи, которую не надеть,
/// карточка состоит из одного рецепта.
///
/// Карточка лучи не ловит вовсе (CanvasGroup.blocksRaycasts = false): иначе
/// она, оказавшись под курсором, отбирала бы наведение у строки, та гасила бы
/// карточку, и так по кругу — мигание в каждом кадре.
///
/// Обновляется из Tick, а не из событий наведения. Строки каталога
/// переиспользуются: при прокрутке под неподвижным курсором строка меняет
/// предмет, а событий наведения при этом нет. Поэтому карточка спрашивает
/// строку каждый кадр, что в ней сейчас, и пересобирается, только если ответ
/// поменялся.
/// </summary>
public class ItemCard {
    /// <summary>Показывать ли карточку вообще. Из конфига.</summary>
    public static bool Enabled = true;
    /// <summary>Сравнивать ли с надетым. Выключено — одна карточка без цвета.</summary>
    public static bool Compare = true;

    const float CardWidth = 300f;
    const float RowHeight = 20f;
    const float Gap = 6f;

    static readonly Color Good = new Color(0.56f, 0.86f, 0.42f);
    static readonly Color Bad = new Color(0.94f, 0.44f, 0.36f);
    static readonly Color Back = new Color(0.08f, 0.07f, 0.06f, 0.94f);

    GameObject _root;
    RectTransform _rt, _cards;
    Column _left, _right;
    RecipeBox _recipe;

    // откуда брать, что показывать
    GameObject _owner;
    System.Func<string> _id;
    System.Func<int> _quality;

    // что показано сейчас
    string _shownId;
    int _shownQ;
    bool _shownCompare;
    ItemDrop.ItemData _shownEq;
    int _shownEqQ;
    float _nextRecount;

    /// <summary>Курсор вошёл в строку. owner — сама строка: по нему уход
    /// с неё отличается от ухода с соседней.</summary>
    public void Point(GameObject owner, System.Func<string> id, System.Func<int> quality) {
        _owner = owner;
        _id = id;
        _quality = quality;
    }

    /// <summary>Курсор ушёл. Если уже успел войти в соседнюю строку — её
    /// карточку не трогаем: события приходят в любом порядке.</summary>
    public void Leave(GameObject owner) {
        if (_owner != owner) return;
        _owner = null;
        Hide();
    }

    public void Hide() {
        if (_root != null && _root.activeSelf) _root.SetActive(false);
        _shownId = null;
    }

    public void Drop() {
        if (_root != null) Object.Destroy(_root);
        _root = null;
        _owner = null;
        _shownId = null;
        _shownEq = null;
    }

    public void Tick() {
        if (!Enabled || _owner == null || !_owner.activeInHierarchy) { Hide(); return; }
        var id = _id != null ? _id() : null;
        var it = string.IsNullOrEmpty(id) ? null : GameData.Get(id);
        if (it == null) { Hide(); return; }
        int q = Mathf.Clamp(_quality != null ? _quality() : 1, 1, Mathf.Max(1, it.MaxQuality));

        var data = Data(it);
        bool wear = data != null && ItemStats.Wearable(data.m_shared.m_itemType);
        var eq = wear && Compare
            ? ItemStats.Equipped(Player.m_localPlayer, data.m_shared.m_itemType) : null;

        if (_root == null) Build();
        bool changed = id != _shownId || q != _shownQ || Compare != _shownCompare
            || eq != _shownEq || (eq != null && eq.m_quality != _shownEqQ);
        if (changed) {
            Fill(it, data, q, wear, eq);
            _shownId = id;
            _shownQ = q;
            _shownCompare = Compare;
            _shownEq = eq;
            _shownEqQ = eq != null ? eq.m_quality : 0;
            _nextRecount = Time.unscaledTime + 1f;
        } else if (Time.unscaledTime >= _nextRecount) {
            // В рюкзаке могло прибавиться, пока карточка висит.
            _recipe.Show(it, q);
            _nextRecount = Time.unscaledTime + 1f;
        }

        if (!_root.activeSelf) {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        Place();
    }

    static ItemDrop.ItemData Data(ItemDef it) {
        if (it.Prefab == null) return null;
        var drop = it.Prefab.GetComponent<ItemDrop>();
        return drop != null ? drop.m_itemData : null;
    }

    /* ————————————————————————— содержимое ————————————————————————— */

    void Fill(ItemDef it, ItemDrop.ItemData data, int q, bool wear, ItemDrop.ItemData eq) {
        _cards.gameObject.SetActive(wear);
        if (wear) {
            // Создаваемая вещь — на текущем уровне мира: такой она и выйдет из
            // кузницы. Надетая — со своим, как есть.
            var mine = ItemStats.Read(data, q, Game.m_worldLevel);
            string sub = L.CardCrafted + Level(it.MaxQuality, q);

            if (Compare && eq != null) {
                var theirs = ItemStats.Read(eq, eq.m_quality, eq.m_worldLevel);
                List<Stat> left, right;
                Pair(mine, theirs, out left, out right);
                _left.Show(it.Icon, it.Shown, sub, left, right);
                _right.Show(eq.GetIcon(), Localization.instance.Localize(eq.m_shared.m_name),
                            L.CardEquipped + Level(eq.m_shared.m_maxQuality, eq.m_quality),
                            right, null);
                _right.Visible(true);
            } else {
                _left.Show(it.Icon, it.Shown, sub, WithHeader(mine), null);
                if (Compare) {
                    _right.ShowNothing();
                    _right.Visible(true);
                } else {
                    _right.Visible(false);
                }
            }
        }
        _recipe.Show(it, q);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);
    }

    static string Level(int max, int q) {
        return max > 1 ? " · " + L.LevelShort + q : "";
    }

    /// <summary>
    /// Выровнять две карточки строка к строке.
    ///
    /// Берётся объединение характеристик: у топора есть «Рубка», у меча нет,
    /// но строка должна стоять в обеих, иначе всё ниже съедет и сравнивать
    /// придётся глазами через полэкрана. Недостающее — ноль или «обычно».
    /// </summary>
    static void Pair(List<Stat> mine, List<Stat> theirs,
                     out List<Stat> left, out List<Stat> right) {
        var a = new Dictionary<string, Stat>();
        var b = new Dictionary<string, Stat>();
        foreach (var s in mine) a[s.Key] = s;
        foreach (var s in theirs) b[s.Key] = s;
        var all = new List<Stat>(mine);
        foreach (var s in theirs) if (!a.ContainsKey(s.Key)) all.Add(s);
        all.Sort((x, y) => x.Order != y.Order ? x.Order.CompareTo(y.Order)
                                             : string.CompareOrdinal(x.Key, y.Key));
        left = new List<Stat>();
        right = new List<Stat>();
        bool header = false;
        foreach (var s in all) {
            if (!header && s.Key.StartsWith("res_")) {
                var h = ItemStats.ResistHeader();
                left.Add(h);
                right.Add(h);
                header = true;
            }
            Stat l, r;
            a.TryGetValue(s.Key, out l);
            b.TryGetValue(s.Key, out r);
            left.Add(l ?? ItemStats.Absent(r));
            right.Add(r ?? ItemStats.Absent(l));
        }
    }

    static List<Stat> WithHeader(List<Stat> stats) {
        var list = new List<Stat>(stats);
        list.Sort((x, y) => x.Order != y.Order ? x.Order.CompareTo(y.Order)
                                              : string.CompareOrdinal(x.Key, y.Key));
        int i = list.FindIndex(s => s.Key.StartsWith("res_"));
        if (i >= 0) list.Insert(i, ItemStats.ResistHeader());
        return list;
    }

    /* ————————————————————————— место на экране ————————————————————————— */

    /// <summary>Справа от курсора; не влезает — слева. Снизу не вылезает:
    /// карточка у нижнего края поднимается, а не обрезается.</summary>
    void Place() {
        var canvas = (RectTransform)UiKit.CanvasRoot;
        var c = canvas.GetComponent<Canvas>();
        Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas, Input.mousePosition, cam, out local)) return;

        var r = canvas.rect;
        var size = _rt.rect.size;
        const float margin = 8f, off = 24f;
        float x = local.x + off;
        if (x + size.x > r.xMax - margin) x = local.x - off - size.x;
        x = Mathf.Max(x, r.xMin + margin);
        float y = local.y + 12f;
        if (y - size.y < r.yMin + margin) y = r.yMin + margin + size.y;
        y = Mathf.Min(y, r.yMax - margin);
        _rt.anchoredPosition = new Vector2(x, y);
    }

    /* ————————————————————————— сборка ————————————————————————— */

    void Build() {
        _root = new GameObject("ForgeplanCard", typeof(RectTransform));
        _root.transform.SetParent(UiKit.CanvasRoot, false);
        _rt = (RectTransform)_root.transform;
        _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
        _rt.pivot = new Vector2(0f, 1f);

        var group = _root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var col = _root.AddComponent<VerticalLayoutGroup>();
        col.spacing = Gap;
        col.childControlWidth = col.childControlHeight = true;
        col.childForceExpandWidth = true;
        col.childForceExpandHeight = false;
        var fit = _root.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _cards = UiKit.Rect(_rt, "cards");
        var row = UiKit.Horizontal(_cards, Gap);
        row.childAlignment = TextAnchor.UpperLeft;
        _left = new Column(_cards, true);
        _right = new Column(_cards, false);
        _recipe = new RecipeBox(_rt);
        _root.SetActive(false);
    }

    static RectTransform Panel(Transform parent, string name) {
        var rt = UiKit.Rect(parent, name);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = CardWidth;
        le.flexibleWidth = 0;
        UiKit.Box(rt, "bg", UiKit.FieldSprite, Back);
        UiKit.Frame(rt, new Color(0.93f, 0.77f, 0.38f, 0.28f));
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(10, 10, 8, 10);
        v.spacing = 2;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return rt;
    }

    static TextMeshProUGUI Shrinking(TextMeshProUGUI t, float min) {
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = t.fontSize;
        return t;
    }

    /* ————————————————————————— колонка характеристик ————————————————————————— */

    class Column {
        readonly RectTransform _rt, _list;
        readonly Image _icon;
        readonly TextMeshProUGUI _name, _sub;
        readonly List<StatRow> _rows = new List<StatRow>();
        readonly bool _deltas;

        public Column(Transform parent, bool deltas) {
            _deltas = deltas;
            _rt = Panel(parent, deltas ? "crafted" : "equipped");

            var head = UiKit.Row(_rt, "head", 34);
            UiKit.Horizontal(head, 8);
            _icon = UiKit.Icon(head, null, 30);
            _name = Shrinking(UiKit.Label(head, "name", "", 16, UiKit.Ink), 12);
            var nle = _name.gameObject.AddComponent<LayoutElement>();
            nle.flexibleWidth = 1;
            nle.minWidth = 0;

            _sub = UiKit.Label(_rt, "sub", "", 13, UiKit.Dim);
            _sub.gameObject.AddComponent<LayoutElement>().minHeight = 18;
            UiKit.Separator(_rt);

            _list = UiKit.Rect(_rt, "stats");
            var v = _list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 1;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
        }

        public void Visible(bool on) {
            if (_rt.gameObject.activeSelf != on) _rt.gameObject.SetActive(on);
        }

        public void Show(Sprite icon, string name, string sub, List<Stat> rows, List<Stat> against) {
            _icon.sprite = icon;
            _icon.color = icon != null ? Color.white : Color.clear;
            _name.text = name;
            _name.color = UiKit.Ink;
            _sub.text = sub;
            while (_rows.Count < rows.Count) _rows.Add(new StatRow(_list, _deltas));
            for (int i = 0; i < _rows.Count; i++) {
                if (i < rows.Count) _rows[i].Show(rows[i], against != null ? against[i] : null);
                else _rows[i].Hide();
            }
        }

        public void ShowNothing() {
            _icon.sprite = null;
            _icon.color = Color.clear;
            _name.text = L.CardNothing;
            _name.color = UiKit.Dim;
            _sub.text = L.CardEquipped;
            foreach (var r in _rows) r.Hide();
        }
    }

    class StatRow {
        readonly GameObject _go;
        readonly TextMeshProUGUI _label, _value, _delta;

        public StatRow(Transform parent, bool deltas) {
            var rt = UiKit.Row(parent, "stat", RowHeight);
            _go = rt.gameObject;
            UiKit.Horizontal(rt, 6);
            _label = Shrinking(UiKit.Label(rt, "label", "", 14, UiKit.Ink), 11);
            var lle = _label.gameObject.AddComponent<LayoutElement>();
            lle.flexibleWidth = 1;
            lle.minWidth = 0;
            _value = Shrinking(UiKit.Label(rt, "value", "", 14, UiKit.Ink,
                                           TextAlignmentOptions.Right), 10);
            var vle = _value.gameObject.AddComponent<LayoutElement>();
            vle.preferredWidth = deltas ? 96 : 110;
            vle.minWidth = 0;
            _delta = UiKit.Label(rt, "delta", "", 13, UiKit.Dim, TextAlignmentOptions.Right);
            var dle = _delta.gameObject.AddComponent<LayoutElement>();
            dle.preferredWidth = 46;
            dle.minWidth = 0;
            _delta.gameObject.SetActive(deltas);
        }

        public void Show(Stat s, Stat other) {
            bool header = s.Key == "res_header";
            _label.text = s.Label;
            _label.color = header ? UiKit.Gold : UiKit.Dim;
            _value.text = s.Text ?? "";
            _value.color = UiKit.Ink;
            _delta.text = "";

            if (other != null && s.Dir != Better.None && !header) {
                float d = s.Value - other.Value;
                if (Mathf.Abs(d) < 0.005f) {
                    _delta.text = "=";
                    _delta.color = UiKit.Dim;
                } else {
                    bool good = s.Dir == Better.Higher ? d > 0 : d < 0;
                    var c = good ? Good : Bad;
                    _value.color = c;
                    _delta.color = c;
                    _delta.text = s.Delta != null ? s.Delta(d) : (d > 0 ? "+" : "−");
                }
            }
            if (!_go.activeSelf) _go.SetActive(true);
        }

        public void Hide() { if (_go.activeSelf) _go.SetActive(false); }
    }

    /* ————————————————————————— рецепт ————————————————————————— */

    /// <summary>
    /// Стоимость до выбранного качества целиком — крафт плюс улучшения, как в
    /// плане, — и сколько из этого уже в рюкзаке. Считается только рюкзак, как
    /// в закреплённом списке: карточка отвечает на вопрос «могу ли я сделать
    /// это прямо сейчас», а на него сундуки в полусотне метров не отвечают.
    /// </summary>
    class RecipeBox {
        readonly RectTransform _list;
        readonly TextMeshProUGUI _title, _where;
        readonly List<RecipeRow> _rows = new List<RecipeRow>();

        public RecipeBox(Transform parent) {
            var rt = Panel(parent, "recipe");
            _title = UiKit.Label(rt, "title", "", 15, UiKit.Gold);
            _title.gameObject.AddComponent<LayoutElement>().minHeight = 20;
            _where = UiKit.Label(rt, "where", "", 13, UiKit.Dim);
            _where.gameObject.AddComponent<LayoutElement>().minHeight = 18;
            UiKit.Separator(rt);
            _list = UiKit.Rect(rt, "mats");
            var v = _list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 1;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
        }

        public void Show(ItemDef it, int q) {
            _title.text = L.CardRecipe + Level(it.MaxQuality, q)
                        + (it.Out > 1 ? "  ×" + it.Out : "");
            _where.text = string.IsNullOrEmpty(it.Station) ? L.ByHand
                : GameData.StationName(it.Station) + (it.FromConversion ? ""
                    : "  " + L.LevelShort + (it.MinStationLevel + q - 1));

            var need = new Dictionary<string, int>();
            for (int k = 0; k < q && k < it.Levels.Count; k++)
                foreach (var kv in it.Levels[k]) {
                    int had;
                    need[kv.Key] = (need.TryGetValue(kv.Key, out had) ? had : 0) + kv.Value;
                }
            var have = Stock.Count(false);

            int i = 0;
            foreach (var kv in need) {
                if (kv.Value <= 0) continue;
                if (i >= _rows.Count) _rows.Add(new RecipeRow(_list));
                int got;
                _rows[i++].Show(kv.Key, have.TryGetValue(kv.Key, out got) ? got : 0, kv.Value);
            }
            for (; i < _rows.Count; i++) _rows[i].Hide();
        }
    }

    class RecipeRow {
        readonly GameObject _go;
        readonly Image _icon;
        readonly TextMeshProUGUI _name, _count;

        public RecipeRow(Transform parent) {
            var rt = UiKit.Row(parent, "mat", 22);
            _go = rt.gameObject;
            UiKit.Horizontal(rt, 6);
            _icon = UiKit.Icon(rt, null, 20);
            _name = Shrinking(UiKit.Label(rt, "name", "", 14, UiKit.Ink), 11);
            var nle = _name.gameObject.AddComponent<LayoutElement>();
            nle.flexibleWidth = 1;
            nle.minWidth = 0;
            _count = UiKit.Label(rt, "count", "", 14, UiKit.Ink, TextAlignmentOptions.Right);
            var cle = _count.gameObject.AddComponent<LayoutElement>();
            cle.preferredWidth = 90;
            cle.minWidth = 0;
        }

        public void Show(string id, int have, int need) {
            var it = GameData.Get(id);
            _icon.sprite = it != null ? it.Icon : null;
            _icon.color = _icon.sprite != null ? Color.white : Color.clear;
            _name.text = it != null ? it.Shown : id;
            // «14 / 10» читается как ошибка счёта — больше нужного не пишем,
            // как и в закреплённом списке.
            _count.text = Mathf.Min(have, need) + " / " + need;
            _count.color = have >= need ? Good : UiKit.Ink;
            if (!_go.activeSelf) _go.SetActive(true);
        }

        public void Hide() { if (_go.activeSelf) _go.SetActive(false); }
    }
}

}
