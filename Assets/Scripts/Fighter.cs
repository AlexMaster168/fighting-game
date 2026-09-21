using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEngine;

public class Fx : MonoBehaviour
{
    float t, life = 0.3f, size;
    Vector3 vel;
    int mode;                 // 0 pulse, 1 fireball, 2 bolt (thins out), 3 glow light
    Light glow;
    float glowMax;

    public static Fx Spawn(Vector3 pos, Color c, float size)
    {
        return Spawn(pos, c, size, 0.3f, Vector3.zero, 0f);
    }

    public static Fx Spawn(Vector3 pos, Color c, float size, float life, Vector3 vel, float delay)
    {
        var tr = Fighter.Part(null, "fx", PrimitiveType.Sphere, pos, Vector3.one * 0.05f, c, Vector3.zero);
        var f = tr.gameObject.AddComponent<Fx>();
        f.size = size; f.life = life; f.vel = vel; f.t = -delay;
        return f;
    }

    public Fx Mode(int m) { mode = m; return this; }

    // a quick burst of tiny sparks: hit / block feedback
    public static void Sparks(Vector3 pos, Color c, int n, float speed)
    {
        for (int i = 0; i < n; i++)
        {
            Vector3 v = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.3f, 1f), Random.Range(-0.6f, 0.6f)).normalized * speed * Random.Range(0.5f, 1f);
            Color k = Color.Lerp(c, Color.white, Random.Range(0f, 0.5f));
            Spawn(pos, k, Random.Range(0.12f, 0.22f), Random.Range(0.18f, 0.32f), v, 0f).Mode(1);
        }
    }

    public static void Seg(Vector3 a, Vector3 b, Color c, float w, float life)
    {
        Vector3 d = b - a;
        var tr = Fighter.Part(null, "bolt", PrimitiveType.Cylinder, (a + b) / 2f, new Vector3(w, d.magnitude / 2f, w), c, Vector3.zero);
        tr.rotation = Quaternion.FromToRotation(Vector3.up, d);
        var f = tr.gameObject.AddComponent<Fx>();
        f.mode = 2; f.size = w; f.life = life;
    }

    // jagged lightning between two points
    public static void Bolt(Vector3 a, Vector3 b, Color c, float w, float life)
    {
        Vector3 prev = a;
        const int n = 7;
        for (int i = 1; i <= n; i++)
        {
            Vector3 pt = Vector3.Lerp(a, b, i / (float)n);
            if (i < n) pt += new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.15f, 0.15f));
            Seg(prev, pt, c, w, life);
            prev = pt;
        }
    }

    public static void Flash(Vector3 pos, Color c, float range, float life)
    {
        var g = new GameObject("flash");
        g.transform.position = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = 6f;
        var f = g.AddComponent<Fx>();
        f.mode = 3; f.life = life; f.glow = l; f.glowMax = 6f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t += dt;
        if (t < 0f) { if (mode != 3) transform.localScale = Vector3.zero; return; }
        float p = t / life;
        if (p >= 1f) { Destroy(gameObject); return; }
        transform.position += vel * dt;
        switch (mode)
        {
            case 0: transform.localScale = Vector3.one * size * (p < 0.5f ? p * 2f : (1f - p) * 2f); break;
            case 1: transform.localScale = Vector3.one * size * (0.6f + 0.8f * p) * (1f - p * 0.7f); break;
            case 2: { float w = size * (1f - p); transform.localScale = new Vector3(w, transform.localScale.y, w); break; }
            case 3: glow.intensity = glowMax * (1f - p); break;
        }
    }
}

// One frame of the whole skeleton. Everything is in the fighter's own 2D plane (X forward, Y up).
public class Pose
{
    public float hipX, hipY, lean, head;
    public float thighF, shinF, thighB, shinB;   // + swings the leg forward
    public float lUp, lFo, lTwo;                 // free arm: FK angles, or 1 = grab the weapon with it
    public float wx, wy, wAng;                   // weapon grip, relative to the shoulders

    public Pose Clone() { return (Pose)MemberwiseClone(); }

    public static Pose Lerp(Pose a, Pose b, float t)
    {
        return new Pose
        {
            hipX = Mathf.Lerp(a.hipX, b.hipX, t), hipY = Mathf.Lerp(a.hipY, b.hipY, t),
            lean = Mathf.Lerp(a.lean, b.lean, t), head = Mathf.Lerp(a.head, b.head, t),
            thighF = Mathf.Lerp(a.thighF, b.thighF, t), shinF = Mathf.Lerp(a.shinF, b.shinF, t),
            thighB = Mathf.Lerp(a.thighB, b.thighB, t), shinB = Mathf.Lerp(a.shinB, b.shinB, t),
            lUp = Mathf.Lerp(a.lUp, b.lUp, t), lFo = Mathf.Lerp(a.lFo, b.lFo, t), lTwo = Mathf.Lerp(a.lTwo, b.lTwo, t),
            wx = Mathf.Lerp(a.wx, b.wx, t), wy = Mathf.Lerp(a.wy, b.wy, t), wAng = Mathf.Lerp(a.wAng, b.wAng, t),
        };
    }
}

public class Move
{
    public string name, next;
    public float dur, hitAt, dmg, reach, knock = 0.5f, step, stun = 0.28f, gain = 8f, hit2At = -1f;
    public bool big, low, launch, knockdown, air, weaponMove, unblockable, power;
    public float[] times;
    public Pose[] keys;

    public Pose Sample(float u)
    {
        u = Mathf.Clamp01(u);
        for (int i = 0; i < times.Length - 1; i++)
            if (u <= times[i + 1])
            {
                float t = (u - times[i]) / Mathf.Max(0.0001f, times[i + 1] - times[i]);
                return Pose.Lerp(keys[i], keys[i + 1], t * t * (3f - 2f * t));
            }
        return keys[keys.Length - 1];
    }
}

public class Fighter : MonoBehaviour
{
    public static float HitStop, Shake;

    public bool isPlayer, controlsEnabled, autoPlay;
    public Item weapon, helmet, armor;
    public Fighter target;
    public bool female;
    public Color hair = new Color(0.15f, 0.1f, 0.08f);
    public float hp = 100, maxHp = 100, energy, dmgMul = 1f, aiLevel = 1f, yawOffset, headY = 3.6f;

    // skeleton
    const float L1 = 0.52f, L2 = 0.5f, FootH = 0.09f;      // thigh, shin, foot
    const float AL1 = 0.5f, AL2 = 0.41f, ForeTilt = 35f;    // upper arm, forearm (as seen in the plane), forearm tilt toward centre
    Transform body, hips, spine, chest, headP, thighFp, shinFp, thighBp, shinBp, upR, foreR, upL, foreL, weaponP;
    Transform pony1, pony2, pony3;
    float ponyA, ponyV, lastX;
    float scale = 1.4f, weaponLen = 1.5f, twoHand = 1f, grip2 = 0.5f;

    // state
    readonly Dictionary<string, Move> moves = new Dictionary<string, Move>();
    Move cur;
    Pose disp;
    float mt, stepped, vy, jumpY, stun, downT, downElapsed, walkT, aiTimer, blockTimer, bufT, guard, blockCd;
    Move seenMove;
    float lastPainT = -10f;
    bool hitDone, blocking, moving;
    string buf;
    bool hit2Done;
    float burnT, burnDps, poisonT, poisonDps, shockT, statusFx, lastDamage, nunA, nunV, lastWAng;
    Transform nunP;
    int facing = 1;

    struct Intent { public float move; public bool jump, light, heavy, punch, kick, block, ult, down; }

    public int Defense { get { return (helmet != null ? helmet.defense : 0) + (armor != null ? armor.defense : 0); } }
    public bool Dead { get { return hp <= 0; } }
    public bool Attacking { get { return cur != null; } }
    public float WeaponReach { get { return (0.75f + weaponLen) * scale; } }

    // every faction has its own super power, charged by the energy bar
    public string PowerName
    {
        get
        {
            switch (weapon.faction)
            {
                case Faction.Legion: return "ПЛАМЯ";
                case Faction.Dynasty: return "МОЛНИЯ";
                case Faction.Heralds: return "СВЕТ";
                default: return "ТЬМА";
            }
        }
    }
    public float PowerRange
    {
        get
        {
            switch (weapon.faction)
            {
                case Faction.Legion: return 9f;
                case Faction.Dynasty: return 30f;
                case Faction.Heralds: return 30f;
                default: return 6f;
            }
        }
    }

