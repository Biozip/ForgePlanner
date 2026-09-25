using System.Collections.Generic;
using UnityEngine;

namespace Forgeplan {

/// <summary>
/// Что у игрока уже есть.
///
/// Это то, чего сайт не может в принципе: там «учитывать, что уже есть» —
/// ручной ввод в каждое поле. Здесь достаточно посмотреть в рюкзак и в
/// сундуки вокруг.
/// </summary>
public static class Stock {
    /// <summary>Радиус, в котором считаем сундуки своими.</summary>
    public static float ChestRadius = 20f;
    public static bool IncludeChests = true;

    public static Dictionary<string, int> Count() { return Count(IncludeChests); }

    /// <summary>
    /// То же самое, но с явным ответом про сундуки.
    ///
    /// Закреплённому списку они не нужны никогда, и это не настройка, а суть:
    /// он висит на экране, пока игрок в поле, и отвечает на вопрос «хватит ли
    /// того, что при мне». Сундуки в базе на этот вопрос не отвечают — «всё
    /// собрано» при пустом рюкзаке отправит домой за тем, чего ты туда и
    /// пришёл добывать.
    /// </summary>
    public static Dictionary<string, int> Count(bool includeChests) {
        var have = new Dictionary<string, int>();
        var player = Player.m_localPlayer;
        if (player == null) return have;

        Take(have, player.GetInventory());
        if (!includeChests) return have;

        // Сундуки рядом. Container.m_nview.IsValid() отсекает выгруженные, а
        // CheckAccess — чужие с замком: считать их своими нечестно.
        // FindObjectsByType, а не FindObjectsOfType: второй устарел и вдобавок
        // сортирует результат по InstanceID, что нам не нужно совсем.
        foreach (var container in Object.FindObjectsByType<Container>(FindObjectsSortMode.None)) {
            if (container == null || container.GetInventory() == null) continue;
            if (Vector3.Distance(container.transform.position, player.transform.position) > ChestRadius)
                continue;
            if (container.m_nview == null || !container.m_nview.IsValid()) continue;
            if (!container.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID())) continue;
            Take(have, container.GetInventory());
        }
        return have;
    }

    static void Take(Dictionary<string, int> have, Inventory inv) {
        if (inv == null) return;
        foreach (var item in inv.GetAllItems()) {
            if (item == null || item.m_dropPrefab == null) continue;
            var id = GameData.Slug(item.m_dropPrefab.name);
            int had;
            have[id] = (have.TryGetValue(id, out had) ? had : 0) + item.m_stack;
        }
    }

    /// <summary>Вычесть имеющееся из нужного; отрицательных остатков нет.</summary>
    public static Dictionary<string, int> Subtract(Dictionary<string, int> need,
                                                   Dictionary<string, int> have) {
        var left = new Dictionary<string, int>();
        foreach (var kv in need) {
            int got;
            int n = kv.Value - (have.TryGetValue(kv.Key, out got) ? got : 0);
            if (n > 0) left[kv.Key] = n;
        }
        return left;
    }
}

/// <summary>
/// Какие станки уже стоят и какого они уровня.
///
/// На сайте под это был целый блок с чекбоксами «отметьте, что уже построено»:
/// браузер про базу игрока не знает ничего. Здесь знать ничего и не надо —
/// станок стоит в трёх метрах и сам скажет свой уровень.
/// </summary>
public static class Stations {
    public static float Radius = 20f;

    /// <summary>
    /// Ближайший станок каждого вида и сколько до него метров.
    ///
    /// Радиусом не ограничено намеренно: вопрос «где моя кузница» полезен и
    /// тогда, когда она в сотне метров. Но список игры держит только
    /// загруженные объекты, так что дальше края подгруженного мира ответа нет
    /// и быть не может — это честное «не вижу», а не «нет».
    /// </summary>
    /// <summary>Что мод знает про ближайший станок этого вида.</summary>
    public struct Found {
        public int Level;
        public float Distance;
        /// <summary>Куда идти: угол в градусах от направления взгляда,
        /// по часовой стрелке. Ноль — прямо перед собой.</summary>
        public float Bearing;
    }

    public static Dictionary<string, Found> Distances() {
        var found = new Dictionary<string, Found>();
        var player = Player.m_localPlayer;
        var all = CraftingStation.m_allStations;
        if (player == null || all == null) return found;

        // Направление взгляда, а не тела: игрок видит камеру. Пока окно
        // открыто, камера заморожена (см. FreezeCameraWhileOpen), поэтому угол
        // не устареет за время, что на него смотрят.
        var eye = GameCamera.instance != null
            ? GameCamera.instance.transform.forward
            : player.transform.forward;
        eye.y = 0f;
        if (eye.sqrMagnitude < 0.0001f) eye = Vector3.forward;
        eye.Normalize();

        var me = player.transform.position;
        foreach (var st in all) {
            if (st == null) continue;
            var id = GameData.Slug(st.gameObject.name.Replace("(Clone)", ""));
            var to = st.transform.position - me;
            float d = to.magnitude;
            int lvl = st.GetLevel(true);
            Found had;
            // Из двух одинаковых станков берём ближний; при равном расстоянии —
            // тот, что выше уровнем.
            if (found.TryGetValue(id, out had) && (had.Distance < d
                || (had.Distance == d && had.Level >= lvl))) continue;
            to.y = 0f;
            found[id] = new Found {
                Level = lvl,
                Distance = d,
                Bearing = to.sqrMagnitude < 0.0001f
                    ? 0f : Vector3.SignedAngle(eye, to, Vector3.up),
            };
        }
        return found;
    }

    public static Dictionary<string, int> Nearby() {
        var levels = new Dictionary<string, int>();
        var player = Player.m_localPlayer;
        if (player == null) return levels;

        // Игра сама держит список всех построенных станков. Поле приватное,
        // но публицизация из .csproj его открывает, а FindObjectsOfType обошёлся
        // бы дороже: он перебирает вообще все объекты сцены.
        var all = CraftingStation.m_allStations;
        if (all == null) return levels;

        foreach (var st in all) {
            if (st == null) continue;
            if (Vector3.Distance(st.transform.position, player.transform.position) > Radius)
                continue;
            var id = GameData.Slug(st.gameObject.name.Replace("(Clone)", ""));
            // Сверено по метаданным: GetLevel принимает bool checkExtensions,
            // без аргумента этот вызов не компилируется. true — считать
            // пристройки, то есть ровно тот уровень, что игра показывает игроку.
            int lvl = st.GetLevel(true);
            int had;
            if (!levels.TryGetValue(id, out had) || lvl > had) levels[id] = lvl;
        }
        return levels;
    }

    /// <summary>Хватает ли уровня станка на эту позицию нужного качества.</summary>
    public static bool Enough(ItemDef it, int quality, Dictionary<string, int> built) {
        if (it == null || string.IsNullOrEmpty(it.Station)) return true;  // руками
        int need = it.MinStationLevel + Mathf.Max(1, quality) - 1;
        int have;
        return built.TryGetValue(it.Station, out have) && have >= need;
    }
}

}
