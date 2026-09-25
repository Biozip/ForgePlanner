using System.Collections.Generic;
using System.Linq;

namespace Forgeplan {

/// <summary>Позиция списка крафта.</summary>
public class Entry {
    public string Id;
    public int Qty = 1;
    public int Quality = 1;
}

/// <summary>Строка блока «Переработка».</summary>
public class ProcessRow {
    public string Station;
    public int Units;
    public int Seconds;
    public int Loads;        // полных загрузок станка, 0 — не ограничено
}

/// <summary>
/// Расчёт. Это прямой порт ядра из app.js — directTotals / rawTotals /
/// refineTotals / processPlan. Логика не переосмыслена намеренно: она уже
/// проверена tools/test_calc.js, и любое расхождение с сайтом будет багом
/// здесь, а не там.
/// </summary>
public static class Planner {
    public const int MaxDepth = 16;

    public static List<Entry> Cart = new List<Entry>();
    /// <summary>Считать ли уголь на переплавку (галочка на сайте).</summary>
    public static bool CountCoal = true;
    /// <summary>Разложить ли уголь до древесины.</summary>
    public static bool BurnCoalFromWood;
    /// <summary>Множитель на группу.</summary>
    public static int Players = 1;

    static void Add(Dictionary<string, int> t, string m, int n) {
        int had;
        t[m] = (t.TryGetValue(m, out had) ? had : 0) + n;
    }

    static int Ceil(int n, int per) => per <= 0 ? n : (n + per - 1) / per;

    static int QtyOf(Entry e) => e.Qty * (Players < 1 ? 1 : Players);

    /// <summary>Сумма доплат с первого уровня по нужный — как levelCost().</summary>
    static Dictionary<string, int> LevelCost(ItemDef it, int quality) {
        var t = new Dictionary<string, int>();
        int n = System.Math.Max(1, System.Math.Min(quality, it.Levels.Count));
        for (int k = 0; k < n; k++)
            foreach (var kv in it.Levels[k]) Add(t, kv.Key, kv.Value);
        return t;
    }

    /// <summary>Во что раскрывается материал. Возвращает null, если дальше
    /// раскладывать нечего — значит это сырьё.</summary>
    static Rule Expandable(string m) {
        if (m == "coal") {
            if (!BurnCoalFromWood) return null;
            return Kiln;
        }
        Rule conv;
        // out здесь обязателен: бродильня отдаёт 6 бутылок с одной основы,
        // и без деления на выход список потребует шесть основ вместо одной
        if (GameData.Convert.TryGetValue(m, out conv)) return conv;

        var it = GameData.Get(m);
        // Заготовка (слиток, гвозди, сырое тесто, основа медовухи) раскрывается
        // наравне с материалом: рецепт у неё свой, но это ещё не конечная вещь.
        // На сайте здесь стоит проверка cat === 'material' || 'prep'.
        if (it != null && it.HasRecipe && it.IsMaterial)
            return new Rule { Mats = it.Levels[0], Out = it.Out };
        return null;
    }

    /// <summary>Пережиг древесины в уголь. Числа приходят из игры — их
    /// вынимает GameData.LiftKiln, чтобы уголь не посчитался дважды.
    /// Значения ниже — запасные, на случай если печь в индекс не попала.</summary>
    static readonly Rule KilnFallback = new Rule {
        Station = "charcoal_kiln", Out = 1, Sec = 15, Cap = 25,
        Mats = new Dictionary<string, int> { { "wood", 1 } },
    };
    public static Rule Kiln => GameData.KilnRule ?? KilnFallback;

    /// <summary>«Как в рецептах»: что нужно на руки, без разложения.</summary>
    public static Dictionary<string, int> Direct() { return Direct(Cart); }

    /// <summary>
    /// То же самое, но для произвольного набора позиций.
    ///
    /// Раньше расчёт смотрел прямо в Cart, и другого списка в моде не
    /// существовало. Закреплённый список — второй: он живёт своей жизнью,
    /// пока план в окне переписывают заново. Считать их одним кодом
    /// обязательно, иначе появится второй источник правды, и расхождение
    /// между окном и подсказкой на экране будет некому заметить.
    /// </summary>
    public static Dictionary<string, int> Direct(List<Entry> cart) {
        var t = new Dictionary<string, int>();
        foreach (var e in cart) {
            var it = GameData.Get(e.Id);
            if (it == null || !it.HasRecipe) continue;
            int crafts = Ceil(QtyOf(e), it.Out);
            foreach (var kv in LevelCost(it, e.Quality)) Add(t, kv.Key, kv.Value * crafts);
        }
        return t;
    }

