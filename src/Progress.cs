using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Forgeplan {

/// <summary>
/// Прогрессия по боссам — чтобы каталог не показывал того, до чего игрок ещё
/// не дошёл.
///
/// Valheim устроен так, что почти всё интересное лежит за боссом: пока не
/// повержен Древний, в Болото идти не с чем, а список из тысячи позиций,
/// открытый на первом часу игры, пересказывает всю игру вперёд. Планировщик
/// для того и нужен, чтобы посмотреть, что ковать дальше, — но «дальше» должно
/// означать следующий шаг, а не финал.
///
/// Луга, Чёрный лес и Океан открыты всегда: там игрок оказывается сам, без
/// чьего-либо разрешения.
///
/// Ключи побед не зашиты в мод. Их пишут сами префабы боссов
/// (<c>Character.m_defeatSetGlobalKey</c>), оттуда они и читаются при входе в
/// мир. Зашито только одно — какая победа какой биом открывает; это знание об
/// игре, а не о её файлах, и добыть его из данных нельзя. Босс, чей ключ мод
/// не узнал, попадает в лог: молча ошибиться тут хуже, чем сказать вслух.
/// </summary>
public static class Progress {
    /// <summary>Скрывать ли неоткрытое. Ставит плагин из конфига.</summary>
    public static bool Enabled = true;

    /// <summary>Биом -> ключ, который обязан стоять в мире. Пусто — открыт
    /// всегда.</summary>
    static readonly string[] Gate = new string[Tiers.Ids.Length];

    /// <summary>Открыт ли биом на самом деле, без оглядки на Enabled.
    /// Пересчитывается в Refresh: GetGlobalKey зовётся тысячу раз за поиск,
    /// а меняется от силы раз за вечер.</summary>
    static readonly bool[] Open = new bool[Tiers.Ids.Length];

    static bool _scanned;

    // До первой разведки всё открыто. Массивы Unity инициализирует нулями, и
    // без этого окно на первом кадре показало бы пустой каталог: замок «null»
    // не пустая строка, а bool по умолчанию — false.
    static Progress() { Reset(); }

    /// <summary>Сколько биомов открыто. Для окна настроек.</summary>
    public static int OpenCount;

    /// <summary>
    /// Кусок ключа победы -> биом, который эта победа открывает.
    ///
    /// Сверено по самой игре: коды боссов видны в её же локализации
    /// (<c>enemy_gdking</c> — Древний, <c>enemy_frozenking</c> — Калл
    /// Фимбульвест). Босс Дальнего севера ничего дальше не открывает — он
    /// последний, поэтому его тут и нет.
    /// </summary>
    static readonly string[][] Opens = {
        new[] { "gdking",     "swamp" },       // Древний
        new[] { "bonemass",   "mountain" },    // Масса костей
        new[] { "dragon",     "plains" },      // Матерь
        new[] { "goblinking", "mistlands" },   // Яглут
        new[] { "queen",      "ashlands" },    // Королева
        new[] { "fader",      "deepnorth" },   // Прародитель
    };

    public static void Reset() {
        _scanned = false;
        for (int i = 0; i < Gate.Length; i++) { Gate[i] = ""; Open[i] = true; }
        OpenCount = Gate.Length;
    }

    static int TierOf(string id) {
        for (int i = 0; i < Tiers.Ids.Length; i++) if (Tiers.Ids[i] == id) return i;
        return -1;
    }

    static void Scan() {
        if (_scanned || ZNetScene.instance == null) return;
        _scanned = true;
        for (int i = 0; i < Gate.Length; i++) Gate[i] = "";

        var known = new List<string>();
        var unknown = new List<string>();
        foreach (var prefab in ZNetScene.instance.m_prefabs) {
            if (prefab == null) continue;
            var ch = prefab.GetComponent<Character>();
            if (ch == null || !ch.m_boss) continue;
            var key = ch.m_defeatSetGlobalKey;
            if (string.IsNullOrEmpty(key)) continue;

            var low = key.ToLowerInvariant();
            bool matched = false;
            foreach (var pair in Opens) {
                if (!low.Contains(pair[0])) continue;
                int t = TierOf(pair[1]);
                if (t >= 0) { Gate[t] = key; known.Add(key + " -> " + pair[1]); }
                matched = true;
                break;
            }
            // Эйктюр и босс Дальнего севера сюда попадут законно: первый ничего
            // не запирает, второй — последний. Всё прочее стоит прочитать.
            if (!matched) unknown.Add(key);
        }

        var msg = new StringBuilder("прогрессия: ");
        msg.Append(known.Count).Append(" ключей узнано");
        if (known.Count > 0) msg.Append(" (").Append(string.Join(", ", known.ToArray())).Append(")");
        if (unknown.Count > 0)
            msg.Append("; не узнано: ").Append(string.Join(", ", unknown.ToArray()));
        ForgeplanPlugin.Log.LogInfo(msg.ToString());

        int missing = 0;
        for (int i = 0; i < Gate.Length; i++) if (string.IsNullOrEmpty(Gate[i])) missing++;
        if (missing > 3)
            ForgeplanPlugin.Log.LogWarning(
                "прогрессия: без замка осталось " + missing + " биомов из " + Gate.Length
                + " — ожидалось 3 (Луга, Чёрный лес, Океан). Фильтр покажет лишнее.");
    }

    /// <summary>Перечитать состояние мира. Зовётся при открытии окна и перед
    /// каждым поиском — дешевле, чем спрашивать по разу на предмет.</summary>
    public static void Refresh() {
        Scan();
        OpenCount = 0;
        var zone = ZoneSystem.instance;
        for (int i = 0; i < Open.Length; i++) {
            // Замка нет — биом открыт. Мир не загружен — тоже: прятать каталог
            // из-за того, что мод спросил слишком рано, незачем.
            Open[i] = string.IsNullOrEmpty(Gate[i]) || zone == null
                      || zone.GetGlobalKey(Gate[i]);
            if (Open[i]) OpenCount++;
        }
    }

    /// <summary>Показывать ли этот биом и его предметы.</summary>
    public static bool Unlocked(int tier) {
        if (!Enabled) return true;
        if (tier < 0 || tier >= Open.Length) return true;
        return Open[tier];
    }
}

}