    // ---------- model ----------
    static readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
    static Material baseMat;

    static Material Mat(Color c)
    {
        Material m;
        if (!mats.TryGetValue(c, out m))
        {
            // BaseMat ships as an asset so its shader is guaranteed to survive a player build.
            if (baseMat == null) baseMat = Resources.Load<Material>("BaseMat");
            if (baseMat != null) m = new Material(baseMat);
            else
            {
                Shader sh = Shader.Find("Standard");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                m = new Material(sh);
            }
            m.color = c;
            mats[c] = m;
        }
        return m;
    }

    public static Transform Part(Transform parent, string n, PrimitiveType type, Vector3 pos, Vector3 scale, Color c, Vector3 euler)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = n;
        Collider col = g.GetComponent<Collider>();
        if (col != null) Destroy(col);
        g.GetComponent<Renderer>().sharedMaterial = Mat(c);
        Transform t = g.transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localScale = scale;
        t.localRotation = Quaternion.Euler(euler);
        return t;
    }

    static Transform Pivot(Transform parent, string n, Vector3 pos)
    {
        var g = new GameObject(n);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        return g.transform;
    }

    static Transform Limb(Transform parent, float len, float dia, Color c)
    {
        return Part(parent, "limb", PrimitiveType.Capsule, new Vector3(0, -len / 2f, 0), new Vector3(dia, len / 2f, dia), c, Vector3.zero);
    }

    // a rod lying along +X, from x0 to x1
    static void Rod(Transform parent, float x0, float x1, float dia, Color c)
    {
        Part(parent, "rod", PrimitiveType.Cylinder, new Vector3((x0 + x1) / 2f, 0, 0), new Vector3(dia, (x1 - x0) / 2f, dia), c, new Vector3(0, 0, 90));
    }

    static Color BladeCol(Item w)
    {
        if (w.tier == 1) return new Color(0.78f, 0.8f, 0.83f);
        if (w.tier == 2) return new Color(0.96f, 0.86f, 0.42f);
        return Color.Lerp(Db.FactionColor(w.faction), Color.white, 0.5f);
    }

    public void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = transform.GetChild(i);
            ch.SetParent(null);
            Destroy(ch.gameObject);
        }
        disp = null; cur = null; pony1 = pony2 = pony3 = null; nunP = null;

        Color skin = new Color(0.92f, 0.74f, 0.57f), cloth = new Color(0.2f, 0.2f, 0.26f),
              wood = new Color(0.36f, 0.24f, 0.14f), gold = new Color(0.92f, 0.78f, 0.34f),
              leather = new Color(0.26f, 0.17f, 0.12f);
        Vector3 z = Vector3.zero;
        PrimitiveType cube = PrimitiveType.Cube, sph = PrimitiveType.Sphere;
        Color pink = new Color(1f, 0.5f, 0.74f), pinkDark = new Color(0.86f, 0.3f, 0.58f);

        float depth = female ? 0.42f : 0.52f;          // chest, seen from the side
        float width = female ? 0.62f : 0.78f;          // across the shoulders
        float sz = width / 2f - 0.03f;
        scale = female ? 1.28f : 1.4f;
        transform.localScale = Vector3.one * scale;
        headY = 2.8f * scale;

        bool hasArmor = armor != null;
        Color ac = hasArmor ? Db.Tinted(armor.faction, armor.tier) : cloth;
        Color acc = hasArmor ? Color.Lerp(ac, new Color(0.12f, 0.12f, 0.16f), 0.45f) : leather;
        int st = hasArmor ? armor.style : -1;

        body = Pivot(transform, "body", z);
        hips = Pivot(body, "hips", new Vector3(0, 1.1f, 0));
        Part(hips, "pelvis", cube, new Vector3(0, 0.02f, 0), new Vector3(depth * 0.95f, 0.3f, width * 0.86f), cloth, z);

        // legs: the near leg is the "front" one
        thighFp = Pivot(hips, "thighF", new Vector3(0, -0.02f, -0.17f));
        shinFp = Pivot(thighFp, "shinF", new Vector3(0, -L1, 0));
        thighBp = Pivot(hips, "thighB", new Vector3(0, -0.02f, 0.17f));
        shinBp = Pivot(thighBp, "shinB", new Vector3(0, -L1, 0));
        foreach (Transform[] leg in new[] { new[] { thighFp, shinFp }, new[] { thighBp, shinBp } })
        {
            Transform th = leg[0], sh = leg[1];
            Limb(th, L1, 0.28f, cloth);
            if (female)
            {
                // pink skirt: one panel per thigh so it swings with the legs and never gets in the way of a kick
                Part(th, "skirtPanel", cube, new Vector3(0.01f, -0.22f, 0), new Vector3(0.42f, 0.46f, 0.42f), pink, z);
                Part(th, "skirtHem", cube, new Vector3(0.01f, -0.45f, 0), new Vector3(0.45f, 0.06f, 0.45f), pinkDark, z);
            }
            Part(sh, "knee", sph, z, Vector3.one * 0.25f, cloth, z);
            Limb(sh, L2, 0.23f, cloth);
            Part(sh, "boot", cube, new Vector3(0.1f, -L2 - 0.02f, 0), new Vector3(0.42f, 0.17f, 0.26f), leather, z);
            if (st >= 1) Part(sh, "kneecap", sph, new Vector3(0.06f, 0f, 0), Vector3.one * 0.3f, acc, z);
            if (st >= 2)
            {
                Part(th, "cuisse", cube, new Vector3(0.02f, -L1 * 0.5f, 0), new Vector3(0.34f, 0.4f, 0.34f), ac, z);
                Part(sh, "greave", cube, new Vector3(0.02f, -L2 * 0.5f, 0), new Vector3(0.31f, 0.4f, 0.31f), acc, z);
            }
        }
        if (female) Part(hips, "waist", PrimitiveType.Cylinder, new Vector3(0, -0.04f, 0), new Vector3(depth + 0.1f, 0.1f, width * 0.95f), pinkDark, z);
        if (st >= 2 && !female) Part(hips, "skirt", cube, new Vector3(0, -0.14f, 0), new Vector3(depth + 0.14f, 0.34f, width + 0.08f), ac, z);

        // torso
        spine = Pivot(hips, "spine", new Vector3(0, 0.12f, 0));
        Part(spine, "torso", cube, new Vector3(0, 0.36f, 0), new Vector3(depth, 0.72f, width), cloth, z);
        if (hasArmor)
        {
            Part(spine, "cuirass", cube, new Vector3(0, 0.38f, 0), new Vector3(depth + 0.09f, 0.74f, width + 0.09f), ac, z);
            Part(spine, "belt", cube, new Vector3(0, 0.02f, 0), new Vector3(depth + 0.11f, 0.15f, width + 0.11f), leather, z);
            Part(spine, "buckle", cube, new Vector3((depth + 0.11f) / 2f, 0.02f, 0), new Vector3(0.08f, 0.16f, 0.16f), gold, z);
            if (st >= 3) Part(spine, "chestplate", cube, new Vector3(0.05f, 0.46f, 0), new Vector3(depth + 0.06f, 0.34f, width + 0.05f), acc, z);
        }
        chest = Pivot(spine, "chest", new Vector3(0, 0.7f, 0));
        if (st >= 3)
            Part(chest, "cape", cube, new Vector3(-depth / 2f - 0.08f, -0.7f, 0), new Vector3(0.09f, 1.5f, width + 0.2f), Db.FactionColor(armor.faction), new Vector3(0, 0, -5));

        // head
        Part(chest, "neck", PrimitiveType.Capsule, new Vector3(0, 0.1f, 0), new Vector3(0.2f, 0.13f, 0.2f), skin, z);
        headP = Pivot(chest, "head", new Vector3(0, 0.18f, 0));
        Part(headP, "skull", sph, new Vector3(0, 0.22f, 0), new Vector3(0.48f, 0.54f, 0.46f), skin, z);
        Part(headP, "eyeL", cube, new Vector3(0.21f, 0.25f, -0.1f), new Vector3(0.05f, 0.06f, 0.08f), Color.black, z);
        Part(headP, "eyeR", cube, new Vector3(0.21f, 0.25f, 0.1f), new Vector3(0.05f, 0.06f, 0.08f), Color.black, z);
        Part(headP, "hairTop", sph, new Vector3(-0.04f, 0.32f, 0), new Vector3(0.53f, 0.42f, 0.5f), hair, z);
        if (female)
        {
            // high ponytail: tied at the back of the head, three segments that swing with movement
            Vector3 tie = new Vector3(-0.3f, 0.4f, 0);
            Part(headP, "tie", sph, tie, Vector3.one * 0.14f, gold, z);
            pony1 = Pivot(headP, "pony1", tie);
            Limb(pony1, 0.36f, 0.19f, hair);
            pony2 = Pivot(pony1, "pony2", new Vector3(0, -0.34f, 0));
            Limb(pony2, 0.36f, 0.16f, hair);
            pony3 = Pivot(pony2, "pony3", new Vector3(0, -0.34f, 0));
            Limb(pony3, 0.32f, 0.1f, hair);
        }
        else Part(headP, "beard", cube, new Vector3(0.17f, 0.03f, 0), new Vector3(0.12f, 0.17f, 0.34f), hair, z);

        if (helmet != null)
        {
            Color hc = Db.Tinted(helmet.faction, helmet.tier), hacc = Color.Lerp(hc, new Color(0.12f, 0.12f, 0.16f), 0.45f);
            Part(headP, "helm", sph, new Vector3(-0.02f, 0.28f, 0), new Vector3(0.6f, 0.52f, 0.58f), hc, z);
            Part(headP, "brow", cube, new Vector3(0.2f, 0.34f, 0), new Vector3(0.16f, 0.09f, 0.5f), hacc, z);
            if (helmet.style == 1) Part(headP, "crest", cube, new Vector3(0, 0.6f, 0), new Vector3(0.5f, 0.22f, 0.08f), gold, z);
            if (helmet.style == 2)
            {
                Part(headP, "hornL", cube, new Vector3(0, 0.6f, -0.3f), new Vector3(0.09f, 0.55f, 0.09f), hacc, new Vector3(-35, 0, 0));
                Part(headP, "hornR", cube, new Vector3(0, 0.6f, 0.3f), new Vector3(0.09f, 0.55f, 0.09f), hacc, new Vector3(35, 0, 0));
            }
            if (helmet.style == 3)
            {
                Part(headP, "mask", cube, new Vector3(0.2f, 0.12f, 0), new Vector3(0.22f, 0.3f, 0.44f), hacc, z);
                Part(headP, "crest", cube, new Vector3(0, 0.62f, 0), new Vector3(0.5f, 0.24f, 0.08f), gold, z);
            }
        }

        // arms: forearms tilt toward the centre line so both hands can meet on one weapon
        upR = Pivot(chest, "upR", new Vector3(0, -0.06f, -sz));
        foreR = Pivot(upR, "foreR", new Vector3(0, -AL1, 0));
        upL = Pivot(chest, "upL", new Vector3(0, -0.06f, sz));
        foreL = Pivot(upL, "foreL", new Vector3(0, -AL1, 0));
        for (int a = 0; a < 2; a++)
        {
            Transform up = a == 0 ? upR : upL, fo = a == 0 ? foreR : foreL;
            float tilt = a == 0 ? -ForeTilt : ForeTilt;
            Part(up, "shoulder", sph, z, Vector3.one * 0.21f, skin, z);
            Limb(up, AL1, 0.19f, skin);
            Part(fo, "elbow", sph, z, Vector3.one * 0.18f, skin, z);
            Transform fm = Pivot(fo, "forearm", z);
            fm.localRotation = Quaternion.Euler(tilt, 0, 0);
            Limb(fm, AL2 / Mathf.Cos(ForeTilt * Mathf.Deg2Rad), 0.17f, skin);
            Part(fm, "hand", sph, new Vector3(0, -AL2 / Mathf.Cos(ForeTilt * Mathf.Deg2Rad), 0), Vector3.one * 0.2f, skin, z);
            if (st >= 0) Part(fm, "bracer", cube, new Vector3(0, -0.3f, 0), new Vector3(0.22f, 0.26f, 0.22f), acc, z);
            if (st >= 2) Part(fm, "gauntlet", cube, new Vector3(0, -0.5f, 0), new Vector3(0.25f, 0.24f, 0.25f), ac, z);
            if (st >= 1) Part(up, "pauldron", sph, new Vector3(0, 0.03f, 0), Vector3.one * (female ? 0.3f : 0.36f), acc, z);
            if (st >= 3) Part(up, "pauldron2", sph, new Vector3(0, 0.08f, 0), Vector3.one * 0.42f, ac, z);
        }

        // weapon: origin at the leading hand, blade along +X
        weaponP = Pivot(chest, "weapon", z);
        BuildWeapon(wood, gold);

        BuildMoves();
    }

    void BuildWeapon(Color wood, Color gold)
    {
        Vector3 z = Vector3.zero;
        PrimitiveType cube = PrimitiveType.Cube;
        Color bl = BladeCol(weapon);
        Color dark = new Color(0.12f, 0.1f, 0.13f);
        float Lw = weapon.reach * 0.95f;
        weaponLen = Lw;
        twoHand = weapon.wtype == 4 ? 0f : 1f;
        grip2 = Mathf.Min(0.55f, Lw * 0.35f);
        switch (weapon.wtype)
        {
            case 0: // sword
                Part(weaponP, "pommel", PrimitiveType.Sphere, new Vector3(-0.3f, 0, 0), Vector3.one * 0.14f, gold, z);
                Rod(weaponP, -0.28f, 0.55f, 0.09f, wood);
                Part(weaponP, "guard", cube, new Vector3(0.58f, 0, 0), new Vector3(0.1f, 0.06f, 0.5f), gold, z);
                Part(weaponP, "blade", cube, new Vector3(0.6f + (Lw - 0.6f) / 2f, 0, 0), new Vector3(Lw - 0.6f, 0.24f, 0.06f), bl, z);
                break;
            case 1: // axe
                Rod(weaponP, -0.4f, Lw, 0.1f, wood);
                Part(weaponP, "head", cube, new Vector3(Lw - 0.2f, 0.05f, 0), new Vector3(0.42f, 0.78f, 0.09f), bl, z);
                Part(weaponP, "spike", cube, new Vector3(Lw - 0.42f, 0.05f, 0), new Vector3(0.24f, 0.14f, 0.1f), gold, z);
                break;
            case 2: // spear
                Rod(weaponP, -0.6f, Lw - 0.3f, 0.08f, wood);
                Part(weaponP, "tip", cube, new Vector3(Lw - 0.05f, 0, 0), new Vector3(0.6f, 0.22f, 0.06f), bl, z);
                Part(weaponP, "collar", cube, new Vector3(Lw - 0.34f, 0, 0), new Vector3(0.1f, 0.16f, 0.16f), gold, z);
                break;
            case 3: // hammer
                Rod(weaponP, -0.4f, Lw, 0.12f, wood);
                Part(weaponP, "head", cube, new Vector3(Lw - 0.12f, 0, 0), new Vector3(0.55f, 0.55f, 0.55f), bl, z);
                Part(weaponP, "band", cube, new Vector3(Lw - 0.12f, 0, 0), new Vector3(0.6f, 0.2f, 0.6f), gold, z);
                break;
            case 5: // katana
                Rod(weaponP, -0.32f, 0.42f, 0.08f, dark);
                Part(weaponP, "tsuba", PrimitiveType.Cylinder, new Vector3(0.45f, 0, 0), new Vector3(0.34f, 0.02f, 0.34f), gold, new Vector3(0, 0, 90));
                Part(weaponP, "blade", cube, new Vector3(0.5f + (Lw - 0.8f) / 2f, 0, 0), new Vector3(Lw - 0.8f, 0.13f, 0.04f), bl, z);
                Part(weaponP, "tip", cube, new Vector3(Lw - 0.2f, 0.03f, 0), new Vector3(0.42f, 0.12f, 0.04f), bl, new Vector3(0, 0, 6));
                break;
            case 6: // greatsword
                Rod(weaponP, -0.4f, 0.7f, 0.11f, wood);
                Part(weaponP, "pommel", PrimitiveType.Sphere, new Vector3(-0.43f, 0, 0), Vector3.one * 0.19f, gold, z);
                Part(weaponP, "guard", cube, new Vector3(0.74f, 0, 0), new Vector3(0.13f, 0.72f, 0.16f), gold, z);
                Part(weaponP, "blade", cube, new Vector3(0.8f + (Lw - 1.0f) / 2f, 0, 0), new Vector3(Lw - 1.0f, 0.38f, 0.07f), bl, z);
                Part(weaponP, "fuller", cube, new Vector3(0.8f + (Lw - 1.0f) / 2f, 0, 0), new Vector3(Lw - 1.1f, 0.1f, 0.09f), Color.Lerp(bl, dark, 0.6f), z);
                Part(weaponP, "tip", cube, new Vector3(Lw - 0.12f, 0, 0), new Vector3(0.27f, 0.27f, 0.07f), bl, new Vector3(0, 0, 45));
                break;
            case 7: // scythe
                Rod(weaponP, -0.4f, Lw, 0.1f, wood);
                Part(weaponP, "b1", cube, new Vector3(Lw - 0.02f, 0.2f, 0), new Vector3(0.14f, 0.5f, 0.06f), bl, new Vector3(0, 0, -20));
                Part(weaponP, "b2", cube, new Vector3(Lw + 0.16f, 0.5f, 0), new Vector3(0.55f, 0.14f, 0.06f), bl, new Vector3(0, 0, 25));
                Part(weaponP, "b3", cube, new Vector3(Lw + 0.5f, 0.55f, 0), new Vector3(0.4f, 0.1f, 0.05f), bl, new Vector3(0, 0, -20));
                break;
            case 8: // halberd
                Rod(weaponP, -0.5f, Lw - 0.1f, 0.1f, wood);
                Part(weaponP, "spike", cube, new Vector3(Lw + 0.1f, 0, 0), new Vector3(0.55f, 0.16f, 0.06f), bl, z);
                Part(weaponP, "axe", cube, new Vector3(Lw - 0.45f, 0.24f, 0), new Vector3(0.45f, 0.56f, 0.07f), bl, z);
                Part(weaponP, "hook", cube, new Vector3(Lw - 0.5f, -0.2f, 0), new Vector3(0.34f, 0.1f, 0.06f), gold, new Vector3(0, 0, -25));
                break;
            case 9: // spiked mace
                Rod(weaponP, -0.3f, Lw, 0.11f, wood);
                Part(weaponP, "ball", PrimitiveType.Sphere, new Vector3(Lw - 0.05f, 0, 0), Vector3.one * 0.5f, bl, z);
                for (int k = 0; k < 6; k++)
                    Part(weaponP, "spike", cube, new Vector3(Lw - 0.05f, 0, 0), new Vector3(0.7f, 0.08f, 0.08f), gold, new Vector3(k * 30f, 0, k * 30f));
                break;
            case 10: // trident
                Rod(weaponP, -0.5f, Lw - 0.2f, 0.09f, wood);
                Part(weaponP, "bar", cube, new Vector3(Lw - 0.3f, 0, 0), new Vector3(0.1f, 0.6f, 0.08f), gold, z);
                Part(weaponP, "pMid", cube, new Vector3(Lw - 0.02f, 0, 0), new Vector3(0.5f, 0.1f, 0.06f), bl, z);
                Part(weaponP, "pUp", cube, new Vector3(Lw - 0.12f, 0.27f, 0), new Vector3(0.38f, 0.09f, 0.06f), bl, z);
                Part(weaponP, "pDn", cube, new Vector3(Lw - 0.12f, -0.27f, 0), new Vector3(0.38f, 0.09f, 0.06f), bl, z);
                break;
            case 11: // nunchaku: one stick in the hand, the other swings on a chain
                Rod(weaponP, -0.25f, 0.5f, 0.1f, dark);
                Part(weaponP, "capA", PrimitiveType.Sphere, new Vector3(-0.25f, 0, 0), Vector3.one * 0.13f, gold, z);
                Part(weaponP, "capB", PrimitiveType.Sphere, new Vector3(0.5f, 0, 0), Vector3.one * 0.13f, gold, z);
                for (int k = 0; k < 3; k++)
                    Part(weaponP, "link", PrimitiveType.Sphere, new Vector3(0.54f + k * 0.055f, 0, 0), Vector3.one * 0.07f, gold, z);
                nunP = Pivot(weaponP, "nun", new Vector3(0.7f, 0, 0));
                Rod(nunP, 0f, 0.75f, 0.1f, dark);
                Part(nunP, "capC", PrimitiveType.Sphere, new Vector3(0.75f, 0, 0), Vector3.one * 0.13f, gold, z);
                break;
            default: // dagger
                Part(weaponP, "pommel", PrimitiveType.Sphere, new Vector3(-0.12f, 0, 0), Vector3.one * 0.11f, gold, z);
                Rod(weaponP, -0.1f, 0.25f, 0.08f, wood);
                Part(weaponP, "guard", cube, new Vector3(0.27f, 0, 0), new Vector3(0.07f, 0.04f, 0.3f), gold, z);
                Part(weaponP, "blade", cube, new Vector3(0.28f + (Lw - 0.28f) / 2f, 0, 0), new Vector3(Lw - 0.28f, 0.17f, 0.04f), bl, z);
                break;
        }
    }

    // ---------- moves ----------
    static Pose G()
    {
        return new Pose { lean = 8, thighF = 22, shinF = -26, thighB = -16, shinB = -12, lUp = 50, lFo = 100, lTwo = 1, wx = 0.55f, wy = -0.05f, wAng = 62 };
    }

    static Pose K(Action<Pose> f) { Pose p = G(); f(p); return p; }

    // weapon held in the leading hand only, free arm doing its own thing
    static Pose OneHand(Action<Pose> f)
    {
        return K(p => { p.lTwo = 0; p.wx = 0.3f; p.wy = 0.05f; p.wAng = 80; f(p); });
    }

    void BuildMoves()
    {
        moves.Clear();
        float ws = Mathf.Lerp(1f, weapon.speed, 0.7f);
        float wr = WeaponReach, wd = weapon.damage;

        moves["slash"] = new Move
        {
            name = "slash", weaponMove = true, dur = 0.55f / ws, hitAt = 0.46f, dmg = wd * 1.1f, reach = wr, step = 0.6f, stun = 0.32f, knock = 0.7f, gain = 10,
            times = new[] { 0f, 0.3f, 0.48f, 0.72f, 1f },
            keys = new[]
            {
                G(),
                K(p => { p.lean = -8; p.hipX = -0.1f; p.wx = 0.05f; p.wy = 0.5f; p.wAng = 148; p.thighF = 26; p.shinF = -30; p.thighB = -22; }),
                K(p => { p.lean = 26; p.hipX = 0.4f; p.wx = 0.95f; p.wy = -0.2f; p.wAng = -55; p.thighF = 46; p.shinF = -40; p.thighB = -34; p.shinB = -22; }),
                K(p => { p.lean = 24; p.hipX = 0.4f; p.wx = 0.9f; p.wy = -0.25f; p.wAng = -62; p.thighF = 46; p.shinF = -40; p.thighB = -34; p.shinB = -22; }),
                G(),
            },
        };

        moves["smash"] = new Move
        {
            name = "smash", weaponMove = true, dur = 1.0f / ws, hitAt = 0.6f, dmg = wd * 2.2f, reach = wr, step = 0.9f, stun = 0.55f, knock = 1.2f, big = true, knockdown = true, gain = 14,
            times = new[] { 0f, 0.42f, 0.6f, 0.8f, 1f },
            keys = new[]
            {
                G(),
                K(p => { p.lean = -14; p.hipX = -0.2f; p.hipY = -0.05f; p.wx = -0.05f; p.wy = 0.8f; p.wAng = 172; p.thighF = 10; p.shinF = -14; p.thighB = -28; p.shinB = -10; }),
                K(p => { p.lean = 36; p.hipX = 0.5f; p.hipY = -0.1f; p.wx = 1.0f; p.wy = -0.45f; p.wAng = -88; p.thighF = 58; p.shinF = -50; p.thighB = -38; p.shinB = -28; }),
                K(p => { p.lean = 34; p.hipX = 0.5f; p.hipY = -0.1f; p.wx = 1.0f; p.wy = -0.5f; p.wAng = -90; p.thighF = 58; p.shinF = -50; p.thighB = -38; p.shinB = -28; }),
                G(),
            },
        };

        moves["power"] = new Move
        {
            name = "power", power = true, dur = 1.0f, hitAt = 0.55f, dmg = wd * 3.2f, reach = PowerRange, step = 0.4f, stun = 0.7f, knock = 1.4f,
            big = true, knockdown = weapon.faction != Faction.Dynasty,
            unblockable = weapon.faction == Faction.Dynasty || weapon.faction == Faction.Heralds, gain = 0,
            times = new[] { 0f, 0.3f, 0.55f, 0.8f, 1f },
            keys = new[]
            {
                G(),
                K(p => { p.lean = -10; p.hipY = -0.05f; p.wx = 0.1f; p.wy = 0.75f; p.wAng = 95; p.thighF = 24; p.shinF = -28; p.thighB = -22; p.shinB = -12; }),
                K(p => { p.lean = 20; p.hipX = 0.5f; p.wx = 1.0f; p.wy = 0.05f; p.wAng = 8; p.thighF = 50; p.shinF = -42; p.thighB = -36; p.shinB = -24; }),
                K(p => { p.lean = 20; p.hipX = 0.5f; p.wx = 1.0f; p.wy = 0.05f; p.wAng = 8; p.thighF = 50; p.shinF = -42; p.thighB = -36; p.shinB = -24; }),
                G(),
            },
        };

        if (weapon.wtype == 11)
        {
            // nunchaku: a quick two-hit flurry instead of one big swing
            moves["slash"] = new Move
            {
                name = "slash", weaponMove = true, dur = 0.62f / ws, hitAt = 0.34f, hit2At = 0.72f, dmg = wd * 0.75f, reach = wr, step = 0.7f, stun = 0.22f, knock = 0.3f, gain = 9,
                times = new[] { 0f, 0.18f, 0.34f, 0.52f, 0.72f, 1f },
                keys = new[]
                {
                    G(),
                    K(p => { p.lean = -4; p.wx = 0.1f; p.wy = 0.45f; p.wAng = 150; p.thighF = 26; p.shinF = -30; }),
                    K(p => { p.lean = 20; p.hipX = 0.3f; p.wx = 0.95f; p.wy = -0.1f; p.wAng = -40; p.thighF = 44; p.shinF = -38; p.thighB = -32; }),
                    K(p => { p.lean = 8; p.hipX = 0.2f; p.wx = 0.5f; p.wy = 0.35f; p.wAng = 120; p.thighF = 38; p.shinF = -34; p.thighB = -28; }),
                    K(p => { p.lean = 26; p.hipX = 0.45f; p.wx = 1.0f; p.wy = -0.25f; p.wAng = -70; p.thighF = 48; p.shinF = -40; p.thighB = -34; }),
                    G(),
                },
            };
        }

        moves["jab"] = new Move
        {
            name = "jab", next = "cross", dur = 0.3f, hitAt = 0.42f, dmg = 7f, reach = 2.15f, step = 0.25f, stun = 0.22f, knock = 0.35f,
            times = new[] { 0f, 0.42f, 1f },
            keys = new[]
            {
                G(),
                OneHand(p => { p.lean = 14; p.hipX = 0.18f; p.lUp = 88; p.lFo = -4; }),
                G(),
            },
        };

        moves["cross"] = new Move
        {
            name = "cross", next = "upper", dur = 0.36f, hitAt = 0.5f, dmg = 9f, reach = 2.3f, step = 0.35f, stun = 0.25f, knock = 0.45f,
            times = new[] { 0f, 0.28f, 0.5f, 1f },
            keys = new[]
            {
                G(),
                OneHand(p => { p.lean = 2; p.hipX = -0.1f; p.lUp = -35; p.lFo = 70; p.thighF = 28; p.shinF = -30; }),
                OneHand(p => { p.lean = 26; p.hipX = 0.35f; p.lUp = 96; p.lFo = -6; p.thighF = 40; p.shinF = -34; p.thighB = -30; }),
                G(),
            },
        };

        moves["upper"] = new Move
        {
            name = "upper", dur = 0.6f, hitAt = 0.5f, dmg = 16f, reach = 2.0f, step = 0.3f, stun = 0.5f, knock = 0.7f, big = true, launch = true, knockdown = true, gain = 12,
            times = new[] { 0f, 0.3f, 0.5f, 0.7f, 1f },
            keys = new[]
            {
                G(),
                OneHand(p => { p.lean = 18; p.hipY = -0.3f; p.lUp = -25; p.lFo = 40; p.thighF = 50; p.shinF = -60; p.thighB = -30; p.shinB = -40; }),
                OneHand(p => { p.lean = -4; p.hipY = 0.05f; p.hipX = 0.25f; p.lUp = 158; p.lFo = 2; p.thighF = 24; p.shinF = -20; }),
                OneHand(p => { p.lean = -6; p.hipX = 0.25f; p.lUp = 160; p.lFo = 4; p.thighF = 24; p.shinF = -20; }),
                G(),
            },
        };

        moves["kick1"] = new Move
        {
            name = "kick1", next = "round", dur = 0.5f, hitAt = 0.5f, dmg = 12f, reach = 2.6f, step = 0.3f, stun = 0.3f, knock = 0.6f, gain = 10,
            times = new[] { 0f, 0.3f, 0.5f, 0.75f, 1f },
            keys = new[]
            {
                G(),
                OneHand(p => { p.lean = -4; p.thighF = 96; p.shinF = -105; p.thighB = -8; p.shinB = -6; p.lUp = 30; p.lFo = 60; }),
                OneHand(p => { p.lean = -14; p.hipX = 0.12f; p.thighF = 92; p.shinF = -2; p.thighB = -10; p.shinB = -4; p.lUp = 20; p.lFo = 50; }),
                OneHand(p => { p.lean = -8; p.thighF = 80; p.shinF = -70; p.thighB = -10; p.lUp = 30; p.lFo = 60; }),
                G(),
            },
        };

        moves["round"] = new Move
        {
            name = "round", dur = 0.62f, hitAt = 0.5f, dmg = 18f, reach = 2.75f, step = 0.35f, stun = 0.45f, knock = 0.9f, big = true, knockdown = true, gain = 12,
            times = new[] { 0f, 0.28f, 0.5f, 0.72f, 1f },
            keys = new[]
            {
                G(),
                OneHand(p => { p.lean = 4; p.hipX = -0.05f; p.thighF = 72; p.shinF = -115; p.thighB = -12; p.lUp = 70; p.lFo = 60; }),
                OneHand(p => { p.lean = -26; p.hipX = 0.15f; p.hipY = 0.02f; p.thighF = 128; p.shinF = -8; p.thighB = -8; p.shinB = -4; p.lUp = -20; p.lFo = 40; }),
                OneHand(p => { p.lean = -12; p.thighF = 80; p.shinF = -80; p.thighB = -12; p.lUp = 30; p.lFo = 60; }),
                G(),
            },
        };

        moves["sweep"] = new Move
        {
            name = "sweep", dur = 0.62f, hitAt = 0.5f, dmg = 13f, reach = 2.7f, step = 0.3f, stun = 0.5f, knock = 0.5f, low = true, knockdown = true, gain = 12,
            times = new[] { 0f, 0.3f, 0.5f, 0.75f, 1f },
            keys = new[]
            {
                G(),
                OneHand(p => { p.lean = 28; p.thighB = 68; p.shinB = -140; p.thighF = 42; p.shinF = -70; p.lUp = -30; p.lFo = 40; }),
                OneHand(p => { p.lean = 32; p.hipX = 0.15f; p.thighB = 68; p.shinB = -140; p.thighF = 90; p.shinF = 0; p.lUp = -40; p.lFo = 30; }),
                OneHand(p => { p.lean = 26; p.thighB = 60; p.shinB = -130; p.thighF = 76; p.shinF = -6; p.lUp = -30; p.lFo = 40; }),
                G(),
            },
        };

        Pose air = K(p => { p.lean = -10; p.thighF = 92; p.shinF = -6; p.thighB = 18; p.shinB = -85; p.lTwo = 0.4f; p.lUp = 25; p.lFo = 50; p.wAng = 40; });
        moves["jumpkick"] = new Move
        {
            name = "jumpkick", dur = 0.6f, hitAt = 0.2f, dmg = 12f, reach = 2.7f, step = 0.5f, stun = 0.3f, knock = 0.6f, air = true,
            times = new[] { 0f, 0.2f, 1f },
            keys = new[] { K(p => { p.thighF = 60; p.shinF = -70; p.thighB = -10; p.shinB = -60; }), air, air },
        };
    }

    static Pose BlockPose = K(p => { p.lean = 12; p.hipY = -0.05f; p.wx = 0.65f; p.wy = 0.0f; p.wAng = 88; p.thighF = 26; p.shinF = -32; p.thighB = -20; p.shinB = -14; });
    static Pose HitPose = K(p => { p.lean = -20; p.head = -12; p.hipX = -0.2f; p.lTwo = 0.2f; p.wx = 0.2f; p.wy = 0.1f; p.wAng = 40; p.lUp = -25; p.lFo = 60; p.thighF = 8; p.shinF = -16; p.thighB = -24; p.shinB = -8; });
    static Pose AirPose = K(p => { p.lean = 6; p.lTwo = 0.6f; p.wAng = 50; p.thighF = 48; p.shinF = -75; p.thighB = -22; p.shinB = -60; p.lUp = 70; p.lFo = 60; });
    static Pose DownPose = K(p => { p.lean = 0; p.lTwo = 0; p.wx = 0.4f; p.wy = 0f; p.wAng = 20; p.lUp = -10; p.lFo = 15; p.thighF = 8; p.shinF = -8; p.thighB = -8; p.shinB = -8; });

    // ---------- input ----------
    Intent PlayerIntent()
    {
        Intent it = new Intent();
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) it.move -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) it.move += 1;
        it.jump = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow);
        it.down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        it.light = Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0);
        it.heavy = Input.GetKeyDown(KeyCode.K) || Input.GetMouseButtonDown(1);
        it.punch = Input.GetKeyDown(KeyCode.U);
        it.kick = Input.GetKeyDown(KeyCode.H);
        it.block = Input.GetKey(KeyCode.L);
        it.ult = Input.GetKeyDown(KeyCode.I);
        return it;
    }

    Intent AIIntent()
    {
        Intent it = new Intent();
        if (target == null || target.Dead) return it;
        float dx = target.transform.position.x - transform.position.x, dist = Mathf.Abs(dx);
        float wr = WeaponReach;
        aiTimer -= Time.deltaTime;

        if (dist > wr * 0.8f) it.move = Mathf.Sign(dx) * Mathf.Min(1f, 0.55f + 0.08f * aiLevel);
        else if (dist < 1.8f) it.move = -Mathf.Sign(dx) * 0.6f;

        blockCd -= Time.deltaTime;
        if (target.cur == null) seenMove = null;
        else if (target.cur != seenMove)
        {
            seenMove = target.cur;
            if (blockCd <= 0f && dist < target.cur.reach + 0.4f && Random.value < 0.2f + 0.04f * aiLevel) { blockTimer = 0.5f; blockCd = 1.6f; }
        }
        if (blockTimer > 0) { blockTimer -= Time.deltaTime; it.block = true; }
        else if (cur == null && aiTimer <= 0f)
        {
            float r = Random.value;
            if (dist <= 2.5f)
            {
                if (r < 0.32f) it.punch = true;
                else if (r < 0.62f) it.kick = true;
                else if (r < 0.78f) { it.kick = true; it.down = true; }
                else it.light = true;
                aiTimer = Random.Range(0.45f, 1.2f) / (0.6f + 0.15f * aiLevel);
            }
            else if (dist <= wr)
            {
                if (r < 0.6f) it.light = true; else it.heavy = true;
                aiTimer = Random.Range(0.6f, 1.4f) / (0.6f + 0.15f * aiLevel);
            }
        }
        if (cur != null && cur.next != null && hitDone && Random.value < 0.06f * (0.5f + 0.15f * aiLevel))
        {
            if (cur.name == "jab" || cur.name == "cross") it.punch = true; else it.kick = true;
        }
        if (energy >= 100f && dist < PowerRange) it.ult = true;
        return it;
    }

    // ---------- combat ----------
    void Buffer(string k) { buf = k; bufT = 0.3f; }

    void StartMove(string name)
    {
        cur = moves[name];
        mt = 0f; stepped = 0f; hitDone = false; hit2Done = false; buf = null;
        if (name == "power")
        {
            energy = 0f;
            Vector3 c = transform.position + new Vector3(0, 1.8f * scale, 0);
            for (int k = 0; k < 14; k++)
            {
                float ang = k / 14f * Mathf.PI * 2f;
                Vector3 ring = transform.position + new Vector3(Mathf.Cos(ang) * 1.3f, 0.2f, Mathf.Sin(ang) * 0.5f);
                Fx.Spawn(ring, Db.FactionColor(weapon.faction), 0.18f, 0.8f, Vector3.up * 3.5f, k * 0.02f);
            }
            Fx.Flash(c, Db.FactionColor(weapon.faction), 10f, 0.9f);
        }
        GameAudio.Sfx(name == "smash" || name == "power" || name == "round" || name == "upper" ? "whoosh2" : "whoosh");
    }

    bool Grounded { get { return jumpY <= 0.02f; } }

    // never walk through the opponent while both are on the ground
    float ClampApproach(float dx)
    {
        if (target == null || !Grounded || target.jumpY > 0.3f) return dx;
        float tx = target.transform.position.x, x = transform.position.x;
        float now = Mathf.Abs(x - tx), nxt = Mathf.Abs(x + dx - tx);
        return (nxt < 1.95f && nxt < now) ? 0f : dx;
    }

    void DoHit(Move m)
    {
        if (target == null || target.Dead || cur == null) return;
        Vector3 tp = target.transform.position, mp = transform.position;
        float dx = (tp.x - mp.x) * facing, dy = Mathf.Abs(tp.y - mp.y);
        if (m.power) PowerVfx(mp, tp);
        bool vertOk = m.power || (dy < 1.9f && (!m.low || target.jumpY < 0.6f));
        if (dx > -0.4f && dx <= m.reach && vertOk)
        {
            float d = m.dmg * dmgMul;
            // weapon strikes can crit; the Heralds' light is always a precise, critical hit
            bool crit = (m.weaponMove || (m.power && weapon.faction == Faction.Heralds)) && Random.value < (m.power ? 1f : weapon.crit);
            if (crit) d *= 1.6f;
            bool landed = target.TakeHit(m, d, facing);
            if (landed)
            {
                energy = Mathf.Min(100f, energy + m.gain + (m.power ? 0f : d * 0.08f));
                if (crit)
                {
                    Fx.Sparks(tp + new Vector3(0, 2.4f, 0), new Color(1f, 0.9f, 0.3f), 14, 7f);
                    Shake = Mathf.Max(Shake, 0.12f);
                }
                if (m.power) PowerEffect();
            }
        }
    }

    // status effect carried by the super power
    void PowerEffect()
    {
        switch (weapon.faction)
        {
            case Faction.Legion:      // burn
                target.burnT = 5f;
                target.burnDps = Mathf.Max(4f, weapon.damage * 0.3f);
                break;
            case Faction.Dynasty:     // electric shock: frozen and twitching
                target.shockT = 1.5f;
                target.stun = Mathf.Max(target.stun, 1.5f);
                break;
            case Faction.Heralds:     // light heals the wielder
                hp = Mathf.Min(maxHp, hp + maxHp * 0.1f);
                Fx.Sparks(transform.position + new Vector3(0, 2.2f * scale, 0), new Color(0.6f, 1f, 0.6f), 10, 3f);
                break;
            default:                  // shadow drains life and poisons
                hp = Mathf.Min(maxHp, hp + target.lastDamage * 0.6f);
                target.poisonT = 4f;
                target.poisonDps = Mathf.Max(3f, weapon.damage * 0.2f);
                break;
        }
    }

    void PowerVfx(Vector3 mp, Vector3 tp)
    {
        Color col = Db.FactionColor(weapon.faction);
        Vector3 chest = mp + new Vector3(0, 1.8f * scale, 0);
        Shake = Mathf.Max(Shake, 0.25f);
        switch (weapon.faction)
        {
            case Faction.Legion:      // a wave of flame rolls across the arena
                GameAudio.Sfx("fire");
                for (int i = 0; i < 26; i++)
                {
                    Color c = Color.Lerp(new Color(1f, 0.9f, 0.25f), new Color(1f, 0.25f, 0.05f), Random.value);
                    Vector3 pos = mp + new Vector3(facing * 1.2f, Random.Range(0.3f, 2.6f), Random.Range(-0.5f, 0.5f));
                    Fx.Spawn(pos, c, Random.Range(0.9f, 1.7f), 0.75f, new Vector3(facing * 11f, Random.Range(-0.5f, 1.5f), 0), Random.Range(0f, 0.3f)).Mode(1);
                }
                Fx.Flash(mp + new Vector3(facing * 3f, 1.5f, 0), new Color(1f, 0.5f, 0.1f), 14f, 0.8f);
                break;
            case Faction.Dynasty:     // lightning from the sky
            {
                GameAudio.Sfx("zap");
                Vector3 hit = new Vector3(tp.x, 0.2f, 0);
                Fx.Bolt(hit + new Vector3(0, 16f, 0), hit, new Color(0.75f, 0.92f, 1f), 0.24f, 0.4f);
                Fx.Bolt(hit + new Vector3(0.7f, 16f, 0.2f), hit, new Color(0.5f, 0.75f, 1f), 0.12f, 0.3f);
                Fx.Flash(hit + new Vector3(0, 2f, 0), new Color(0.6f, 0.85f, 1f), 16f, 0.5f);
                Fx.Sparks(hit + new Vector3(0, 1.5f, 0), new Color(0.8f, 0.95f, 1f), 14, 8f);
                break;
            }
            case Faction.Heralds:     // a column of holy light
            {
                GameAudio.Sfx("holy");
                Vector3 hit = new Vector3(tp.x, 0f, 0);
                Fx.Seg(hit, hit + new Vector3(0, 18f, 0), new Color(1f, 0.93f, 0.55f), 1.5f, 0.7f);
                Fx.Seg(hit, hit + new Vector3(0, 18f, 0), Color.white, 0.7f, 0.7f);
                Fx.Flash(hit + new Vector3(0, 2f, 0), new Color(1f, 0.95f, 0.6f), 16f, 0.7f);
                for (int k = 0; k < 10; k++)
                {
                    float ang = k / 10f * Mathf.PI * 2f;
                    Fx.Spawn(hit + new Vector3(Mathf.Cos(ang) * 1.6f, 0.2f, Mathf.Sin(ang) * 0.6f), new Color(1f, 0.95f, 0.7f), 0.25f, 0.7f, Vector3.up * 4f, 0f);
                }
                break;
            }
            default:                  // shadow tendrils
            {
                GameAudio.Sfx("dark");
                Vector3 hitC = tp + new Vector3(0, 1.8f, 0);
                Fx.Bolt(chest, hitC, new Color(0.6f, 0.15f, 0.9f), 0.16f, 0.45f);
                Fx.Bolt(chest, hitC + new Vector3(0, 0.6f, 0), new Color(0.35f, 0.05f, 0.5f), 0.12f, 0.4f);
                for (int k = 0; k < 14; k++)
                    Fx.Spawn(hitC + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-1f, 1f), Random.Range(-0.4f, 0.4f)), col, Random.Range(0.2f, 0.4f), 0.6f, Vector3.up * 2f, 0f);
                Fx.Flash(hitC, new Color(0.6f, 0.2f, 0.9f), 10f, 0.5f);
                break;
            }
        }
    }

    // returns true if the hit connected (not blocked)
    public bool TakeHit(Move m, float d, int dir)
    {
        if (Dead) return false;
        if (downT > 0f) return false;   // no hitting someone who is already on the floor
        d *= 1f - Defense / (Defense + 100f);
        Vector3 chest = transform.position + new Vector3(0, 1.9f * scale + jumpY, 0);
        bool blocked = blocking && facing == -dir && !m.low && !m.unblockable;
        Vector3 p = transform.position;
        if (blocked)
        {
            d *= 0.12f;
            energy = Mathf.Min(100f, energy + 5f);
            p.x += dir * 0.35f;
            transform.position = p;
            Fx.Sparks(chest + new Vector3(-dir * 0.5f, 0, 0), new Color(0.45f, 0.75f, 1f), 8, 5f);
            HitStop = Mathf.Max(HitStop, 0.03f);
            GameAudio.Sfx("block");
            guard += m.big ? 0.45f : 0.25f;
            if (guard >= 1f)
            {
                // guard broken: the block shatters and the defender is left open
                guard = 0f; blocking = false; stun = 0.8f;
                Fx.Sparks(chest, new Color(1f, 0.85f, 0.3f), 16, 8f);
                GameAudio.Sfx("hit2");
                Shake = Mathf.Max(Shake, 0.15f);
            }
            lastDamage = d; DirectDamage(d);
            return false;
        }
        cur = null; buf = null;
        stun = m.stun;
        p.x += dir * m.knock;
        transform.position = p;
        if (m.launch) vy = 8.5f;
        if (m.knockdown) { downT = 1.15f; downElapsed = 0f; }
        energy = Mathf.Min(100f, energy + d * 0.35f);
        Fx.Sparks(chest, new Color(1f, 0.35f, 0.15f), m.big ? 16 : 9, m.big ? 8f : 6f);
        HitStop = Mathf.Max(HitStop, m.big ? 0.09f : 0.05f);
        GameAudio.Sfx(m.big ? "hit2" : "hit");
        if (Time.unscaledTime - lastPainT > 0.3f) { lastPainT = Time.unscaledTime; GameAudio.Pain(female, m.big); }
        Shake = Mathf.Max(Shake, m.big ? 0.18f : 0.07f);
        lastDamage = d; DirectDamage(d);
        return true;
    }

    public void ResetForRound(float x)
    {
        hp = maxHp; cur = null; buf = null; stun = 0f; downT = 0f; downElapsed = 0f;
        vy = 0f; jumpY = 0f; blocking = false; moving = false; blockTimer = 0f; aiTimer = 1f; guard = 0f; blockCd = 0f; seenMove = null;
        controlsEnabled = false;
        burnT = poisonT = shockT = 0f; hit2Done = false;
        facing = x < 0f ? 1 : -1;
        transform.position = new Vector3(x, 0, 0);
        disp = null;
    }

    void DirectDamage(float d)
    {
        if (Dead) return;
        hp = Mathf.Max(0f, hp - d);
        if (Dead) OnDeath();
    }

    void OnDeath()
    {
        GameAudio.Sfx("ko");
        GameAudio.Pain(female, true);
        downT = 999f; downElapsed = 0f; cur = null;
        burnT = poisonT = shockT = 0f;
        HitStop = 0.15f; Shake = 0.3f;
    }

    // burn / poison / shock ticking
    void Status(float dt)
    {
        statusFx -= dt;
        Vector3 p = transform.position;
        if (burnT > 0f)
        {
            burnT -= dt;
            DirectDamage(burnDps * dt);
            if (statusFx <= 0f) Fx.Spawn(p + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.4f, 3.4f), 0), new Color(1f, 0.5f, 0.1f), 0.6f, 0.4f, Vector3.up * 2.5f, 0f).Mode(1);
        }
        if (poisonT > 0f)
        {
            poisonT -= dt;
            DirectDamage(poisonDps * dt);
            if (statusFx <= 0f) Fx.Spawn(p + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.4f, 3.4f), 0), new Color(0.5f, 0.15f, 0.7f), 0.45f, 0.4f, Vector3.up * 1.5f, 0f);
        }
        if (shockT > 0f)
        {
            shockT -= dt;
            stun = Mathf.Max(stun, 0.1f);
            if (statusFx <= 0f) Fx.Spawn(p + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.4f, 3.4f), 0), new Color(0.7f, 0.9f, 1f), 0.4f, 0.2f, Vector3.zero, 0f);
        }
        if (statusFx <= 0f) statusFx = 0.12f;
    }

    // ---------- update ----------
    void Update()
    {
        if (weapon == null || body == null) return;
        float dt = Time.deltaTime;
        if (!Dead && controlsEnabled) { Status(dt); if (!Dead) Tick(dt); }
        else moving = false;
        Animate(dt);
    }

    void Motion(float dt, float dx)
    {
        Vector3 p = transform.position;
        p.x += ClampApproach(dx);
        vy -= 25f * dt;
        jumpY += vy * dt;
        if (jumpY < 0) { jumpY = 0; vy = 0; }
        p.x = Mathf.Clamp(p.x, -7f, 7f);
        p.y = jumpY;
        transform.position = p;
    }

    void Tick(float dt)
    {
        Intent it = (isPlayer && !autoPlay) ? PlayerIntent() : AIIntent();
        guard = Mathf.Max(0f, guard - 0.3f * dt);
        energy = Mathf.Min(100f, energy + 2.5f * dt);   // trickles in on its own
        if (it.punch) Buffer("U");
        else if (it.kick) Buffer(it.down ? "D" : "H");
        else if (it.light) Buffer("J");
        else if (it.heavy) Buffer("K");
        else if (it.ult) Buffer("I");
        bufT -= dt;
        if (bufT <= 0f) buf = null;

        moving = false;
        blocking = false;

        if (downT > 0f) { downT -= dt; Motion(dt, 0f); return; }
        if (stun > 0f) { stun -= dt; Motion(dt, 0f); return; }

        if (target != null && cur == null) facing = target.transform.position.x >= transform.position.x ? 1 : -1;

        float dx = 0f;
        if (cur != null)
        {
            mt += dt;
            float u = mt / cur.dur;
            float want = cur.step * Mathf.Clamp01(u / Mathf.Max(0.01f, cur.hitAt));
            dx = (want - stepped) * facing;
            stepped = want;
            if (!hitDone && u >= cur.hitAt) { hitDone = true; DoHit(cur); }
            if (cur != null && cur.hit2At > 0f && !hit2Done && u >= cur.hit2At) { hit2Done = true; DoHit(cur); }
            if (hitDone && cur != null && buf != null && cur.next != null &&
                ((buf == "U" && (cur.name == "jab" || cur.name == "cross")) || (buf == "H" && cur.name == "kick1")))
                StartMove(cur.next);
            else if (u >= 1f || (cur.air && Grounded && mt > 0.15f)) cur = null;
        }
        else
        {
            blocking = it.block && Grounded;
            if (!blocking)
            {
                dx = it.move * (isPlayer ? 4.5f : 3.5f) * dt;
                moving = it.move != 0f && Grounded;
                if (it.jump && Grounded) vy = 9f;
                if (buf != null)
                {
                    if (!Grounded) { if (buf == "U" || buf == "H" || buf == "D") StartMove("jumpkick"); }
                    else if (buf == "U") StartMove("jab");
                    else if (buf == "H") StartMove("kick1");
                    else if (buf == "D") StartMove("sweep");
                    else if (buf == "J") StartMove("slash");
                    else if (buf == "K") StartMove("smash");
                    else if (buf == "I" && energy >= 100f) StartMove("power");
                    else buf = null;
                }
            }
        }
        Motion(dt, dx);
    }

    // ---------- animation ----------
    Pose Locomotion()
    {
        Pose p = G();
        float b = Mathf.Sin(Time.time * 2.2f + transform.position.x);
        p.lean += b * 1.5f;
        p.hipY = b * 0.02f;
        p.wAng += b * 2f;
        if (moving)
        {
            walkT += Time.deltaTime * 9f;
            float s = Mathf.Sin(walkT);
            p.thighF = 20f + s * 30f;
            p.shinF = -22f - Mathf.Max(0f, -s) * 40f;
            p.thighB = -14f - s * 30f;
            p.shinB = -12f - Mathf.Max(0f, s) * 40f;
        }
        return p;
    }

    void Animate(float dt)
    {
        Pose target;
        float fall = 0f;
        bool down = Dead || downT > 0f;
        if (down)
        {
            downElapsed += dt;
            target = DownPose;
            fall = Mathf.Clamp01(downElapsed / 0.22f);
            if (!Dead && downT < 0.45f) fall = Mathf.Clamp01(downT / 0.45f);
        }
        else if (stun > 0f) target = HitPose;
        else if (cur != null) target = cur.Sample(mt / cur.dur);
        else if (blocking) target = BlockPose;
        else if (!Grounded) target = AirPose;
        else target = Locomotion();

        disp = disp == null ? target.Clone() : Pose.Lerp(disp, target, 1f - Mathf.Exp(-dt * (cur != null ? 36f : 22f)));
        if (shockT > 0f)
        {
            Pose j = disp.Clone();
            j.lean += Random.Range(-12f, 12f);
            j.lUp += Random.Range(-30f, 30f);
            j.thighF += Random.Range(-15f, 15f);
            j.thighB += Random.Range(-15f, 15f);
            ApplyPose(j, fall);
        }
        else ApplyPose(disp, fall);
    }

    static float Leg(float th, float sh)
    {
        return L1 * Mathf.Cos(th * Mathf.Deg2Rad) + L2 * Mathf.Cos((th + sh) * Mathf.Deg2Rad);
    }

    // two-bone IK in the arm's plane: returns shoulder and elbow angles that put the hand on `t`
    static void Ik(Vector2 t, out float a1, out float a2)
    {
        float d = Mathf.Clamp(t.magnitude, 0.15f, AL1 + AL2 - 0.01f);
        float psi = Mathf.Atan2(t.y, t.x);
        float cosA = (AL1 * AL1 + d * d - AL2 * AL2) / (2f * AL1 * d);
        float psi1 = psi - Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f));   // elbow ends up below the line
        Vector2 e = new Vector2(Mathf.Cos(psi1), Mathf.Sin(psi1)) * AL1;
        Vector2 hand = new Vector2(Mathf.Cos(psi), Mathf.Sin(psi)) * d;
        Vector2 f = hand - e;
        float psi2 = Mathf.Atan2(f.y, f.x);
        a1 = psi1 * Mathf.Rad2Deg + 90f;
        a2 = psi2 * Mathf.Rad2Deg + 90f - a1;
    }

    void ApplyPose(Pose p, float fall)
    {
        Quaternion yaw = Quaternion.Euler(0, (facing == 1 ? 0 : 180) + yawOffset, 0);
        transform.rotation = yaw;

        // keep the lowest foot on the ground
        float fF = Leg(p.thighF, p.shinF), fB = Leg(p.thighB, p.shinB);
        hips.localPosition = new Vector3(p.hipX * 0.5f, Mathf.Max(fF, fB) + FootH + p.hipY, 0);
        thighFp.localRotation = Quaternion.Euler(0, 0, p.thighF);
        shinFp.localRotation = Quaternion.Euler(0, 0, p.shinF);
        thighBp.localRotation = Quaternion.Euler(0, 0, p.thighB);
        shinBp.localRotation = Quaternion.Euler(0, 0, p.shinB);
        spine.localRotation = Quaternion.Euler(0, 0, -p.lean);
        headP.localRotation = Quaternion.Euler(0, 0, -p.head + p.lean * 0.5f);

        weaponP.localPosition = new Vector3(p.wx, p.wy, 0);
        weaponP.localRotation = Quaternion.Euler(0, 0, p.wAng);
        if (nunP != null)
        {
            float dtn = Mathf.Max(Time.deltaTime, 0.0001f);
            float vel = Mathf.Clamp((p.wAng - lastWAng) / dtn, -900f, 900f);
            lastWAng = p.wAng;
            float want = -25f - Mathf.Clamp(vel * 0.06f, -75f, 75f);
            nunA = Mathf.SmoothDampAngle(nunA, want, ref nunV, 0.05f);
            nunP.localRotation = Quaternion.Euler(0, 0, nunA);
        }

        // leading hand always holds the weapon grip
        float a1, a2;
        Ik(new Vector2(p.wx, p.wy), out a1, out a2);
        upR.localRotation = Quaternion.Euler(0, 0, a1);
        foreR.localRotation = Quaternion.Euler(0, 0, a2);

        // free hand: FK, or onto the shaft for two-handed grips
        float w = p.lTwo * twoHand;
        float r = p.wAng * Mathf.Deg2Rad;
        Ik(new Vector2(p.wx, p.wy) + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * grip2, out a1, out a2);
        upL.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(p.lUp, a1, w));
        foreL.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(p.lFo, a2, w));

        if (pony1 != null)
        {
            float dtp = Mathf.Max(Time.deltaTime, 0.0001f);
            float vx = Mathf.Clamp((transform.position.x - lastX) / dtp * facing, -12f, 12f);
            lastX = transform.position.x;
            float want = -28f - vx * 3f - p.lean * 0.6f;
            ponyA = Mathf.SmoothDampAngle(ponyA, want, ref ponyV, 0.09f);
            pony1.localRotation = Quaternion.Euler(0, 0, ponyA);
            float bend = Mathf.Clamp(-ponyV * 0.03f, -25f, 25f);
            pony2.localRotation = Quaternion.Euler(0, 0, 20f + bend);
            pony3.localRotation = Quaternion.Euler(0, 0, 14f + bend * 0.7f);
        }

        // knocked down: tip over backwards, feet stay put
        body.localRotation = Quaternion.Euler(0, 0, 90f * fall);
        body.localPosition = new Vector3(0, 0.32f * fall, 0);
    }
}
