using System.Collections.Generic;
using UnityEngine;

public enum Slot { Weapon, Helmet, Armor }
public enum Faction { Legion, Dynasty, Heralds, ShadowGuild }

public class Item
{
    public string id, name;
    public Slot slot;
    public Faction faction;
    public int tier;      // 1..3
    public int style;     // helmet/armor look 0..3
    // 0 sword 1 axe 2 spear 3 hammer 4 dagger 5 katana 6 greatsword 7 scythe 8 halberd 9 mace 10 trident 11 nunchaku
    public int wtype;
    public float damage, reach, speed, crit;
    public int defense;
    public long price;
}


// a look for the hero: clothes, skin tone and a few extra details; armour and helmet go on top
public class Skin
{
    public string id, name, desc;
    public Color cloth, skinCol, accent, cape, eye;
    public bool glowEyes, metal;
    public int extra;
    public long price;
}

public static class Db
{
    public static readonly List<Item> All = new List<Item>();
    public static readonly Dictionary<string, Item> ById = new Dictionary<string, Item>();

    static readonly string[] TierName = { "Iron", "Steel", "Mythic" };
    static readonly float[] TierMul = { 1f, 1.7f, 2.6f };

    // base stats per weapon type, before the faction's own style is applied
    static readonly float[] Dmg = { 10, 14, 9, 18, 6, 11, 16, 13, 14, 12, 10, 8 };
    static readonly float[] Reach = { 1.7f, 1.6f, 2.4f, 1.5f, 1.1f, 1.9f, 2.2f, 2.3f, 2.6f, 1.4f, 2.5f, 1.5f };
    static readonly float[] Spd = { 1f, 0.8f, 0.95f, 0.6f, 1.5f, 1.25f, 0.7f, 0.85f, 0.75f, 0.9f, 1f, 1.6f };

    // each faction fights its own way
    //                                      Legion Dynasty Heralds Shadow
    static readonly float[] DmgMod   = { 1.30f, 0.90f, 1.00f, 1.00f };   // Legion: heavy hitters
    static readonly float[] SpdMod   = { 0.90f, 1.25f, 1.05f, 1.10f };   // Dynasty: fast combos
    static readonly float[] ReachMod = { 1.00f, 1.00f, 1.10f, 1.00f };   // Heralds: long, precise
    static readonly float[] CritMod  = { 0.05f, 0.08f, 0.22f, 0.12f };  // Heralds: precision = crits

    static readonly int[][] WTypes =
    {
        new[] { 0, 6, 3, 1, 8, 9 },    // Legion: gladius, greatsword, warhammer, axe, halberd, mace
        new[] { 11, 4, 0, 2, 7 },      // Dynasty: nunchaku, twin dirk, jian, guandao, moon scythe
        new[] { 5, 0, 2, 10, 4 },      // Heralds: katana, blade, lance, trident, dagger
        new[] { 4, 7, 0, 2, 10 },      // Shadow Guild: kris, scythe, fang, spear, trident
    };
    static readonly string[][] WNames =
    {
        new[] { "Gladius", "Imperator Greatsword", "Warhammer", "Legion Axe", "Legion Halberd", "Legion Mace" },
        new[] { "Dragon Nunchaku", "Twin Dirk", "Jian", "Guandao", "Moon Scythe" },
        new[] { "Dawn Katana", "Herald Blade", "Lance of Dawn", "Halo Trident", "Light Dagger" },
        new[] { "Shade Kris", "Death Scythe", "Night Fang", "Void Spear", "Nightmare Trident" },
    };
    static readonly string[] Traits =
    {
        "тяжёлое оружие: огромный урон, медленные удары",
        "быстрые серии ударов, нунчаки",
        "точность: длинный выпад и частые криты, катана",
        "хитрость: быстрые удары и криты",
    };

    static readonly string[][] HNames =
    {
        new[] { "Legion Cap", "Crested Helm", "Centurion Horns", "Iron Visage" },
        new[] { "Bamboo Cap", "Jade Crest", "Dragon Horns", "Emperor Mask" },
        new[] { "Herald Hood", "Dawn Crest", "Seraph Horns", "Sun Visor" },
        new[] { "Shade Cowl", "Night Crest", "Demon Horns", "Void Mask" },
    };
    static readonly string[][] ANames =
    {
        new[] { "Legion Tunic", "Chainmail", "Plate Cuirass", "Fortress Armor" },
        new[] { "Silk Robe", "Scale Vest", "Lacquer Plate", "Dragon Armor" },
        new[] { "Herald Robe", "Light Mail", "Radiant Plate", "Sunforged Armor" },
        new[] { "Shadow Garb", "Dark Mail", "Night Plate", "Abyss Armor" },
    };

