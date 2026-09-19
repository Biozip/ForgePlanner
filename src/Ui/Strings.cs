namespace Forgeplan.Ui {

/// <summary>
/// Подписи самого окна.
///
/// Названия предметов, станков и биомов сюда не входят: они приходят из
/// `Localization` игры и следуют её языку. Переключать их мод не может и не
/// должен — иначе в одном списке окажутся «Бронзовый топор» и «Bronze Axe».
///
/// А вот собственные подписи мода до сих пор были русскими намертво, и на
/// английской игре окно выходило двуязычным. Отсюда переключатель и отсюда же
/// умолчание: английский, потому что площадки с модами международные, а
/// русскоязычный игрок переключит один раз и запомнится в конфиг.
///
/// «ForgePlanner» не переводится ни при каком языке: это имя.
/// </summary>
public static class L {
    /// <summary>Русский вместо английского. Ставит плагин из конфига.</summary>
    public static bool Ru;

    static string T(string en, string ru) { return Ru ? ru : en; }

    public static string Title { get { return "ForgePlanner"; } }
    /// <summary>Подпись кнопки — язык, на который переключит, а не текущий.</summary>
    public static string OtherLang { get { return Ru ? "EN" : "RU"; } }

    public static string Clear { get { return T("Clear", "Очистить"); } }
    public static string Settings { get { return T("Settings", "Настройки"); } }
    public static string Info { get { return T("About", "Инфо"); } }
    public static string Site { get { return T("Website", "Сайт"); } }
    public static string OpenLink { get { return T("Open this link?", "Перейти по ссылке?"); } }
    public static string Open { get { return T("Open", "Открыть"); } }
    public static string Cancel { get { return T("Cancel", "Отмена"); } }
    public static string LinkLeaves {
        get {
            return T("The game keeps running; the link opens in your browser — "
                     + "or in the Steam overlay, if you started the game through it.",
                     "Игра останется запущенной, ссылка откроется в браузере — "
                     + "или в оверлее Steam, если игра запущена через него.");
        }
    }
    public static string NoAddress { get { return T("no address yet", "адреса пока нет"); } }
    public static string Author { get { return T("Author", "Автор"); } }
    public static string SiteLine {
        get {
            return T("A web version of the planner lives on the ForgePlanner site.",
                     "Веб-версия планировщика живёт на сайте ForgePlanner.");
        }
    }
    public static string StoreLine {
        get {
            return T("The mod is published on Thunderstore.",
                     "Мод выложен на Thunderstore.");
        }
    }
    public static string DataLine {
        get {
            return T("Recipes, stations, item icons and the font all come from "
                     + "your own copy of the game. No Valheim files are bundled "
                     + "with the mod or redistributed by it.",
                     "Рецепты, станки, иконки предметов и шрифт мод берёт прямо "
                     + "из вашей копии игры. Файлов Valheim он с собой не везёт "
                     + "и не раздаёт.");
        }
    }
    public static string GameVersion { get { return T("Game: ", "Игра: "); } }
    public static string Done { get { return T("Done", "Готово"); } }

    public static string NoSpoilers {
        get { return T("Hide what you have not reached", "Скрывать недостигнутое"); }
    }
    public static string NoSpoilersHint {
        get {
            return T("Biomes open as their bosses fall. Meadows, Black Forest and "
                     + "the Ocean are open from the start. Switch this off and the "
                     + "catalogue shows the whole game at once.",
                     "Биомы открываются по мере побед над боссами. Луга, Чёрный лес "
                     + "и Океан открыты сразу. Выключите — и каталог покажет всю "
                     + "игру целиком.");
        }
    }
    public static string BiomesOpen { get { return T("Biomes open: ", "Открыто биомов: "); } }
    public static string OfNine { get { return T(" of ", " из "); } }
    public static string UseChests {
        get { return T("Count nearby chests", "Считать сундуки рядом"); }
    }
    public static string UseChestsHint {
        get {
            return T("«Inventory» then subtracts what already lies in chests around you.",
                     "«Инвентарь» тогда вычитает и то, что лежит в сундуках вокруг.");
        }
    }
    public static string Locked { get { return T("not yet reached", "ещё не открыт"); } }
    public static string Close { get { return T("Close", "Закрыть"); } }

    public static string Biome { get { return T("Biome", "Биом"); } }
    public static string AllBiomes { get { return T("All biomes", "Все биомы"); } }
    public static string Items { get { return T("Items", "Предметы"); } }
    public static string Search { get { return T("Search", "Поиск"); } }
    public static string SearchHint {
        get { return T("bow, pickaxe, bronze…", "лук, кирка, бронза…"); }
    }
    public static string Found { get { return T("Found: ", "Найдено: "); } }
    public static string OutOf { get { return T(" of ", " из "); } }
    public static string Filtered {
        get {
            return T("filters are hiding the rest",
                     "остальное скрыто фильтрами");
        }
    }

    public static string All { get { return T("All", "Всё"); } }
    public static string Weapons { get { return T("Weapons", "Оружие"); } }
    public static string Armour { get { return T("Armour", "Броня"); } }
    public static string Tools { get { return T("Tools", "Инструменты"); } }
    public static string Food { get { return T("Food", "Еда"); } }
    public static string Materials { get { return T("Materials", "Материалы"); } }
    public static string Buildings { get { return T("Buildings", "Постройки"); } }
    public static string Other { get { return T("Other", "Прочее"); } }

    public static string CraftPlan { get { return T("Craft plan", "План крафта"); } }
    public static string TotalItems { get { return T("Items total: ", "Всего предметов: "); } }
    public static string ToGather { get { return T("To gather", "Нужно собрать"); } }
    public static string Processing { get { return T("Processing", "Переработка"); } }
    public static string StationsNeeded { get { return T("Stations", "Станки"); } }
    public static string Metres { get { return T(" m", " м"); } }
    public static string NotNearby { get { return T("none nearby", "поблизости нет"); } }
    public static string AddToPlan { get { return T("Add to plan", "Добавить в план"); } }
    public static string LevelShort { get { return T("lv ", "ур. "); } }
    public static string NeedLevel { get { return T("need lv ", "нужен ур. "); } }

    public static string Raw { get { return T("Raw", "Сырьё"); } }
    public static string Recipes { get { return T("Recipes", "Рецепты"); } }
    public static string Coal { get { return T("Coal", "Уголь"); } }
    public static string Firewood { get { return T("Firewood", "Дрова"); } }
    public static string Inventory { get { return T("Inventory", "Инвентарь"); } }

    public static string ByHand { get { return T("By hand", "Руками"); } }
    public static string Pieces { get { return T(" pcs · ", " шт · "); } }
    public static string Loads { get { return T(" · loads ", " · загрузок "); } }

    public static string HintEmpty {
        get {
            return T("The list is empty. Add something from the catalogue — "
                     + "the «+» button, or a double-click on the row.",
                     "Список пуст. Добавьте что-нибудь из каталога: кнопкой «+» "
                     + "или двойным щелчком по строке.");
        }
    }
    public static string HintProcessing {
        get {
            return T("Processing time assumes one station working without a break. "
                     + "Loads — how many batches of metal go into the furnace.",
                     "Время переработки указано, если работать без перерыва. "
                     + "Загрузка — сколько заходов металла уйдёт в печь.");
        }
    }
    public static string HintRaw {
        get {
            return T("«Raw» — counted all the way down to ore, wood and hide.",
                     "«Сырьё» — считаем до руды, дерева и шкур.");
        }
    }
    public static string HintRecipes {
        get {
            return T("«Recipes» — what the workbench asks for, without breaking parts down.",
                     "«Рецепты» — как написано в верстаке, без разбора составляющих.");
        }
    }

    /// <summary>Часы, минуты, секунды. Planner.Time зовёт это.</summary>
    public static string Hours { get { return T(" h", " ч"); } }
    public static string Minutes { get { return T(" min", " мин"); } }
    public static string Seconds { get { return T(" s", " с"); } }
}

}
