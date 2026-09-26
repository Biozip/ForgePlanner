using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Forgeplan.Ui;
using HarmonyLib;
using UnityEngine;

namespace Forgeplan {

/// <summary>
/// Точка входа.
///
/// Цель — BepInEx 5.4.x, тот самый, что приезжает в BepInExPack_Valheim от
/// denikson: почти вся сцена сидит на нём, и без него мод у игрока просто не
/// запустится. Поэтому наследуемся от BaseUnityPlugin (BepInEx 5), а не от
/// шестёрки — у той другой API и другой загрузчик.
/// </summary>
[BepInPlugin(Guid, "Forgeplan", Version)]
[BepInProcess("valheim.exe")]
public class ForgeplanPlugin : BaseUnityPlugin {
    public const string Guid = "dev.forgeplanner.forgeplan";
    /// <summary>Из файла VERSION: сборка кладёт его в Version.g.cs и сверяет
    /// с верхней записью Versions.md. Руками здесь ничего не правят.</summary>
    public const string Version = Build.Version;

    internal static ManualLogSource Log;
    internal static readonly PlannerPanel Panel = new PlannerPanel();

    ConfigEntry<KeyboardShortcut> _hotkey;
    ConfigEntry<float> _chestRadius;
    ConfigEntry<bool> _includeChests;
    ConfigEntry<bool> _selfTest;
    ConfigEntry<bool> _russian;
    ConfigEntry<bool> _noSpoilers;
    ConfigEntry<bool> _stationButton;
    ConfigEntry<float> _stationX, _stationY;
    ConfigEntry<Ui.StationButton.Corner> _stationCorner;
    ConfigEntry<bool> _pinEnabled;
    ConfigEntry<Ui.PinHud.Corner> _pinCorner;
    ConfigEntry<float> _pinX, _pinY, _pinInterval;
    ConfigEntry<int> _pinRows;
    ConfigEntry<bool> _cardEnabled, _cardCompare;

    Harmony _harmony;

