using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Forgeplan.Ui {

/// <summary>
/// Кнопка «ForgePlanner» на панели крафта у станка.
///
/// Третьей вкладкой рядом с «Изготовить» и «Улучшить» это сделать нельзя:
/// вкладки у игры расставлены не разметкой, а числами, и третья либо наедет на
/// соседнюю, либо уедет за край — по-разному у каждого, потому что размер
/// интерфейса в Valheim настраивается.
///
/// Поэтому кнопка вынесена под панель, на собственную полку. Полка — не
/// растянутая панель игры, а свой прямоугольник тем же спрайтом, что у панели:
/// менять размеры чужого окна значит пересчитывать чужую разметку, и любое
/// дополнение Valheim это сломает. Свой прямоугольник ничего чужого не трогает.
///
/// Верхним краем полка встаёт ровно на нижнюю границу панели. Раньше она
/// заезжала под панель на десяток точек — считалось, что так не видно шва. Шва
/// и правда не видно, но «Изготовить» у игры доходит до самого низа панели, и
/// заезжала полка не под панель, а на кнопку. Прятать нечего: край у спрайта
/// рваный и мягкий, встык он выглядит как ярлычок, а не как стык.
///
/// Чужое окно — вещь по определению хрупкая: панель подвинут, и полка окажется
/// не там. Ломаться это будет тихо и безопасно, а чинится стороной и отступами
/// в конфиге, без пересборки мода.
/// </summary>
[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
static class StationButton {
    /// <summary>С какой стороны панели держать полку. Вертикаль в названиях
    /// осталась от прежнего размещения и ни на что не влияет: полка всегда
    /// снизу, значение читается только как «слева» или «справа».</summary>
    public enum Corner { TopRight, TopLeft, BottomLeft, BottomRight }

    /// <summary>Показывать ли кнопку. Ставит плагин из конфига.</summary>
    public static bool Enabled = true;
    public static Corner Where = Corner.BottomRight;
    /// <summary>Сдвиг полки: X — отступ от края панели, Y — насколько опустить
    /// ниже её нижней границы.</summary>
    public static Vector2 Offset = new Vector2(14f, 0f);

    const float BtnW = 150f, BtnH = 26f;
    /// <summary>Поля вокруг кнопки. Полка ровно на столько больше кнопки —
    /// рамка, а не подставка: пустой фон над кнопкой читается как чужой
    /// недорисованный кусок окна.</summary>
    const float PadX = 10f, PadY = 6f;

    static RectTransform _shelf;

    static void Postfix() {
        if (!Enabled) {
            // Unity переопределяет ==, поэтому проверка ловит и уничтоженный
            // объект, оставивший ненулевую ссылку.
            if (_shelf != null) _shelf.gameObject.SetActive(false);
            return;
        }

        var gui = InventoryGui.instance;
        if (gui == null || gui.m_crafting == null) return;
        if (_shelf == null) Create(gui.m_crafting);
        if (_shelf == null) return;
        _shelf.gameObject.SetActive(true);
    }

    static void Create(RectTransform panel) {
        if (!UiKit.Init()) return;

        bool left = Where == Corner.TopLeft || Where == Corner.BottomLeft;

        var shelf = UiKit.Rect(panel, "forgeplan-shelf");
        shelf.anchorMin = shelf.anchorMax = new Vector2(left ? 0f : 1f, 0f);
        shelf.pivot = new Vector2(left ? 0f : 1f, 1f);   // верхним краем к низу панели
        shelf.sizeDelta = new Vector2(BtnW + PadX * 2f, BtnH + PadY * 2f);
        shelf.anchoredPosition = new Vector2(left ? Offset.x : -Offset.x,
                                             -Offset.y);

        var bg = shelf.gameObject.AddComponent<Image>();
        var src = Backdrop(panel);
        if (src != null) {
            bg.sprite = src.sprite;
            bg.type = src.type;
            bg.color = src.color;
            bg.material = src.material;
            bg.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
        } else {
            bg.color = new Color(0.13f, 0.12f, 0.11f, 0.95f);
        }
        bg.raycastTarget = false;

        var btn = UiKit.Button(shelf, L.Title, BtnW, BtnH, Open, 13f);
        var rt = (RectTransform)btn.transform;
        var le = rt.GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = true;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        foreach (var t in btn.GetComponentsInChildren<TMP_Text>(true))
            t.color = UiKit.Gold;

        _shelf = shelf;
        ForgeplanPlugin.Log.LogInfo(
            "полка с кнопкой поставлена: " + Where + " " + Offset
            + (src != null ? ", фон " + src.sprite.name : ", фон свой"));
    }

    /// <summary>
    /// Чем закрашена сама панель. Берём её спрайт, а не свой: полка должна
    /// выглядеть продолжением окна станка, а не куском нашего.
    /// </summary>
    static Image Backdrop(RectTransform panel) {
        for (Transform t = panel; t != null; t = t.parent) {
            var img = t.GetComponent<Image>();
            if (img != null && img.sprite != null) return img;
        }
        // Иногда фон лежит отдельным ребёнком панели. Берём только прямых
        // детей и только с говорящим именем: иначе легко подцепить иконку.
        for (int i = 0; i < panel.childCount; i++) {
            var img = panel.GetChild(i).GetComponent<Image>();
            if (img == null || img.sprite == null) continue;
            var n = img.gameObject.name.ToLowerInvariant();
            if (n.Contains("bkg") || n.Contains("background") || n.Contains("panel"))
                return img;
        }
        return null;
    }

    static void Open() {
        // Станок закрываем — два окна, дерущихся за курсор, худшее из
        // возможного. Но открыться сразу нельзя: InventoryGui.Hide() не
        // мгновенный, окно уходит анимацией, и ещё несколько кадров игра
        // считает его открытым. Планировщик в этот момент открывался и тут же
        // закрывался сам — снаружи это выглядело так, будто кнопка просто
        // закрыла станок. Поэтому не открываем, а просим открыть, когда станет
        // можно; ждёт просьба ограниченное время и сама сгорает.
        var gui = InventoryGui.instance;
        if (gui != null) gui.Hide();
        ForgeplanPlugin.OpenRequest = 180;
    }
}

}
