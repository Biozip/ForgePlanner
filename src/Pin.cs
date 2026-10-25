using System.Collections.Generic;
using UnityEngine;

namespace Forgeplan {

/// <summary>
/// Закреплённое: то, что остаётся на экране, когда окно закрыто.
///
/// Закрепляется не копия, а сам план: подсказка на экране показывает ровно
/// то, что сейчас в окне, и меняется вместе с ним. Две части закрепляются
/// отдельно: «План» (что делаем) и «Материалы» (из чего). Можно одну, можно
/// обе.
///
/// «Материалы» закрепляются вместе со снимком хранилища: что лежит в
/// сундуках рядом в момент закрепления, все предметы, плюс когда и где снимок
/// снят. Рюкзак считается живым. В поле сундуков нет, а снимок их помнит;
/// подобрал, выронил, скрафтил: рюкзак показывает как есть. План можно
/// править и в поле: в снимке всё содержимое сундуков, а не только то, что
/// было в плане. Вернулся домой и разложил по сундукам: обновил снимок.
///
/// Что закреплено, хранится в файле плана, см. PlanStore.
/// </summary>
public static class Pin {
    /// <summary>Закреплён список позиций плана.</summary>
    public static bool Plan;
    /// <summary>Закреплены «Материалы».</summary>
    public static bool Gather;

    /// <summary>Считать ли до сырья. Зеркалит переключатель в окне и
    /// хранится в файле плана, чтобы после перезахода вид был тот же.</summary>
    public static bool RawView = true;

    /* Снимок хранилища. */
    /// <summary>Сколько каждого предмета лежало в сундуках рядом.</summary>
    public static readonly Dictionary<string, int> Chests = new Dictionary<string, int>();
    /// <summary>Сколько сундуков попало в счёт. Ноль: сняли вдали от сундуков.</summary>
    public static int Boxes;
    /// <summary>Когда снят, местное время.</summary>
    public static System.DateTime At;
    /// <summary>Где стоял игрок.</summary>
    public static Vector3 Where;
    /// <summary>Снимок есть.</summary>
    public static bool HasSnapshot;

    /// <summary>Закреплённое поменялось: подсказке на экране пора пересобраться.</summary>
    public static System.Action Changed;

    static void Fire() { if (Changed != null) Changed(); }

    public static bool Any { get { return Plan || Gather; } }

    static readonly List<Entry> None = new List<Entry>();

    /// <summary>Позиции, по которым считается закреплённое: сам план, пока
    /// что-нибудь закреплено.</summary>
    public static List<Entry> Items { get { return Any ? Planner.Cart : None; } }

    public static void TogglePlan() { Plan = !Plan; Fire(); }
    public static void ToggleGather() {
        Gather = !Gather;
        if (Gather) Take(); else Forget();
        Fire();
    }

    /// <summary>Обновить снимок хранилища. Кнопка в окне и на панели.</summary>
    public static void Snapshot() { Take(); Fire(); }

    static void Forget() {
        Chests.Clear();
        Boxes = 0;
        HasSnapshot = false;
    }

    static void Take() {
        Chests.Clear();
        int boxes;
        foreach (var kv in Stock.ChestsOnly(out boxes)) Chests[kv.Key] = kv.Value;
        Boxes = boxes;
        At = System.DateTime.Now;
        var me = Player.m_localPlayer;
        Where = me != null ? me.transform.position : Vector3.zero;
        HasSnapshot = true;
    }

    /// <summary>Сколько метров от места снимка до игрока. -1: игрока нет.</summary>
    public static int DistanceFromSnapshot() {
        var me = Player.m_localPlayer;
        if (me == null) return -1;
        return Mathf.RoundToInt(Vector3.Distance(me.transform.position, Where));
    }

    /// <summary>Материалы закреплены, а снимка нет (файл из старой версии).
    /// Сам мод снимок не снимает: в поле это обнулило бы сундуки, поэтому
    /// только просит.</summary>
    public static bool Stale { get { return Gather && !HasSnapshot; } }

    public static void Set(bool plan, bool gather) {
        if (Plan == plan && Gather == gather) return;
        Plan = plan;
        Gather = gather;
        if (!gather) Forget();
        Fire();
    }

    public static void Clear() { Set(false, false); }

    /// <summary>
    /// Строка «Материалы»: сколько нужно всего, сколько из этого в сундуках
    /// по снимку, сколько в рюкзаке сейчас и сколько ещё добыть. Части не
    /// больше целого.
    /// </summary>
    public struct MatRow {
        public string Id;
        public int Total, Chests, Bag;
        public int Left { get { return Total - Chests - Bag; } }
        public bool Done { get { return Left <= 0; } }
    }

    /// <summary>
    /// Что показывает панель: только то, за чем идти, то есть чего не
    /// хватило в сундуках при снимке. Добытое гаснет, а не пропадает.
    /// </summary>
    public static List<MatRow> Rows() {
        var rows = new List<MatRow>();
        if (!Gather || !HasSnapshot) return rows;
        var need = RawView ? Planner.Raw(Planner.Cart) : Planner.Direct(Planner.Cart);
        var bag = Stock.Count(false);
        foreach (var kv in need) {
            if (kv.Value <= 0) continue;
            int c, b;
            Chests.TryGetValue(kv.Key, out c);
            bag.TryGetValue(kv.Key, out b);
            c = System.Math.Min(c, kv.Value);
            if (kv.Value - c <= 0) continue;
            b = System.Math.Min(b, kv.Value - c);
            rows.Add(new MatRow { Id = kv.Key, Total = kv.Value, Chests = c, Bag = b });
        }
        return rows;
    }
}

}
