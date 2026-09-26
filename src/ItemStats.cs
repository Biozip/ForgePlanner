using System.Collections.Generic;
using Forgeplan.Ui;
using UnityEngine;

namespace Forgeplan {

/// <summary>Какое значение лучше: большее, меньшее или сравнивать нечего.</summary>
public enum Better { Higher, Lower, None }

/// <summary>Одна строка карточки: «Рубящий 36».</summary>
public class Stat {
    public string Key;       // по нему строки двух карточек встают друг напротив друга
    public int Order;        // порядок строк, общий для обеих карточек
    public string Label;
    public float Value;      // для сравнения; у текстовых строк не используется
    public string Text;      // как показать значение
    public Better Dir;
    /// <summary>Как показать разницу: тем же форматом, что и значение.</summary>
    public System.Func<float, string> Delta;
}

/// <summary>
/// Характеристики вещи — те же, что показывает подсказка игры, и посчитанные
/// теми же методами игры: GetDamage, GetArmor, GetBaseBlockPower и прочие
/// уже учитывают качество и уровень мира. Своей арифметики здесь нет
/// намеренно: формула доплаты за уровни однажды уже разошлась с игрой
/// (Versions.md сайта, 1.7.0), и повторять её для урона незачем.
///
/// Сравнение — дело карточки, здесь только чтение. Но направление («больше —
/// лучше» или наоборот) записано прямо у строки: оно часть смысла
/// характеристики, а не оформления. Вес, штраф к скорости и цена удара —
/// меньше значит лучше, и перепутать это легче всего.
/// </summary>
public static class ItemStats {
    /// <summary>Вещь, которую можно надеть или взять в руки.</summary>
    public static bool Wearable(ItemDrop.ItemData.ItemType t) {
        switch (t) {
            case ItemDrop.ItemData.ItemType.OneHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
            case ItemDrop.ItemData.ItemType.Bow:
            case ItemDrop.ItemData.ItemType.Attach_Atgeir:
            case ItemDrop.ItemData.ItemType.Torch:
            case ItemDrop.ItemData.ItemType.Tool:
            case ItemDrop.ItemData.ItemType.Shield:
            case ItemDrop.ItemData.ItemType.Helmet:
            case ItemDrop.ItemData.ItemType.Chest:
            case ItemDrop.ItemData.ItemType.Legs:
            case ItemDrop.ItemData.ItemType.Shoulder:
            case ItemDrop.ItemData.ItemType.Utility:
            case ItemDrop.ItemData.ItemType.Trinket:
            case ItemDrop.ItemData.ItemType.Ammo:
                return true;
        }
        return false;
    }

    static bool InHands(ItemDrop.ItemData.ItemType t) {
        switch (t) {
            case ItemDrop.ItemData.ItemType.OneHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
            case ItemDrop.ItemData.ItemType.Bow:
            case ItemDrop.ItemData.ItemType.Attach_Atgeir:
            case ItemDrop.ItemData.ItemType.Torch:
            case ItemDrop.ItemData.ItemType.Tool:
                return true;
        }
        return false;
    }

    /// <summary>
    /// С чем сравнивать: что надето в тот же слот.
    ///
    /// Оружие сравнивается с тем, что в руках, а не строго с тем же типом:
    /// игрок с мечом, наводящий на топор, спрашивает «топор или мой меч», а
    /// не «топор или мой прошлый топор». Лук игра держит в левой руке, поэтому
    /// правая пустая — ещё не значит, что в руках ничего нет.
    /// </summary>
    public static ItemDrop.ItemData Equipped(Player p, ItemDrop.ItemData.ItemType t) {
        if (p == null) return null;
        if (InHands(t)) {
            if (p.m_rightItem != null) return p.m_rightItem;
            var left = p.m_leftItem;
            if (left != null && InHands(left.m_shared.m_itemType)) return left;
            return null;
        }
        switch (t) {
            case ItemDrop.ItemData.ItemType.Shield:
                var l = p.m_leftItem;
                return l != null && l.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield
                    ? l : null;
            case ItemDrop.ItemData.ItemType.Helmet: return p.m_helmetItem;
            case ItemDrop.ItemData.ItemType.Chest: return p.m_chestItem;
            case ItemDrop.ItemData.ItemType.Legs: return p.m_legItem;
            case ItemDrop.ItemData.ItemType.Shoulder: return p.m_shoulderItem;
            case ItemDrop.ItemData.ItemType.Utility: return p.m_utilityItem;
            case ItemDrop.ItemData.ItemType.Trinket: return p.m_trinketItem;
            case ItemDrop.ItemData.ItemType.Ammo: return p.m_ammoItem;
        }
        return null;
    }

