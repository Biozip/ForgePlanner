using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Forgeplan {

/// <summary>Рецепт или материал каталога. Аналог записи в data.js, только
/// собранный из живой игры, а не из дампа.</summary>
public class ItemDef {
    public string Id;                       // имя префаба в нижнем регистре
    public string Name;                     // уже локализованное имя
    public Sprite Icon;
    public int Out = 1;                     // сколько штук даёт один крафт
    public string Station = "";             // пусто — крафтится руками
    public int MinStationLevel = 1;
    public int MaxQuality = 1;
    /// <summary>Стоимость по уровням: [0] — базовый крафт, [k] — доплата за
    /// улучшение до уровня k+1. Ровно та же раскладка, что lv[] на сайте.</summary>
    public List<Dictionary<string, int>> Levels = new List<Dictionary<string, int>>();
    public bool HasRecipe => Levels.Count > 0;

    /// <summary>
    /// Заготовка, а не готовая вещь: слиток, гвозди, сырое тесто, основа
    /// медовухи. На сайте это категории material и prep из build_data.py —
    /// по ним решается, раскрывать ли материал дальше при разложении до сырья.
    /// Категорий в игре нет, но есть ItemType.Material, и он ложится ровно на
    /// ту же границу: бронза и тесто — Material, хлеб и топор — нет.
    /// </summary>
    public bool IsMaterial;

    /// <summary>Раздел каталога. Берётся из ItemType самой игры, поэтому
    /// таблицы категорий, как в build_data.py, здесь не нужно.</summary>
    public Kind Group = Kind.Other;

    /// <summary>Биом: индекс в Tiers.Ids, или -1, если определить не вышло.
    /// Считается один раз при сборке каталога, см. GameData.TierOf.</summary>
    public int Tier = -1;

    /// <summary>Строка, по которой ищут: название из игры, слаг префаба и
    /// названия на обоих языках. Всё в нижнем регистре и склеено заранее —
    /// поиск идёт по каждой букве и по двум тысячам позиций сразу.</summary>
    public string Haystack = "";

    /// <summary>Названия из data.js сайта. Пустые там, куда его конвейер не
    /// добрался, — тогда показываем то, что сказала игра.</summary>
    public string Ru, En;

    /// <summary>
    /// Что показать сейчас. Игра знает один язык, а окно переключается — без
    /// этого половина подписей меняла язык, а половина нет.
    ///
    /// Да, слой данных здесь смотрит на состояние интерфейса. Альтернатива —
    /// тащить язык параметром через все вызовы подряд, а выигрыш нулевой:
    /// язык в игре ровно один на всё окно.
    /// </summary>
    public string Shown {
        get {
            var pick = Ui.L.Ru ? Ru : En;
            if (!string.IsNullOrEmpty(pick)) return pick;
            // Игра для части построек не знает названия вовсе и возвращает
            // сырой токен — «[piece_darkwoodbeam67]». Второй язык читается
            // лучше квадратных скобок; если и его нет, показывать всё равно
            // нечего.
            if (!string.IsNullOrEmpty(Name) && Name[0] != '[') return Name;
            var other = Ui.L.Ru ? En : Ru;
            return string.IsNullOrEmpty(other) ? Name : other;
        }
    }

    /// <summary>Рецепт не настоящий, а переписанный из превращения станка.
    /// Нужно, чтобы отличать бронзу (её правда куют) от хлеба (его только пекут).</summary>
    public bool FromConversion;
}

/// <summary>Разделы каталога. Дробнее, чем «снаряжение одним куском»:
/// в списке под пять сотен строк, и разница между «показать оружие» и
/// «показать всё, что надевают» — это разница между двумя десятками строк
/// и сотней.</summary>
public enum Kind { Weapon, Armor, Tool, Food, Material, Piece, Other }

/// <summary>Превращение на станке: плавка, брожение, выпечка.</summary>
public class Rule {
    public Dictionary<string, int> Mats = new Dictionary<string, int>();
    public string Station = "";
    public int Out = 1;      // сколько выходит за прогон (бродильня даёт 6)
    public int Sec;          // секунд на прогон
    public int Cap;          // сколько влезает за раз, 0 — не ограничено
    public int Slots = 1;    // сколько жарится одновременно (печь — 4)
}

/// <summary>
/// Каталог, собранный из игры. Здесь нет ничего от конвейера сайта: ни
/// JotunnDoc, ни атласа иконок, ни таблицы биомов — всё читается из ObjectDB
/// и ZNetScene в момент первого открытия окна.
///
/// ВАЖНО: собирать можно только в загруженном мире. ObjectDB есть и в главном
/// меню, а вот ZNetScene с префабами построек — нет, и без него не будет ни
/// печи, ни бродильни.
/// </summary>
public static class GameData {
    public static readonly Dictionary<string, ItemDef> Items =
        new Dictionary<string, ItemDef>();
    /// <summary>Что станок делает сам: ключ — что получится.</summary>
    public static readonly Dictionary<string, Rule> Convert =
        new Dictionary<string, Rule>();

    /// <summary>Названия станков. Отдельный словарь не для красоты: станок —
    /// это постройка, а не предмет, и в Items его нет. Без этого в блоке
    /// «Переработка» вместо «Каменная печь» стояло бы piece_oven.</summary>
    public static readonly Dictionary<string, string> StationNames =
        new Dictionary<string, string>();

    /// <summary>Иконка станка — та же, что рисует молоток. В итогах строка
    /// «Переработка» без неё выглядела чужой среди строк с иконками.</summary>
    public static readonly Dictionary<string, Sprite> StationIcons =
        new Dictionary<string, Sprite>();

    public static Sprite StationIcon(string id) {
        Sprite s;
        return StationIcons.TryGetValue(id, out s) ? s : null;
    }

    public static string StationName(string id) {
        if (string.IsNullOrEmpty(id)) return id;
        var pick = Names.Get(Ui.L.Ru ? Names.StationRu : Names.StationEn, id);
        if (!string.IsNullOrEmpty(pick)) return pick;
        string n;
        return StationNames.TryGetValue(id, out n) ? n : id;
    }

    public static bool Ready { get; private set; }

    /// <summary>
    /// Имя префаба в тот же вид, в каком его пишет конвейер сайта:
    /// <c>re.sub(r'[^a-z0-9]+', '_', s.lower()).strip('_')</c>.
    ///
    /// Раньше здесь стоял только ToLowerInvariant, и это молча ломало поиск
    /// имени у всего, где в префабе есть точка или дефис. Заметно это было на
    /// одной строке: «Бревенчатый брус 2 м» переводился, а «4 м» — нет, потому
    /// что его префаб зовётся wood_wall_log_4x0.5, и ключ расходился на одну
    /// точку. Таблицы имён, биомов и превращений — все с той стороны, так что
    /// правило должно быть общим.
    /// </summary>
    public static string Slug(string prefab) {
        if (string.IsNullOrEmpty(prefab)) return "";
        var sb = new StringBuilder(prefab.Length);
        bool gap = false;
        foreach (var ch in prefab) {
            char c = char.ToLowerInvariant(ch);
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) {
                if (gap && sb.Length > 0) sb.Append('_');
                gap = false;
                sb.Append(c);
            } else {
                gap = true;      // подряд идущие разделители схлопываются в один
            }
        }
        return sb.ToString();
    }

    public static ItemDef Get(string id) {
        ItemDef it;
        return Items.TryGetValue(id, out it) ? it : null;
    }

    public static string NameOf(string id) {
        var it = Get(id);
        if (it != null) return it.Shown;
        // Материал без собственной записи в каталоге: имя всё равно может
        // найтись в таблице с сайта.
        var pick = Names.Get(Ui.L.Ru ? Names.Ru : Names.En, id);
        return string.IsNullOrEmpty(pick) ? id : pick;
    }

    /// <summary>Превращения, снятые как дубли настоящих рецептов. Держим для
    /// самопроверки: список должен быть коротким и осмысленным.</summary>
    public static readonly List<string> Shadowed = new List<string>();
    /// <summary>Сколько превращений переписано в рецепты предметов.</summary>
    public static int Mirrored;

    /// <summary>Сколько требований отброшено как идолы Кузни потенциала.</summary>
    public static int SkippedUpgraders;

    /// <summary>Сколько построек попало в каталог. Для самопроверки.</summary>
    public static int Pieces;

    /// <summary>Сколько рецептов нашлось на каждую вещь. Больше одного — не
    /// редкость: бронзу куют и по одной, и сразу по пять.</summary>
    public static readonly Dictionary<string, List<int>> RecipeAmounts =
        new Dictionary<string, List<int>>();

    public static void Reset() {
        Items.Clear(); Convert.Clear(); StationNames.Clear(); Shadowed.Clear();
        StationIcons.Clear(); TierMemo.Clear();
        RecipeAmounts.Clear(); KilnRule = null; Mirrored = 0;
        SkippedUpgraders = 0; Pieces = 0; Ready = false;
        SkippedPlanting = 0; SkippedServings = 0;
    }

    /// <summary>
    /// Биом предмета — максимум по его материалам, ровно как на сайте.
    ///
    /// В данных игры биома нет вовсе: ни у ItemDrop, ни у Piece. Это
    /// редакторское знание, и таблица сырья приезжает сгенерированной из
    /// data.js (см. Tiers.g.cs и tools/export_tiers.py). Неизвестный материал
    /// не тянет вещь на Луга, а просто не участвует: иначе любая мелочь,
    /// до которой не добрался конвейер сайта, красила бы вещь зелёным.
    /// </summary>
    static readonly Dictionary<string, int> TierMemo = new Dictionary<string, int>();

    public static int TierOf(ItemDef it) { return TierOf(it, 0); }

    static int TierOf(ItemDef it, int depth) {
        if (it == null || it.Levels.Count == 0) return -1;
        int best = -1;
        foreach (var mat in it.Levels[0].Keys) {
            int t = MatTier(mat, 0);
            if (t > best) best = t;
        }

        // Станок — такое же условие, как материал, и без него биом врёт.
        // Арбалет собран из болотного железа и корней, но куют его в Чёрной
        // кузнице, а та строится из чёрного мрамора, то есть не раньше
        // Туманных земель. Игрок на Равнинах видел его в списке «Болото» и
        // сделать не мог.
        //
        // Станок, который сам ставится у другого станка, наследует и его
        // порог: рекурсия короткая, но проверять её надо.
        if (depth < 4 && !string.IsNullOrEmpty(it.Station)) {
            var station = Get(PieceId(it.Station));
            if (station != null && station != it) {
                int t = TierOf(station, depth + 1);
                if (t > best) best = t;
            }
        }
        return best;
    }

    static int MatTier(string id, int depth) {
        int t;
        if (TierMemo.TryGetValue(id, out t)) return t;
        if (depth > 8) return -1;
        TierMemo[id] = -1;                       // защита от циклов

        if (Tiers.OfMaterial.TryGetValue(id, out t)) { TierMemo[id] = t; return t; }

        int best = -1;
        Rule rule;
        if (Convert.TryGetValue(id, out rule)) {
            foreach (var src in rule.Mats.Keys) {
                int v = MatTier(src, depth + 1);
                if (v > best) best = v;
            }
        } else {
            var made = Get(id);
            if (made != null && made.Levels.Count > 0) {
                foreach (var src in made.Levels[0].Keys) {
                    int v = MatTier(src, depth + 1);
                    if (v > best) best = v;
                }
            }
        }
        TierMemo[id] = best;
        return best;
    }

    public static bool Build() {
        if (Ready) return true;
        if (ObjectDB.instance == null || ObjectDB.instance.m_items.Count == 0) return false;
        if (ZNetScene.instance == null) return false;   // нужен загруженный мир

        // Каталог пересобирается при каждом входе в мир, и с приходом построек
        // он почти удвоился. Время в лог — чтобы при жалобе на долгий вход
        // было что посмотреть, а не что предположить.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Reset();
        IndexItems();
        IndexRecipes();
        IndexConversions();
        // Порядок важен. DropShadowed обязан отработать до MirrorConversions:
        // тот раздаёт рецепты всем превращениям подряд, и после него «есть ли
        // у вещи настоящий рецепт» уже не спросить.
        DropShadowed();
        LiftKiln();
        MirrorConversions();
        IndexPieces();
        foreach (var it in Items.Values) {
            it.Tier = TierOf(it);
            var slug = it.Id.StartsWith("piece:") ? it.Id.Substring(6) : it.Id;
            it.Ru = Names.Get(Names.Ru, slug);
            it.En = Names.Get(Names.En, slug);
            it.Haystack = (it.Name + " " + slug + " " + it.Ru + " " + it.En).ToLowerInvariant();
        }
        Ready = true;
        ForgeplanPlugin.Log.LogInfo(string.Format(
            "каталог собран за {0} мс: {1} предметов, из них {2} построек; "
            + "отброшено: посадок {3}, подач на стол {4}",
            clock.ElapsedMilliseconds, Items.Count, Pieces,
            SkippedPlanting, SkippedServings));
        return true;
    }

    /// <summary>Правило пережига древесины в уголь, вынутое из общего индекса.</summary>
    public static Rule KilnRule;

    /// <summary>
    /// Углевыжигательная печь — такое же превращение, как все, и в индекс она
    /// попадает сама. Но уголь на сайте живёт по отдельным правилам: он
    /// включается галочкой и приходит к плавильне топливом, а не рецептом.
    /// Оставь его в общем индексе — и он посчитается дважды: один раз как
    /// топливо плавильни, второй раз как самостоятельная переработка.
    /// Поэтому вынимаем правило отдельно: числа берём из игры, а поведение
    /// остаётся ровно тем, что проверено tools/test_calc.js.
    /// </summary>
    /// <summary>
    /// Снять превращения, дублирующие настоящий рецепт.
    ///
    /// Бронзу в игре делают двумя способами: куют в кузнице из меди с оловом и
    /// переплавляют в плавильне из лома. Превращения в Expandable() смотрятся
    /// раньше рецептов, поэтому побеждал лом — и план на три бронзовых топора
    /// требовал 36 лома вместо 72 меди и 36 олова. Лом падает из руин, фармить
    /// его нельзя, то есть ответ был не просто другим, а бесполезным.
    ///
    /// Чёрный металл при этом переплавляется из лома и только — рецепта у него
    /// нет, и его правило остаётся на месте. Признак ровно этот: есть ли у
    /// вещи собственный рецепт в ObjectDB.
    /// </summary>
    static void DropShadowed() {
        foreach (var kv in Convert) {
            var it = Get(kv.Key);
            if (it != null && it.HasRecipe) Shadowed.Add(kv.Key);
        }
        foreach (var id in Shadowed) Convert.Remove(id);
    }

    /// <summary>
    /// Переписать превращение в рецепт предмета.
    ///
    /// Хлеб, медовуха и жаркое рецепта в ObjectDB не имеют — их только пекут,
    /// бродят и жарят. Из-за этого заказ прямо на хлеб давал пустой список:
    /// Direct() перебирает корзину и пропускает всё без рецепта. На сайте того
    /// же самого не видно, потому что build_data.py дублирует превращение ещё
    /// и в lv[] предмета — здесь делается ровно то же.
    ///
    /// Побочно вещь попадает в каталог окна: он фильтруется по HasRecipe.
    /// Это и нужно — заказать хлеб иначе было нельзя.
    /// </summary>
    static void MirrorConversions() {
        foreach (var kv in Convert) {
            var it = Get(kv.Key);
            if (it == null || it.HasRecipe) continue;
            it.Levels.Add(new Dictionary<string, int>(kv.Value.Mats));
            it.Out = Mathf.Max(1, kv.Value.Out);
            it.Station = kv.Value.Station;
            it.MinStationLevel = 1;
            it.FromConversion = true;
            Mirrored++;
        }
    }

    static void LiftKiln() {
        Rule rule;
        if (!Convert.TryGetValue("coal", out rule)) return;
        KilnRule = rule;
        Convert.Remove("coal");
    }

    /// <summary>Все предметы — чтобы у материала без рецепта тоже были имя и
    /// иконка. На сайте под это был отдельный словарь mats.</summary>
    static void IndexItems() {
        foreach (var go in ObjectDB.instance.m_items) {
            if (go == null) continue;
            var drop = go.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null) continue;
            var shared = drop.m_itemData.m_shared;
            var id = Slug(go.name);
            if (Items.ContainsKey(id)) continue;
            Items[id] = new ItemDef {
                Id = id,
                Name = Localization.instance.Localize(shared.m_name),
                Icon = (shared.m_icons != null && shared.m_icons.Length > 0)
                    ? shared.m_icons[0] : null,
                MaxQuality = Mathf.Max(1, shared.m_maxQuality),
                IsMaterial = shared.m_itemType == ItemDrop.ItemData.ItemType.Material,
                Group = GroupOf(shared.m_itemType),
            };
        }
    }

    static Kind GroupOf(ItemDrop.ItemData.ItemType t) {
        switch (t) {
            case ItemDrop.ItemData.ItemType.OneHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
            case ItemDrop.ItemData.ItemType.Bow:
            case ItemDrop.ItemData.ItemType.Attach_Atgeir:
            case ItemDrop.ItemData.ItemType.Ammo:
            case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                return Kind.Weapon;
            // Щит здесь, а не в оружии: чинят и улучшают его вместе с бронёй,
            // и ищут его тоже там.
            case ItemDrop.ItemData.ItemType.Shield:
            case ItemDrop.ItemData.ItemType.Helmet:
            case ItemDrop.ItemData.ItemType.Chest:
            case ItemDrop.ItemData.ItemType.Legs:
            case ItemDrop.ItemData.ItemType.Shoulder:
            case ItemDrop.ItemData.ItemType.Hands:
            case ItemDrop.ItemData.ItemType.Utility:
            case ItemDrop.ItemData.ItemType.Trinket:
                return Kind.Armor;
            case ItemDrop.ItemData.ItemType.Tool:
            case ItemDrop.ItemData.ItemType.Torch:
                return Kind.Tool;
            case ItemDrop.ItemData.ItemType.Consumable:
                return Kind.Food;
            case ItemDrop.ItemData.ItemType.Material:
                return Kind.Material;
            default:
                return Kind.Other;
        }
    }

    /// <summary>Идентификатор постройки. Своё пространство имён: у стены и у
    /// предмета имена префабов могут совпасть, а материалом постройка не
    /// бывает никогда — двоеточие в слаге появиться не может.</summary>
    public static string PieceId(string prefab) { return "piece:" + Slug(prefab); }

    /// <summary>
    /// Постройки.
    ///
    /// В ObjectDB их нет — там только предметы, — поэтому идём по префабам
    /// сцены и берём те, у которых есть компонент Piece со списком материалов.
    /// Это ровно тот список, что показывает молоток.
    ///
    /// Уровней качества у построек не бывает, апгрейдов тоже: один уровень,
    /// одна цена. Зато есть станок — верстак, кузница, каменотёс, — и он
    /// попадает в подсказку так же, как у предметов.
    ///
    /// m_amount берётся как есть: Piece.Requirement — это НЕ тот Requirement,
    /// что у рецептов. У него нет ни доплаты за уровень, ни идолов, только
    /// предмет и количество.
    /// </summary>
    /// <summary>
    /// Что игрок вообще может построить.
    ///
    /// Компонент Piece висит не только на постройках игрока: в сцене полно
    /// чужих копий — костры торговцев, чаши в святилищах, обломки подземелий.
    /// Без отбора каталог показывал по три «Campfire» подряд, и отличить их
    /// было нечем.
    ///
    /// Признак берём у самой игры: набор построек лежит в PieceTable, а
    /// таблицы висят на инструментах — молотке, мотыге, культиваторе. Что не
    /// попало ни в одну таблицу, тем игрок не строит.
    /// </summary>
    /// <summary>Инструменты, чьи «постройки» планировщику крафта не нужны.
    /// Пока один: культиватор ставит грядки и саженцы, а это посадка, а не
    /// сборка. Цена саженца — одно семя, и в списке они только мешают.</summary>
    static readonly HashSet<string> SkipTools = new HashSet<string> { "cultivator" };

    static HashSet<GameObject> Buildable() {
        var set = new HashSet<GameObject>();
        var skipped = new HashSet<GameObject>();
        foreach (var go in ObjectDB.instance.m_items) {
            if (go == null) continue;
            var drop = go.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                continue;
            var table = drop.m_itemData.m_shared.m_buildPieces;
            if (table == null || table.m_pieces == null) continue;
            var into = SkipTools.Contains(Slug(go.name)) ? skipped : set;
            foreach (var p in table.m_pieces) if (p != null) into.Add(p);
        }
        // Что кладут и молотком, и культиватором, остаётся: отбрасываем только
        // то, чему кроме культиватора места нет.
        skipped.ExceptWith(set);
        SkippedPlanting = skipped.Count;
        return set;
    }

    /// <summary>Сколько посадок отброшено. Для самопроверки.</summary>
    public static int SkippedPlanting;

    /// <summary>Сколько «поставить готовую еду на стол» отброшено.</summary>
    public static int SkippedServings;

    static void IndexPieces() {
        var buildable = Buildable();
        foreach (var prefab in ZNetScene.instance.m_prefabs) {
            if (prefab == null) continue;
            if (!buildable.Contains(prefab)) continue;
            var piece = prefab.GetComponent<Piece>();
            if (piece == null || !piece.m_enabled) continue;
            if (piece.m_resources == null || piece.m_resources.Length == 0) continue;

            var id = PieceId(prefab.name);
            if (Items.ContainsKey(id)) continue;

            var cost = new Dictionary<string, int>();
            ItemDrop single = null;
            foreach (var req in piece.m_resources) {
                if (req == null || req.m_resItem == null) continue;
                if (req.m_amount <= 0) continue;
                cost[Slug(req.m_resItem.gameObject.name)] = req.m_amount;
                single = req.m_resItem;
            }
            if (cost.Count == 0) continue;   // ставится бесплатно — считать нечего

            var name = Localization.instance.Localize(piece.m_name);
            if (IsServing(cost, single, name)) { SkippedServings++; continue; }

            var station = piece.m_craftingStation != null
                ? Slug(piece.m_craftingStation.gameObject.name) : "";
            var def = new ItemDef {
                Id = id,
                Name = name,
                Icon = piece.m_icon,
                MaxQuality = 1,
                Out = 1,
                Station = station,
                MinStationLevel = 1,
                Group = Kind.Piece,
            };
            def.Levels.Add(cost);
            Items[id] = def;
            if (piece.m_craftingStation != null)
                Remember(station, piece.m_craftingStation.m_name);
            Pieces++;
        }
    }

    /// <summary>
    /// «Поставить готовое на стол»: тарелка с едой, кружка медовухи,
    /// праздничное блюдо. Стоит ровно одну порцию того же самого и называется
    /// так же — в каталоге такая строка была неотличимым дублем рецепта, но с
    /// подписью «Руками», будто еду можно приготовить без котла.
    ///
    /// Признак структурный, а не по категории постройки, и это не от лени:
    /// номера категорий в этой сборке разъезжаются. Праздничные блюда лежат в
    /// том же номере, что стены Дальнего севера, — отделить их числом нельзя.
    /// А «стоит одну штуку себя самого» — это ровно то, чем такая постройка и
    /// является, при любой нумерации.
    /// </summary>
    static bool IsServing(Dictionary<string, int> cost, ItemDrop single, string name) {
        if (cost.Count != 1 || single == null || string.IsNullOrEmpty(name)) return false;
        foreach (var kv in cost) if (kv.Value != 1) return false;
        if (single.m_itemData == null || single.m_itemData.m_shared == null) return false;
        return Localization.instance.Localize(single.m_itemData.m_shared.m_name) == name;
    }

    static void IndexRecipes() {
        foreach (var recipe in ObjectDB.instance.m_recipes) {
            if (recipe == null || recipe.m_item == null) continue;
            if (!recipe.m_enabled) continue;            // выключенные — мимо
            var id = Slug(recipe.m_item.gameObject.name);
            var it = Get(id);
            if (it == null) continue;

            int amount = Mathf.Max(1, recipe.m_amount);
            List<int> seen;
            if (!RecipeAmounts.TryGetValue(id, out seen)) RecipeAmounts[id] = seen = new List<int>();
            seen.Add(amount);

            // Рецептов на одну вещь бывает несколько: бронзу куют и по одной,
            // и сразу по пять. Раньше побеждал последний в ObjectDB, и это был
            // пятёрочный — заказ на 36 слитков превращался в восемь пачек, то
            // есть 40 штук и 80 меди вместо 72. Четыре слитка в мусор.
            // Берём самый мелкий: он всегда делится без остатка на любой заказ.
            if (it.HasRecipe && amount >= it.Out) continue;

            it.Out = amount;
            it.Station = recipe.m_craftingStation != null
                ? Slug(recipe.m_craftingStation.gameObject.name) : "";
            it.MinStationLevel = Mathf.Max(1, recipe.m_minStationLevel);
            // Названия станков крафта раньше не собирались вовсе, и в каталоге
            // вместо «Верстака» стояло piece_workbench: StationNames заполняли
            // только превращения, а верстак с кузницей туда не попадают.
            if (recipe.m_craftingStation != null)
                Remember(it.Station, recipe.m_craftingStation.m_name,
                         recipe.m_craftingStation.gameObject);

            // Стоимость по уровням качества. Requirement.GetAmount(q) в игре
            // возвращает цену КОНКРЕТНОГО уровня, а нам, как и на сайте, нужна
            // доплата за каждый шаг — их потом просто складывают.
            it.Levels.Clear();
            for (int q = 1; q <= it.MaxQuality; q++) {
                var step = new Dictionary<string, int>();
                foreach (var req in recipe.m_resources) {
                    if (req == null || req.m_resItem == null) continue;
                    // Идол Кузни потенциала — не ингредиент. Его несут вместе
                    // с вещью в отдельную кузню в Горах, чтобы поднять качество
                    // выше обычного предела, и это лотерея: при неудаче вещь
                    // уничтожается. В рецепте он лежит только затем, чтобы
                    // привязать вещь к своему тиру, и верстак его не просит —
                    // проверено в игре. Сайт чинился тем же правилом в 1.6.0.
                    if (req.m_upgraderResource) {
                        if (q == 1) SkippedUpgraders++;
                        continue;
                    }
                    int n = req.GetAmount(q);
                    if (n <= 0) continue;
                    step[Slug(req.m_resItem.gameObject.name)] = n;
                }
                it.Levels.Add(step);
            }
        }
    }

    /// <summary>
    /// Печь, бродильня, плавильня и кухня. Ровно то, чего сайту не хватало до
    /// 1.3.0 и ради чего пришлось писать extract_conversions.py — здесь это
    /// три вызова GetComponent.
    /// </summary>
    static void IndexConversions() {
        foreach (var prefab in ZNetScene.instance.m_prefabs) {
            if (prefab == null) continue;
            var station = Slug(prefab.name);

            var smelter = prefab.GetComponent<Smelter>();
            if (smelter != null) {
                Remember(station, PieceName(prefab, smelter.m_name), prefab);
                foreach (var c in smelter.m_conversion) {
                    if (c == null || c.m_from == null || c.m_to == null) continue;
                    var rule = new Rule {
                        Station = station,
                        Out = 1,
                        Sec = Mathf.RoundToInt(smelter.m_secPerProduct),
                        Cap = smelter.m_maxOre,
                        Slots = 1,
                    };
                    rule.Mats[Slug(c.m_from.gameObject.name)] = 1;
                    // топливо у плавильни лежит полем станка, а не в списке
                    // превращений — на сайте его приходилось дописывать руками
                    if (smelter.m_fuelItem != null && smelter.m_fuelPerProduct > 0) {
                        rule.Mats[Slug(smelter.m_fuelItem.gameObject.name)] =
                            smelter.m_fuelPerProduct;
                    }
                    Put(Slug(c.m_to.gameObject.name), rule);
                }
            }

            var fermenter = prefab.GetComponent<Fermenter>();
            if (fermenter != null) {
                Remember(station, PieceName(prefab, fermenter.m_name), prefab);
                foreach (var c in fermenter.m_conversion) {
                    if (c == null || c.m_from == null || c.m_to == null) continue;
                    var rule = new Rule {
                        Station = station,
                        Out = Mathf.Max(1, c.m_producedItems),
                        Sec = Mathf.RoundToInt(fermenter.m_fermentationDuration),
                        Slots = 1,
                    };
                    rule.Mats[Slug(c.m_from.gameObject.name)] = 1;
                    Put(Slug(c.m_to.gameObject.name), rule);
                }
            }

            var cooking = prefab.GetComponent<CookingStation>();
            if (cooking != null) {
                Remember(station, PieceName(prefab, cooking.m_name), prefab);
                int slots = cooking.m_slots != null ? cooking.m_slots.Length : 1;
                foreach (var c in cooking.m_conversion) {
                    if (c == null || c.m_from == null || c.m_to == null) continue;
                    var rule = new Rule {
                        Station = station,
                        Out = 1,
                        Sec = Mathf.RoundToInt(c.m_cookTime),
                        Slots = Mathf.Max(1, slots),
                    };
                    rule.Mats[Slug(c.m_from.gameObject.name)] = 1;
                    Put(Slug(c.m_to.gameObject.name), rule);
                }
            }
        }
    }

    /// <summary>
    /// Первый записавший побеждает. Это не мелочь: одно и то же мясо жарится и
    /// на простой стойке, и на железной, а станок в карточке один. Порядок
    /// префабов в ZNetScene не гарантирован, поэтому при желании сюда нужна
    /// явная таблица предпочтений — иначе кабанина с Лугов потребует железную
    /// стойку. На сайте это ровно та же ошибка, что чинилась в 1.3.1.
    /// </summary>
    /// <summary>
    /// Из чего плавить, когда правил несколько.
    ///
    /// У железа и меди в игре по два правила, и «первое попавшееся» — лотерея
    /// порядка префабов в ZNetScene: мод брал ironore, сайт ironscrap, и числа
    /// расходились молча. Выбор записан явно и с основанием:
    ///
    ///   iron   — из лома. Железная руда падает бонусом с гигантской сельди на
    ///            рыбалке и больше ниоткуда (вики, статья Iron Ore); планировать
    ///            через неё добычу железа бессмысленно, весь металл берут в
    ///            Затонувших склепах.
    ///   copper — из жилы. Медную жилу рубят в Чёрном лесу с первых часов, а
    ///            медный лом — поздняя добыча.
    ///
    /// Та же таблица с тем же обоснованием лежит в tools/build_data.py сайта.
    /// </summary>
    static readonly Dictionary<string, string> Prefer = new Dictionary<string, string> {
        { "iron", "ironscrap" },
        { "copper", "copperore" },
    };

    static void Put(string to, Rule rule) {
        if (string.IsNullOrEmpty(to)) return;
        string want;
        bool preferred = Prefer.TryGetValue(to, out want) && rule.Mats.ContainsKey(want);
        // Первый записавший побеждает — кроме случая, когда пришло то самое
        // правило, которое мы и хотели видеть.
        if (Convert.ContainsKey(to) && !preferred) return;
        Convert[to] = rule;
    }

    /// <summary>
    /// Имя станка для показа. У Smelter и Fermenter поле m_name бывает готовой
    /// английской строкой без токена — тогда в интерфейсе появлялся «Fermenter»
    /// посреди русских названий. У Piece токен есть всегда.
    /// </summary>
    static string PieceName(GameObject prefab, string fallback) {
        var piece = prefab.GetComponent<Piece>();
        return piece != null && !string.IsNullOrEmpty(piece.m_name) ? piece.m_name : fallback;
    }

    static void Remember(string station, string token, GameObject prefab = null) {
        if (string.IsNullOrEmpty(station)) return;
        if (prefab != null && !StationIcons.ContainsKey(station)) {
            var piece = prefab.GetComponent<Piece>();
            if (piece != null && piece.m_icon != null) StationIcons[station] = piece.m_icon;
        }
        if (string.IsNullOrEmpty(token)) return;
        if (StationNames.ContainsKey(station)) return;
        StationNames[station] = Localization.instance.Localize(token);
    }
}

}
