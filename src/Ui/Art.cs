using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Forgeplan.Ui {

/// <summary>
/// Свои картинки — единственные, что мод везёт с собой.
///
/// Лежат они внутри DLL (см. EmbeddedResource в Forgeplan.csproj), а не файлами
/// рядом: мод ставят распаковкой в BepInEx\plugins, и второй файл рано или
/// поздно не докопируют. Один файл ломаться не умеет.
///
/// PNG белые с прозрачностью, а не «под цвет интерфейса»: Unity красит спрайт
/// множителем, поэтому белая картинка принимает любой оттенок. Держать по файлу
/// на каждое состояние кнопки — лишняя работа и лишний вес.
///
/// Из Valheim не взято ничего: на этом держится вся история с лицензией.
/// </summary>
public static class Art {
    static readonly Dictionary<string, Sprite> Cache =
        new Dictionary<string, Sprite>(StringComparer.Ordinal);

    /// <summary>Спрайт по имени файла без расширения. null — не нашлось;
    /// звать можно хоть каждый кадр, результат запоминается.</summary>
    public static Sprite Get(string name) {
        Sprite s;
        if (Cache.TryGetValue(name, out s)) return s;
        Cache[name] = s = Load(name);
        return s;
    }

    static MethodInfo _loadImage;
    static bool _loadImageLooked;

    /// <summary>
    /// Texture2D.LoadImage через отражение.
    ///
    /// Не от хорошей жизни: метод лежит в UnityEngine.ImageConversionModule,
    /// а тот собран под netstandard 2.1. Мод — под net472, то есть
    /// netstandard 2.0, и компилятор отказывается ссылаться на модуль вовсе
    /// (CS1705). В рантайме же он есть и работает.
    /// </summary>
    static bool LoadInto(Texture2D tex, byte[] bytes) {
        if (!_loadImageLooked) {
            _loadImageLooked = true;
            var type = Type.GetType(
                "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (type != null)
                _loadImage = type.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static,
                                            null, new[] { typeof(Texture2D), typeof(byte[]) }, null);
            if (_loadImage == null)
                ForgeplanPlugin.Log.LogWarning(
                    "ImageConversion.LoadImage не найден — иконки не загрузятся");
        }
        if (_loadImage == null) return false;
        return (bool)_loadImage.Invoke(null, new object[] { tex, bytes });
    }

    static Sprite Load(string name) {
        try {
            var asm = Assembly.GetExecutingAssembly();
            // Имя ресурса собирается из корневого пространства имён и пути, и
            // зависит от настроек сборки. Искать по хвосту надёжнее, чем
            // угадывать префикс.
            string full = null;
            var tail = "." + name + ".png";
            foreach (var n in asm.GetManifestResourceNames())
                if (n.EndsWith(tail, StringComparison.OrdinalIgnoreCase)) { full = n; break; }
            if (full == null) {
                ForgeplanPlugin.Log.LogWarning("нет картинки в ресурсах: " + name);
                return null;
            }

            byte[] bytes;
            using (var stream = asm.GetManifestResourceStream(full))
            using (var mem = new MemoryStream()) {
                if (stream == null) return null;
                stream.CopyTo(mem);
                bytes = mem.ToArray();
            }

            // Размер здесь неважен: LoadImage читает его из самого PNG.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!LoadInto(tex, bytes)) {
                ForgeplanPlugin.Log.LogWarning("не разобрался PNG: " + name);
                return null;
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "forgeplan_" + name;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f), 100f, 0u,
                                 SpriteMeshType.FullRect);
        } catch (Exception e) {
            ForgeplanPlugin.Log.LogWarning("картинка " + name + " не загрузилась: " + e.Message);
            return null;
        }
    }
}

}
