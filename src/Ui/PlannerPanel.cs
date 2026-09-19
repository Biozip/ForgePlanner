using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Forgeplan.Ui {

/// <summary>
/// Двойной щелчок по строке. Одиночный намеренно не считается: строка занимает
/// всю ширину списка, и случайный клик мимо кнопки добавлял бы вещь в план
/// незаметно для игрока.
/// </summary>
public class DoubleClick : MonoBehaviour, IPointerClickHandler {
    public System.Action Action;
    public void OnPointerClick(PointerEventData e) {
        if (e.clickCount == 2 && Action != null) Action();
    }
}

/// <summary>Таскает окно за заголовок.</summary>
public class DragMove : MonoBehaviour, IBeginDragHandler, IDragHandler {
    public RectTransform Target;
    Vector2 _grab;

    public void OnBeginDrag(PointerEventData e) {
        if (Target == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)Target.parent, e.position, e.pressEventCamera, out _grab);
        _grab = Target.anchoredPosition - _grab;
    }

    public void OnDrag(PointerEventData e) {
        if (Target == null) return;
        Vector2 p;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)Target.parent, e.position, e.pressEventCamera, out p))
            Target.anchoredPosition = p + _grab;
    }
}

/// <summary>
/// Окно планировщика.
///
/// Раскладка — три колонки: разделы, каталог, план. Так устроены большие окна
/// самой игры, и так же расставлен калькулятор на сайте: человек, пришедший с
/// сайта, не должен заново искать, где что.
///
/// Дерево объектов строится один раз, а обновляются только подписи — поэтому
/// каталог держит все пятьсот рецептов без ограничения, а строки
/// переиспользуются вместо пересоздания при каждой букве в поиске.
///
/// Planner и GameData про этот файл ничего не знают: они считают, он рисует.
/// </summary>
public class PlannerPanel {
    /* Фон. Угольный, а не чёрный: чистый чёрный выглядит дырой в экране, а
     * деревянная панель игры в своём цвете слишком светлая — подписи на ней
     * теряются. Поэтому спрайт панели умножается на угольный тон, а сверху
     * ложится ровный слой той же гаммы. Суммарная непрозрачность около 0,88:
     * сцена за окном угадывается, но читать не мешает. */
    static readonly Color Charcoal = new Color(0.30f, 0.28f, 0.25f, 0.80f);
    static readonly Color Veil = new Color(0.10f, 0.10f, 0.09f, 0.40f);

    GameObject _root;
    RectTransform _window;
    TMP_InputField _search;
    TextMeshProUGUI _found, _hint, _cartCount;
    RectTransform _catalog, _cart, _totals;
    Button _rawBtn, _directBtn, _coalBtn, _woodCoalBtn, _haveBtn;

    readonly List<CatalogRow> _catalogRows = new List<CatalogRow>();
    readonly List<CartRow> _cartRows = new List<CartRow>();
    readonly List<TotalPair> _pairs = new List<TotalPair>();
    readonly List<TotalRow> _notes = new List<TotalRow>();

    readonly List<KeyValuePair<Kind?, Button>> _tabs = new List<KeyValuePair<Kind?, Button>>();
    Kind? _group;                  // null — показывать всё

    readonly List<KeyValuePair<int, Button>> _biomes = new List<KeyValuePair<int, Button>>();
    int _biome = -1;               // -1 — все биомы

    GameObject _settings;          // оверлей настроек; null — ещё не собран
    GameObject _about;             // оверлей «Инфо»
    Button _spoilerBtn, _chestBtn;
    TextMeshProUGUI _openLine;

    /// <summary>Настройки поменялись — плагину нужно сохранить их в конфиг.</summary>
    public System.Action<bool> OnSpoilersChanged;
    public System.Action<bool> OnChestsChanged;
    /// <summary>Считать ли сундуки. Живёт в конфиге, сюда кладёт плагин.</summary>
    public bool Chests = true;

    List<ItemDef> _matches = new List<ItemDef>();
    string _queryShown;
    bool _rawView = true;
    bool _countHave;
    bool _planDirty = true;

    public bool Visible {
        get { return _root != null && _root.activeSelf; }
    }

    /// <summary>Курсор стоит в строке поиска. Пока так — буквы принадлежат
    /// строке, а не игре: иначе «i» на середине слова откроет инвентарь.</summary>
    public bool Typing {
        get { return Visible && _search != null && _search.isFocused; }
    }

    /* ————————————————————————— открыть и закрыть ————————————————————————— */

    public void Toggle() {
        if (Visible) { Hide(); return; }
        if (!GameData.Build()) {
            ForgeplanPlugin.Log.LogWarning("Каталог не собран: нужен загруженный мир.");
            return;
        }
        if (!UiKit.Init()) {
            ForgeplanPlugin.Log.LogWarning("Интерфейс игры ещё не поднялся.");
            return;
        }
        // Время открытия пишем в лог намеренно. «Ничего не происходит по F7»
        // и «окно строится восемь секунд» снаружи выглядят одинаково, а
        // чинятся по-разному; одна строка в логе снимает вопрос.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Progress.Refresh();
        bool first = _root == null;
        if (first) Build();
        _root.SetActive(true);
        Search(force: true);
        _planDirty = true;
        if (first)
            ForgeplanPlugin.Log.LogInfo(string.Format(
                "окно собрано за {0} мс, в каталоге {1} позиций",
                clock.ElapsedMilliseconds, _matches.Count));
    }

    public void Hide() {
        if (_root != null) _root.SetActive(false);
    }

    /// <summary>
    /// Пересобрать окно. Нужно при смене языка: подписи расставлены по всему
    /// дереву объектов, и обойти их, подменяя текст, дороже и хрупче, чем
    /// собрать заново — тем более что сборка занимает три десятка миллисекунд.
    /// Выбранный раздел, биом и содержимое списка пересборку переживают: они
    /// живут в полях, а не в интерфейсе.
    /// </summary>
    public void Rebuild() {
        bool wasVisible = Visible;
        Drop();
        if (!wasVisible) return;
        Build();
        _root.SetActive(true);
        Search(force: true);
        _planDirty = true;
    }

    /// <summary>Каталог держит ссылки на префабы мира — при выходе из мира
    /// окно нужно выбросить целиком, иначе иконки станут пустыми.</summary>
    /// <summary>
    /// Забыть выбранный раздел и биом.
    ///
    /// Зовётся при выходе из мира, а не из Drop: пересборку при смене языка
    /// фильтры переживать обязаны — там ничего не изменилось, кроме подписей.
    /// А вот фильтр, выставленный прошлым персонажем в прошлом мире, в новом
    /// не помнит никто, и окно открывается почти пустым.
    /// </summary>
    public void ResetFilters() {
        _group = null;
        _biome = -1;
        _queryShown = null;
    }