    void Awake() {
        Log = Logger;

        // Ключи и пояснения по-английски, хотя весь остальной проект русский.
        // Конфиг читают глазами в текстовом файле, и площадки с модами
        // международные: игрок из Осло, открывший сплошную кириллицу, не
        // сможет даже сменить горячую клавишу.
        //
        // Переименование ломает чужие настройки, поэтому сделано ровно один
        // раз — перед первой публикацией, когда «чужих настроек» в мире ещё
        // не существует. Отсюда и MAJOR у этого выпуска.
        _hotkey = Config.Bind("General", "Open", new KeyboardShortcut(KeyCode.F7),
            "Hotkey that opens the planner.");
        _includeChests = Config.Bind("Stock", "CountChests", true,
            "Count nearby chests, not just your backpack.");
        _chestRadius = Config.Bind("Stock", "Radius", 20f,
            "How far a chest counts as yours, in metres.");
        // Английский по умолчанию: площадки с модами международные. Названия
        // предметов сюда не относятся — они приходят из Localization игры и
        // следуют её языку, подменять их мод не может и не должен.
        _russian = Config.Bind("General", "Russian", false,
            "Window labels in Russian. Also toggled by the button in the header.");
        L.Ru = _russian.Value;

        // По умолчанию включено, и это не осторожность, а единственное
        // разумное умолчание: кто боссов уже побил, ничего не теряет — у него
        // открыто всё. А кто не побил, тому список из тысячи позиций
        // пересказывает игру вперёд, и выключить он это уже не сможет.
        _noSpoilers = Config.Bind("General", "HideUnreached", true,
            "Lock biomes whose boss is still alive. Meadows, Black Forest and "
            + "Ocean are always open. Also toggled in the Settings window.");
        Progress.Enabled = _noSpoilers.Value;

        _stationButton = Config.Bind("Station", "Button", true,
            "Show the ForgePlanner button on the crafting panel.");
        // Положение вынесено в конфиг не ради настройки, а ради ремонта:
        // панель крафта чужая, и следующее дополнение Valheim может её
        // подвинуть. Тогда кнопку можно вернуть на место, не дожидаясь
        // нового выпуска мода.
        _stationCorner = Config.Bind("Station", "Corner",
            Ui.StationButton.Corner.BottomRight,
            "Which side of the panel the button's shelf sits on. The shelf is "
            + "always below the panel, so this reads as left or right.");
        _stationX = Config.Bind("Station", "OffsetX", 14f,
            "Shelf offset from the panel edge, in points.");
        _stationY = Config.Bind("Station", "DropDown", 0f,
            "How far below the panel edge to drop the shelf, in points. "
            + "0 keeps it flush with the panel.");
        Ui.StationButton.Enabled = _stationButton.Value;
        Ui.StationButton.Where = _stationCorner.Value;
        Ui.StationButton.Offset = new Vector2(_stationX.Value, _stationY.Value);

        // Закреплённый список. Положение и частота вынесены наружу по той же
        // причине, что у кнопки у станка: свободное место на экране зависит от
        // размера интерфейса и от того, что игрок себе включил, а частота — от
        // того, сколько у него сундуков в базе. Угадать за всех нельзя.
        _pinEnabled = Config.Bind("Pin", "Enabled", true,
            "Keep pinned materials on screen after the planner is closed.");
        _pinCorner = Config.Bind("Pin", "Corner", Ui.PinHud.Corner.TopRight,
            "Which corner of the screen the pinned list sits in.");
        // Умолчания подобраны в игре: панель ставится под миникартой с
        // зазором. Дальше их правит не рука, а перетаскивание.
        _pinX = Config.Bind("Pin", "OffsetX", 42f,
            "Distance from the side of the screen, in points. Dragging the "
            + "panel writes this.");
        _pinY = Config.Bind("Pin", "OffsetY", 268f,
            "Distance from the top or bottom of the screen, in points. The "
            + "default clears the minimap.");
        _pinInterval = Config.Bind("Pin", "RefreshSeconds", 1f,
            "How often the remaining amounts are recounted. Counting chests "
            + "walks the loaded scene, so this is not done every frame.");
        _pinRows = Config.Bind("Pin", "MaxRows", 8,
            "How many materials to list before collapsing the rest into a count.");
        Ui.PinHud.Enabled = _pinEnabled.Value;
        Ui.PinHud.Where = _pinCorner.Value;
        Ui.PinHud.Offset = new Vector2(_pinX.Value, _pinY.Value);
        Ui.PinHud.Interval = _pinInterval.Value;
        Ui.PinHud.MaxRows = _pinRows.Value;
        Pin.Changed = Ui.PinHud.Invalidate;
        // Панель таскают мышью, а живёт положение в конфиге: иначе его
        // пришлось бы подбирать заново каждый запуск.
        Ui.PinHud.OnMoved = p => { _pinX.Value = p.x; _pinY.Value = p.y; };

        // Карточка предмета. Обе настройки дублируются кнопками в окне
        // «Настройки», а сюда пишутся оттуда же.
        _cardEnabled = Config.Bind("Tooltip", "Enabled", true,
            "Show an item card with stats and recipe when hovering a row in "
            + "the planner.");
        _cardCompare = Config.Bind("Tooltip", "Compare", true,
            "Compare the hovered item with what you have equipped in the same "
            + "slot. Off shows the item's own stats only.");
        ItemCard.Enabled = _cardEnabled.Value;
        ItemCard.Compare = _cardCompare.Value;

        _selfTest = Config.Bind("Debug", "SelfTest", false,
            "On entering a world, build the catalogue and dump the conversion "
            + "table and a few calculated orders to the log. Used to check the "
            + "mod against the website after a game update.");

        // Подбор спрайтов фона — единственное место, где мод угадывает по
        // именам ассетов. Список найденного уходит в лог вместе с самопроверкой.
        UiKit.Verbose = _selfTest.Value;
        Panel.OnLanguageChanged = ru => { _russian.Value = ru; };
        Panel.OnSpoilersChanged = on => { _noSpoilers.Value = on; };
        Panel.OnChestsChanged = on => { _includeChests.Value = on; };
        Panel.OnCardChanged = (on, compare) => {
            _cardEnabled.Value = on;
            _cardCompare.Value = compare;
        };
        Panel.Chests = _includeChests.Value;

        _harmony = new Harmony(Guid);
        _harmony.PatchAll();
        Log.LogInfo("Форджплан " + Version + " загружен");
    }