    /* ——— порядок строк: одинаковый у обеих карточек ——— */

    static readonly string[] OrderKeys = {
        "damage", "slash", "blunt", "pierce", "fire", "frost", "lightning",
        "poison", "spirit", "chop", "pickaxe", "tooltier",
        "armor", "block", "parry", "deflect", "knockback", "backstab",
        "stamina", "eitr", "health",
        "eitrregen", "adrenaline",
        "res_header", "res_blunt", "res_slash", "res_pierce", "res_fire",
        "res_frost", "res_lightning", "res_poison", "res_spirit",
        "movement", "durability", "weight",
        "effect", "adreffect", "set",
    };

    static int OrderOf(string key) {
        int i = System.Array.IndexOf(OrderKeys, key);
        return i < 0 ? OrderKeys.Length : i;
    }

    /* ——— формат ——— */

    static readonly System.Globalization.CultureInfo Inv =
        System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>Целое — без дробной части, дробное — с одним знаком.</summary>
    static string Num(float v) {
        float r = Mathf.Round(v * 10f) / 10f;
        return Mathf.Approximately(r, Mathf.Round(r))
            ? Mathf.RoundToInt(r).ToString(Inv)
            : r.ToString("0.0", Inv);
    }

    static string Signed(string body, float v) {
        return (v > 0 ? "+" : v < 0 ? "−" : "") + body;
    }

    static string Pct(float v) { return Num(v * 100f) + "%"; }

    static Stat Numeric(List<Stat> list, string key, string label, float value,
                        Better dir, System.Func<float, string> fmt = null) {
        if (fmt == null) fmt = Num;
        var s = new Stat {
            Key = key, Order = OrderOf(key), Label = label, Value = value,
            Dir = dir, Delta = d => Signed(fmt(Mathf.Abs(d)), d),
        };
        s.Text = key == "movement" || key == "eitrregen"
            ? Signed(fmt(Mathf.Abs(value)), value) : fmt(value);
        list.Add(s);
        return s;
    }

    static void Words(List<Stat> list, string key, string label, string text) {
        if (string.IsNullOrEmpty(text)) return;
        list.Add(new Stat { Key = key, Order = OrderOf(key), Label = label,
                            Text = text, Dir = Better.None });
    }

    static string Localized(StatusEffect se) {
        if (se == null || string.IsNullOrEmpty(se.m_name)) return null;
        return Localization.instance.Localize(se.m_name);
    }

    /* ——— чтение ——— */