    /// <summary>«Сырьё и добыча»: до того, что копают и рубят.</summary>
    public static Dictionary<string, int> Raw() { return Raw(Cart); }

    public static Dictionary<string, int> Raw(List<Entry> cart) {
        var t = new Dictionary<string, int>();
        System.Action<string, int, int> add = null;
        add = (m, n, depth) => {
            var rule = depth > MaxDepth ? null : Expandable(m);
            if (rule == null) { Add(t, m, n); return; }
            int crafts = Ceil(n, rule.Out);
            foreach (var kv in rule.Mats) {
                if (kv.Key == "coal" && !CountCoal) continue;
                add(kv.Key, kv.Value * crafts, depth + 1);
            }
        };
        foreach (var kv in Direct(cart)) add(kv.Key, kv.Value, 0);
        return t;
    }

    /// <summary>Что предстоит получить на станках по дороге.</summary>
    public static Dictionary<string, int> Refine() { return Refine(Cart); }

    public static Dictionary<string, int> Refine(List<Entry> cart) {
        var t = new Dictionary<string, int>();
        System.Action<string, int, int> add = null;
        add = (m, n, depth) => {
            var it = GameData.Get(m);
            // Сырьё сюда попадать не должно: на сайте руда лежит отдельным
            // словарём mats, и ветка material|prep для неё не срабатывает.
            // Здесь ту же границу держит HasRecipe — у руды рецепта нет.
            if (GameData.Convert.ContainsKey(m) || (it != null && it.HasRecipe && it.IsMaterial))
                Add(t, m, n);
            var rule = depth > MaxDepth ? null : Expandable(m);
            if (rule == null) return;
            int crafts = Ceil(n, rule.Out);
            foreach (var kv in rule.Mats) {
                if (kv.Key == "coal") continue;
                add(kv.Key, kv.Value * crafts, depth + 1);
            }
        };
        foreach (var kv in Direct(cart)) add(kv.Key, kv.Value, 0);

        // Позиция, которую делает сам станок (медовуха, выпечка), в Direct()
        // уже разложена до полуфабриката. Но станок и его время относятся к ней
        // самой, поэтому её считаем отдельно — иначе заказ на семь медовух не
        // покажет ни бродильни, ни сорока минут брожения.
        foreach (var e in cart)
            if (GameData.Convert.ContainsKey(e.Id)) Add(t, e.Id, QtyOf(e));
        return t;
    }

    /// <summary>Что и на каких станках предстоит переработать.</summary>
    public static List<ProcessRow> Process(Dictionary<string, int> refine) {
        var byStation = new Dictionary<string, ProcessRow>();
        int coal = 0;

        foreach (var kv in refine) {
            Rule rule;
            if (!GameData.Convert.TryGetValue(kv.Key, out rule)) continue;
            int fuel;
            if (rule.Mats.TryGetValue("coal", out fuel)) coal += fuel * kv.Value;

            // Время считаем прогонами, а не штуками: за один прогон бродильня
            // выдаёт сразу шесть бутылок, а печь жарит во всех четырёх слотах
            // разом — умножать время на штуки нельзя ни там, ни там.
            int perRun = System.Math.Max(1, rule.Out) * System.Math.Max(1, rule.Slots);
            int batches = Ceil(kv.Value, perRun);

            ProcessRow row;
            if (!byStation.TryGetValue(rule.Station, out row)) {
                row = new ProcessRow { Station = rule.Station, Loads = rule.Cap };
                byStation[rule.Station] = row;
            }
            row.Units += kv.Value;
            row.Seconds += batches * rule.Sec;
        }

        var rows = byStation.Values.ToList();
        foreach (var r in rows) r.Loads = r.Loads > 0 ? Ceil(r.Units, r.Loads) : 0;

        if (BurnCoalFromWood && CountCoal && coal > 0) {
            rows.Add(new ProcessRow {
                Station = Kiln.Station, Units = coal,
                Seconds = coal * Kiln.Sec, Loads = Ceil(coal, Kiln.Cap),
            });
        }
        rows.Sort((a, b) => b.Seconds.CompareTo(a.Seconds));
        return rows;
    }

    /// <summary>4590 -> «1 ч 16 мин».</summary>
    public static string Time(int sec) {
        int h = sec / 3600, m = (sec % 3600 + 30) / 60;
        if (h > 0) return m > 0 ? h + Ui.L.Hours + " " + m + Ui.L.Minutes : h + Ui.L.Hours;
        return m > 0 ? m + Ui.L.Minutes : sec + Ui.L.Seconds;
    }
}

}
