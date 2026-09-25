using System.Collections.Generic;

namespace Forgeplan {

/// <summary>
/// Закреплённый список — то, что остаётся на экране, когда окно закрыто.
///
/// Планировщик отвечает на вопрос «что мне понадобится», но ответ живёт ровно
/// столько, сколько открыто окно: дальше игрок идёт копать и держит числа в
/// голове. Закреплённый список снимает это с головы — висит сбоку и убывает
/// сам, пока руда падает в рюкзак.
///
/// Почему это отдельный список, а не сам план. План в окне переписывают:
/// прикинул броню, стёр, прикинул мебель. Если бы подсказка на экране была
/// зеркалом плана, она стирались бы вместе с ним — а закрепляют как раз затем,
/// чтобы не потерять. Поэтому «Закрепить» снимает копию, и дальше два списка
/// не связаны.
///
/// Между запусками список не хранится. План в моде тоже не хранится, и
/// заводить хранилище ради одной подсказки — значит решать, чьё оно: мира,
/// персонажа или профиля. Вопрос не праздный (миров у человека несколько), и
/// ответ на него стоит дороже самой возможности.
/// </summary>
public static class Pin {
    /// <summary>Что закреплено. Копии, а не ссылки на позиции плана.</summary>
    public static readonly List<Entry> Items = new List<Entry>();

    /// <summary>Считать ли до сырья. Зеркалит переключатель в окне: две
    /// разные картинки одного заказа сбивали бы с толку сильнее, чем
    /// отсутствие настройки.</summary>
    public static bool RawView = true;

    /// <summary>Список поменялся — подсказке на экране пора пересобраться.</summary>
    public static System.Action Changed;

    static void Fire() { if (Changed != null) Changed(); }

    public static bool Any { get { return Items.Count > 0; } }

    public static bool Has(string id) {
        return Find(id) != null;
    }

    static Entry Find(string id) {
        foreach (var e in Items) if (e.Id == id) return e;
        return null;
    }

    /// <summary>Закрепить одну позицию из каталога или снять её. Качество
    /// берётся то же, что стоит у этой вещи в плане, иначе первое: закрепляют
    /// обычно то, что только что прикидывали.</summary>
    public static void Toggle(string id) {
        var had = Find(id);
        if (had != null) {
            Items.Remove(had);
            Fire();
            return;
        }
        int quality = 1;
        foreach (var e in Planner.Cart)
            if (e.Id == id) { quality = e.Quality; break; }
        Items.Add(new Entry { Id = id, Qty = 1, Quality = quality });
        Fire();
    }

    /// <summary>Закрепить весь план целиком, заменив прежнее закреплённое.</summary>
    public static void Set(List<Entry> entries) {
        Items.Clear();
        foreach (var e in entries)
            Items.Add(new Entry { Id = e.Id, Qty = e.Qty, Quality = e.Quality });
        Fire();
    }

    public static void Clear() {
        if (Items.Count == 0) return;
        Items.Clear();
        Fire();
    }

    /// <summary>Совпадает ли закреплённое с планом позиция в позицию. Нужно
    /// кнопке в окне: она должна читаться как «закреплено», а не «нажми
    /// ещё раз».</summary>
    public static bool SameAs(List<Entry> cart) {
        if (cart.Count != Items.Count || cart.Count == 0) return false;
        foreach (var e in cart) {
            var mine = Find(e.Id);
            if (mine == null || mine.Qty != e.Qty || mine.Quality != e.Quality) return false;
        }
        return true;
    }

    /// <summary>Строка списка: сколько при себе и сколько надо всего.</summary>
    public struct MatRow {
        public string Id;
        public int Have, Need;
        public bool Done { get { return Have >= Need; } }
    }

    /// <summary>
    /// Что нужно и сколько из этого уже при себе.
    ///
    /// Считается только рюкзак — сундуки сюда не входят намеренно, см.
    /// Stock.Count(bool). Показывать «есть» то, что лежит в базе, значит
    /// отвечать не на тот вопрос: список висит на экране, пока игрок в поле.
    ///
    /// Нужное не урезается до остатка: видно и то, сколько собрано, и сколько
    /// всего. «1 / 10» отвечает сразу на оба вопроса, а «ещё 9» — только на
    /// один, и по нему не понять, далеко ли до конца.
    /// </summary>
    public static List<MatRow> Rows() {
        var rows = new List<MatRow>();
        if (Items.Count == 0) return rows;

        var need = RawView ? Planner.Raw(Items) : Planner.Direct(Items);
        var have = Stock.Count(false);
        foreach (var kv in need) {
            if (kv.Value <= 0) continue;
            int got;
            rows.Add(new MatRow {
                Id = kv.Key,
                Need = kv.Value,
                // Больше нужного показывать незачем: «14 / 10» читается как
                // ошибка счёта, а не как запас.
                Have = System.Math.Min(have.TryGetValue(kv.Key, out got) ? got : 0,
                                       kv.Value),
            });
        }
        return rows;
    }
}

}