    void OnDestroy() {
        if (_harmony != null) _harmony.UnpatchSelf();
    }

    void Update() {
        // Самопроверка ждёт загруженного мира: до него Build() всё равно вернёт
        // false, а две проверки на null каждый кадр ничего не стоят.
        if (_selfTest.Value && !SelfTest.Done && GameData.Build()) SelfTest.Run();

        // Переключатель в окне настроек меняет Panel.Chests; запас читается
        // отсюда, поэтому значение переносится каждый кадр, а не при открытии.
        if (Panel.Visible) Stock.IncludeChests = Panel.Chests;

        // Настройку про сундуки закреплённый список не читает вовсе: он
        // всегда считает только рюкзак, см. Stock.Count(bool).
        Ui.PinHud.Tick(Panel.Visible);

        if (Panel.Visible) {
            // Меню Esc и инвентарь главнее: два окна, дерущихся за курсор, —
            // худшее из возможных поведений.
            if (GameGuiReallyOpen()) Panel.Hide();
            else if (Input.GetKeyDown(KeyCode.Escape)) Panel.Hide();
            else Panel.Tick();
        }

        if (OpenRequest > 0) {
            OpenRequest--;
            if (!Panel.Visible && !IsGameBusy()) {
                OpenRequest = 0;
                OpenPanel();
            }
        }

        if (!_hotkey.Value.IsDown()) return;
        if (!Panel.Visible && IsGameBusy()) return;

        OpenPanel();
    }

    void OpenPanel() {
        Panel.Chests = _includeChests.Value;
        Stock.ChestRadius = _chestRadius.Value;
        Panel.Toggle();

        // Закрылись — вернуть камере мышь сразу, не дожидаясь следующего кадра.
        if (!Panel.Visible && GameCamera.instance != null)
            GameCamera.instance.UpdateMouseCapture();
    }

    /// <summary>
    /// Пока окно открыто, курсор наш. Патч на UpdateMouseCapture не даёт камере
    /// его отобрать, но состояние курсора никто не выставит за нас — игра ведь
    /// про это окно не знает.
    /// </summary>
    /// <summary>
    /// Просьба открыть окно от кнопки у станка: сколько кадров ещё ждать,
    /// пока игра закроет своё окно. Ноль — никто не просил.
    ///
    /// Счётчик, а не флаг, намеренно: если окно игры почему-то не закроется,
    /// просьба должна сгореть сама, а не висеть до конца игры.
    /// </summary>
    internal static int OpenRequest;

