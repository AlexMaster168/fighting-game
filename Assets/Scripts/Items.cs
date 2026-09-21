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