    /// <summary>
    /// Характеристики вещи данного качества на данном уровне мира.
    ///
    /// Строки попадают в список, только если не пустые: у кожаной куртки нет
    /// урона, и строка «Урон 0» только шумит. Пустую строку против непустой
    /// карточка допишет сама — при сравнении недостающее считается нулём.
    /// </summary>
    public static List<Stat> Read(ItemDrop.ItemData d, int quality, float worldLevel) {
        var list = new List<Stat>();
        if (d == null || d.m_shared == null) return list;
        var s = d.m_shared;
        var t = s.m_itemType;
        quality = Mathf.Max(1, quality);

        bool hands = InHands(t) || t == ItemDrop.ItemData.ItemType.Ammo;
        if (hands) {
            var dmg = d.GetDamage(quality, worldLevel);
            Dmg(list, "damage", L.StDamage, dmg.m_damage);
            Dmg(list, "slash", L.StSlash, dmg.m_slash);
            Dmg(list, "blunt", L.StBlunt, dmg.m_blunt);
            Dmg(list, "pierce", L.StPierce, dmg.m_pierce);
            Dmg(list, "fire", L.StFire, dmg.m_fire);
            Dmg(list, "frost", L.StFrost, dmg.m_frost);
            Dmg(list, "lightning", L.StLightning, dmg.m_lightning);
            Dmg(list, "poison", L.StPoison, dmg.m_poison);
            Dmg(list, "spirit", L.StSpirit, dmg.m_spirit);
            Dmg(list, "chop", L.StChop, dmg.m_chop);
            Dmg(list, "pickaxe", L.StPickaxe, dmg.m_pickaxe);
            if (s.m_toolTier > 0 && (dmg.m_chop > 0 || dmg.m_pickaxe > 0))
                Numeric(list, "tooltier", L.StToolTier, s.m_toolTier, Better.Higher);
        }

        if (hands || t == ItemDrop.ItemData.ItemType.Shield) {
            float block = d.GetBaseBlockPower(quality);
            if (block > 0) Numeric(list, "block", L.StBlock, block, Better.Higher);
            if (s.m_timedBlockBonus > 1f)
                Numeric(list, "parry", L.StParry, s.m_timedBlockBonus, Better.Higher,
                        v => "×" + Num(v));
        }
        if (t == ItemDrop.ItemData.ItemType.Shield) {
            float deflect = d.GetDeflectionForce(quality);
            if (deflect > 0) Numeric(list, "deflect", L.StDeflect, deflect, Better.Higher);
        }
        if (InHands(t)) {
            if (s.m_attackForce > 0)
                Numeric(list, "knockback", L.StKnockback, s.m_attackForce, Better.Higher);
            if (s.m_backstabBonus > 1f)
                Numeric(list, "backstab", L.StBackstab, s.m_backstabBonus, Better.Higher,
                        v => "×" + Num(v));
            var a = s.m_attack;
            if (a != null) {
                if (a.m_attackStamina > 0)
                    Numeric(list, "stamina", L.StStamina, a.m_attackStamina, Better.Lower);
                if (a.m_attackEitr > 0)
                    Numeric(list, "eitr", L.StEitr, a.m_attackEitr, Better.Lower);
                if (a.m_attackHealth > 0)
                    Numeric(list, "health", L.StHealth, a.m_attackHealth, Better.Lower);
            }
        }

        float armor = d.GetArmor(quality, worldLevel);
        if (armor > 0) Numeric(list, "armor", L.StArmor, armor, Better.Higher);

        if (!Mathf.Approximately(s.m_eitrRegenModifier, 0f))
            Numeric(list, "eitrregen", L.StEitrRegen, s.m_eitrRegenModifier, Better.Higher, Pct);

        if (t == ItemDrop.ItemData.ItemType.Trinket && s.m_maxAdrenaline > 0)
            // Меньше — лучше: столько адреналина нужно набрать до срабатывания.
            Numeric(list, "adrenaline", L.StAdrenaline, s.m_maxAdrenaline, Better.Lower);

        Resistances(list, s.m_damageModifiers);

        if (!Mathf.Approximately(s.m_movementModifier, 0f))
            // Штраф хранится отрицательным: −0,05 — это «−5 %». Больше — лучше.
            Numeric(list, "movement", L.StMovement, s.m_movementModifier, Better.Higher, Pct);
        if (s.m_useDurability)
            Numeric(list, "durability", L.StDurability, d.GetMaxDurability(quality), Better.Higher);
        if (s.m_weight > 0)
            Numeric(list, "weight", L.StWeight, s.m_weight, Better.Lower);

        Words(list, "effect", L.StEffect, Localized(s.m_equipStatusEffect));
        if (t == ItemDrop.ItemData.ItemType.Trinket)
            Words(list, "adreffect", L.StEffect, Localized(s.m_fullAdrenalineSE));
        if (s.m_setStatusEffect != null && s.m_setSize > 1) {
            var name = Localized(s.m_setStatusEffect);
            if (name != null) Words(list, "set", L.StSet, name + " (" + s.m_setSize + ")");
        }
        return list;
    }

