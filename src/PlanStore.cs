using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Forgeplan {

/// <summary>
/// План и закреплённое — между запусками игры.
///
/// До 1.6.0 мод не хранил ничего: план жил, пока открыта игра. Пока в плане
/// было только «что сделать», это было терпимо — набрать заново пять строк
/// недолго. С отметками «этот меч уже есть на первом уровне» иначе: это
/// записанный прогресс группы, и терять его на выходе из игры нельзя.
///
/// Чьё хранилище. Отдельный файл на мир и персонажа: у человека бывает
/// несколько миров, и заказ брони на сервере друзей не должен всплывать в
/// одиночной игре. Мир узнаётся по имени и сиду — их знает и клиент на
/// выделенном сервере; персонаж — по файлу профиля.
///
/// Только у себя на диске, в BepInEx/config/forgeplanner. В мир и в
/// сохранения мод по-прежнему не пишет ничего (README, «Client-side only»):
/// удалили мод — остался файл, который никто не читает.
///
/// Пишем не по каждому щелчку, а раз в пару секунд и только если что-то
/// поменялось — и обязательно при выходе из мира.
/// </summary>
public static class PlanStore {
    const float Every = 2f;
    const string Header = "# ForgePlanner";

    static string _path;
    static string _saved;
    static float _checkAt;

    /// <summary>
    /// Мы только что вышли из мира, а персонаж ещё жив. Выход идёт в
    /// несколько кадров: ZNetScene уже закрыт, план дописан и стёрт, а
    /// Player.m_localPlayer ещё не уничтожен. Без этой отметки Tick увидел бы
    /// «персонаж есть, плана нет» и загрузил его снова — в уходящий мир.
    /// Закреплённая панель строилась на миг и пропадала со сценой, а путь к
    /// файлу оставался старым, и следующий мир писал бы в чужой файл.
    /// </summary>
    static bool _leaving;

    /// <summary>Зовётся каждый кадр. Первый кадр с персонажем в мире —
    /// загрузка, дальше — запись, если план поменялся.</summary>
    public static void Tick() {
        if (Player.m_localPlayer == null) { _leaving = false; return; }
        if (_leaving) return;
        if (_path == null) {
            _path = PathFor();
            if (_path == null) return;
            Load();
            _saved = Snapshot();
            return;
        }
        if (Time.unscaledTime < _checkAt) return;
        _checkAt = Time.unscaledTime + Every;
        // Мир или персонаж сменились, а выход мы почему-то пропустили: не
        // писать чужой план в этот файл.
        var now = PathFor();
        if (now != null && now != _path) {
            ForgeplanPlugin.Log.LogWarning("план: сменился мир или персонаж без выхода — "
                                           + Path.GetFileName(_path) + " → " + Path.GetFileName(now));
            Flush();
            _path = now;
            Load();
            _saved = Snapshot();
            return;
        }
        Flush();
    }

    /// <summary>Выход из мира: дописать и забыть, чей это был план.</summary>
    public static void Leave() {
        Flush();
        _path = null;
        _saved = null;
        _leaving = true;
    }

    static void Flush() {
        if (_path == null) return;
        var snap = Snapshot();
        if (snap == _saved) return;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, snap, new UTF8Encoding(false));
            _saved = snap;
        } catch (Exception e) {
            // Не записалось — попробуем через пару секунд снова; окну это не
            // мешает, а строка в логе скажет, почему план не пережил выход.
            ForgeplanPlugin.Log.LogWarning("план не записан в " + _path + " — " + e.Message);
            _saved = snap;
        }
    }

    static string PathFor() {
        var world = WorldGenerator.instance != null ? WorldGenerator.instance.m_world : null;
        var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
        if (world == null || profile == null) return null;
        string name = Safe(world.m_name) + "-" + Safe(world.m_seedName) + "--" + Safe(profile.GetFilename());
        return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "forgeplanner"), name + ".plan");
    }

    static string Safe(string s) {
        if (string.IsNullOrEmpty(s)) return "_";
        var bad = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var c in s) sb.Append(bad.Contains(c) || c == ' ' ? '_' : c);
        return sb.ToString();
    }

    /* Формат — строки через табуляцию, чтобы файл читался и правился руками:

           # ForgePlanner
           [plan]
           swordiron	4	2	1,1,0,0
           [pin]
           swordiron	4	2	1,1,0,0

       id, сколько, уровень, уровни уже сделанных экземпляров. */

    static string Snapshot() {
        var sb = new StringBuilder();
        sb.Append(Header).Append('\n');
        Write(sb, "[plan]", Planner.Cart);
        Write(sb, "[pin]", Pin.Items);
        return sb.ToString();
    }

    static void Write(StringBuilder sb, string title, List<Entry> list) {
        sb.Append(title).Append('\n');
        foreach (var e in list)
            sb.Append(e.Id).Append('\t').Append(e.Qty).Append('\t').Append(e.Quality)
              .Append('\t').Append(string.Join(",", e.Made.Select(h => h.ToString()).ToArray()))
              .Append('\n');
    }

    static void Load() {
        if (!File.Exists(_path)) {
            // У этого мира и персонажа плана ещё нет — начинаем с пустого,
            // а не с того, что осталось от прошлого мира.
            Planner.Cart.Clear();
            Pin.Clear();
            return;
        }
        var plan = new List<Entry>();
        var pin = new List<Entry>();
        List<Entry> into = null;
        try {
            foreach (var raw in File.ReadAllLines(_path)) {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line == "[plan]") { into = plan; continue; }
                if (line == "[pin]") { into = pin; continue; }
                var e = Parse(raw);
                if (e != null && into != null) into.Add(e);
            }
        } catch (Exception ex) {
            ForgeplanPlugin.Log.LogWarning("план не прочитан из " + _path + " — " + ex.Message);
            return;
        }
        // Без PlanBuild строки тотемов показать нечем: убираем, а не держим
        // строку, которая никогда не наполнится.
        if (!PlanTotems.Available) {
            plan.RemoveAll(e => PlanTotems.IsTotem(e.Id));
            pin.RemoveAll(e => PlanTotems.IsTotem(e.Id));
        }
        Planner.Cart.Clear();
        Planner.Cart.AddRange(plan);
        Pin.Set(pin);
        ForgeplanPlugin.Log.LogInfo("план загружен: позиций " + plan.Count + ", закреплено "
                                    + pin.Count + " — " + Path.GetFileName(_path));
    }

    static Entry Parse(string line) {
        var f = line.Split('\t');
        if (f.Length < 3 || f[0].Length == 0) return null;
        int qty, quality;
        if (!int.TryParse(f[1], out qty) || !int.TryParse(f[2], out quality)) return null;
        var e = new Entry { Id = f[0], Qty = Mathf.Max(1, qty), Quality = Mathf.Clamp(quality, 1, 4) };
        if (f.Length > 3 && f[3].Length > 0)
            foreach (var part in f[3].Split(',')) {
                int h;
                e.Made.Add(int.TryParse(part, out h) ? Mathf.Clamp(h, 0, 4) : 0);
            }
        e.Fit();
        return e;
    }
}

}