    void LateUpdate() {
        if (!Panel.Visible) return;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>
    /// Пока окно открыто, мод отвечает игре, что открыт интерфейс станка —
    /// см. PretendGuiOpen. Собственным проверкам эта подделка мешает: окно
    /// увидело бы "открыт инвентарь" и тут же закрыло само себя. Поэтому на
    /// время своих проверок она выключается.
    /// </summary>
    internal static bool SpoofGui = true;

    /// <summary>
    /// Блокировать ли действие этого игрока. Проверка на своего обязательна:
    /// патчи стоят на методах экземпляра Player, а в мире их столько, сколько
    /// людей на сервере.
    /// </summary>
    internal static bool BlockFor(Player p) {
        return Panel.Visible && p != null && p == Player.m_localPlayer;
    }

    static bool GameGuiReallyOpen() {
        SpoofGui = false;
        try { return Menu.IsVisible() || InventoryGui.IsVisible(); }
        finally { SpoofGui = true; }
    }

    static bool IsGameBusy() {
        if (Player.m_localPlayer == null) return true;
        if (Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large) return true;
        if (GameGuiReallyOpen() || Console.IsVisible()) return true;
        if (TextViewer.instance != null && TextViewer.instance.IsVisible()) return true;
        return false;
    }
}

/// <summary>
/// Каталог собирается из живой игры, поэтому его нужно выбрасывать при выходе
/// из мира: в следующем ObjectDB это уже другие объекты, а держать ссылки на
/// выгруженные префабы — верный способ получить пустые иконки.
/// </summary>
[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Shutdown))]
static class ResetOnLeave {
    static void Postfix() {
        ForgeplanPlugin.Panel.Drop();
        ForgeplanPlugin.Panel.ResetFilters();
        Ui.PinHud.Drop();
        GameData.Reset();
        Trade.Reset();
        Progress.Reset();
        Planner.Cart.Clear();
        // Закреплённое уходит вместе с планом: идентификаторы те же, но
        // каталог за ними будет уже из другого мира, а «нужно ещё 40 железа»
        // от прошлого персонажа — подсказка хуже, чем никакой.
        Pin.Clear();
        SelfTest.Done = false;
    }
}

/// <summary>
/// Пока окно открыто, игра считает, что открыт интерфейс станка.
///
/// Это главный патч блокировки, и добрался я до него не сразу. Сначала гасил
/// ввод поимённо, каждый раз находя что-то ещё: имена методов обманывали.
/// Настоящий ответ нашёлся в разборе IL игры, и он короткий — калиток всего
/// две, и обе читают одну проверку:
///
///   Player.TakeInput        — хвост Player.Update: радиальное меню (эмоции),
///                             пояс, установка построек, сила стража;
///   PlayerController.Fixed- — движение, удар, блок, прыжок, обзор мышью.
///   Update / LateUpdate
///
/// Заодно выяснилось, почему патч на TakeInput в 0.9.0 не сделал ничего.
/// Методов с этим именем два. Player.TakeInput переопределяет
/// Character.TakeInput и зовётся виртуально — прямых вызовов у него ноль, по
/// метаданным он выглядит мёртвым. А PlayerController.TakeInput — другой
/// метод, однофамилец, и движение идёт через него.
///
/// Отвечая на саму проверку, мод закрывает обе калитки разом и не переписывает
/// у себя список из шестнадцати мест, где игра её спрашивает.
///
/// Цена честная: игра действительно поверит, что окно станка открыто. Сверх
/// выключения управления это ничего не делает, но мод рядом, завязанный на ту
/// же проверку, увидит то же самое.
/// </summary>
[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsVisible))]
static class PretendGuiOpen {
    static void Postfix(ref bool __result) {
        if (ForgeplanPlugin.SpoofGui && ForgeplanPlugin.Panel.Visible) __result = true;
    }
}

/// <summary>
/// Дальше — страховка к PretendGuiOpen: те же действия, погашенные напрямую.
///
/// После разбора IL стало видно, что они избыточны. Обе настоящие калитки игры
/// читают ту же проверку: <c>Player.TakeInput</c> закрывает хвост
/// <c>Player.Update</c> (радиальное меню, пояс, установка построек, сила
/// стража), <c>PlayerController</c> — движение, удар и обзор мышью. Подделка
/// закрывает обе разом.
///
/// Оставлены они потому, что цена им — проверка булева поля на кадр, а мои
/// рассуждения об этом коде ошибались трижды подряд. Но у каждого теперь есть
/// проверка на своего игрока: это методы экземпляра <c>Player</c>, и без неё
/// префикс гасил действия всех игроков в округе, а не только наши.
/// </summary>
[HarmonyPatch(typeof(Player), "PlayerAttackInput")]
static class NoAttacksWhileOpen {
    static bool Prefix(Player __instance) {
        return !ForgeplanPlugin.BlockFor(__instance);
    }
}

