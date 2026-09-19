using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Forgeplan {

/// <summary>
/// Самопроверка: собирает каталог из живой игры и выкладывает результат в лог.
///
/// Нужна потому, что проверить мод иначе нечем. Что плагин загрузился, видно
/// по одной строке в LogOutput.log — но загрузиться и работать это разные
/// вещи. Весь каталог строится лениво, при первом открытии окна, и в случае
/// успеха не пишет ни строки: молчание в логе одинаково выглядит и когда всё
/// хорошо, и когда окно ни разу не открывали.
///
/// Поэтому здесь печатается то, что можно сверить глазами с сайтом: таблица
/// превращений и несколько посчитанных заказов. Совпали числа — значит
/// ObjectDB прочитан верно, публицизация сработала, локализация на месте и
/// порт расчёта не разошёлся с оригиналом.
///
/// Выключена по умолчанию: обычному игроку эти полсотни строк при каждом
/// входе в мир не нужны. Включается в конфиге.
/// </summary>
public static class SelfTest {
    /// <summary>Уже прогоняли в этом мире.</summary>
    public static bool Done;

    /// <summary>Предметы, на которых проверяем расчёт. Взяты не случайно:
    /// топор — рецепт с уровнями качества, медовуха — бродильня с выходом 6,
    /// хлеб — печь с четырьмя слотами, жаркое — простая стойка. Если имя
    /// префаба в игре изменилось, замена подбирается сама.</summary>
    static readonly string[] Preferred = {
        "axebronze", "meadhealthminor", "bread", "cookedmeat",
    };

    public static void Run() {
        if (Done) return;
        Done = true;
        var log = ForgeplanPlugin.Log;
        try {
            Report(log);
        } catch (System.Exception e) {
            // Самопроверка, уронившая игру, хуже отсутствия самопроверки.
            log.LogError("самопроверка упала: " + e);
        }
    }

    static void Report(BepInEx.Logging.ManualLogSource log) {
        log.LogInfo("=== самопроверка ===");

        int withRecipe = GameData.Items.Values.Count(i => i.HasRecipe);
        int withIcon = GameData.Items.Values.Count(i => i.Icon != null);
        int withLevels = GameData.Items.Values.Count(i => i.Levels.Count > 1);
        log.LogInfo(string.Format(
            "каталог: {0} предметов, {1} с рецептом, {2} с иконкой, {3} с уровнями качества",
            GameData.Items.Count, withRecipe, withIcon, withLevels));
        log.LogInfo(string.Format("превращений: {0}, станков с названием: {1}",
            GameData.Convert.Count, GameData.StationNames.Count));
        // Постройки идут не из ObjectDB, а из префабов сцены, и уровней
        // качества у них нет — в сверку кривых стоимости они не попадают.
        log.LogInfo(string.Format("  из них построек: {0}", GameData.Pieces));

        // Дубли настоящих рецептов. Список должен быть коротким и осмысленным:
        // бронза (куют из меди с оловом, а не только плавят из лома) — да,
        // чёрный металл (рецепта нет вовсе) — нет.
        log.LogInfo(GameData.Shadowed.Count == 0
            ? "  дублей рецептов среди превращений нет"
            : "  снято как дубль рецепта: " + string.Join(", ", GameData.Shadowed.ToArray()));
        // Хлеб, медовуха, жаркое: рецепта в ObjectDB нет, и без этого шага
        // заказ прямо на них давал пустой список материалов.
        log.LogInfo(string.Format("  превращений переписано в рецепты: {0}", GameData.Mirrored));
        // Идолы Кузни потенциала. В стоимости крафта их быть не должно:
        // верстак их не просит, они для отдельной механики.
        log.LogInfo(string.Format("  отброшено идолов Кузни потенциала: {0}",
            GameData.SkippedUpgraders));
        var leaked = GameData.Items.Values
            .Where(i => i.HasRecipe && i.Levels.Any(s => s.Keys.Any(k => k.StartsWith("upgrader"))))
            .Select(i => i.Id).ToList();
        log.LogInfo(leaked.Count == 0
            ? "  идолов в стоимости не осталось — верно"
            : "  ОШИБКА: идол уцелел в стоимости у " + leaked.Count + ": "
              + string.Join(", ", leaked.Take(10).ToArray()));
        log.LogInfo(string.Format("  заготовок (ItemType.Material с рецептом): {0}",
            GameData.Items.Values.Count(i => i.IsMaterial && i.HasRecipe)));

        // Несколько рецептов на вещь. Берём мелкий: пятёрочный округляет заказ
        // вверх и заставляет купить лишнего.
        var many = GameData.RecipeAmounts.Where(kv => kv.Value.Count > 1)
            .OrderBy(kv => kv.Key).ToList();
        log.LogInfo(many.Count == 0
            ? "  вещей с несколькими рецептами нет"
            : string.Format("  вещей с несколькими рецептами: {0}", many.Count));
        foreach (var kv in many) {
            var it = GameData.Get(kv.Key);
            log.LogInfo(string.Format("    {0}: рецепты по {1} — взят по {2}",
                kv.Key, string.Join("/", kv.Value.Select(n => n.ToString()).ToArray()),
                it != null ? it.Out : 0));
        }

        // Расхождение №3 из сверки: станок это постройка, в каталоге предметов
        // его нет. Если здесь вместо «Каменная печь» стоит piece_oven —
        // словарь StationNames не наполнился.
        foreach (var kv in GameData.StationNames.OrderBy(k => k.Key))
            log.LogInfo("  станок  " + kv.Key + " = " + kv.Value);

        // Расхождение №4: печь должна быть вынута из общего индекса, иначе
        // уголь посчитается дважды.
        var kiln = GameData.KilnRule;
        log.LogInfo(kiln == null
            ? "  ВНИМАНИЕ: углевыжигательная печь не найдена, работает запасное правило"
            : string.Format("  печь вынута: {0}, {1}, {2} с, вместимость {3}",
                kiln.Station, Mats(kiln.Mats), kiln.Sec, kiln.Cap));
        log.LogInfo("  уголь остался в общем индексе: " +
            (GameData.Convert.ContainsKey("coal") ? "ДА — это ошибка" : "нет, верно"));

        // Расхождение №1: Localization живёт в assembly_guiutils. Если ссылка
        // отвалится, здесь будут сырые токены вида $item_coal.
        log.LogInfo("  локализация: " + string.Join(", ",
            new[] { "wood", "coal", "stone", "bronze" }
                .Select(id => id + " -> " + GameData.NameOf(id)).ToArray()));

        log.LogInfo("--- превращения ---");
        foreach (var kv in GameData.Convert.OrderBy(k => k.Value.Station).ThenBy(k => k.Key)) {
            var r = kv.Value;
            log.LogInfo(string.Format("  {0,-22} <- {1,-24} {2}  выход {3}, {4} с, влезает {5}, слотов {6}",
                kv.Key, r.Station, Mats(r.Mats), r.Out, r.Sec, r.Cap, r.Slots));
        }

        // Кривая доплаты за уровни. Сайт берёт её из дампа JotunnDoc, где
        // множители идут 1, 2, 3, а игра на бронзовом топоре отдала 1, 2, 4.
        // Одного рецепта мало, чтобы объявить это правилом, поэтому берём
        // несколько штук вразнобой по каталогу.
        log.LogInfo("--- сырая кривая стоимости ---");
        RawCurve(log, "axebronze");
        var spread = GameData.Items.Values
            .Where(i => i.HasRecipe && !i.FromConversion && i.MaxQuality >= 4 && i.Id != "axebronze")
            .OrderBy(i => i.Id).ToList();
        for (int k = 0; k < spread.Count && k < 5 * 37; k += 37) RawCurve(log, spread[k].Id);

        log.LogInfo("--- расчёт ---");
        foreach (var id in Pick()) Plan(log, id);

        log.LogInfo("=== самопроверка пройдена ===");
    }

    /// <summary>Что считаем. Сначала список выше, потом — добор по месту,
    /// чтобы проверка пережила переименование префабов.</summary>
    static List<string> Pick() {
        var picked = new List<string>();
        foreach (var id in Preferred)
            if (GameData.Get(id) != null || GameData.Convert.ContainsKey(id))
                picked.Add(id);

        if (!picked.Any(id => Station(id).Contains("fermenter"))) {
            var id = GameData.Convert.FirstOrDefault(
                kv => kv.Value.Station.Contains("fermenter")).Key;
            if (id != null) picked.Add(id);
        }
        if (!picked.Any(id => Station(id).Contains("cook") || Station(id).Contains("oven"))) {
            var id = GameData.Convert.FirstOrDefault(
                kv => kv.Value.Station.Contains("cook") || kv.Value.Station.Contains("oven")).Key;
            if (id != null) picked.Add(id);
        }
        // Вещь, которую куют из переплавленного металла: проверяет, что цепочка
        // рецепт -> превращение -> руда раскрывается до конца.
        if (!picked.Any(id => { var it = GameData.Get(id);
                return it != null && it.HasRecipe &&
                       it.Levels[0].Keys.Any(GameData.Convert.ContainsKey); })) {
            var it = GameData.Items.Values.FirstOrDefault(
                x => x.HasRecipe && x.Levels[0].Keys.Any(GameData.Convert.ContainsKey));
            if (it != null) picked.Add(it.Id);
        }
        return picked;
    }

    static string Station(string id) {
        Rule r;
        return GameData.Convert.TryGetValue(id, out r) ? r.Station : "";
    }

    /// <summary>Посчитать один заказ и выложить результат так, чтобы его можно
    /// было сверить с сайтом по ссылке #p=1&amp;i=&lt;id&gt;.</summary>
    static void Plan(BepInEx.Logging.ManualLogSource log, string id) {
        var saved = Planner.Cart;
        var savedCoal = Planner.BurnCoalFromWood;
        try {
            var it = GameData.Get(id);
            int quality = it != null && it.MaxQuality > 1 ? 2 : 1;

            Planner.Cart = new List<Entry> {
                new Entry { Id = id, Qty = 3, Quality = quality }
            };
            Planner.BurnCoalFromWood = true;   // чтобы печь тоже попала в отчёт

            log.LogInfo(string.Format("  заказ: {0} x3, качество {1}  ({2})",
                id, quality, GameData.NameOf(id)));
            log.LogInfo("    как в рецептах: " + Mats(Planner.Direct()));
            log.LogInfo("    сырьё:          " + Mats(Planner.Raw()));

            var refine = Planner.Refine();
            log.LogInfo("    получить:       " + Mats(refine));
            foreach (var row in Planner.Process(refine)) {
                log.LogInfo(string.Format("    станок: {0,-24} {1} шт, {2}{3}",
                    GameData.StationName(row.Station), row.Units,
                    Planner.Time(row.Seconds),
                    row.Loads > 0 ? ", загрузок " + row.Loads : ""));
            }
        } finally {
            Planner.Cart = saved;
            Planner.BurnCoalFromWood = savedCoal;
        }
    }

    /// <summary>
    /// Стоимость по уровням прямо из ObjectDB, без пересчётов мода.
    ///
    /// Нужна, чтобы закрыть спор о жетонах улучшения: сайт считает, что жетон
    /// нужен только на сам крафт, мод намерил, что и на каждое улучшение тоже.
    /// Разница касается 224 предметов сайта, и решать её выводом одной из
    /// сторон нельзя — ниже ответ самой игры.
    /// </summary>
    static void RawCurve(BepInEx.Logging.ManualLogSource log, string id) {
        foreach (var recipe in ObjectDB.instance.m_recipes) {
            if (recipe == null || recipe.m_item == null) continue;
            if (GameData.Slug(recipe.m_item.gameObject.name) != id) continue;
            log.LogInfo(string.Format("  {0}, рецепт на {1} шт, станок {2} ур. {3}:",
                id, System.Math.Max(1, recipe.m_amount),
                recipe.m_craftingStation != null
                    ? GameData.Slug(recipe.m_craftingStation.gameObject.name) : "руками",
                System.Math.Max(1, recipe.m_minStationLevel)));
            foreach (var req in recipe.m_resources) {
                if (req == null || req.m_resItem == null) continue;
                var steps = new List<string>();
                for (int q = 1; q <= 4; q++) steps.Add("ур." + q + "=" + req.GetAmount(q));
                // m_upgraderResource — флаг игры «это идол улучшения, а не
                // обычный ресурс». Ни сайт, ни мод его не учитывают: в дампе
                // JotunnDoc поля нет вовсе, и идол попадает в список наравне
                // с деревом. Печатаем, чтобы стало видно, кого он метит.
                log.LogInfo("    " + GameData.Slug(req.m_resItem.gameObject.name)
                    + ": " + string.Join(", ", steps.ToArray())
                    + (req.m_upgraderResource ? "   [ИДОЛ УЛУЧШЕНИЯ]" : "")
                    + (req.m_recover ? "" : "   [не возвращается]"));
            }
        }
    }

    static string Mats(Dictionary<string, int> t) {
        if (t == null || t.Count == 0) return "(пусто)";
        var sb = new StringBuilder();
        foreach (var kv in t.OrderByDescending(k => k.Value).ThenBy(k => k.Key)) {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(kv.Key).Append(" x").Append(kv.Value);
        }
        return sb.ToString();
    }
}

}