    static void Dmg(List<Stat> list, string key, string label, float v) {
        if (v > 0) Numeric(list, key, label, v, Better.Higher);
    }

    /* ——— сопротивления ——— */

    /// <summary>Ступени сопротивления по возрастанию пользы. Игра хранит их
    /// перечислением без порядка — «Ignore» стоит раньше «VeryResistant», — так
    /// что сравнивать сырые числа нельзя.</summary>
    public static int Rank(HitData.DamageModifier m) {
        switch (m) {
            case HitData.DamageModifier.VeryWeak: return -3;
            case HitData.DamageModifier.Weak: return -2;
            case HitData.DamageModifier.SlightlyWeak: return -1;
            case HitData.DamageModifier.SlightlyResistant: return 1;
            case HitData.DamageModifier.Resistant: return 2;
            case HitData.DamageModifier.VeryResistant: return 3;
            case HitData.DamageModifier.Immune: return 4;
            case HitData.DamageModifier.Ignore: return 4;
        }
        return 0;
    }

    public static string RankName(int r) {
        switch (r) {
            case -3: return L.ModVeryWeak;
            case -2: return L.ModWeak;
            case -1: return L.ModSlightlyWeak;
            case 1: return L.ModSlightlyResistant;
            case 2: return L.ModResistant;
            case 3: return L.ModVeryResistant;
            case 4: return L.ModImmune;
        }
        return L.ModNormal;
    }

    static string DamageName(HitData.DamageType t) {
        switch (t) {
            case HitData.DamageType.Blunt: return L.StBlunt;
            case HitData.DamageType.Slash: return L.StSlash;
            case HitData.DamageType.Pierce: return L.StPierce;
            case HitData.DamageType.Fire: return L.StFire;
            case HitData.DamageType.Frost: return L.StFrost;
            case HitData.DamageType.Lightning: return L.StLightning;
            case HitData.DamageType.Poison: return L.StPoison;
            case HitData.DamageType.Spirit: return L.StSpirit;
        }
        return null;
    }

    static void Resistances(List<Stat> list, List<HitData.DamageModPair> mods) {
        if (mods == null) return;
        foreach (var pair in mods) {
            var name = DamageName(pair.m_type);
            if (name == null) continue;
            int r = Rank(pair.m_modifier);
            if (r == 0) continue;
            var key = "res_" + pair.m_type.ToString().ToLowerInvariant();
            list.Add(new Stat {
                Key = key, Order = OrderOf(key), Label = "  " + name, Value = r,
                Text = RankName(r), Dir = Better.Higher,
                Delta = d => d > 0 ? "+" : d < 0 ? "−" : "=",
            });
        }
    }

    /// <summary>Строка «чего нет» для сравнения: ноль у чисел, «обычно» у
    /// сопротивлений. Нужна, когда характеристика есть только у одной вещи.</summary>
    public static Stat Absent(Stat like) {
        bool res = like.Key.StartsWith("res_");
        return new Stat {
            Key = like.Key, Order = like.Order, Label = like.Label, Value = 0f,
            Dir = like.Dir, Delta = like.Delta,
            Text = like.Dir == Better.None ? "—" : res ? L.ModNormal : "—",
        };
    }

    /// <summary>Заголовок блока сопротивлений — если в нём есть хоть одна строка.</summary>
    public static Stat ResistHeader() {
        return new Stat { Key = "res_header", Order = OrderOf("res_header"),
                          Label = L.StResist, Text = "", Dir = Better.None };
    }
}

}