/// <summary>Строительство: клик по кнопке окна не должен ставить стену.</summary>
[HarmonyPatch(typeof(Player), "UpdatePlacement")]
static class NoBuildingWhileOpen {
    static void Prefix(Player __instance, ref bool takeInput) {
        if (ForgeplanPlugin.BlockFor(__instance)) takeInput = false;
    }
}

/// <summary>Быстрый пояс: цифры принадлежат окну, а не поясу.</summary>
[HarmonyPatch(typeof(Player), nameof(Player.UseHotbarItem))]
static class NoHotbarWhileOpen {
    static bool Prefix(Player __instance) {
        return !ForgeplanPlugin.BlockFor(__instance);
    }
}

[HarmonyPatch(typeof(Player), "UpdateCrouch")]
static class NoCrouchWhileOpen {
    static bool Prefix(Player __instance) {
        return !ForgeplanPlugin.BlockFor(__instance);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.StartGuardianPower))]
static class NoGuardianPowerWhileOpen {
    static bool Prefix(Player __instance, ref bool __result) {
        if (!ForgeplanPlugin.BlockFor(__instance)) return true;
        __result = false;
        return false;
    }
}

/// <summary>
/// Движение. Единственное, что стоит здесь не ради страховки: ходьба пережила
/// и <c>TakeInput</c>, и состояние интерфейса — её проверяли живьём.
///
/// <c>SetControls</c> — точка, куда стекается управление персонажем целиком:
/// направление, удар, блок, прыжок, бег, уворот. Своего <c>__instance</c> тут
/// не проверяем: метод зовётся только из <c>PlayerController</c>, а тот один и
/// принадлежит нашему игроку.
///
/// Ходить с открытым окном сперва было можно намеренно. Не прижилось: фокус со
/// строки поиска слетает незаметно, и буквы уходят персонажу — то инвентарь
/// откроется посреди слова, то «W» отправит гулять. Спокойно печатать нужнее.
/// </summary>
[HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
static class BlockControlsWhileOpen {
    static void Prefix(ref Vector3 movedir, ref bool attack, ref bool attackHold,
                       ref bool secondaryAttack, ref bool secondaryAttackHold,
                       ref bool block, ref bool blockHold, ref bool jump,
                       ref bool crouch, ref bool run, ref bool autoRun, ref bool dodge) {
        if (!ForgeplanPlugin.Panel.Visible) return;
        movedir = Vector3.zero;
        attack = attackHold = secondaryAttack = secondaryAttackHold = false;
        block = blockHold = jump = crouch = run = autoRun = dodge = false;
    }
}

/// <summary>
/// Пока окно открыто, камера стоит.
///
/// Иначе колесо мыши над списком не прокручивает его, а приближает камеру:
/// игра читает колесо напрямую, мимо системы событий интерфейса, и о том, что
/// поверх неё открыто чужое окно, не знает. Камере при этом всё равно нечего
/// делать — персонаж не двигается.
/// </summary>
[HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
static class FreezeCameraWhileOpen {
    static bool Prefix() {
        return !ForgeplanPlugin.Panel.Visible;
    }
}

/// <summary>
/// Камера прячет и захватывает курсор каждый кадр, ориентируясь на свои окна.
/// Про наше она не знает, поэтому пока оно открыто — просто не даём ей
/// вмешиваться. Именно из-за этого у окна на IMGUI курсор освобождался только
/// вместе с меню Esc.
/// </summary>
[HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
static class FreeCursorWhileOpen {
    static bool Prefix() {
        return !ForgeplanPlugin.Panel.Visible;
    }
}

}