    public void Drop() {
        _settings = null;
        _about = null;
        _ask = null;
        _askWhere = null;
        _spoilerBtn = _chestBtn = null;
        _openLine = null;
        // Гасим до Destroy: тот откладывает удаление до конца кадра, и при
        // пересборке на экране на кадр оказались бы два окна внахлёст.
        if (_root != null) _root.SetActive(false);
        if (_root != null) Object.Destroy(_root);
        _root = null;
        _catalogRows.Clear();
        _cartRows.Clear();
        _pairs.Clear();
        _notes.Clear();
        _matches.Clear();
        _tabs.Clear();
        _biomes.Clear();
        _queryShown = null;
    }

    public void Tick() {
        if (!Visible) return;
        if (_search != null && _search.text != _queryShown) Search();
        if (_catalogRows.Count < _matches.Count) Fill(false);
        if (_planDirty) { RefreshPlan(); _planDirty = false; }
    }

    /* ————————————————————————— сборка дерева ————————————————————————— */

    void Build() {
        _root = new GameObject("Forgeplan", typeof(RectTransform));
        _root.transform.SetParent(UiKit.CanvasRoot, false);
        var rootRt = (RectTransform)_root.transform;
        UiKit.Stretch(rootRt);

        _window = UiKit.Rect(rootRt, "window");
        _window.anchorMin = _window.anchorMax = new Vector2(0.5f, 0.5f);
        _window.pivot = new Vector2(0.5f, 0.5f);
        _window.sizeDelta = WindowSize(rootRt);

        UiKit.Box(_window, "bg", UiKit.PanelSprite, UiKit.PanelSprite != null
            ? Charcoal : new Color(0.13f, 0.12f, 0.11f, 0.92f));
        UiKit.Box(_window, "veil", null, Veil);

        var col = _window.gameObject.AddComponent<VerticalLayoutGroup>();
        col.padding = new RectOffset(16, 16, 12, 14);
        col.spacing = 10;
        col.childForceExpandHeight = false;
        col.childForceExpandWidth = true;
        col.childControlHeight = true;
        col.childControlWidth = true;

        BuildHeader();
        BuildBiomes();
        BuildBody();

        var foot = UiKit.Row(_window, "foot", 30);
        UiKit.Horizontal(foot, 10);
        UiKit.Button(foot, L.Info, 96, 28, ShowAbout, 15);
        _hint = UiKit.Label(foot, "hint", "", 14, UiKit.Dim);
        _hint.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    /// <summary>Окно во весь экран не нужно, но и жёсткий размер не годится:
    /// у игры канва масштабируется настройкой «размер интерфейса», и окно в
    /// 1180 точек у кого-то вылезет за край. Отсюда потолок по канве.</summary>
    static Vector2 WindowSize(RectTransform root) {
        var r = root.rect;
        float w = r.width > 1f ? Mathf.Min(1240f, r.width * 0.94f) : 1240f;
        float h = r.height > 1f ? Mathf.Min(806f, r.height * 0.94f) : 806f;
        return new Vector2(w, h);
    }

    void BuildHeader() {
        var bar = UiKit.Row(_window, "header", 40);
        UiKit.Horizontal(bar, 10);

        // Тащим окно за заголовок. Прозрачная картинка нужна, чтобы полоса
        // ловила курсор: без raycastTarget тянуть будет не за что.
        var grip = bar.gameObject.AddComponent<Image>();
        grip.color = new Color(0, 0, 0, 0);
        grip.raycastTarget = true;
        bar.gameObject.AddComponent<DragMove>().Target = _window;

        // Заголовок стоит вне разметки и растянут на всю полосу: иначе он
        // центрируется по остатку строки, а остаток зависит от ширины кнопок —
        // и на разных языках название уезжает то влево, то вправо.
        var title = UiKit.Label(bar, "title", L.Title, 26, UiKit.Gold,
                                TextAlignmentOptions.Center);
        title.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var trt = (RectTransform)title.transform;
        UiKit.Stretch(trt);
        trt.offsetMin = new Vector2(10f, 0f);
        trt.offsetMax = new Vector2(-10f, 0f);
        title.raycastTarget = false;   // тащить окно за заголовок всё ещё можно

        // Пустое место слева: кнопки должны остаться справа, а заголовок их
        // больше не отталкивает — он в разметке не участвует.
        UiKit.Rect(bar, "spacer").gameObject
             .AddComponent<LayoutElement>().flexibleWidth = 1;

        // Подпись — язык, на который переключит, а не текущий: так видно, что
        // кнопка сделает, и не надо гадать, где ты сейчас.
        UiKit.FlagButton(bar, L.Ru, L.OtherLang, 84, 32, () => {
            L.Ru = !L.Ru;
            if (OnLanguageChanged != null) OnLanguageChanged(L.Ru);
            Rebuild();
        });
        UiKit.Button(bar, L.Settings, 128, 32, ShowSettings);
        UiKit.Button(bar, L.Close, 110, 32, Hide);
    }

    /// <summary>Язык переключили — плагину нужно сохранить это в конфиг.</summary>
    public System.Action<bool> OnLanguageChanged;

    /// <summary>
    /// Полоса биомов. Десять кнопок в два ряда: одним рядом они не помещаются
    /// ни на одном языке, а убрать их в колонку каталога нельзя — фильтр общий
    /// и относится к разделам тоже.
    ///
    /// Кружок цвета — символом в самой подписи, разметкой TMP. Спрайта под это
    /// заводить незачем, а цвета те же, что на сайте: они приезжают вместе с
    /// таблицей биомов в Tiers.g.cs.
    /// </summary>
    void BuildBiomes() {
        var block = UiKit.Rect(_window, "biomes");
        var ble = block.gameObject.AddComponent<LayoutElement>();
        ble.minHeight = ble.preferredHeight = 84;
        ble.flexibleHeight = 0;
        var h = UiKit.Horizontal(block, 10);
        h.childAlignment = TextAnchor.UpperLeft;

        var caption = UiKit.Label(block, "caption", L.Biome, 14, UiKit.Dim,
                                  TextAlignmentOptions.TopRight);
        var cle = caption.gameObject.AddComponent<LayoutElement>();
        cle.minWidth = cle.preferredWidth = 74;
        cle.minHeight = cle.preferredHeight = 32;

        var stack = UiKit.Rect(block, "stack");
        stack.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var col = stack.gameObject.AddComponent<VerticalLayoutGroup>();
        col.spacing = 6;
        col.childForceExpandHeight = false;
        col.childForceExpandWidth = false;
        col.childControlHeight = true;
        col.childControlWidth = true;
        col.childAlignment = TextAnchor.UpperLeft;

        var row1 = UiKit.Row(stack, "row1", 36);
        UiKit.Horizontal(row1, 6);
        var row2 = UiKit.Row(stack, "row2", 36);
        UiKit.Horizontal(row2, 6);

        AddBiome(row1, L.AllBiomes, -1);
        for (int i = 0; i < Tiers.Ids.Length; i++)
            AddBiome(i < 5 ? row1 : row2, BiomeLabel(i), i);
    }

    static string BiomeLabel(int tier) {
        var name = L.Ru ? Tiers.Ru[tier] : Tiers.En[tier];
        return "<color=" + Tiers.Color[tier] + ">" + "\u25CF" + "</color> " + name;
    }

    void AddBiome(Transform parent, string label, int tier) {
        var btn = UiKit.ButtonAuto(parent, label, 34, () => {
            if (!Progress.Unlocked(tier)) return;
            _biome = tier;
            Search(force: true);
        }, 14);
        _biomes.Add(new KeyValuePair<int, Button>(tier, btn));
    }

    /// <summary>
    /// Закрытый биом гасится, а не прячется: исчезающий ряд кнопок читается
    /// как поломка, а погасшая — как «сюда пока нельзя». Название при этом
    /// видно, и это осознанно: биомы игрок и так перечислит по карте, спойлер
    /// не в них, а в предметах.
    /// </summary>
    void MarkBiomes() {
        foreach (var b in _biomes) {
            bool open = b.Key < 0 || Progress.Unlocked(b.Key);
            var btn = b.Value;
            btn.interactable = open;
            foreach (var t in btn.GetComponentsInChildren<TMP_Text>(true))
                t.color = !open ? UiKit.Faint
                        : b.Key == _biome ? UiKit.Gold : UiKit.Dim;
        }
    }

    void BuildBody() {
        var body = UiKit.Rect(_window, "body");
        body.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        var h = UiKit.Horizontal(body, 12);
        h.childForceExpandHeight = true;

        BuildRail(body);
        BuildCatalogColumn(body);
        BuildPlanColumn(body);
    }

    /// <summary>Колонка разделов слева. Вертикальная, как в окне навыков и в
    /// фильтрах сайта: горизонтальный ряд на семь разделов не помещался — на
    /// прошлом заходе строка переключателей ровно так и вылезла за край
    /// окна.</summary>
    void BuildRail(Transform parent) {
        var rail = UiKit.Rect(parent, "rail");
        var le = rail.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = 150;
        le.flexibleWidth = 0;

        UiKit.Box(rail, "bg", UiKit.FieldSprite, new Color(0f, 0f, 0f, 0.30f));

        var col = rail.gameObject.AddComponent<VerticalLayoutGroup>();
        col.padding = new RectOffset(8, 8, 8, 8);
        col.spacing = 4;
        col.childForceExpandHeight = false;
        col.childForceExpandWidth = true;
        col.childControlHeight = true;
        col.childControlWidth = true;
        col.childAlignment = TextAnchor.UpperLeft;

        UiKit.Caption(rail, L.Items, 18);
        AddTab(rail, L.All, null);
        AddTab(rail, L.Weapons, Kind.Weapon);
        AddTab(rail, L.Armour, Kind.Armor);
        AddTab(rail, L.Tools, Kind.Tool);
        AddTab(rail, L.Food, Kind.Food);
        AddTab(rail, L.Materials, Kind.Material);
        AddTab(rail, L.Buildings, Kind.Piece);
        AddTab(rail, L.Other, Kind.Other);
    }

    void BuildCatalogColumn(Transform parent) {
        var left = UiKit.Rect(parent, "catalog-column");
        var le = left.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = 390;
        le.flexibleWidth = 0;

        var col = left.gameObject.AddComponent<VerticalLayoutGroup>();
        col.spacing = 6;
        col.childForceExpandHeight = false;
        col.childForceExpandWidth = true;
        col.childControlHeight = true;
        col.childControlWidth = true;

        UiKit.Caption(left, L.Search, 18);

        // Поле и крестик — один ряд с общей обводкой: крестик должен читаться
        // как часть поля, а не как отдельная кнопка рядом.
        var searchRow = UiKit.Row(left, "search-row", 36);
        // Справа отступ больше: крестик впритык к краю читался как часть
        // соседней колонки.
        UiKit.Horizontal(searchRow, 0, 1, 8);
        UiKit.Frame(searchRow, new Color(0.93f, 0.77f, 0.38f, 0.32f), 1f);

        _search = UiKit.Input(searchRow, L.SearchHint, 34);
        _search.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1;
        UiKit.Button(searchRow, "\u00D7", 34, 26, () => {
            _search.text = "";
            Search(force: true);
        });

        _found = UiKit.Label(left, "found", "", 14, UiKit.Dim);
        _found.gameObject.AddComponent<LayoutElement>().minHeight = 18;

        var frame = UiKit.Rect(left, "frame");
        frame.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        _catalog = UiKit.ScrollBox(frame);
    }

    void BuildPlanColumn(Transform parent) {
        var right = UiKit.Rect(parent, "plan-column");
        right.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var col = right.gameObject.AddComponent<VerticalLayoutGroup>();
        col.spacing = 6;
        col.childForceExpandHeight = false;
        col.childForceExpandWidth = true;
        col.childControlHeight = true;
        col.childControlWidth = true;

        var cartHead = UiKit.Row(right, "cart-head", 30);
        UiKit.Horizontal(cartHead, 8);
        UiKit.Label(cartHead, "cart-title", L.CraftPlan, 20, UiKit.Gold)
             .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        // «Очистить» стоит у плана, который она чистит. В заголовке окна, рядом
        // с «Закрыть», её принимали за «закрыть без сохранения».
        UiKit.Button(cartHead, L.Clear, 104, 24, () => {
            Planner.Cart.Clear();
            _planDirty = true;
        });
        _cartCount = UiKit.Label(cartHead, "count", "", 14, UiKit.Dim,
                                 TextAlignmentOptions.Right);
        // Ширину подписи задаёт сам текст. Фиксированные 170 пикселей при
        // выравнивании вправо оставляли между кнопкой и числом дыру в полполосы.
        var cle0 = _cartCount.gameObject.AddComponent<LayoutElement>();
        cle0.flexibleWidth = 0;

        var cartFrame = UiKit.Rect(right, "cart-frame");
        var cle = cartFrame.gameObject.AddComponent<LayoutElement>();
        cle.flexibleHeight = 1;
        cle.minHeight = 150;
        _cart = UiKit.ScrollBox(cartFrame, 4);

        BuildToggles(right);

        UiKit.Label(right, "totals-title", L.ToGather, 20, UiKit.Gold)
             .gameObject.AddComponent<LayoutElement>().minHeight = 26;

        var totalsFrame = UiKit.Rect(right, "totals-frame");
        var tle = totalsFrame.gameObject.AddComponent<LayoutElement>();
        tle.flexibleHeight = 1.5f;
        tle.minHeight = 180;
        _totals = UiKit.ScrollBox(totalsFrame, 3);
    }

    /// <summary>
    /// Переключатели режима. Кнопками, а не Toggle: галочке нужен свой спрайт,
    /// которого у игры под наш размер нет, а состояние прекрасно читается
    /// цветом — включённое золотом, выключенное серым.
    ///
    /// Подписи короткие намеренно. «Сырьё и добыча» с соседями не влезало в
    /// столбец и выпихивало его за край окна; смысл вынесен в подсказку внизу.
    /// </summary>
    void BuildToggles(Transform parent) {
        var bar = UiKit.Row(parent, "toggles", 32);
        UiKit.Horizontal(bar, 6);

        _rawBtn = UiKit.Button(bar, L.Raw, 112, 30, () => {
            _rawView = true; _planDirty = true;
        }, 15);
        _directBtn = UiKit.Button(bar, L.Recipes, 112, 30, () => {
            _rawView = false; _planDirty = true;
        }, 15);
        _coalBtn = UiKit.Button(bar, L.Coal, 100, 30, () => {
            Planner.CountCoal = !Planner.CountCoal; _planDirty = true;
        }, 15);
        _woodCoalBtn = UiKit.Button(bar, L.Firewood, 100, 30, () => {
            Planner.BurnCoalFromWood = !Planner.BurnCoalFromWood; _planDirty = true;
        }, 15);
        _haveBtn = UiKit.Button(bar, L.Inventory, 124, 30, () => {
            _countHave = !_countHave; _planDirty = true;
        }, 15);
    }

    static void Mark(Button b, bool on) {
        if (b == null) return;
        foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
            t.color = on ? UiKit.Gold : UiKit.Dim;
    }

    /* ————————————————————————— каталог ————————————————————————— */

    /// <summary>Кнопка раздела. null — «Всё».</summary>
    void AddTab(Transform parent, string label, Kind? kind) {
        var btn = UiKit.Button(parent, label, 134, 34, () => {
            _group = kind;
            Search(force: true);
        }, 15, TextAlignmentOptions.Left);
        _tabs.Add(new KeyValuePair<Kind?, Button>(kind, btn));
    }

    void Search(bool force = false) {
        var q = _search != null ? _search.text : "";
        if (!force && q == _queryShown) return;
        _queryShown = q;
        Progress.Refresh();
        // Выбранный биом мог закрыться: фильтр включили в настройках, или
        // окно осталось открытым с прошлого мира. Тогда возвращаемся ко «всем».
        if (_biome >= 0 && !Progress.Unlocked(_biome)) _biome = -1;

        foreach (var t in _tabs) Mark(t.Value, t.Key.Equals(_group));
        MarkBiomes();

        var needle = (q ?? "").Trim().ToLowerInvariant();
        _matches = GameData.Items.Values
            .Where(i => i.HasRecipe)
            .Where(i => !_group.HasValue || i.Group == _group.Value)
            .Where(i => _biome < 0 || i.Tier == _biome)
            .Where(i => Progress.Unlocked(i.Tier))
            .Where(i => needle.Length == 0 || i.Haystack.Contains(needle))
            .OrderBy(i => i.Shown)
            .ToList();

        int total = GameData.Items.Values.Count(i => i.HasRecipe);
        _found.text = L.Found + _matches.Count;
        if (_matches.Count < total) {
            _found.text += L.OutOf + total;
            if (_matches.Count == 0) _found.text += " — " + L.Filtered;
        }

        // Ноль найденных при полном каталоге — либо фильтры, либо ошибка в них.
        // Снаружи это выглядит одинаково, поэтому состояние уходит в лог.
        if (_matches.Count == 0 && total > 0)
            ForgeplanPlugin.Log.LogInfo(string.Format(
                "поиск ничего не дал: раздел={0}, биом={1}, запрос=\"{2}\", "
                + "прогрессия={3}, всего с рецептом={4}",
                _group.HasValue ? _group.Value.ToString() : "всё",
                _biome < 0 ? "все" : Tiers.Ids[_biome],
                needle, Progress.Enabled ? "вкл" : "выкл", total));
        Fill(true);

        // Смена раздела при прокрутке вниз оставляла пустоту: список стал
        // короче, а положение осталось прежним.
        _catalog.anchoredPosition = Vector2.zero;
    }

    /// <summary>Сколько строк каталога создавать за кадр.</summary>
    const int RowsPerFrame = 48;

    /// <summary>
    /// Достроить и заполнить строки каталога.
    ///
    /// Строки переиспользуются: лишние прячем, недостающие создаём — полный
    /// пересбор на каждую букву в поиске дёргал бы кадры. Но создаём их не все
    /// разом, а горстью за кадр, и вот почему: с приходом построек в каталоге
    /// стало 1126 позиций вместо 490, а строка — это шесть объектов Unity.
    /// Семь тысяч объектов в одном кадре подвешивали игру на несколько секунд,
    /// и первое нажатие F7 выглядело так, будто мод не работает.
    ///
    /// Прокрутке это не мешает: пока строки достраиваются, список уже виден и
    /// растёт сверху вниз, а полный он через десяток кадров.
    /// </summary>
    void Fill(bool refill) {
        int have = _catalogRows.Count;
        int want = Mathf.Min(_matches.Count, have + RowsPerFrame);
        for (int i = have; i < want; i++) _catalogRows.Add(new CatalogRow(_catalog, this));

        // При дозаполнении трогаем только новые: у прежних текст уже верный.
        for (int i = refill ? 0 : have; i < _catalogRows.Count; i++) {
            if (i < _matches.Count) _catalogRows[i].Show(_matches[i]);
            else _catalogRows[i].Hide();
        }
    }

    public void Add(string id) {
        var e = Planner.Cart.FirstOrDefault(x => x.Id == id);
        if (e != null) e.Qty++;
        else Planner.Cart.Add(new Entry { Id = id, Qty = 1, Quality = 1 });
        _planDirty = true;
    }

    /* ————————————————————————— список и итог ————————————————————————— */

    void RefreshPlan() {
        Mark(_rawBtn, _rawView);
        Mark(_directBtn, !_rawView);
        Mark(_coalBtn, Planner.CountCoal);
        Mark(_woodCoalBtn, Planner.BurnCoalFromWood);
        Mark(_haveBtn, _countHave);

        for (int i = 0; i < Planner.Cart.Count; i++) {
            if (i >= _cartRows.Count) _cartRows.Add(new CartRow(_cart, this));
            _cartRows[i].Show(Planner.Cart[i]);
        }
        for (int i = Planner.Cart.Count; i < _cartRows.Count; i++) _cartRows[i].Hide();

        int pieces = Planner.Cart.Sum(e => e.Qty);
        _cartCount.text = pieces > 0 ? L.TotalItems + pieces : "";

        var totals = _rawView ? Planner.Raw() : Planner.Direct();
        if (_countHave) totals = Stock.Subtract(totals, Stock.Count());

        var mats = totals
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => GameData.NameOf(kv.Key))
            .Select(kv => new TotalRow.Data {
                Icon = GameData.Get(kv.Key) != null ? GameData.Get(kv.Key).Icon : null,
                Name = GameData.NameOf(kv.Key) + BuyNote(kv.Key),
                Value = kv.Value.ToString(),
            }).ToList();

        // Материалы — в две колонки. Полтора десятка позиций в одну колонку
        // занимают всю высоту окна, а половина ширины при этом пустует.
        int rows = (mats.Count + 1) / 2;
        int order = 0;
        for (int i = 0; i < rows; i++) {
            if (i >= _pairs.Count) _pairs.Add(new TotalPair(_totals));
            var pair = _pairs[i];
            pair.Show(mats[i * 2],
                      i * 2 + 1 < mats.Count ? mats[i * 2 + 1] : (TotalRow.Data?)null);
            pair.Go.transform.SetSiblingIndex(order++);
        }
        for (int i = rows; i < _pairs.Count; i++) _pairs[i].Hide();

        var plan = Planner.Process(Planner.Refine());
        var notes = new List<TotalRow.Data>();
        AddStations(notes);
        if (plan.Count > 0) {
            notes.Add(new TotalRow.Data { Name = L.Processing, Header = true });
            foreach (var row in plan) {
                notes.Add(new TotalRow.Data {
                    Icon = GameData.StationIcon(row.Station),
                    Name = GameData.StationName(row.Station),
                    Value = row.Units + L.Pieces + Planner.Time(row.Seconds)
                          + (row.Loads > 0 ? L.Loads + row.Loads : ""),
                });
            }
        }
        // Порядок строк задаётся явно: пары и заметки лежат в разных пулах и
        // дописываются в конец по мере роста, поэтому на второй перерисовке
        // новая пара оказалась бы ниже «Переработки».
        for (int i = 0; i < notes.Count; i++) {
            if (i >= _notes.Count) _notes.Add(new TotalRow(_totals));
            _notes[i].Show(notes[i]);
            _notes[i].Go.transform.SetSiblingIndex(order++);
        }
        for (int i = notes.Count; i < _notes.Count; i++) _notes[i].Hide();

        _hint.text =
            Planner.Cart.Count == 0 ? L.HintEmpty
            : plan.Count > 0 ? L.HintProcessing
            : _rawView ? L.HintRaw
            : L.HintRecipes;
    }

