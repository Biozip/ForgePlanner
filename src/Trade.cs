using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Forgeplan {

/// <summary>
/// Что где продаётся.
///
/// Часть сырья в игре не выпадает нигде: за ним идут к торговцу с монетами.
/// Без подписи строка «Громовой камень — 1» выглядит как задача, которую забыли
/// объяснить: обыскивать мир бессмысленно, его там нет.
///
/// Прайс-лист читается из живой игры, а не из таблицы в исходниках, — по той же
/// причине, по которой так читаются рецепты: дополнение, добавившее торговцу
/// товар, окажется в списке само.
///
/// У каждой строки прайс-листа есть m_requiredGlobalKey: пусто — товар лежит с
/// самого начала, заполнено — появится, когда ключ выставлен. Убийство босса
/// выставляет defeated_&lt;босс&gt;, отсюда и расширение ассортимента по ходу игры.
/// Но ключи бывают и не от боссов: Hildir1..3 — её собственные задания. Поэтому
/// мод не пытается перевести ключ в имя босса, а лишь отмечает, что товар
/// появится не сразу.
/// </summary>
public static class Trade {
    public struct Offer {
        /// <summary>Имя торговца, уже на языке игрока.</summary>
        public string Trader;
        public int Price;
        /// <summary>Условие появления на прилавке. Пусто — в продаже сразу.</summary>
        public string Key;
    }

    static Dictionary<string, Offer> _map;

    /// <summary>Забыть прочитанное: каталог живёт ровно один мир.</summary>
    public static void Reset() { _map = null; }

    public static bool TryGet(string id, out Offer offer) {
        Build();
        return _map.TryGetValue(id, out offer);
    }

    static void Build() {
        if (_map != null) return;
        _map = new Dictionary<string, Offer>();
        var scene = ZNetScene.instance;
        if (scene == null || scene.m_prefabs == null) return;

        int traders = 0;
        foreach (var go in scene.m_prefabs) {
            if (go == null) continue;
            var trader = go.GetComponent<Trader>();
            if (trader == null || trader.m_items == null) continue;
            traders++;
            var who = Localization.instance.Localize(trader.m_name);
            foreach (var it in trader.m_items) {
                if (it == null || it.m_prefab == null) continue;
                var id = GameData.Slug(it.m_prefab.gameObject.name);
                var offer = new Offer {
                    Trader = who, Price = it.m_price,
                    Key = it.m_requiredGlobalKey ?? "",
                };
                Offer had;
                // Один товар у двух торговцев — показываем, где дешевле:
                // «до кого ближе» мод знать не может, а цену видит.
                if (_map.TryGetValue(id, out had) && had.Price <= offer.Price) continue;
                _map[id] = offer;
            }
        }
        ForgeplanPlugin.Log.LogInfo(string.Format(
            "прайс-листы прочитаны: торговцев {0}, товаров {1}", traders, _map.Count));
    }
}

}
