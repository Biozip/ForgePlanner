using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Forgeplan {

/// <summary>
/// Тотемы мода PlanBuild как строки плана.
///
/// PlanBuild ставит в мире «планы» построек, а тотем рядом с ними собирает,
/// чего этим планам не хватает. Игрок, который строит базу по планам, хочет
/// видеть этот список рядом со своим: сколько железа уйдёт на броню и сколько
/// на гвозди для крыши — одна и та же вылазка в Болото. Попросили об этом на
/// Hexium, к 1.5.0.
///
/// Строка тотема — обычная позиция плана, только «рецепт» у неё не из
/// ObjectDB, а из тотема, и читается заново раз в секунду. Поэтому итог,
/// разложение до сырья, блок станков, закреплённая панель и карточка
/// работают для неё сами, без отдельного кода. Синхронизировать руками
/// ничего не нужно: отнёс доски в тотем — строка похудела.
///
/// Зависимость мягкая. Сборки PlanBuild у мода нет, всё через отражение: у
/// тотема нет открытого API, его класс internal. Отсюда и осторожность — не
/// нашлось поле или метод бросил исключение, значит PlanBuild поменялся, и
/// функция выключается с записью в лог, а не роняет окно.
///
/// Пересечение. План в радиусе двух тотемов PlanBuild считает у обоих, и
/// сумма двух строк посчитала бы его дважды. Поэтому каждая постройка идёт в
/// строку первого добавленного тотема, у которого она есть, а следующие
/// тотемы добирают только своё. Итог плана — без дублей.
/// </summary>
public static class PlanTotems {
    const string Prefix = "planbuild:totem:";

    /// <summary>Как часто перечитывать тотемы. Сам тотем пересчитывается раз
    /// в три секунды, чаще читать незачем, реже — строка отстаёт от него.</summary>
    const float Every = 1f;

    /// <summary>Как далеко искать тотем при добавлении. Радиус тотема по
    /// умолчанию — 30 м, и стоят обычно в его круге или у края.</summary>
    public const float Reach = 40f;

    static bool _probed, _ok;
    static FieldInfo _all, _connected, _original;
    static MethodInfo _remaining;
    static float _readAt = -100f;
    static readonly HashSet<string> Unknown = new HashSet<string>();

    class State {
        public string Id;
        public string Key;
        public ItemDef Def;
        public string Title;
        public int Pieces;
        public bool Live;
        public List<Group> Groups = new List<Group>();
    }

    /// <summary>
    /// Постройки тотема, сложенные по виду: «костёр ×2 — 4 дерева, 10 камня».
    /// Строка тотема — сумма, а по ней не понять, какой из планов съедает
    /// камень. Попросили при проверке 1.5.0: видеть под тотемом, что именно
    /// он строит.
    ///
    /// Числа — сколько не хватает самим планам. Запас в тотеме сюда не
    /// вычитается: тотем раздаёт его по планам сам, раз в три секунды, и
    /// между двумя раздачами строки могут быть чуть больше итога.
    /// </summary>
    public class Group {
        public string Name;
        public Sprite Icon;
        public int Count;
        public Dictionary<string, int> Need = new Dictionary<string, int>();
    }

    /// <summary>Тотемы, которые есть в плане или в закреплённом, в порядке
    /// добавления: от порядка зависит, кому достаются общие постройки.</summary>
    static readonly List<State> Known = new List<State>();

    public static bool IsTotem(string id) {
        return id != null && id.StartsWith(Prefix, StringComparison.Ordinal);
    }

    /// <summary>Стоит ли PlanBuild и узнал ли мод его устройство.</summary>
    public static bool Available { get { Probe(); return _ok; } }

    public static void Reset() {
        Known.Clear();
        Unknown.Clear();
        _readAt = -100f;
    }

    static void Probe() {
        if (_probed) return;
        _probed = true;
        Type totem = null, piece = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            if (asm.GetName().Name != "PlanBuild") continue;
            totem = asm.GetType("PlanBuild.Plans.PlanTotem");
            piece = asm.GetType("PlanBuild.Plans.PlanPiece");
            break;
        }
        if (totem == null && piece == null) return;   // PlanBuild не стоит — молчим

        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
                               | BindingFlags.Instance | BindingFlags.Static;
        if (totem != null) {
            _all = totem.GetField("m_allPlanTotems", Any);
            _connected = totem.GetField("m_connectedPieces", Any);
        }
        if (piece != null) {
            _original = piece.GetField("originalPiece", Any);
            _remaining = piece.GetMethod("GetRemaining", BindingFlags.Public | BindingFlags.Instance,
                                         null, Type.EmptyTypes, null);
        }
        _ok = _all != null && _connected != null && _original != null && _remaining != null
              && typeof(Container).IsAssignableFrom(totem)
              && typeof(Dictionary<string, int>).IsAssignableFrom(_remaining.ReturnType);
        if (_ok)
            ForgeplanPlugin.Log.LogInfo("PlanBuild найден: тотемы можно добавлять в план");
        else
            ForgeplanPlugin.Log.LogWarning(
                "PlanBuild найден, но устроен не так, как ждёт мод (тотем "
                + (totem != null) + ", план " + (piece != null) + ", поля "
                + (_all != null) + "/" + (_connected != null) + "/" + (_original != null)
                + "/" + (_remaining != null) + ") — тотемы в план не добавляются");
    }

    /// <summary>Живые тотемы мира: только те, что сейчас загружены.</summary>
    static IEnumerable<Container> Totems() {
        var list = _all.GetValue(null) as IList;
        if (list == null) yield break;
        foreach (var o in list) {
            var c = o as Container;
            // Уничтоженный объект Unity не null для C#, но == null для Unity.
            if (c == null || !c) continue;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) continue;
            yield return c;
        }
    }

    /// <summary>
    /// Кто этот тотем. По месту, а не по ZDO: тотем не двигается, а при
    /// выгрузке участка и возвращении Unity создаёт его заново, так что
    /// ссылка на объект не годится. ZDO — сетевой тип, и мод его не трогает
    /// вовсе (см. README, раздел про клиентскую сторону). Два тотема в одной
    /// точке с точностью до десяти сантиметров не поставить.
    /// </summary>
    static string KeyOf(Container c) {
        var p = c.transform.position;
        return Mathf.RoundToInt(p.x * 10) + ":" + Mathf.RoundToInt(p.y * 10)
             + ":" + Mathf.RoundToInt(p.z * 10);
    }

    static string IdOf(Container c) { return Prefix + KeyOf(c); }

    /// <summary>Ближайший к точке тотем не дальше Reach — его id или null.</summary>
    public static string Nearest(Vector3 at) {
        if (!Available) return null;
        try {
            Container best = null;
            float bestDist = Reach;
            foreach (var c in Totems()) {
                float d = Vector3.Distance(at, c.transform.position);
                if (d <= bestDist) { best = c; bestDist = d; }
            }
            return best != null ? IdOf(best) : null;
        } catch (Exception e) {
            Fail(e);
            return null;
        }
    }

    /// <summary>Завести строку под тотем. Зовётся перед тем, как id попадёт
    /// в план.</summary>
    ///
    /// Тотем может быть и не загружен: план с ним прочитан с диска, а игрок
    /// заходит в мир далеко от базы. Тогда строка заводится сразу, пустой и
    /// с пометкой «далеко», а название и иконку получает, когда тотем
    /// подгрузится.
    public static void Track(string id) {
        if (!IsTotem(id) || Known.Any(s => s.Id == id)) return;
        var st = new State {
            Id = id, Key = id.Substring(Prefix.Length), Title = DefaultTitle,
            Def = new ItemDef {
                Id = id, Name = DefaultTitle + L0(Ui.L.TotemFar), Totem = true,
                Levels = { new Dictionary<string, int>() },
            },
        };
        try {
            foreach (var c in Totems())
                if (IdOf(c) == id) { Dress(st, c); break; }
        } catch (Exception e) {
            Fail(e);
        }
        Known.Add(st);
        _readAt = -100f;
    }

    const string DefaultTitle = "Plan Totem";

    /// <summary>Название и иконка — от живого тотема, один раз.</summary>
    static void Dress(State st, Container c) {
        if (st.Def.Icon != null) return;
        var piece = c.GetComponent<Piece>();
        if (piece == null) return;
        st.Title = Localization.instance.Localize(piece.m_name);
        st.Def.Icon = piece.m_icon;
    }

    public static ItemDef Def(string id) {
        if (!Available) return null;
        if (!Known.Any(s => s.Id == id)) Track(id);
        if (Time.time - _readAt >= Every) Read();
        var st = Known.FirstOrDefault(s => s.Id == id);
        return st != null ? st.Def : null;
    }

    /// <summary>Постройки тотема по видам; пусто, если тотема нет.</summary>
    public static List<Group> Groups(string id) {
        if (Time.time - _readAt >= Every) Read();
        var st = Known.FirstOrDefault(s => s.Id == id);
        return st != null ? st.Groups : new List<Group>();
    }

    /// <summary>Есть ли в списке тотем, который сейчас не загружен и держит
    /// старые числа.</summary>
    public static bool AnyFar(List<Entry> list) {
        foreach (var e in list) {
            if (!IsTotem(e.Id)) continue;
            var st = Known.FirstOrDefault(s => s.Id == e.Id);
            if (st != null && !st.Live) return true;
        }
        return false;
    }

    static void Read() {
        _readAt = Time.time;
        // Забываем тотемы, которых нет ни в плане, ни в закреплённом: иначе
        // выброшенный из плана тотем продолжал бы забирать себе общие постройки.
        Known.RemoveAll(s => !Planner.Cart.Any(e => e.Id == s.Id)
                          && !Pin.Items.Any(e => e.Id == s.Id));
        if (!_ok || Known.Count == 0) return;
        try {
            var live = new Dictionary<string, Container>();
            foreach (var c in Totems()) live[KeyOf(c)] = c;

            var claimed = new HashSet<int>();
            foreach (var st in Known) {
                Container c;
                if (!live.TryGetValue(st.Key, out c)) {
                    // Тотем выгрузился вместе со своим участком. Держим то, что
                    // прочли последним, и говорим об этом в названии.
                    st.Live = false;
                    st.Def.Name = st.Title + L0(Ui.L.TotemFar);
                    continue;
                }
                ReadOne(st, c, claimed);
            }
        } catch (Exception e) {
            Fail(e);
        }
    }

    static string L0(string s) { return "  <size=80%><color=#8A8172>" + s + "</color></size>"; }

    static void ReadOne(State st, Container c, HashSet<int> claimed) {
        Dress(st, c);
        var need = new Dictionary<string, int>();
        var stations = new List<string>();
        var groups = new Dictionary<string, Group>();
        int pieces = 0;

        var connected = _connected.GetValue(c) as IEnumerable;
        if (connected != null)
            foreach (var p in connected) {
                var comp = p as Component;
                if (comp == null || !comp) continue;
                if (!claimed.Add(comp.GetInstanceID())) continue;
                pieces++;

                var orig = _original.GetValue(p) as Piece;
                Group g = null;
                if (orig != null) {
                    if (!groups.TryGetValue(orig.m_name, out g))
                        groups[orig.m_name] = g = new Group {
                            Name = Localization.instance.Localize(orig.m_name),
                            Icon = orig.m_icon,
                        };
                    g.Count++;
                }

                var rem = _remaining.Invoke(p, null) as Dictionary<string, int>;
                if (rem != null)
                    foreach (var kv in rem) {
                        if (kv.Value <= 0) continue;
                        var id = GameData.IdByToken(kv.Key);
                        if (id == null) {
                            if (Unknown.Add(kv.Key))
                                ForgeplanPlugin.Log.LogWarning(
                                    "тотем просит материал, которого нет в каталоге: " + kv.Key);
                            continue;
                        }
                        int had;
                        need[id] = (need.TryGetValue(id, out had) ? had : 0) + kv.Value;
                        if (g != null)
                            g.Need[id] = (g.Need.TryGetValue(id, out had) ? had : 0) + kv.Value;
                    }

                if (orig != null && orig.m_craftingStation != null) {
                    var s = GameData.Slug(orig.m_craftingStation.gameObject.name);
                    if (!stations.Contains(s)) stations.Add(s);
                }
            }

        // Что уже лежит в самом тотеме, ему больше не нужно — так считает и
        // подсказка PlanBuild.
        var inv = c.GetInventory();
        if (inv != null)
            foreach (var id in need.Keys.ToList()) {
                var it = GameData.Get(id);
                int left = need[id] - (it != null ? inv.CountItems(it.Token) : 0);
                if (left > 0) need[id] = left; else need.Remove(id);
            }

        st.Live = true;
        st.Pieces = pieces;
        st.Groups = groups.Values.OrderBy(x => x.Name).ToList();
        st.Def.Levels[0] = need;
        st.Def.Station = stations.Count > 0 ? stations[0] : "";
        st.Def.MoreStations = stations.Count > 1 ? stations.Skip(1).ToList() : null;
        st.Def.Name = st.Title + L0(Ui.L.TotemPieces(pieces));
    }

    static void Fail(Exception e) {
        if (!_ok) return;
        _ok = false;
        ForgeplanPlugin.Log.LogWarning("PlanBuild: тотем не читается, строки тотемов "
                                       + "застыли на последних числах — " + e);
    }

    /// <summary>Есть ли тотемы в плане: окну тогда пора перерисовываться само,
    /// а не только по щелчку.</summary>
    public static bool AnyIn(List<Entry> cart) {
        foreach (var e in cart) if (IsTotem(e.Id)) return true;
        return false;
    }
}

}