    /// <summary>
    /// Раздел «Станки»: у чего именно придётся стоять и где оно.
    ///
    /// Раньше окно называло станок в строке каталога и на этом умолкало. Для
    /// арбалета это «Чёрная кузница» — и дальше игрок сам вспоминает, есть ли
    /// она у него, какого уровня и в какой стороне. Всё это мод знает.
    ///
    /// Нужный уровень считается по самой дорогой позиции плана: улучшение до
    /// второго качества требует станка на ступень выше, и узнать об этом,
    /// собрав материалы, — обидно.
    /// </summary>
    /// <summary>
    /// Приписка «у кого и почём» к названию покупного сырья.
    ///
    /// Разметкой в самой подписи, а не отдельным полем строки: половинка итогов
    /// и так тесная, а приписка нужна редко — у двух десятков позиций из
    /// четырёхсот. Своя колонка ради них отняла бы место у всех остальных.
    ///
    /// Звёздочка у запертых товаров значит «появится на прилавке не сразу».
    /// Переводить ключ в имя босса мод не берётся: половина ключей у торговцев
    /// не боссовые.
    /// </summary>
    static string BuyNote(string id) {
        Trade.Offer o;
        if (!Trade.TryGet(id, out o)) return "";
        return "  <size=72%><color=#8A8172>" + o.Trader + " · " + o.Price
             + (string.IsNullOrEmpty(o.Key) ? "" : " *") + "</color></size>";
    }