    public static readonly Skin[] Skins =
    {
        new Skin { id = "S0", name = "Классика", desc = "простая тёмная одежда", cloth = C(0.2f, 0.2f, 0.26f), skinCol = C(0.92f, 0.74f, 0.57f), accent = C(0.26f, 0.17f, 0.12f), eye = Color.black, price = 0 },
        new Skin { id = "S1", name = "Ниндзя", desc = "чёрный костюм, красная повязка, маска", cloth = C(0.07f, 0.07f, 0.09f), skinCol = C(0.92f, 0.74f, 0.57f), accent = C(0.8f, 0.1f, 0.1f), eye = Color.black, extra = 1, price = 500000 },
        new Skin { id = "S2", name = "Самурай", desc = "алое кимоно, золотой пояс, пучок", cloth = C(0.52f, 0.08f, 0.1f), skinCol = C(0.9f, 0.72f, 0.55f), accent = C(0.92f, 0.78f, 0.34f), eye = Color.black, extra = 2, price = 1500000 },
        new Skin { id = "S3", name = "Королевский", desc = "белые одежды, золото, пурпурный плащ", cloth = C(0.9f, 0.9f, 0.95f), skinCol = C(0.95f, 0.8f, 0.66f), accent = C(0.95f, 0.8f, 0.3f), cape = C(0.45f, 0.1f, 0.6f), eye = C(0.1f, 0.3f, 0.8f), extra = 3, price = 3000000 },
        new Skin { id = "S4", name = "Зомби", desc = "зелёная кожа, лохмотья, светящиеся глаза", cloth = C(0.28f, 0.32f, 0.22f), skinCol = C(0.55f, 0.72f, 0.46f), accent = C(0.35f, 0.25f, 0.18f), eye = C(0.5f, 1f, 0.3f), glowEyes = true, extra = 5, price = 5000000 },
        new Skin { id = "S5", name = "Демон", desc = "красная кожа, рога, огненный взгляд", cloth = C(0.14f, 0.02f, 0.03f), skinCol = C(0.72f, 0.18f, 0.15f), accent = C(0.1f, 0.08f, 0.08f), eye = C(1f, 0.6f, 0.1f), glowEyes = true, extra = 4, price = 10000000 },
        new Skin { id = "S6", name = "Робот", desc = "металлический корпус, антенна, ядро", cloth = C(0.42f, 0.45f, 0.5f), skinCol = C(0.62f, 0.65f, 0.7f), accent = C(0.2f, 0.9f, 1f), eye = C(0.2f, 0.9f, 1f), glowEyes = true, metal = true, extra = 6, price = 25000000 },
        new Skin { id = "S7", name = "Золотой воин", desc = "золото с головы до ног и сияющая аура", cloth = C(0.85f, 0.66f, 0.2f), skinCol = C(0.95f, 0.8f, 0.45f), accent = C(1f, 0.9f, 0.5f), eye = Color.white, glowEyes = true, metal = true, extra = 7, price = 100000000 },
    };

    static Color C(float r, float g, float b) { return new Color(r, g, b); }

    public static string Trait(Faction f) { return Traits[(int)f]; }

    public static Color FactionColor(Faction f)
    {
        switch (f)
        {
            case Faction.Legion: return new Color(0.8f, 0.15f, 0.15f);
            case Faction.Dynasty: return new Color(0.15f, 0.65f, 0.4f);
            case Faction.Heralds: return new Color(0.3f, 0.55f, 0.95f);
            default: return new Color(0.55f, 0.2f, 0.75f);
        }
    }

    public static Color Tinted(Faction f, int tier)
    {
        Color b = FactionColor(f);
        if (tier <= 1) return Color.Lerp(b, Color.gray, 0.45f);
        if (tier == 3) return Color.Lerp(b, Color.white, 0.25f);
        return b;
    }

    static Db()
    {
        for (int f = 0; f < 4; f++)
        {
            Faction fac = (Faction)f;
            for (int t = 1; t <= 3; t++)
                for (int i = 0; i < WTypes[f].Length; i++)
                {
                    int w = WTypes[f][i];
                    float m = TierMul[t - 1];
                    Add(new Item
                    {
                        id = "W_" + f + "_" + w + "_" + t, name = TierName[t - 1] + " " + WNames[f][i],
                        slot = Slot.Weapon, faction = fac, tier = t, wtype = w,
                        damage = Mathf.Round(Dmg[w] * m * DmgMod[f]),
                        reach = Mathf.Round(Reach[w] * ReachMod[f] * 100f) / 100f,
                        speed = Mathf.Round(Spd[w] * SpdMod[f] * 100f) / 100f,
                        crit = CritMod[f] + (t - 1) * 0.03f,
                        price = (long)(2000 * m * m * (1 + i * 0.2f)),
                    });
                }
            for (int i = 0; i < 4; i++)
            {
                Add(new Item
                {
                    id = "H_" + f + "_" + i, name = HNames[f][i], slot = Slot.Helmet, faction = fac,
                    tier = Mathf.Min(3, i + 1), style = i, defense = 8 + i * 10, price = 1500L * (i + 1) * (i + 1),
                });
                Add(new Item
                {
                    id = "A_" + f + "_" + i, name = ANames[f][i], slot = Slot.Armor, faction = fac,
                    tier = Mathf.Min(3, i + 1), style = i, defense = 12 + i * 14, price = 2500L * (i + 1) * (i + 1),
                });
            }
        }
    }

    static void Add(Item it) { All.Add(it); ById[it.id] = it; }

    public static Item Get(string id) { Item it; return id != null && ById.TryGetValue(id, out it) ? it : null; }
}