    /// <summary>До скольких метров показывать стрелку направления.</summary>
    const float ArrowRange = 150f;

    void AddStations(List<TotalRow.Data> notes) {
        // Что за станки нужны и какого уровня.
        var need = new Dictionary<string, int>();
        foreach (var e in Planner.Cart) {
            var it = GameData.Get(e.Id);
            if (it == null || string.IsNullOrEmpty(it.Station)) continue;
            int lvl = it.MinStationLevel + Mathf.Max(1, e.Quality) - 1;
            int had;
            if (!need.TryGetValue(it.Station, out had) || lvl > had)
                need[it.Station] = lvl;
        }
        if (need.Count == 0) return;

        var found = Stations.Distances();
        notes.Add(new TotalRow.Data { Name = L.StationsNeeded, Header = true });

        foreach (var kv in need.OrderBy(k => GameData.StationName(k.Key))) {
            var row = new TotalRow.Data {
                Icon = GameData.StationIcon(kv.Key),
                Name = GameData.StationName(kv.Key),
            };
            Stations.Found at;
            if (found.TryGetValue(kv.Key, out at)) {
                row.Value = Mathf.RoundToInt(at.Distance) + L.Metres
                          + "  " + L.LevelShort + at.Level;
                // Дальше полутораста метров стрелка бесполезна: на таком
                // расстоянии между «туда» и «туда же, но правее» — минута ходу
                // не в ту сторону.
                if (at.Distance <= ArrowRange) row.Bearing = at.Bearing;
                // Стоит, но не дорос: собирать материалы под улучшение, которое
                // некому сделать, — то же самое, что не иметь станка вовсе.
                if (at.Level < kv.Value) {
                    row.Value += "  (" + L.NeedLevel + kv.Value + ")";
                    row.Muted = true;
                }
            } else {
                row.Value = L.NotNearby;
                row.Muted = true;
                var pieceId = GameData.PieceId(kv.Key);
                if (GameData.Get(pieceId) != null) {
                    row.Action = L.AddToPlan;
                    row.OnAction = () => Add(pieceId);
                }
            }
            notes.Add(row);
        }
    }

    public void MarkDirty() { _planDirty = true; }

    /* ————————————————————————— настройки ————————————————————————— */

    /// <summary>
    /// Настройки поверх окна, а не отдельным окном: своё окно пришлось бы
    /// таскать, закрывать и держать поверх планировщика, а показать тут нужно
    /// два переключателя.
    /// </summary>
    void ShowSettings() {
        if (_settings == null) BuildSettings();
        Progress.Refresh();
        RefreshSettings();
        _settings.SetActive(true);
    }

    void HideSettings() {
        if (_settings != null) _settings.SetActive(false);
        Search(force: true);
    }

    void BuildSettings() {
        var box = Overlay("settings", 420f, 344f);
        _settings = box.parent.gameObject;

        UiKit.Label(box, "title", L.Settings, 22, UiKit.Gold)
             .gameObject.AddComponent<LayoutElement>().minHeight = 30;

        _spoilerBtn = UiKit.Button(Line(box), L.NoSpoilers, 320, 30, () => {
            Progress.Enabled = !Progress.Enabled;
            if (OnSpoilersChanged != null) OnSpoilersChanged(Progress.Enabled);
            Progress.Refresh();
            RefreshSettings();
        }, 16, TextAlignmentOptions.Left);
        Hint(box, L.NoSpoilersHint);

        _openLine = UiKit.Label(box, "open", "", 14, UiKit.Ink);
        _openLine.gameObject.AddComponent<LayoutElement>().minHeight = 22;

        _chestBtn = UiKit.Button(Line(box), L.UseChests, 320, 30, () => {
            Chests = !Chests;
            if (OnChestsChanged != null) OnChestsChanged(Chests);
            RefreshSettings();
        }, 16, TextAlignmentOptions.Left);
        Hint(box, L.UseChestsHint);

        var foot = UiKit.Row(box, "foot", 34);
        UiKit.Horizontal(foot, 8);
        UiKit.Rect(foot, "spacer").gameObject
             .AddComponent<LayoutElement>().flexibleWidth = 1;
        UiKit.Button(foot, L.Done, 120, 30, HideSettings);
    }

    /// <summary>
    /// Полоса под один элемент, прижатый влево.
    ///
    /// Нужна потому, что колонка настроек растягивает детей на всю ширину, и
    /// переключатель выходил во всю коробку — кнопка в шестьсот точек читается
    /// как заголовок, а не как то, что нажимают.
    /// </summary>
    static RectTransform Line(RectTransform parent) {
        var row = UiKit.Row(parent, "line", 30);
        UiKit.Horizontal(row, 8);
        return row;
    }

    static void Hint(RectTransform parent, string text) {
        var t = UiKit.Label(parent, "hint", text, 13, UiKit.Dim);
        t.textWrappingMode = TextWrappingModes.Normal;
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 34;
        le.flexibleHeight = 0;
    }

    void RefreshSettings() {
        Mark(_spoilerBtn, Progress.Enabled);
        Mark(_chestBtn, Chests);
        if (_openLine != null)
            _openLine.text = L.BiomesOpen + Progress.OpenCount
                           + L.OfNine + Tiers.Ids.Length;
    }

    /* ————————————————————————— инфо ————————————————————————— */

    void ShowAbout() {
        if (_about == null) BuildAbout();
        _about.SetActive(true);
    }

    void BuildAbout() {
        var box = Overlay("about", 520f, 260f);
        _about = box.parent.gameObject;

        UiKit.Label(box, "title", L.Title + "  " + Forgeplan.Build.Version, 22, UiKit.Gold)
             .gameObject.AddComponent<LayoutElement>().minHeight = 28;

        // Автор и его Steam на одной строке: отдельная строка под одну иконку
        // растила окно на пустом месте.
        var who = UiKit.Row(box, "author", 34);
        UiKit.Horizontal(who, 8);
        var name = UiKit.Label(who, "name", L.Author + ": Biozip", 16, UiKit.Ink);
        var nle = name.gameObject.AddComponent<LayoutElement>();
        nle.preferredWidth = name.GetPreferredValues(name.text).x + 4f;
        LinkButton(who, "steam", "Steam", Links.Steam, 30f);
        UiKit.Rect(who, "spacer").gameObject
             .AddComponent<LayoutElement>().flexibleWidth = 1;

        Hint(box, L.DataLine);

        var row = UiKit.Row(box, "links", 48);
        UiKit.Horizontal(row, 10);
        LinkButton(row, "globe", L.Site, Links.Site);
        LinkButton(row, "github", "GitHub", Links.Github);
        LinkButton(row, "nexus", "Nexus Mods", Links.Nexus);
        UiKit.Rect(row, "spacer").gameObject
             .AddComponent<LayoutElement>().flexibleWidth = 1;

        var foot = UiKit.Row(box, "foot", 32);
        UiKit.Horizontal(foot, 8);
        var ver = UiKit.Label(foot, "game", L.GameVersion + GameVersionString(),
                              13, UiKit.Dim);
        ver.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        UiKit.Button(foot, L.Close, 120, 30, () => _about.SetActive(false));
    }

    /// <summary>
    /// Кнопка-ссылка. Без адреса — гаснет и не нажимается: пустая кнопка,
    /// которая молча ничего не делает, хуже отсутствующей.
    /// </summary>
    void LinkButton(RectTransform parent, string icon, string title, string url,
                    float size = 44f) {
        bool live = !string.IsNullOrEmpty(url);
        var btn = UiKit.IconButton(parent, Ui.Art.Get(icon), size,
            () => { if (live) AskLink(title, url); },
            live ? UiKit.Ink : UiKit.Faint);
        btn.interactable = live;
        if (!live) {
            var note = UiKit.Label(parent, "none", L.NoAddress, 12, UiKit.Faint,
                                   TextAlignmentOptions.Left);
            note.gameObject.AddComponent<LayoutElement>().preferredWidth = 86;
        }
    }

    /// <summary>
    /// Версия самой игры. Спрашивают её не из любопытства: расчёт мода держится
    /// на данных конкретной сборки Valheim, и в жалобе «числа не сходятся»
    /// первым делом нужно знать, о какой игре речь.
    /// </summary>
    static string GameVersionString() {
        try {
            return global::Version.GetVersionString(false);
        } catch {
            return "?";
        }
    }

    /* —————————————————————— подтверждение перехода —————————————————————— */

    GameObject _ask;
    TextMeshProUGUI _askWhere;
    string _askUrl;

    /// <summary>
    /// Спросить, прежде чем уводить игрока из игры.
    ///
    /// Показываем сам адрес: «перейти на сайт» без адреса — это то, чему
    /// доверять не за что, а мод уже стоит с полными правами. Пусть видно,
    /// куда именно.
    /// </summary>
    void AskLink(string title, string url) {
        if (_ask == null) BuildAsk();
        _askUrl = url;
        _askWhere.text = title + "\n<color=#B9AE99>" + url + "</color>";
        _ask.SetActive(true);
    }

    void BuildAsk() {
        var box = Overlay("ask", 520f, 216f);
        _ask = box.parent.gameObject;

        UiKit.Label(box, "title", L.OpenLink, 20, UiKit.Gold)
             .gameObject.AddComponent<LayoutElement>().minHeight = 28;

        _askWhere = UiKit.Label(box, "where", "", 15, UiKit.Ink);
        _askWhere.textWrappingMode = TextWrappingModes.Normal;
        _askWhere.gameObject.AddComponent<LayoutElement>().minHeight = 46;

        Hint(box, L.LinkLeaves);

        var foot = UiKit.Row(box, "foot", 34);
        UiKit.Horizontal(foot, 8);
        UiKit.Rect(foot, "spacer").gameObject
             .AddComponent<LayoutElement>().flexibleWidth = 1;
        UiKit.Button(foot, L.Cancel, 120, 30, () => _ask.SetActive(false));
        UiKit.Button(foot, L.Open, 120, 30, () => {
            _ask.SetActive(false);
            if (string.IsNullOrEmpty(_askUrl)) return;
            ForgeplanPlugin.Log.LogInfo("открываю ссылку: " + _askUrl);
            Application.OpenURL(_askUrl);
        });
    }

    /// <summary>
    /// Завеса с коробкой посередине. Возвращает коробку — в неё складывают
    /// содержимое; завеса лежит на её родителе.
    /// </summary>
    RectTransform Overlay(string name, float w, float h) {
        var veil = UiKit.Rect(_window, name);
        UiKit.Stretch(veil);
        veil.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        // Непрозрачная для лучей завеса: без неё кнопки под оверлеем остаются
        // нажимаемыми, а окно выглядит сломанным.
        var block = veil.gameObject.AddComponent<Image>();
        block.color = new Color(0.06f, 0.06f, 0.05f, 0.72f);
        block.raycastTarget = true;

        var box = UiKit.Rect(veil, "box");
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(w, h);
        UiKit.Box(box, "bg", UiKit.PanelSprite, UiKit.PanelSprite != null
            ? Charcoal : new Color(0.13f, 0.12f, 0.11f, 0.96f));
        UiKit.Box(box, "veil", null, Veil);
        UiKit.Frame(box, UiKit.Line);

        var col = box.gameObject.AddComponent<VerticalLayoutGroup>();
        col.padding = new RectOffset(22, 22, 16, 16);
        col.spacing = 8;
        col.childForceExpandHeight = false;
        col.childForceExpandWidth = true;
        col.childControlHeight = true;
        col.childControlWidth = true;
        return box;
    }

    /* ————————————————————————— строки ————————————————————————— */

    class CatalogRow {
        readonly GameObject _go;
        readonly Image _icon;
        readonly TextMeshProUGUI _name, _station;
        string _id;

        public CatalogRow(Transform parent, PlannerPanel owner) {
            var rt = UiKit.Row(parent, "row", 30);
            _go = rt.gameObject;
            UiKit.Horizontal(rt, 8, 4, 4);
            _icon = UiKit.Icon(rt, null, 26);
            _name = UiKit.Label(rt, "name", "", 16, UiKit.Ink);
            _name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            _station = UiKit.Label(rt, "station", "", 12, UiKit.Dim,
                                   TextAlignmentOptions.Right);
            var sle = _station.gameObject.AddComponent<LayoutElement>();
            sle.preferredWidth = 84;
            sle.minWidth = 0;
            UiKit.Button(rt, "+", 34, 26, () => { if (_id != null) owner.Add(_id); });

            // Ловушка для щелчка. Подписи и иконки лучи не ловят, поэтому без
            // прозрачной картинки на самой строке двойной щелчок попадал бы в
            // пустоту. Прокрутке она не мешает: колесо система событий отдаёт
            // ближайшему предку, который его обрабатывает, — это ScrollRect.
            var catcher = rt.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;
            rt.gameObject.AddComponent<DoubleClick>().Action =
                () => { if (_id != null) owner.Add(_id); };
        }

        public void Show(ItemDef it) {
            _id = it.Id;
            _icon.sprite = it.Icon;
            _icon.color = it.Icon != null ? Color.white : Color.clear;
            _name.text = it.Shown;
            _station.text = string.IsNullOrEmpty(it.Station)
                ? L.ByHand : GameData.StationName(it.Station);
            if (!_go.activeSelf) _go.SetActive(true);
        }

        public void Hide() { if (_go.activeSelf) _go.SetActive(false); }
    }

    class CartRow {
        readonly GameObject _go;
        readonly Image _icon;
        readonly TextMeshProUGUI _name, _qty;
        readonly List<Button> _quality = new List<Button>();
        Entry _entry;

        public CartRow(Transform parent, PlannerPanel owner) {
            var rt = UiKit.Row(parent, "row", 32);
            _go = rt.gameObject;
            UiKit.Horizontal(rt, 6, 4, 4);

            _icon = UiKit.Icon(rt, null, 26);
            _name = UiKit.Label(rt, "name", "", 16, UiKit.Ink);
            _name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Кнопки качества создаём на максимум сразу: у самой «глубокой»
            // вещи в игре четыре уровня, а прятать лишние дешевле, чем
            // пересобирать строку при смене предмета.
            for (int q = 1; q <= 4; q++) {
                int level = q;
                _quality.Add(UiKit.Button(rt, q.ToString(), 28, 26, () => {
                    if (_entry != null) { _entry.Quality = level; owner.MarkDirty(); }
                }, 14));
            }

            UiKit.Button(rt, "−", 32, 26, () => {
                if (_entry != null && _entry.Qty > 1) { _entry.Qty--; owner.MarkDirty(); }
            });
            _qty = UiKit.Label(rt, "qty", "", 17, UiKit.Ink, TextAlignmentOptions.Center);
            _qty.gameObject.AddComponent<LayoutElement>().preferredWidth = 36;
            UiKit.Button(rt, "+", 32, 26, () => {
                if (_entry != null) { _entry.Qty++; owner.MarkDirty(); }
            });
            UiKit.Button(rt, "×", 32, 26, () => {
                if (_entry != null) { Planner.Cart.Remove(_entry); owner.MarkDirty(); }
            });
        }

        public void Show(Entry e) {
            _entry = e;
            var it = GameData.Get(e.Id);
            _icon.sprite = it != null ? it.Icon : null;
            _icon.color = _icon.sprite != null ? Color.white : Color.clear;
            _name.text = it != null ? it.Shown : e.Id;
            _qty.text = e.Qty.ToString();

            int max = it != null ? it.MaxQuality : 1;
            for (int q = 0; q < _quality.Count; q++) {
                bool on = q < max && max > 1;
                _quality[q].gameObject.SetActive(on);
                if (on) Mark(_quality[q], e.Quality == q + 1);
            }
            if (!_go.activeSelf) _go.SetActive(true);
        }

        public void Hide() { if (_go.activeSelf) _go.SetActive(false); }
    }

    /// <summary>Строка во всю ширину: заголовок «Переработка» и строки
    /// станков — у них длинное значение, в половину ширины оно не влезает.</summary>
    class TotalRow {
        public struct Data {
            public Sprite Icon;
            public string Name, Value;
            public bool Header;
            /// <summary>Есть — в строке появляется кнопка с этой подписью.</summary>
            public string Action;
            public System.Action OnAction;
            /// <summary>Приглушить строку: станка поблизости нет.</summary>
            public bool Muted;
            /// <summary>Угол до цели от направления взгляда. null — не показывать.</summary>
            public float? Bearing;
        }

        readonly GameObject _go;
        readonly Image _icon;
        readonly TextMeshProUGUI _name, _value;

        public GameObject Go { get { return _go; } }

        public TotalRow(Transform parent) {
            var rt = UiKit.Row(parent, "note", 26);
            _go = rt.gameObject;
            UiKit.Horizontal(rt, 8, 4, 6);
            _icon = UiKit.Icon(rt, null, 22);
            _name = UiKit.Label(rt, "name", "", 16, UiKit.Ink);
            _name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            // Стрелка направления. Стоит слева от числа, потому что читается
            // вместе с ним: «119 м вон туда».
            _arrow = UiKit.Icon(rt, Art.Get("arrow"), 15);
            _arrow.gameObject.SetActive(false);

            _value = UiKit.Label(rt, "value", "", 15, UiKit.Ink, TextAlignmentOptions.Right);
            var vle = _value.gameObject.AddComponent<LayoutElement>();
            vle.preferredWidth = 230;
            vle.minWidth = 60;

            // Кнопка есть всегда, но обычно спрятана: создавать её на лету
            // значит пересобирать разметку строки посреди перерисовки.
            _btn = UiKit.Button(rt, "", 150, 22, () => {
                if (_action != null) _action();
            }, 13f);
            _btn.gameObject.SetActive(false);
            _btnLabel = _btn.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        readonly Image _arrow;
        readonly Button _btn;
        readonly TextMeshProUGUI _btnLabel;
        System.Action _action;

        public void Show(Data d) {
            _icon.sprite = d.Icon;
            _icon.color = d.Icon != null ? Color.white : Color.clear;
            _name.text = d.Name;
            _name.color = d.Header ? UiKit.Gold : d.Muted ? UiKit.Dim : UiKit.Ink;
            _value.text = d.Value ?? "";
            _value.color = d.Muted ? UiKit.Dim : UiKit.Ink;

            // Без картинки стрелку не показываем вовсе: Image с пустым спрайтом
            // рисует не ничто, а сплошной прямоугольник своего цвета.
            bool hasArrow = d.Bearing.HasValue && _arrow.sprite != null;
            if (_arrow.gameObject.activeSelf != hasArrow)
                _arrow.gameObject.SetActive(hasArrow);
            if (hasArrow) {
                // Экранный поворот против часовой: у Unity положительный Z
                // крутит влево, а угол мы считали по часовой.
                _arrow.transform.localRotation =
                    Quaternion.Euler(0f, 0f, -d.Bearing.Value);
                _arrow.color = d.Muted ? UiKit.Dim : UiKit.Gold;
            }

            _action = d.OnAction;
            bool hasBtn = !string.IsNullOrEmpty(d.Action);
            if (hasBtn && _btnLabel != null) _btnLabel.text = d.Action;
            if (_btn.gameObject.activeSelf != hasBtn) _btn.gameObject.SetActive(hasBtn);
            if (!_go.activeSelf) _go.SetActive(true);
        }

        public void Hide() { if (_go.activeSelf) _go.SetActive(false); }
    }

    /// <summary>Половинка строки итогов: иконка, название, число.</summary>
    class TotalCell {
        readonly GameObject _go;
        readonly Image _icon;
        readonly TextMeshProUGUI _name, _value;

        public TotalCell(Transform parent) {
            var rt = UiKit.Rect(parent, "cell");
            _go = rt.gameObject;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            // preferredWidth = 0 намеренно: без него половинка с длинным
            // названием требует больше места, чем соседняя, и колонки чисел
            // перестают стоять друг под другом. Так вся ширина раздаётся
            // через flexibleWidth поровну.
            le.preferredWidth = 0;
            le.flexibleWidth = 1;
            le.minWidth = 80;
            le.minHeight = le.preferredHeight = 26;
            UiKit.Horizontal(rt, 8, 4, 8);
            _icon = UiKit.Icon(rt, null, 22);
            _name = UiKit.Label(rt, "name", "", 16, UiKit.Ink);
            _name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            _value = UiKit.Label(rt, "value", "", 17, UiKit.Gold, TextAlignmentOptions.Right);
            var vle = _value.gameObject.AddComponent<LayoutElement>();
            vle.preferredWidth = 64;
            vle.minWidth = 40;
        }

        public void Show(TotalRow.Data d) {
            _icon.sprite = d.Icon;
            _icon.color = d.Icon != null ? Color.white : Color.clear;
            _name.text = d.Name;
            _value.text = d.Value ?? "";
            if (!_go.activeSelf) _go.SetActive(true);
        }

        public void Hide() { if (_go.activeSelf) _go.SetActive(false); }
    }

    class TotalPair {
        readonly GameObject _go;
        readonly TotalCell _a, _b;

        public GameObject Go { get { return _go; } }

        public TotalPair(Transform parent) {
            var rt = UiKit.Row(parent, "pair", 26);
            _go = rt.gameObject;
            UiKit.Horizontal(rt, 10);
            _a = new TotalCell(rt);
            _b = new TotalCell(rt);
        }

        public void Show(TotalRow.Data left, TotalRow.Data? right) {
            _a.Show(left);
            if (right.HasValue) _b.Show(right.Value); else _b.Hide();
            if (!_go.activeSelf) _go.SetActive(true);
        }

        public void Hide() { if (_go.activeSelf) _go.SetActive(false); }
    }
}

}
