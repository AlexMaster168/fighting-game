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
        var rr = tr.GetComponent<Renderer>();
        rr.sharedMaterial = Fighter.GlowOf(c);
        rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

    // puffs of dust kicked up from the floor (not glowing)
    public static void Dust(Vector3 pos, int n, float spread)
    {
        for (int i = 0; i < n; i++)
        {
            Vector3 v = new Vector3(Random.Range(-1f, 1f) * spread, Random.Range(0.2f, 1.2f), Random.Range(-0.5f, 0.5f));
            Fx f = Spawn(pos + new Vector3(Random.Range(-0.3f, 0.3f), 0.1f, 0f), Color.white, Random.Range(0.25f, 0.45f), Random.Range(0.35f, 0.6f), v, 0f).Mode(1);
            f.GetComponent<Renderer>().sharedMaterial = Fighter.MatOf(new Color(0.55f, 0.5f, 0.45f));
        }
    }

    public static void Seg(Vector3 a, Vector3 b, Color c, float w, float life)
    {
        Vector3 d = b - a;
        var tr = Fighter.Part(null, "bolt", PrimitiveType.Cylinder, (a + b) / 2f, new Vector3(w, d.magnitude / 2f, w), c, Vector3.zero);
        tr.GetComponent<Renderer>().sharedMaterial = Fighter.GlowOf(c);
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
    public float critBonus, hop, stepFrom, teleportAt = -1f;
    public float[] hits;                 // several hit moments for flurries; defaults to hitAt
    public int ability;                  // 1..3: a cast that fires an ability instead of hitting
    public bool big, low, launch, knockdown, air, weaponMove, unblockable, power, kick, slam, flashLine, ranged;
    public bool armor;                   // super armor: hits taken during the move do not interrupt it

    float[] hitCache;
    public float[] Hits
    {
        get
        {
            if (hitCache == null) hitCache = hits ?? (hit2At > 0f ? new[] { hitAt, hit2At } : new[] { hitAt });
            return hitCache;
        }
    }
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
    public static bool DebugLog;   // demo runs log dashes and throws

    public bool isPlayer, controlsEnabled, autoPlay;
    public Item weapon, helmet, armor;
    public Fighter target;
    public bool female, victory, boss;
    public int skinId;
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
    // evasion: dash with i-frames, invulnerability after getting up, no chain-stun from ranged hits
    float dashT, dashDir, dashCd, invulnT, rangedImmuneT, aiAbilityCd, aiDashCd, shurikenT, lastTapT;
    int lastTapDir;
    public int shuriken = 3;
    public const int MaxShuriken = 3;
    bool shown = true;
    Move seenMove;
    float lastPainT = -10f;
    bool hitDone, blocking, moving;
    string buf;
    bool hit2Done;
    float burnT, burnDps, poisonT, poisonDps, shockT, statusFx, lastDamage, nunA, nunV, lastWAng;
    Transform nunP;
    int facing = 1;
    float giantT, invertT, slowT, mirrorT, bossCd = 5f;
    public bool disarmed;                // the weapon was knocked out of the hands for this round
    Item realWeapon;
    GameObject droppedWeapon;
    static bool HitCrit;                 // the blow being dealt right now is a critical one
    public static string EventMsg = "";
    public static float EventMsgT;
    public int[] bossPowers;             // story bosses: their strange powers
    int bossNext;
    public static string BossMsg = "";
    public static float BossMsgT;
    float rageT, hasteT, shieldT, shieldFx, crouchT, landT, moveDir, yawCur, idSeed, bounceY, capeA, capeV, flashT;
    bool critNext, tpDone, wasAir, snapYaw = true, groundHit, flashDirty;
    int hitIdx, hitVariant;
    Transform capeP;
    Renderer[] rends;
    Color[] baseCols;
    MaterialPropertyBlock mpb;
    TrailRenderer wTrail, fTrail;

    struct Intent { public float move; public int ab; public bool jump, light, heavy, punch, kick, block, ult, down, dash, throwStar; }

    float Pace { get { return (hasteT > 0f ? 1.35f : 1f) * (slowT > 0f ? 0.55f : 1f) * (weapon.faction == Faction.Dynasty ? 1.12f : 1f); } }
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
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Material baseMat, glowMat, trailMat;

    // BaseMat / GlowMat / TrailMat ship as assets so their shaders and keywords survive a player build
    public static Material MatX(Color c, float metal, float gloss, bool glow)
    {
        string key = c.ToString("F3") + "|" + metal + "|" + gloss + "|" + glow;
        Material m;
        if (mats.TryGetValue(key, out m)) return m;
        if (glow)
        {
            if (glowMat == null) glowMat = Resources.Load<Material>("GlowMat");
            m = glowMat != null ? new Material(glowMat) : NewStandard();
            m.color = c;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 1.6f);
        }
        else
        {
            if (baseMat == null) baseMat = Resources.Load<Material>("BaseMat");
            m = baseMat != null ? new Material(baseMat) : NewStandard();
            m.color = c;
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Glossiness", gloss);
        }
        mats[key] = m;
        return m;
    }

    static Material NewStandard()
    {
        Shader sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        return new Material(sh);
    }

    public static Material MatOf(Color c) { return MatX(c, 0f, 0.25f, false); }
    public static Material GlowOf(Color c) { return MatX(c, 0f, 0.5f, true); }
    static Material Mat(Color c) { return MatOf(c); }

    static readonly HashSet<string> ArmorParts = new HashSet<string>
    {
        "cuirass", "chestplate", "pauldron", "pauldron2", "greave", "cuisse", "gauntlet", "kneecap", "bracer",
        "helm", "brow", "crest", "hornL", "hornR", "mask", "buckle", "skirt",
    };

    // swap matte materials for metal ones on the parts that should shine
    static void Shine(Transform root, float metal, float gloss, Func<string, bool> pick)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (r is TrailRenderer || !pick(r.name) || r.sharedMaterial.IsKeywordEnabled("_EMISSION")) continue;
            r.sharedMaterial = MatX(r.sharedMaterial.color, metal, gloss, false);
        }
    }

    public static TrailRenderer MakeTrail(Transform parent, Vector3 local, Color c, float width, float time)
    {
        var g = new GameObject("trail");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = local;
        var tr = g.AddComponent<TrailRenderer>();
        if (trailMat == null) trailMat = Resources.Load<Material>("TrailMat");
        if (trailMat == null) trailMat = new Material(Shader.Find("Sprites/Default"));
        tr.sharedMaterial = trailMat;
        tr.time = time;
        tr.minVertexDistance = 0.04f;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, 0f));
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.Lerp(c, Color.white, 0.55f), 0f), new GradientColorKey(c, 1f) },
                   new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = gr;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.emitting = false;
        return tr;
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
        disp = null; cur = null; pony1 = pony2 = pony3 = null; nunP = null; capeP = null; snapYaw = true; idSeed = Random.value * 10f;

        Skin sk = Db.Skins[Mathf.Clamp(skinId, 0, Db.Skins.Length - 1)];
        Color skin = sk.skinCol, cloth = sk.cloth,
              wood = new Color(0.36f, 0.24f, 0.14f), gold = new Color(0.92f, 0.78f, 0.34f),
              leather = sk.extra == 0 ? new Color(0.26f, 0.17f, 0.12f) : Color.Lerp(sk.cloth, Color.black, 0.4f);
        Vector3 z = Vector3.zero;
        PrimitiveType cube = PrimitiveType.Cube, sph = PrimitiveType.Sphere;
        Color pink = new Color(1f, 0.5f, 0.74f), pinkDark = new Color(0.86f, 0.3f, 0.58f);

        float depth = female ? 0.42f : 0.52f;          // chest, seen from the side
        float width = female ? 0.62f : 0.78f;          // across the shoulders
        float sz = width / 2f - 0.03f;
        scale = (female ? 1.28f : 1.4f) * (boss ? 1.15f : 1f);   // bosses tower over you
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
        if (st >= 3 || sk.extra == 3)
        {
            // the cape hangs from a pivot at the shoulders so it can flutter
            capeP = Pivot(chest, "capeP", new Vector3(-depth / 2f - 0.07f, 0f, 0));
            Part(capeP, "cape", cube, new Vector3(-0.02f, -0.72f, 0), new Vector3(0.08f, 1.5f, width + 0.2f), st >= 3 ? Db.FactionColor(armor.faction) : sk.cape, z);
        }

        // head
        Part(chest, "neck", PrimitiveType.Capsule, new Vector3(0, 0.1f, 0), new Vector3(0.2f, 0.13f, 0.2f), skin, z);
        headP = Pivot(chest, "head", new Vector3(0, 0.18f, 0));
        Part(headP, "skull", sph, new Vector3(0, 0.22f, 0), new Vector3(0.48f, 0.54f, 0.46f), skin, z);
        Transform eyeL = Part(headP, "eyeL", cube, new Vector3(0.21f, 0.25f, -0.1f), new Vector3(0.05f, 0.06f, 0.08f), sk.eye, z);
        Transform eyeR = Part(headP, "eyeR", cube, new Vector3(0.21f, 0.25f, 0.1f), new Vector3(0.05f, 0.06f, 0.08f), sk.eye, z);
        if (sk.glowEyes || boss)
        {
            Color ec = boss ? new Color(1f, 0.15f, 0.1f) : sk.eye;
            eyeL.GetComponent<Renderer>().sharedMaterial = GlowOf(ec);
            eyeR.GetComponent<Renderer>().sharedMaterial = GlowOf(ec);
        }
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

        // skin details
        Color acc2 = sk.accent;
        switch (sk.extra)
        {
            case 1: // ninja
                Part(headP, "headband", PrimitiveType.Cylinder, new Vector3(-0.01f, 0.36f, 0), new Vector3(0.52f, 0.05f, 0.5f), acc2, z);
                Part(headP, "bandTail", cube, new Vector3(-0.32f, 0.3f, 0.08f), new Vector3(0.3f, 0.06f, 0.05f), acc2, new Vector3(0, 0, -30));
                Part(headP, "ninjaMask", cube, new Vector3(0.14f, 0.1f, 0), new Vector3(0.24f, 0.2f, 0.47f), cloth, z);
                break;
            case 2: // samurai
                Part(spine, "sash", cube, new Vector3(0, 0.4f, 0), new Vector3(depth + 0.1f, 0.12f, width + 0.04f), acc2, new Vector3(0, 0, 32));
                Part(spine, "obi", cube, new Vector3(0, 0.05f, 0), new Vector3(depth + 0.08f, 0.2f, width + 0.06f), acc2, z);
                Part(headP, "topknot", sph, new Vector3(-0.12f, 0.58f, 0), Vector3.one * 0.17f, hair, z);
                break;
            case 3: // royal
                Part(chest, "collar", PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0), new Vector3(depth + 0.16f, 0.05f, width + 0.1f), acc2, z);
                Part(spine, "royalBelt", cube, new Vector3(0, 0.05f, 0), new Vector3(depth + 0.08f, 0.14f, width + 0.06f), acc2, z);
                break;
            case 4: // demon
                Part(headP, "dhornL", cube, new Vector3(0.02f, 0.55f, -0.16f), new Vector3(0.07f, 0.28f, 0.07f), acc2, new Vector3(-20, 0, -15));
                Part(headP, "dhornR", cube, new Vector3(0.02f, 0.55f, 0.16f), new Vector3(0.07f, 0.28f, 0.07f), acc2, new Vector3(20, 0, -15));
                break;
            case 5: // zombie
                Part(spine, "rag", cube, new Vector3(depth / 2f, 0.2f, 0.12f), new Vector3(0.06f, 0.35f, 0.2f), acc2, new Vector3(0, 0, 18));
                Part(spine, "rag", cube, new Vector3(depth / 2f, 0.55f, -0.15f), new Vector3(0.06f, 0.22f, 0.16f), acc2, new Vector3(0, 0, -14));
                break;
            case 6: // robot
                Part(headP, "antenna", PrimitiveType.Cylinder, new Vector3(-0.05f, 0.64f, 0), new Vector3(0.03f, 0.14f, 0.03f), cloth, z);
                Part(headP, "antennaTip", sph, new Vector3(-0.05f, 0.8f, 0), Vector3.one * 0.09f, acc2, z).GetComponent<Renderer>().sharedMaterial = GlowOf(new Color(1f, 0.2f, 0.2f));
                Part(spine, "core", sph, new Vector3(depth / 2f + 0.02f, 0.45f, 0), Vector3.one * 0.16f, acc2, z).GetComponent<Renderer>().sharedMaterial = GlowOf(acc2);
                break;
        }

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

        // materials: metal where it should shine
        if (sk.metal) Shine(body, 0.65f, 0.7f, n => true);
        Shine(body, 0.55f, 0.62f, n => ArmorParts.Contains(n));
        Shine(weaponP, 0.85f, 0.8f, n => n != "rod");
        if (boss)
        {
            var bossAura = new GameObject("bossAura").AddComponent<Light>();
            bossAura.transform.SetParent(chest, false);
            bossAura.type = LightType.Point; bossAura.color = Db.FactionColor(weapon.faction); bossAura.range = 5f; bossAura.intensity = 2f;
        }
        if (sk.extra == 7)
        {
            var aura = new GameObject("aura").AddComponent<Light>();
            aura.transform.SetParent(chest, false);
            aura.type = LightType.Point; aura.color = new Color(1f, 0.85f, 0.4f); aura.range = 4f; aura.intensity = 1.4f;
        }

        // renderers for the hit flash (before the trails, which are renderers too)
        var list = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>()) if (!(r is TrailRenderer)) list.Add(r);
        rends = list.ToArray();
        baseCols = new Color[rends.Length];
        for (int i = 0; i < rends.Length; i++) baseCols[i] = rends[i].sharedMaterial.color;
        flashT = 0f; flashDirty = false;

        Color tc = Db.FactionColor(weapon.faction);
        wTrail = MakeTrail(weaponP, new Vector3(weaponLen, 0f, 0f), tc, 0.5f * scale, 0.16f);
        fTrail = MakeTrail(shinFp, new Vector3(0.1f, -L2, 0f), Color.Lerp(tc, Color.white, 0.6f), 0.3f * scale, 0.14f);

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
        if (Db.IsFist(weapon)) return;   // bare hands: nothing to hold
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
            name = "power", power = true, armor = true, dur = 1.0f, hitAt = 0.55f, dmg = wd * 4.6f + 15f, reach = PowerRange, step = 0.4f, stun = 0.7f, knock = 1.4f,
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

        BuildFactionMoves(ws, wr, wd);
        BuildAbilityMoves();
        foreach (string k in new[] { "kick1", "round", "sweep", "jumpkick" }) moves[k].kick = true;
    }

    // every faction swings its weapon its own way
    void BuildFactionMoves(float ws, float wr, float wd)
    {
        switch (weapon.faction)
        {
            case Faction.Legion:
                // heavy overhead cleave that chains into a rising cut; the heavy is a leaping ground slam
                moves["slash"] = new Move
                {
                    name = "slash", next = "slash2", weaponMove = true, dur = 0.7f / ws, hitAt = 0.52f, dmg = wd * 1.4f, reach = wr, step = 0.5f, stun = 0.4f, knock = 0.9f, gain = 11,
                    times = new[] { 0f, 0.38f, 0.52f, 0.75f, 1f },
                    keys = new[]
                    {
                        G(),
                        K(p => { p.lean = -12; p.hipX = -0.15f; p.wx = -0.05f; p.wy = 0.85f; p.wAng = 170; p.thighF = 20; p.shinF = -24; p.thighB = -26; p.shinB = -10; }),
                        K(p => { p.lean = 32; p.hipX = 0.45f; p.hipY = -0.1f; p.wx = 1.0f; p.wy = -0.4f; p.wAng = -80; p.thighF = 55; p.shinF = -48; p.thighB = -36; p.shinB = -26; }),
                        K(p => { p.lean = 30; p.hipX = 0.45f; p.hipY = -0.1f; p.wx = 1.0f; p.wy = -0.45f; p.wAng = -84; p.thighF = 55; p.shinF = -48; p.thighB = -36; p.shinB = -26; }),
                        G(),
                    },
                };
                moves["slash2"] = new Move
                {
                    name = "slash2", weaponMove = true, armor = true, dur = 0.6f / ws, hitAt = 0.45f, dmg = wd * 1.2f, reach = wr, step = 0.4f, stun = 0.5f, knock = 0.8f, big = true, launch = true, knockdown = true, gain = 12,
                    times = new[] { 0f, 0.25f, 0.45f, 0.7f, 1f },
                    keys = new[]
                    {
                        K(p => { p.lean = 30; p.hipX = 0.45f; p.wx = 1.0f; p.wy = -0.45f; p.wAng = -84; p.thighF = 55; p.shinF = -48; p.thighB = -36; }),
                        K(p => { p.lean = 24; p.hipY = -0.2f; p.wx = 0.7f; p.wy = -0.55f; p.wAng = -60; p.thighF = 50; p.shinF = -60; p.thighB = -30; p.shinB = -30; }),
                        K(p => { p.lean = -14; p.hipX = 0.3f; p.wx = 0.45f; p.wy = 0.85f; p.wAng = 110; p.thighF = 24; p.shinF = -20; }),
                        K(p => { p.lean = -12; p.hipX = 0.3f; p.wx = 0.4f; p.wy = 0.85f; p.wAng = 115; p.thighF = 24; p.shinF = -20; }),
                        G(),
                    },
                };
                moves["smash"] = new Move
                {
                    name = "smash", weaponMove = true, slam = true, armor = true, dur = 1.1f / ws, hitAt = 0.58f, dmg = wd * 2.0f, reach = wr + 2.6f, step = 0.3f, stun = 0.6f, knock = 1.3f, big = true, knockdown = true, hop = 5.5f, gain = 15,
                    times = new[] { 0f, 0.4f, 0.58f, 0.82f, 1f },
                    keys = new[]
                    {
                        G(),
                        K(p => { p.lean = -16; p.hipY = 0.05f; p.wx = -0.1f; p.wy = 0.9f; p.wAng = 175; p.thighF = 60; p.shinF = -90; p.thighB = 30; p.shinB = -90; }),
                        K(p => { p.lean = 40; p.hipY = -0.25f; p.wx = 1.0f; p.wy = -0.6f; p.wAng = -95; p.thighF = 70; p.shinF = -80; p.thighB = -40; p.shinB = -40; }),
                        K(p => { p.lean = 38; p.hipY = -0.25f; p.wx = 1.0f; p.wy = -0.62f; p.wAng = -95; p.thighF = 70; p.shinF = -80; p.thighB = -40; p.shinB = -40; }),
                        G(),
                    },
                };
                break;

            case Faction.Dynasty:
                // a fast three-hit flurry; the heavy is a flying kick that crosses half the arena
                moves["slash"] = new Move
                {
                    name = "slash", weaponMove = true, dur = 0.7f / ws, hitAt = 0.22f, hits = new[] { 0.22f, 0.48f, 0.76f }, dmg = wd * 0.6f, reach = wr, step = 0.9f, stun = 0.2f, knock = 0.25f, gain = 7,
                    times = new[] { 0f, 0.12f, 0.22f, 0.36f, 0.48f, 0.62f, 0.76f, 1f },
                    keys = new[]
                    {
                        G(),
                        K(p => { p.lean = -4; p.wx = 0.1f; p.wy = 0.45f; p.wAng = 150; p.thighF = 26; p.shinF = -30; }),
                        K(p => { p.lean = 20; p.hipX = 0.3f; p.wx = 0.95f; p.wy = -0.1f; p.wAng = -40; p.thighF = 44; p.shinF = -38; p.thighB = -32; }),
                        K(p => { p.lean = 6; p.hipX = 0.25f; p.wx = 0.4f; p.wy = 0.4f; p.wAng = 130; p.thighF = 38; p.shinF = -34; p.thighB = -28; }),
                        K(p => { p.lean = 26; p.hipX = 0.4f; p.wx = 1.0f; p.wy = -0.3f; p.wAng = -70; p.thighF = 48; p.shinF = -40; p.thighB = -34; }),
                        K(p => { p.lean = 0; p.hipX = 0.35f; p.wx = 0.2f; p.wy = 0.2f; p.wAng = 200; p.thighF = 40; p.shinF = -36; p.thighB = -30; }),
                        K(p => { p.lean = 22; p.hipX = 0.5f; p.wx = 1.05f; p.wy = 0.05f; p.wAng = 0; p.thighF = 50; p.shinF = -44; p.thighB = -36; }),
                        G(),
                    },
                };
                moves["smash"] = new Move
                {
                    name = "smash", weaponMove = true, kick = true, dur = 0.85f / ws, hitAt = 0.5f, stepFrom = 0.15f, dmg = wd * 1.7f, reach = wr, step = 3.0f, stun = 0.5f, knock = 1.2f, big = true, knockdown = true, hop = 7f, gain = 13,
                    times = new[] { 0f, 0.15f, 0.5f, 0.8f, 1f },
                    keys = new[]
                    {
                        G(),
                        K(p => { p.lean = 20; p.hipY = -0.3f; p.thighF = 60; p.shinF = -90; p.thighB = -20; p.shinB = -60; }),
                        K(p => { p.lean = -18; p.thighF = 95; p.shinF = -4; p.thighB = 10; p.shinB = -95; p.lTwo = 0; p.wx = 0.2f; p.wy = 0.4f; p.wAng = 120; p.lUp = 40; p.lFo = 40; }),
                        K(p => { p.lean = -14; p.thighF = 90; p.shinF = -6; p.thighB = 10; p.shinB = -95; p.lTwo = 0; p.wx = 0.2f; p.wy = 0.4f; p.wAng = 120; p.lUp = 40; p.lFo = 40; }),
                        G(),
                    },
                };
                break;

            case Faction.Heralds:
                // precision: a long lunging thrust, and a drawn-sword dash that cuts clean through
                moves["slash"] = new Move
                {
                    name = "slash", weaponMove = true, dur = 0.48f / ws, hitAt = 0.45f, dmg = wd * 1.15f, reach = wr + 0.9f, step = 1.1f, stun = 0.3f, knock = 0.6f, critBonus = 0.2f, gain = 10,
                    times = new[] { 0f, 0.3f, 0.45f, 0.7f, 1f },
                    keys = new[]
                    {
                        G(),
                        K(p => { p.lean = -6; p.hipX = -0.2f; p.wx = 0.1f; p.wy = 0.1f; p.wAng = 5; p.thighF = 20; p.shinF = -30; p.thighB = -20; }),
                        K(p => { p.lean = 22; p.hipX = 0.6f; p.wx = 1.05f; p.wy = 0.05f; p.wAng = 0; p.thighF = 70; p.shinF = -60; p.thighB = -50; p.shinB = -10; }),
                        K(p => { p.lean = 20; p.hipX = 0.6f; p.wx = 1.05f; p.wy = 0.05f; p.wAng = 0; p.thighF = 70; p.shinF = -60; p.thighB = -50; p.shinB = -10; }),
                        G(),
                    },
                };
                moves["smash"] = new Move
                {
                    name = "smash", weaponMove = true, flashLine = true, dur = 1.05f / ws, hitAt = 0.62f, stepFrom = 0.5f, dmg = wd * 2.3f, reach = wr + 0.4f, step = 3.2f, stun = 0.55f, knock = 1.3f, big = true, knockdown = true, critBonus = 0.4f, gain = 14,
                    times = new[] { 0f, 0.45f, 0.55f, 0.62f, 0.85f, 1f },
                    keys = new[]
                    {
                        G(),
                        K(p => { p.lean = 28; p.hipY = -0.3f; p.wx = -0.2f; p.wy = -0.25f; p.wAng = -150; p.thighF = 60; p.shinF = -80; p.thighB = -40; p.shinB = -40; }),
                        K(p => { p.lean = 30; p.hipY = -0.32f; p.wx = -0.22f; p.wy = -0.25f; p.wAng = -152; p.thighF = 62; p.shinF = -82; p.thighB = -40; p.shinB = -40; }),
                        K(p => { p.lean = 18; p.hipX = 0.6f; p.hipY = -0.15f; p.wx = 1.0f; p.wy = 0.3f; p.wAng = 30; p.thighF = 65; p.shinF = -55; p.thighB = -50; p.shinB = -20; }),
                        K(p => { p.lean = 10; p.hipX = 0.5f; p.wx = 0.8f; p.wy = 0.55f; p.wAng = 60; p.thighF = 50; p.shinF = -40; p.thighB = -40; }),
                        G(),
                    },
                };
                break;

            default:
                // shadow: two quick stabs, and a vanish that reappears behind the enemy
                moves["slash"] = new Move
                {
                    name = "slash", weaponMove = true, dur = 0.5f / ws, hitAt = 0.3f, hits = new[] { 0.3f, 0.62f }, dmg = wd * 0.75f, reach = wr, step = 0.6f, stun = 0.22f, knock = 0.3f, critBonus = 0.1f, gain = 8,
                    times = new[] { 0f, 0.2f, 0.3f, 0.46f, 0.62f, 1f },
                    keys = new[]
                    {
                        G(),
                        K(p => { p.lean = -4; p.hipX = -0.1f; p.wx = 0.2f; p.wy = 0.15f; p.wAng = 20; }),
                        K(p => { p.lean = 20; p.hipX = 0.4f; p.wx = 1.0f; p.wy = 0f; p.wAng = -5; p.thighF = 44; p.shinF = -38; p.thighB = -32; }),
                        K(p => { p.lean = 6; p.hipX = 0.3f; p.wx = 0.3f; p.wy = 0.3f; p.wAng = 30; p.thighF = 38; p.shinF = -34; }),
                        K(p => { p.lean = 24; p.hipX = 0.45f; p.wx = 1.05f; p.wy = -0.15f; p.wAng = -15; p.thighF = 48; p.shinF = -40; p.thighB = -34; }),
                        G(),
                    },
                };
                moves["smash"] = new Move
                {
                    name = "smash", weaponMove = true, teleportAt = 0.3f, dur = 0.9f / ws, hitAt = 0.62f, dmg = wd * 1.9f, reach = wr + 0.5f, step = 0f, stun = 0.55f, knock = 1.0f, big = true, knockdown = true, critBonus = 0.25f, gain = 13,
                    times = new[] { 0f, 0.28f, 0.4f, 0.62f, 0.85f, 1f },
                    keys = new[]
                    {
                        G(),
                        K(p => { p.lean = 30; p.hipY = -0.35f; p.thighF = 60; p.shinF = -90; p.thighB = -30; p.shinB = -60; p.wx = 0.1f; p.wy = -0.1f; p.wAng = -120; }),
                        K(p => { p.lean = 10; p.hipY = -0.2f; p.wx = 0f; p.wy = 0.6f; p.wAng = 160; p.thighF = 40; p.shinF = -60; }),
                        K(p => { p.lean = 34; p.hipX = 0.4f; p.wx = 1.0f; p.wy = -0.4f; p.wAng = -85; p.thighF = 55; p.shinF = -48; p.thighB = -36; p.shinB = -26; }),
                        K(p => { p.lean = 32; p.hipX = 0.4f; p.wx = 1.0f; p.wy = -0.42f; p.wAng = -88; p.thighF = 55; p.shinF = -48; p.thighB = -36; p.shinB = -26; }),
                        G(),
                    },
                };
                break;
        }
    }

    // short casts that fire the abilities; they can be chained one after another
    void BuildAbilityMoves()
    {
        Pose palm = OneHand(p => { p.lean = 10; p.hipX = 0.15f; p.lUp = 90; p.lFo = -5; p.thighF = 36; p.shinF = -36; p.thighB = -26; });
        Pose raise = K(p => { p.lean = -8; p.head = 6; p.wx = 0.2f; p.wy = 0.9f; p.wAng = 95; p.thighF = 24; p.shinF = -20; p.thighB = -20; });
        moves["bosscast"] = new Move
        {
            name = "bosscast", armor = true, dur = 0.6f, hitAt = 0.5f, hits = new float[0], step = 0f, gain = 0,
            times = new[] { 0f, 0.4f, 1f },
            keys = new[]
            {
                G(),
                K(p => { p.lean = -12; p.hipY = -0.08f; p.wx = 0.1f; p.wy = 0.9f; p.wAng = 100; p.thighF = 22; p.shinF = -26; p.thighB = -22; p.shinB = -12; }),
                G(),
            },
        };
        moves["throw"] = new Move
        {
            name = "throw", ability = 4, dur = 0.32f, hitAt = 0.4f, step = 0f, gain = 0,
            times = new[] { 0f, 0.4f, 1f },
            keys = new[]
            {
                OneHand(p => { p.lean = -6; p.lUp = 160; p.lFo = 60; }),
                OneHand(p => { p.lean = 14; p.hipX = 0.15f; p.lUp = 80; p.lFo = -5; p.thighF = 34; p.shinF = -34; p.thighB = -24; }),
                G(),
            },
        };
        for (int a = 1; a <= 3; a++)
            moves["cast" + a] = new Move
            {
                name = "cast" + a, ability = a, dur = 0.4f, hitAt = 0.42f, step = a == 1 ? 0.1f : 0f, gain = 0,
                times = new[] { 0f, 0.42f, 1f },
                keys = new[] { G(), a == 1 ? palm : raise, G() },
            };
    }

    static Pose BlockPose = K(p => { p.lean = 12; p.hipY = -0.05f; p.wx = 0.65f; p.wy = 0.0f; p.wAng = 88; p.thighF = 26; p.shinF = -32; p.thighB = -20; p.shinB = -14; });
    static Pose HitPose = K(p => { p.lean = -20; p.head = -12; p.hipX = -0.2f; p.lTwo = 0.2f; p.wx = 0.2f; p.wy = 0.1f; p.wAng = 40; p.lUp = -25; p.lFo = 60; p.thighF = 8; p.shinF = -16; p.thighB = -24; p.shinB = -8; });
    static Pose AirPose = K(p => { p.lean = 6; p.lTwo = 0.6f; p.wAng = 50; p.thighF = 48; p.shinF = -75; p.thighB = -22; p.shinB = -60; p.lUp = 70; p.lFo = 60; });
    static Pose HitPose2 = K(p => { p.lean = -34; p.head = -20; p.hipX = -0.35f; p.hipY = -0.1f; p.lTwo = 0; p.wx = 0.1f; p.wy = 0.3f; p.wAng = 120; p.lUp = 120; p.lFo = 40; p.thighF = 30; p.shinF = -50; p.thighB = -30; p.shinB = -10; });
    static Pose CrouchPose = K(p => { p.lean = 16; p.hipY = -0.28f; p.wy = -0.1f; p.thighF = 55; p.shinF = -85; p.thighB = -10; p.shinB = -70; });
    static Pose FallPose = K(p => { p.lean = 2; p.lTwo = 0.7f; p.wAng = 55; p.thighF = 30; p.shinF = -30; p.thighB = -10; p.shinB = -25; p.lUp = 80; p.lFo = 50; });
    static Pose KneelPose = K(p => { p.lean = 22; p.head = 6; p.lTwo = 0.3f; p.wx = 0.5f; p.wy = -0.35f; p.wAng = -30; p.thighF = 75; p.shinF = -95; p.thighB = -5; p.shinB = -115; });
    static Pose VictoryPose = K(p => { p.lean = -8; p.head = 8; p.lTwo = 0; p.wx = 0.3f; p.wy = 0.95f; p.wAng = 95; p.lUp = 150; p.lFo = 10; p.thighF = 18; p.shinF = -10; p.thighB = -18; p.shinB = -6; });
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
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) it.ab = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) it.ab = 2;
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) it.ab = 3;
        it.throwStar = Input.GetKeyDown(KeyCode.O);
        // dash: Shift, or a quick double tap of A / D
        it.dash = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
        int tap = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0;
        if (tap != 0)
        {
            if (tap == lastTapDir && Time.time - lastTapT < 0.25f) { it.dash = true; lastTapDir = 0; }
            else { lastTapDir = tap; lastTapT = Time.time; }
        }
        if (invertT > 0f) it.move = -it.move;   // a boss twisted your senses
        return it;
    }

    Intent AIIntent()
    {
        Intent it = new Intent();
        if (target == null || target.Dead) return it;
        if (boss && bossPowers != null && bossPowers.Length > 0)
        {
            bossCd -= Time.deltaTime;
            if (bossCd <= 0f && cur == null && Grounded && target.Hittable)
            {
                bossCd = Random.Range(7f, 10f) * (hp < maxHp * 0.5f ? 0.7f : 1f);
                BossPower(bossPowers[bossNext++ % bossPowers.Length]);
                return it;
            }
        }
        float dx = target.transform.position.x - transform.position.x, dist = Mathf.Abs(dx);
        float wr = WeaponReach;
        aiTimer -= Time.deltaTime;

        aiAbilityCd -= Time.deltaTime;
        aiDashCd -= Time.deltaTime;
        // sometimes dash out of the way of a big attack
        if (aiDashCd <= 0f && target.cur != null && (target.cur.power || target.cur.big) && dist < target.cur.reach + 0.5f && Random.value < 0.015f * aiLevel)
        {
            it.dash = true; it.move = -Mathf.Sign(dx); aiDashCd = 2f;
        }
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
        else if (cur == null && aiTimer <= 0f && aiAbilityCd <= 0f && Charges >= 1 && Random.value < 0.3f)
        {
            it.ab = AiPickAbility(dist);
            aiTimer = it.ab > 0 ? 0.35f : 0.2f;
            if (it.ab > 0) aiAbilityCd = Random.Range(2.5f, 4.5f) / (0.7f + 0.05f * aiLevel);   // no endless spam
        }
        else if (cur == null && aiTimer <= 0f && dist > 4f && shuriken > 0 && Random.value < 0.25f)
        {
            it.throwStar = true;
            aiTimer = Random.Range(0.4f, 0.9f);
        }
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
        if (Charges >= 3 && dist < PowerRange && Random.value < 0.03f) it.ult = true;
        return it;
    }

    // ---------- combat ----------
    void Buffer(string k) { buf = k; bufT = 0.3f; }

    void StartMove(string name)
    {
        cur = moves[name];
        mt = 0f; stepped = 0f; hitDone = false; hit2Done = false; buf = null; hitIdx = 0; tpDone = false;
        if (cur.hop > 0f && Grounded) vy = cur.hop;
        if (name == "power") Zoom = 1f;
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

    // ---------- abilities: the energy bar holds three charges ----------
    public const float Seg = 100f / 3f;
    public const float ArenaHalf = 10f;
    public static float Zoom;
    public int Charges { get { return Mathf.FloorToInt(energy / Seg + 0.001f); } }
    public bool Hittable { get { return !Dead && downT <= 0f && invulnT <= 0f; } }
    public bool Dashing { get { return dashT > 0f; } }

    static readonly string[,] AbilityNames =
    {
        { "Огненный шар", "Ярость", "Метеоры" },
        { "Шаровая молния", "Ускорение", "Гроза" },
        { "Световая стрела", "Исцеление", "Святой щит" },
        { "Теневой сгусток", "Шаг в тень", "Ядовитое облако" },
    };

    static readonly Move ProjMove = new Move { name = "proj", ranged = true, stun = 0.3f, knock = 0.5f, gain = 0f };
    public static readonly Move StarMove = new Move { name = "shuriken", ranged = true, stun = 0.12f, knock = 0.15f, gain = 0f };
    static readonly Move MeteorMove = new Move { name = "meteor", ranged = true, stun = 0.45f, knock = 0.8f, big = true, gain = 0f };
    static readonly Move MeteorBig = new Move { name = "meteor", ranged = true, stun = 0.5f, knock = 1.1f, big = true, knockdown = true, gain = 0f };
    static readonly Move BoltMove = new Move { name = "bolt", ranged = true, stun = 0.25f, knock = 0.2f, unblockable = true, gain = 0f };
    static readonly Move BoltLast = new Move { name = "bolt", ranged = true, stun = 0.4f, knock = 0.6f, big = true, unblockable = true, gain = 0f };

    public string AbilityName(int a) { return AbilityNames[(int)weapon.faction, a - 1]; }
    public int AbilityCost(int a) { return a == 3 ? 2 : 1; }
    public static bool IsAbilityKey(string k) { return k == "1" || k == "2" || k == "3"; }

    bool CanCast(int a)
    {
        if (!Grounded || Charges < AbilityCost(a)) return false;
        if (a == 2 && weapon.faction == Faction.Legion && rageT > 0f) return false;
        if (a == 2 && weapon.faction == Faction.Dynasty && hasteT > 0f) return false;
        return true;
    }

    void StartCast(int a)
    {
        energy = Mathf.Max(0f, energy - AbilityCost(a) * Seg);
        StartMove("cast" + a);
        Color c = Db.FactionColor(weapon.faction);
        Fx.Flash(transform.position + new Vector3(0, 2f * scale, 0), c, 7f, 0.4f);
        Fx.Sparks(transform.position + new Vector3(facing * 0.6f, 2f * scale, 0), c, 10, 4f);
    }

    void CastAbility(int a)
    {
        if (target == null) return;
        Fighter tgt = target;
        if (a == 4)
        {
            GameAudio.Sfx("whoosh");
            if (DebugLog) Debug.Log("EVT shuriken by " + name);
            Vector3 from = transform.position + new Vector3(facing * 1.1f * scale, 1.8f * scale, -0.2f);
            Projectile.Shuriken(tgt, from, new Vector3(facing * 19f, 0f, 0f), (4f + weapon.damage * 0.15f) * dmgMul);
            return;
        }
        float wd = weapon.damage, mul = dmgMul * (rageT > 0f ? 1.5f : 1f);
        Vector3 hand = transform.position + new Vector3(facing * 1.2f * scale, 1.7f * scale, 0f);
        Vector3 chestP = transform.position + new Vector3(0f, 1.9f * scale, 0f);
        switch (weapon.faction)
        {
            case Faction.Legion:
                if (a == 1)
                {
                    GameAudio.Sfx("fire");
                    Projectile.Launch(tgt, hand, new Vector3(facing * 11f, 0f, 0f), 0.55f, new Color(1f, 0.45f, 0.1f), (10f + wd * 0.6f) * mul, ProjMove,
                        t => t.ApplyStatus("burn", 2.5f, Mathf.Max(3f, wd * 0.25f)), false, 1.0f);
                }
                else if (a == 2) { rageT = 6f; GameAudio.Sfx("fire"); Fx.Sparks(chestP, new Color(1f, 0.25f, 0.1f), 20, 6f); }
                else StartCoroutine(Meteors(tgt, (9f + wd * 0.55f) * mul));
                break;
            case Faction.Dynasty:
                if (a == 1)
                {
                    GameAudio.Sfx("zap");
                    Projectile.Launch(tgt, hand, new Vector3(facing * 16f, 0f, 0f), 0.42f, new Color(0.6f, 0.85f, 1f), (8f + wd * 0.5f) * mul, ProjMove,
                        t => t.ApplyStatus("shock", 0.6f, 0f), false, 0.9f);
                }
                else if (a == 2) { hasteT = 6f; GameAudio.Sfx("zap"); Fx.Sparks(chestP, new Color(0.5f, 0.9f, 1f), 20, 6f); }
                else StartCoroutine(Storm(tgt, (6f + wd * 0.35f) * mul));
                break;
            case Faction.Heralds:
                if (a == 1)
                {
                    GameAudio.Sfx("holy");
                    Projectile.Launch(tgt, hand, new Vector3(facing * 24f, 0f, 0f), 0.35f, new Color(1f, 0.92f, 0.55f), (9f + wd * 0.6f) * mul * 1.6f, ProjMove, null, false, 0.9f);
                }
                else if (a == 2)
                {
                    hp = Mathf.Min(maxHp, hp + maxHp * 0.15f);
                    GameAudio.Sfx("holy");
                    for (int k = 0; k < 16; k++)
                        Fx.Spawn(transform.position + new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(0.2f, 3f), Random.Range(-0.3f, 0.3f)), new Color(0.5f, 1f, 0.5f), 0.22f, 0.8f, Vector3.up * 2.5f, k * 0.02f).Mode(1);
                }
                else { shieldT = 4f; GameAudio.Sfx("holy"); }
                break;
            default:
                if (a == 1)
                {
                    GameAudio.Sfx("dark");
                    Projectile.Launch(tgt, hand, new Vector3(facing * 8f, 0f, 0f), 0.6f, new Color(0.55f, 0.15f, 0.8f), (8f + wd * 0.5f) * mul, ProjMove,
                        t => t.ApplyStatus("poison", 3f, Mathf.Max(3f, wd * 0.2f)), false, 1.1f);
                }
                else if (a == 2) { TeleportBehind(); critNext = true; }
                else { GameAudio.Sfx("dark"); PoisonCloud.Spawn(tgt, tgt.transform.position.x, Mathf.Max(4f, wd * 0.3f), new Color(0.5f, 0.15f, 0.7f)); }
                break;
        }
    }

    System.Collections.IEnumerator Meteors(Fighter tgt, float dmg)
    {
        GameAudio.Sfx("fire");
        float[] off = { -0.9f, 0.9f, 0f };
        for (int i = 0; i < 3; i++)
        {
            float x = tgt.transform.position.x + off[i];
            Vector3 start = new Vector3(x - facing * 3f, 13f, 0.3f);
            Vector3 v = (new Vector3(x, 0.3f, 0f) - start).normalized * 20f;
            Projectile.Launch(tgt, start, v, 0.75f, new Color(1f, 0.4f, 0.08f), dmg, i == 2 ? MeteorBig : MeteorMove, t => t.ApplyStatus("burn", 1.5f, 3f), true, 1.6f);
            yield return new WaitForSeconds(0.28f);
        }
    }

    System.Collections.IEnumerator Storm(Fighter tgt, float dmg)
    {
        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForSeconds(0.22f);
            if (tgt == null) yield break;
            Vector3 hit = new Vector3(tgt.transform.position.x + Random.Range(-0.4f, 0.4f), 0.2f, 0f);
            GameAudio.Sfx("zap");
            Fx.Bolt(hit + new Vector3(Random.Range(-1f, 1f), 16f, 0f), hit, new Color(0.7f, 0.9f, 1f), 0.2f, 0.35f);
            Fx.Flash(hit + Vector3.up * 2f, new Color(0.6f, 0.85f, 1f), 12f, 0.35f);
            Fx.Sparks(hit + Vector3.up, new Color(0.8f, 0.95f, 1f), 10, 7f);
            Shake = Mathf.Max(Shake, 0.12f);
            if (tgt.Hittable && Mathf.Abs(tgt.transform.position.x - hit.x) < 1.3f)
            {
                bool landed = tgt.TakeHit(i == 3 ? BoltLast : BoltMove, dmg, tgt.transform.position.x >= transform.position.x ? 1 : -1);
                if (landed && i == 3) tgt.ApplyStatus("shock", 0.8f, 0f);
            }
        }
    }

    // ---------- disarm ----------
    public void Disarm(int dir)
    {
        if (disarmed || Db.IsFist(weapon)) return;
        disarmed = true;
        realWeapon = weapon;
        if (DebugLog) Debug.Log("EVT disarm " + name + " lost " + weapon.name);
        EventMsg = (isPlayer ? "ТЕБЯ ОБЕЗОРУЖИЛИ" : "ОРУЖИЕ ВЫБИТО") + "!";
        EventMsgT = Time.unscaledTime + 1.8f;
        GameAudio.Sfx("block");
        Fx.Sparks(weaponP.position, new Color(1f, 0.9f, 0.5f), 16, 7f);

        // the weapon flies off and lies on the floor until the round ends
        Transform w = weaponP;
        w.SetParent(null, true);
        droppedWeapon = w.gameObject;
        StartCoroutine(FlyAway(w, dir));

        weapon = Db.Fists(realWeapon.faction);
        weaponP = Pivot(chest, "weapon", Vector3.zero);
        BuildWeapon(Color.white, Color.white);
        wTrail = MakeTrail(weaponP, new Vector3(weaponLen, 0f, 0f), Color.white, 0.3f * scale, 0.12f);
        BuildMoves();
    }

    System.Collections.IEnumerator FlyAway(Transform w, int dir)
    {
        Vector3 v = new Vector3(dir * 4.5f, 8f, 0f);
        float spin = 900f * dir;
        while (w != null)
        {
            float dt = Time.deltaTime;
            v.y -= 24f * dt;
            w.position += v * dt;
            w.Rotate(0f, 0f, -spin * dt, Space.World);
            Vector3 p = w.position;
            p.x = Mathf.Clamp(p.x, -ArenaHalf - 1f, ArenaHalf + 1f);
            w.position = p;
            if (v.y < 0f && p.y <= 0.15f)
            {
                w.position = new Vector3(p.x, 0.12f, 0.4f);
                w.rotation = Quaternion.Euler(90f, 0f, Random.Range(0f, 360f));
                Fx.Dust(w.position, 6, 1.5f);
                GameAudio.Sfx("hit");
                yield break;
            }
            yield return null;
        }
    }

    // ---------- boss powers ----------
    public static readonly string[] BossPowerNames = { "ГИГАНТ", "ЗЕРКАЛЬНЫЙ ПАНЦИРЬ", "ИНВЕРСИЯ", "ЗАМЕДЛЕНИЕ ВРЕМЕНИ", "ТЕНЕВОЙ ШКВАЛ", "ВОРОНКА", "ДОЖДЬ ЧЕРЕПОВ", "ПОХИЩЕНИЕ ДУШИ" };
    static readonly Color[] BossPowerCol =
    {
        new Color(1f, 0.3f, 0.1f), new Color(0.85f, 0.92f, 1f), new Color(0.9f, 0.3f, 1f), new Color(0.5f, 0.8f, 1f),
        new Color(0.5f, 0.1f, 0.8f), new Color(0.35f, 0.05f, 0.5f), new Color(0.6f, 1f, 0.4f), new Color(0.3f, 1f, 0.9f),
    };
    static readonly Move FlurryMove = new Move { name = "flurry", stun = 0.35f, knock = 0.4f, gain = 0f };
    static readonly Move FlurryLast = new Move { name = "flurry", stun = 0.5f, knock = 1.2f, big = true, knockdown = true, gain = 0f };
    static readonly Move VortexBurst = new Move { name = "vortex", stun = 0.5f, knock = 2.2f, big = true, knockdown = true, unblockable = true, gain = 0f };

    void BossPower(int k)
    {
        cur = moves["bosscast"];
        mt = 0f; stepped = 0f; hitDone = false; hitIdx = 0; buf = null;
        BossMsg = BossPowerNames[k];
        BossMsgT = Time.unscaledTime + 2.2f;
        if (DebugLog) Debug.Log("EVT boss power " + BossMsg);
        Color col = BossPowerCol[k];
        Vector3 c = transform.position + new Vector3(0, 2f * scale, 0);
        Fx.Flash(c, col, 12f, 0.7f);
        Shake = Mathf.Max(Shake, 0.15f);
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Mathf.PI * 2f;
            Fx.Spawn(c, col, 0.35f, 0.6f, new Vector3(Mathf.Cos(a) * 5f, Mathf.Sin(a) * 5f, 0f), 0f).Mode(1);
        }
        Vector3 tc = target.transform.position + new Vector3(0, 2f, 0);
        switch (k)
        {
            case 0: giantT = 7f; GameAudio.Sfx("hit2"); break;                                   // grows huge, hits harder, cannot be staggered
            case 1: mirrorT = 4f; GameAudio.Sfx("holy"); break;                                  // every blow is thrown back
            case 2: target.invertT = 4f; GameAudio.Sfx("dark"); SmokePuff(target.transform.position); break;   // left is right and right is left
            case 3: target.slowT = 5f; GameAudio.Sfx("zap"); Fx.Flash(tc, col, 10f, 0.6f); break; // time crawls for you
            case 4: StartCoroutine(ShadowFlurry()); break;
            case 5: StartCoroutine(Vortex()); break;
            case 6: StartCoroutine(SkullRain()); break;
            default:                                                                               // steals your charges and some life
                GameAudio.Sfx("dark");
                float stolen = target.energy;
                target.energy = Mathf.Max(0f, target.energy - 67f);
                energy = Mathf.Min(100f, energy + stolen * 0.5f);
                hp = Mathf.Min(maxHp, hp + maxHp * 0.08f);
                if (target.Hittable) { target.lastDamage = 6f * dmgMul; target.DirectDamage(6f * dmgMul); target.flashT = 0.1f; target.flashDirty = true; }
                for (int i = 0; i < 3; i++) Fx.Bolt(tc, c + new Vector3(0, i * 0.3f - 0.3f, 0), col, 0.1f, 0.5f);
                break;
        }
    }

    // vanishes and strikes from behind three times
    System.Collections.IEnumerator ShadowFlurry()
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(0.45f);
            if (target == null || Dead || target.Dead) yield break;
            TeleportBehind();
            Vector3 tp = target.transform.position + new Vector3(0, 2f, 0);
            Fx.Seg(tp + new Vector3(-1f, 1f, 0), tp + new Vector3(1f, -1f, 0), new Color(0.7f, 0.3f, 1f), 0.15f, 0.25f);
            if (target.Hittable) target.TakeHit(i == 2 ? FlurryLast : FlurryMove, (8f + weapon.damage * 0.6f) * dmgMul, facing);
        }
    }

    // a black hole drags you in, then bursts
    System.Collections.IEnumerator Vortex()
    {
        GameAudio.Sfx("dark");
        float t = 0f, tick = 0f;
        while (t < 1.6f)
        {
            if (target == null || Dead || target.Dead) yield break;
            t += Time.deltaTime; tick += Time.deltaTime;
            Vector3 tp = target.transform.position;
            float gap = transform.position.x - tp.x;
            if (Mathf.Abs(gap) > 1.6f && target.dashT <= 0f) { tp.x += Mathf.Sign(gap) * 5.5f * Time.deltaTime; target.transform.position = tp; }
            Vector3 hole = transform.position + new Vector3(facing * 1.3f, 2f * scale, 0f);
            float a = t * 14f;
            Fx.Spawn(hole + new Vector3(Mathf.Cos(a) * 1.4f, Mathf.Sin(a) * 1.4f, 0f), new Color(0.35f, 0.05f, 0.5f), 0.35f, 0.3f, new Vector3(-Mathf.Cos(a), -Mathf.Sin(a), 0f) * 4f, 0f);
            if (tick > 0.3f) { tick = 0f; if (target.Hittable) { target.lastDamage = 2.5f * dmgMul; target.DirectDamage(2.5f * dmgMul); } }
            yield return null;
        }
        if (target != null && !Dead && target.Hittable && Mathf.Abs(target.transform.position.x - transform.position.x) < 3.5f)
        {
            Fx.Flash(target.transform.position + new Vector3(0, 2f, 0), new Color(0.6f, 0.2f, 0.9f), 14f, 0.5f);
            target.TakeHit(VortexBurst, (10f + weapon.damage * 0.8f) * dmgMul, target.transform.position.x >= transform.position.x ? 1 : -1);
        }
    }

    // glowing skulls rain down around you
    System.Collections.IEnumerator SkullRain()
    {
        GameAudio.Sfx("dark");
        for (int i = 0; i < 6; i++)
        {
            if (target == null || Dead) yield break;
            float x = target.transform.position.x + Random.Range(-1.8f, 1.8f);
            Vector3 start = new Vector3(x + Random.Range(-1f, 1f), 14f, 0.2f);
            Vector3 v = (new Vector3(x, 0.3f, 0f) - start).normalized * 18f;
            Projectile.Launch(target, start, v, 0.55f, new Color(0.6f, 1f, 0.4f), (5f + weapon.damage * 0.35f) * dmgMul, MeteorMove, t => t.ApplyStatus("poison", 2f, 2f), true, 1.2f);
            yield return new WaitForSeconds(0.22f);
        }
    }

    // vanish in smoke and reappear on the far side of the enemy (in front if there is no room behind)
    void TeleportBehind()
    {
        if (target == null) return;
        float tx = target.transform.position.x;
        float nx = tx + facing * 1.7f;
        if (nx > ArenaHalf - 0.2f || nx < -ArenaHalf + 0.2f) nx = tx - facing * 1.7f;
        SmokePuff(transform.position);
        transform.position = new Vector3(Mathf.Clamp(nx, -ArenaHalf, ArenaHalf), transform.position.y, 0f);
        facing = tx >= nx ? 1 : -1;
        snapYaw = true;
        SmokePuff(transform.position);
        GameAudio.Sfx("whoosh2");
    }

    void SmokePuff(Vector3 p)
    {
        for (int k = 0; k < 10; k++)
            Fx.Spawn(p + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 3.2f), Random.Range(-0.3f, 0.3f)), new Color(0.3f, 0.1f, 0.4f), Random.Range(0.5f, 0.9f), 0.5f,
                new Vector3(Random.Range(-1f, 1f), 1.5f, 0f), 0f).Mode(1);
    }

    void SlamVfx(Vector3 mp)
    {
        GameAudio.Sfx("hit2");
        Shake = Mathf.Max(Shake, 0.3f);
        Color c = Color.Lerp(Db.FactionColor(weapon.faction), new Color(1f, 0.8f, 0.4f), 0.5f);
        for (int i = 0; i < 12; i++)
        {
            Vector3 pos = new Vector3(mp.x + facing * (0.9f + i * 0.35f), 0.1f, Random.Range(-0.4f, 0.4f));
            Fx.Spawn(pos, c, 0.5f + i * 0.03f, 0.45f, Vector3.up * Random.Range(1f, 3f), i * 0.025f).Mode(1);
            if (i % 2 == 0) Fx.Dust(pos, 2, 1f);
        }
        Fx.Flash(mp + new Vector3(facing * 2f, 0.5f, 0f), c, 10f, 0.4f);
    }

    int AiPickAbility(float dist)
    {
        var opts = new List<int>();
        Faction f = weapon.faction;
        if (dist > 3.2f) opts.Add(1);
        switch (f)
        {
            case Faction.Legion: if (dist < 6f && rageT <= 0f) opts.Add(2); break;
            case Faction.Dynasty: if (dist < 6f && hasteT <= 0f) opts.Add(2); break;
            case Faction.Heralds: if (hp < maxHp * 0.65f) { opts.Add(2); opts.Add(2); } break;
            default: if (dist > 2.5f) opts.Add(2); break;
        }
        if (Charges >= 2 && (f != Faction.Heralds || hp < maxHp * 0.5f || (target != null && target.Attacking))) opts.Add(3);
        return opts.Count == 0 ? 0 : opts[Random.Range(0, opts.Count)];
    }

    public void ApplyStatus(string kind, float t, float dps)
    {
        if (Dead || shieldT > 0f) return;
        switch (kind)
        {
            case "burn": burnDps = burnT > 0f ? Mathf.Max(burnDps, dps) : dps; burnT = Mathf.Max(burnT, t); break;
            case "poison": poisonDps = poisonT > 0f ? Mathf.Max(poisonDps, dps) : dps; poisonT = Mathf.Max(poisonT, t); break;
            case "shock": if (shockT > 0f) break; shockT = t; stun = Mathf.Max(stun, t); break;
        }
    }

    // buffs and debuffs, shown above the head
    public string Buffs
    {
        get
        {
            string b = "";
            if (rageT > 0f) b += "<color=#ff5030>ЯРОСТЬ</color> ";
            if (hasteT > 0f) b += "<color=#60e0ff>УСКОРЕНИЕ</color> ";
            if (disarmed) b += "<color=#ff8080>БЕЗ ОРУЖИЯ</color> ";
            if (giantT > 0f) b += "<color=#ff5020>ГИГАНТ</color> ";
            if (mirrorT > 0f) b += "<color=#d8e8ff>ЗЕРКАЛО</color> ";
            if (invertT > 0f) b += "<color=#e060ff>ИНВЕРСИЯ</color> ";
            if (slowT > 0f) b += "<color=#90b8d0>ЗАМЕДЛЕН</color> ";
            if (shieldT > 0f) b += "<color=#ffe070>ЩИТ</color> ";
            if (invulnT > 0f && dashT <= 0f && controlsEnabled) b += "<color=#ffffff>НЕУЯЗВИМ</color> ";
            if (critNext) b += "<color=#d080ff>КРИТ</color> ";
            if (burnT > 0f) b += "<color=#ff9030>ГОРИТ</color> ";
            if (poisonT > 0f) b += "<color=#b060ff>ЯД</color> ";
            if (shockT > 0f) b += "<color=#a0d8ff>ПАРАЛИЧ</color> ";
            return b;
        }
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
        if (m.slam) SlamVfx(mp);
        if (m.flashLine)
            Fx.Seg(new Vector3(mp.x - facing * 3.4f, 1.7f * scale, 0f), new Vector3(mp.x + facing * 0.6f, 1.7f * scale, 0f), Color.white, 0.12f, 0.3f);
        // the Heralds are precise: their strikes reach a little further, catch jumpers and find the gaps in armor
        bool precise = weapon.faction == Faction.Heralds && !m.power;
        bool vertOk = m.power || (dy < (precise ? 2.4f : 1.9f) && (!m.low || target.jumpY < 0.6f) && (!m.slam || target.jumpY < 0.8f));
        if (dx > (precise ? -0.9f : -0.4f) && dx <= m.reach + (precise ? 0.35f : 0f) && vertOk)
        {
            float d = m.dmg * dmgMul;
            if (precise) { float def = target.Defense; d *= 1f + 0.6f * def / (def + 100f); }
            if (giantT > 0f) d *= 1.4f;
            // weapon strikes can crit; the Heralds' light is always a precise, critical hit
            bool crit = critNext || ((m.weaponMove || (m.power && weapon.faction == Faction.Heralds)) && Random.value < (m.power ? 1f : weapon.crit + m.critBonus));
            critNext = false;
            if (crit) d *= 1.6f;
            if (rageT > 0f) d *= 1.5f;
            HitCrit = crit;
            bool landed = target.TakeHit(m, d, facing);
            HitCrit = false;
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
        if (invulnT > 0f) return false;  // dashing or just got up
        if (shieldT > 0f)
        {
            Fx.Sparks(transform.position + new Vector3(-dir * 0.6f, 1.9f * scale, 0f), new Color(1f, 0.9f, 0.4f), 12, 6f);
            GameAudio.Sfx("block");
            HitStop = Mathf.Max(HitStop, 0.03f);
            return false;
        }
        d *= 1f - Defense / (Defense + 100f);
        Vector3 chest = transform.position + new Vector3(0, 1.9f * scale + jumpY, 0);
        if (mirrorT > 0f && target != null && !target.Dead)
        {
            // the mirror shell throws the blow back at the attacker
            Fx.Sparks(chest, new Color(0.85f, 0.9f, 1f), 14, 7f);
            Fx.Bolt(chest, target.transform.position + new Vector3(0, 1.9f, 0), new Color(0.8f, 0.9f, 1f), 0.08f, 0.25f);
            GameAudio.Sfx("block");
            target.flashT = 0.1f; target.flashDirty = true;
            target.lastDamage = d * 0.7f; target.DirectDamage(d * 0.7f);
            return false;
        }
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
        if (m.ranged && rangedImmuneT > 0f)
        {
            // already staggered by a ranged hit a moment ago: take the damage but keep control
            if (DebugLog) Debug.Log("EVT ranged hit without stun on " + name);
            Fx.Sparks(chest, new Color(1f, 0.35f, 0.15f), 6, 5f);
            GameAudio.Sfx("hit");
            flashT = 0.08f; flashDirty = true;
            lastDamage = d; DirectDamage(d);
            return true;
        }
        if ((cur != null && cur.armor) || giantT > 0f)
        {
            // super armor: the blow hurts but the attack goes on
            if (cur != null && cur.power) d *= 0.5f;
            if (DebugLog) Debug.Log("EVT armor hit on " + name + " during " + (cur != null ? cur.name : "giant"));
            Fx.Sparks(chest, new Color(1f, 0.75f, 0.3f), 8, 5f);
            GameAudio.Sfx("hit");
            HitStop = Mathf.Max(HitStop, 0.03f);
            flashT = 0.08f; flashDirty = true;
            energy = Mathf.Min(100f, energy + d * 0.2f);
            lastDamage = d; DirectDamage(d);
            return true;
        }
        if (m.ranged) rangedImmuneT = 1.2f;
        cur = null; buf = null;
        crouchT = 0f; hitVariant = m.big ? 1 : 0; flashT = 0.1f; flashDirty = true;
        stun = m.stun;
        p.x += dir * m.knock;
        transform.position = p;
        if (m.launch) vy = 8.5f;
        if (m.knockdown) { downT = 1.15f; downElapsed = 0f; }
        // only a heavy critical blow can knock the weapon away, and rarely; bosses never let go
        if (HitCrit && !boss && (m.big || m.power) && !m.ranged && d >= maxHp * 0.12f && Random.value < 0.2f) Disarm(dir);
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
        if (disarmed)
        {
            // a new round: the weapon is back in the hands
            disarmed = false;
            weapon = realWeapon;
            if (droppedWeapon != null) Destroy(droppedWeapon);
            Build();
        }
        hp = maxHp; cur = null; buf = null; stun = 0f; downT = 0f; downElapsed = 0f;
        vy = 0f; jumpY = 0f; blocking = false; moving = false; blockTimer = 0f; aiTimer = 1f; guard = 0f; blockCd = 0f; seenMove = null;
        controlsEnabled = false;
        burnT = poisonT = shockT = 0f; hit2Done = false;
        facing = x < 0f ? 1 : -1;
        transform.position = new Vector3(x, 0, 0);
        disp = null;
        rageT = hasteT = shieldT = crouchT = landT = 0f;
        giantT = invertT = slowT = mirrorT = 0f; bossCd = 5f; bossNext = 0;
        transform.localScale = Vector3.one * scale;
        dashT = dashCd = invulnT = rangedImmuneT = aiAbilityCd = 0f; shuriken = MaxShuriken; shurikenT = 0f; critNext = false; victory = false; snapYaw = true; wasAir = false;
        StopAllCoroutines();
        if (wTrail != null) { wTrail.Clear(); fTrail.Clear(); }
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
        if (rageT > 0f)
        {
            rageT -= dt;
            if (statusFx <= 0f) Fx.Spawn(p + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.5f, 3.6f), Random.Range(-0.3f, 0.3f)), new Color(1f, 0.2f, 0.1f), 0.3f, 0.45f, Vector3.up * 2.5f, 0f).Mode(1);
        }
        if (cur != null && cur.armor && statusFx <= 0f)
            Fx.Spawn(p + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.5f, 3.4f), 0f), new Color(1f, 0.7f, 0.25f), 0.25f, 0.25f, Vector3.up, 0f).Mode(1);
        if (boss)
        {
            float gs = giantT > 0f ? 1.3f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * scale * gs, dt * 5f);
        }
        if (giantT > 0f)
        {
            giantT -= dt;
            if (statusFx <= 0f) Fx.Spawn(p + new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(0.2f, 4.5f), 0f), new Color(1f, 0.3f, 0.1f), 0.4f, 0.4f, Vector3.up * 3f, 0f).Mode(1);
        }
        if (mirrorT > 0f)
        {
            mirrorT -= dt;
            if (statusFx <= 0f)
            {
                float a = Random.value * Mathf.PI * 2f;
                Fx.Spawn(p + new Vector3(Mathf.Cos(a) * 1.1f, 2f + Mathf.Sin(a) * 1.6f, 0f), new Color(0.85f, 0.92f, 1f), 0.3f, 0.3f, Vector3.zero, 0f).Mode(1);
            }
        }
        if (invertT > 0f)
        {
            invertT -= dt;
            if (statusFx <= 0f)
            {
                float a = Time.time * 8f;
                Fx.Spawn(p + new Vector3(Mathf.Cos(a) * 0.6f, 4.3f, Mathf.Sin(a) * 0.3f), new Color(0.9f, 0.3f, 1f), 0.25f, 0.35f, Vector3.zero, 0f).Mode(1);
            }
        }
        if (slowT > 0f)
        {
            slowT -= dt;
            if (statusFx <= 0f) Fx.Spawn(p + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.4f, 3.6f), 0f), new Color(0.55f, 0.75f, 0.85f), 0.35f, 0.8f, Vector3.down * 0.4f, 0f);
        }
        if (hasteT > 0f)
        {
            hasteT -= dt;
            if (statusFx <= 0f) Fx.Spawn(p + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.5f, 3.6f), 0f), new Color(0.4f, 0.9f, 1f), 0.22f, 0.3f, new Vector3(-facing * 4f, 0f, 0f), 0f).Mode(1);
        }
        if (shieldT > 0f)
        {
            shieldT -= dt;
            shieldFx -= dt;
            if (shieldFx <= 0f)
            {
                shieldFx = 0.025f;
                Vector3 c = p + new Vector3(0f, 1.9f * scale, 0f);
                for (int k = 0; k < 2; k++)
                {
                    float ang = Time.time * 7f + k * Mathf.PI;
                    Fx.Spawn(c + new Vector3(Mathf.Cos(ang) * 1.3f, Mathf.Sin(ang) * 2.1f, -0.4f), new Color(1f, 0.9f, 0.45f), 0.2f, 0.4f, Vector3.zero, 0f);
                }
            }
        }
        if (boss && statusFx <= 0f)
            Fx.Spawn(p + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 3.8f), Random.Range(-0.3f, 0.3f)), Color.Lerp(Db.FactionColor(weapon.faction), Color.black, 0.3f), 0.25f, 0.6f, Vector3.up * 1.8f, 0f).Mode(1);
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
        bool air = jumpY > 0.02f;
        if (wasAir && !air) { landT = 0.13f; Fx.Dust(new Vector3(transform.position.x, 0.05f, 0f), 6, 1.6f); }
        wasAir = air;
        p.x = Mathf.Clamp(p.x, -ArenaHalf, ArenaHalf);
        p.y = jumpY;
        transform.position = p;
    }

    void Tick(float dt)
    {
        Intent it = (isPlayer && !autoPlay) ? PlayerIntent() : AIIntent();
        guard = Mathf.Max(0f, guard - 0.3f * dt);
        energy = Mathf.Min(100f, energy + 2.5f * dt);   // trickles in on its own
        invulnT -= dt; rangedImmuneT -= dt; dashCd -= dt;
        if (shuriken < MaxShuriken) { shurikenT += dt; if (shurikenT >= 1.6f) { shurikenT = 0f; shuriken++; } }
        if (it.ab > 0) Buffer(it.ab.ToString());
        else if (it.throwStar) Buffer("O");
        else if (it.punch) Buffer("U");
        else if (it.kick) Buffer(it.down ? "D" : "H");
        else if (it.light) Buffer("J");
        else if (it.heavy) Buffer("K");
        else if (it.ult) Buffer("I");
        bufT -= dt;
        if (bufT <= 0f) buf = null;

        moving = false;
        blocking = false;

        if (downT > 0f)
        {
            downT -= dt;
            if (downT <= 0f) { invulnT = 0.8f; if (DebugLog) Debug.Log("EVT getup invuln " + name); }
            Motion(dt, 0f);
            return;
        }
        if (stun > 0f) { stun -= dt; Motion(dt, 0f); return; }
        if (dashT > 0f)
        {
            dashT -= dt;
            if (Random.value < 0.6f)
                Fx.Spawn(transform.position + new Vector3(-dashDir * 0.4f, Random.Range(0.5f, 3f), 0f), Db.FactionColor(weapon.faction), 0.25f, 0.25f, new Vector3(-dashDir * 3f, 0f, 0f), 0f).Mode(1);
            Motion(dt, dashDir * 15f * dt);
            return;
        }
        if (it.dash && cur == null && Grounded && dashCd <= 0f)
        {
            // dash with invulnerability; with no direction held it goes away from the enemy
            dashDir = it.move != 0f ? Mathf.Sign(it.move) : -facing;
            dashT = 0.22f; dashCd = 0.6f; invulnT = 0.26f;
            if (DebugLog) Debug.Log("EVT dash by " + name);
            blocking = false; crouchT = 0f;
            Fx.Dust(new Vector3(transform.position.x, 0.05f, 0f), 6, 1.5f);
            GameAudio.Sfx("whoosh");
            Motion(dt, 0f);
            return;
        }

        if (target != null && cur == null) facing = target.transform.position.x >= transform.position.x ? 1 : -1;

        float dx = 0f;
        if (cur != null)
        {
            mt += dt * Pace;
            float u = mt / cur.dur;
            float want = cur.step * Mathf.Clamp01((u - cur.stepFrom) / Mathf.Max(0.01f, cur.hitAt - cur.stepFrom));
            dx = (want - stepped) * facing;
            stepped = want;
            if (cur.teleportAt >= 0f && !tpDone && u >= cur.teleportAt) { tpDone = true; TeleportBehind(); }
            float[] hs = cur.Hits;
            while (cur != null && hitIdx < hs.Length && u >= hs[hitIdx])
            {
                hitIdx++;
                hitDone = true;
                if (cur.ability > 0) CastAbility(cur.ability); else DoHit(cur);
            }
            // once a move has landed it can be cancelled into an ability or the next combo hit
            if (hitDone && cur != null && IsAbilityKey(buf) && CanCast(buf[0] - '0')) StartCast(buf[0] - '0');
            else if (hitDone && cur != null && buf != null && cur.next != null &&
                ((buf == "U" && (cur.name == "jab" || cur.name == "cross")) || (buf == "H" && cur.name == "kick1") || (buf == "J" && cur.name == "slash")))
                StartMove(cur.next);
            else if (u >= 1f || (cur.air && Grounded && mt > 0.15f)) cur = null;
        }
        else
        {
            blocking = it.block && Grounded;
            if (!blocking)
            {
                float spd = (isPlayer ? 4.5f : 3.5f) * Pace * (it.move * facing < 0f ? 0.95f : 1f);
                dx = it.move * spd * dt;
                moving = it.move != 0f && Grounded;
                moveDir = it.move;
                if (it.jump && Grounded && crouchT <= 0f) crouchT = 0.09f;
                if (crouchT > 0f)
                {
                    // a short crouch before take-off
                    crouchT -= dt;
                    if (crouchT <= 0f && Grounded) { vy = 9.5f; Fx.Dust(new Vector3(transform.position.x, 0.05f, 0f), 5, 1.2f); }
                }
                if (buf != null)
                {
                    if (!Grounded) { if (buf == "U" || buf == "H" || buf == "D") StartMove("jumpkick"); }
                    else if (buf == "U") StartMove("jab");
                    else if (buf == "H") StartMove("kick1");
                    else if (buf == "D") StartMove("sweep");
                    else if (buf == "J") StartMove("slash");
                    else if (buf == "K") StartMove("smash");
                    else if (IsAbilityKey(buf)) { if (CanCast(buf[0] - '0')) StartCast(buf[0] - '0'); else buf = null; }
                    else if (buf == "O") { if (shuriken > 0) { shuriken--; StartMove("throw"); } else buf = null; }
                    else if (buf == "I" && energy >= 100f) StartMove("power");
                    else buf = null;
                }
            }
        }
        Motion(dt, dx);
    }

    // ---------- animation ----------
    // idle breathing and weight shifts; a different walk forwards and backwards
    Pose Locomotion(float dt)
    {
        Pose p = G();
        float t = Time.time + idSeed;
        float br = Mathf.Sin(t * 2.4f), sway = Mathf.Sin(t * 1.1f);
        p.lean += br * 2f + sway * 1.5f;
        p.head = -br * 2f;
        p.hipY = -0.03f - br * 0.025f;
        p.hipX = sway * 0.04f;
        p.thighF += br * 3f; p.shinF -= br * 4f;
        p.wAng += br * 3f + sway * 2f;
        p.wy += br * 0.03f;
        if (moving)
        {
            bool fwd = moveDir * facing > 0f;
            walkT += dt * (fwd ? 11f : 8.5f) * Pace;
            float s = Mathf.Sin(walkT), c = Mathf.Cos(walkT);
            float amp = fwd ? 34f : 24f;
            p.thighF = 20f + s * amp;
            p.shinF = -24f - Mathf.Max(0f, -s) * 50f;
            p.thighB = -14f - s * amp;
            p.shinB = -14f - Mathf.Max(0f, s) * 50f;
            p.hipY = -0.06f - Mathf.Abs(c) * 0.07f;
            p.lean = fwd ? 14f + s * 2f : s * 1.5f;
            p.hipX = fwd ? 0.08f : -0.06f;
            p.wAng += fwd ? -6f + c * 5f : 10f;
            p.wy += fwd ? c * 0.04f : 0.08f;
            p.head = fwd ? -4f : 4f;
        }
        return p;
    }

    Pose DashPose(bool forward)
    {
        return forward
            ? K(p => { p.lean = 28; p.hipY = -0.2f; p.thighF = 60; p.shinF = -60; p.thighB = -50; p.shinB = -30; })
            : K(p => { p.lean = -18; p.hipY = -0.2f; p.thighF = 40; p.shinF = -80; p.thighB = -10; p.shinB = -60; p.wy = 0.1f; });
    }

    Pose Victory()
    {
        float t = Time.time * 5f;
        Pose p = VictoryPose.Clone();
        p.hipY = Mathf.Abs(Mathf.Sin(t)) * 0.06f;
        p.wAng += Mathf.Sin(t) * 6f;
        p.lUp += Mathf.Sin(t) * 8f;
        return p;
    }

    void Animate(float dt)
    {
        Pose target;
        float fall = 0f;
        bounceY = 0f;
        bool down = Dead || downT > 0f;
        if (down)
        {
            downElapsed += dt;
            if (!Dead && downT < 0.55f)
            {
                // getting up: onto one knee, then back to the stance
                target = KneelPose;
                fall = Mathf.Clamp01((downT - 0.18f) / 0.37f);
            }
            else
            {
                target = DownPose;
                fall = Mathf.Clamp01(downElapsed / 0.22f);
                if (downElapsed >= 0.22f && !groundHit)
                {
                    groundHit = true;
                    Fx.Dust(new Vector3(transform.position.x - facing * 1.2f, 0.05f, 0f), 10, 2.2f);
                    Shake = Mathf.Max(Shake, 0.1f);
                }
                if (downElapsed > 0.22f && downElapsed < 0.5f) bounceY = Mathf.Sin((downElapsed - 0.22f) / 0.28f * Mathf.PI) * 0.18f;
            }
        }
        else if (victory) target = Victory();
        else if (dashT > 0f) target = DashPose(dashDir * facing > 0f);
        else if (stun > 0f) target = hitVariant == 1 ? HitPose2 : HitPose;
        else if (cur != null) target = cur.Sample(mt / cur.dur);
        else if (blocking) target = BlockPose;
        else if (crouchT > 0f || landT > 0f) target = CrouchPose;
        else if (!Grounded) target = vy > 0f ? AirPose : FallPose;
        else target = Locomotion(dt);
        if (landT > 0f) landT -= dt;
        if (!down) groundHit = false;

        float rate = cur != null ? 38f : stun > 0f ? 30f : 18f;
        disp = disp == null ? target.Clone() : Pose.Lerp(disp, target, 1f - Mathf.Exp(-dt * rate));
        Pose show = disp;
        if (shockT > 0f)
        {
            show = disp.Clone();
            show.lean += Random.Range(-12f, 12f);
            show.lUp += Random.Range(-30f, 30f);
            show.thighF += Random.Range(-15f, 15f);
            show.thighB += Random.Range(-15f, 15f);
        }
        ApplyPose(show, fall);
        UpdateTrails();
        UpdateFlash();
        // blink while invulnerable after getting up
        bool vis = invulnT <= 0f || dashT > 0f || !controlsEnabled || ((int)(Time.time * 14f) & 1) == 0;
        if (vis != shown && rends != null)
        {
            shown = vis;
            foreach (var r in rends) if (r != null) r.enabled = vis;
        }
    }

    void UpdateTrails()
    {
        if (wTrail == null) return;
        float u = cur != null ? mt / cur.dur : 0f;
        bool live = cur != null && controlsEnabled;
        wTrail.emitting = live && (cur.weaponMove || cur.power) && u > 0.15f && u < 0.9f;
        fTrail.emitting = live && cur.kick && u > 0.2f && u < 0.85f;
    }

    // a white flash over the whole body when a hit lands
    void UpdateFlash()
    {
        if (!flashDirty || rends == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        flashT -= Time.unscaledDeltaTime;
        if (flashT <= 0f)
        {
            flashDirty = false;
            for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].SetPropertyBlock(null);
            return;
        }
        float k = Mathf.Clamp01(flashT / 0.1f) * 0.85f;
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;
            mpb.SetColor("_Color", Color.Lerp(baseCols[i], Color.white, k));
            rends[i].SetPropertyBlock(mpb);
        }
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
        // turn around smoothly instead of snapping; the winner turns a little toward the camera
        float wantYaw = (facing == 1 ? 0f : 180f) + yawOffset + (victory ? facing * 35f : 0f);
        yawCur = snapYaw ? wantYaw : Mathf.LerpAngle(yawCur, wantYaw, 1f - Mathf.Exp(-Time.deltaTime * 16f));
        snapYaw = false;
        transform.rotation = Quaternion.Euler(0f, yawCur, 0f);

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

        float dtp = Mathf.Max(Time.deltaTime, 0.0001f);
        float vx = Mathf.Clamp((transform.position.x - lastX) / dtp * facing, -12f, 12f);
        lastX = transform.position.x;
        if (capeP != null)
        {
            float cw = Mathf.Clamp(-4f - Mathf.Max(0f, vx) * 4f + p.lean * 0.8f - Mathf.Max(0f, -vy) * 2.5f, -75f, 15f);
            capeA = Mathf.SmoothDampAngle(capeA, cw, ref capeV, 0.12f);
            capeP.localRotation = Quaternion.Euler(0f, 0f, capeA);
        }
        if (pony1 != null)
        {
            float want = -28f - vx * 3f - p.lean * 0.6f;
            ponyA = Mathf.SmoothDampAngle(ponyA, want, ref ponyV, 0.09f);
            pony1.localRotation = Quaternion.Euler(0, 0, ponyA);
            float bend = Mathf.Clamp(-ponyV * 0.03f, -25f, 25f);
            pony2.localRotation = Quaternion.Euler(0, 0, 20f + bend);
            pony3.localRotation = Quaternion.Euler(0, 0, 14f + bend * 0.7f);
        }

        // knocked down: tip over backwards, feet stay put
        body.localRotation = Quaternion.Euler(0, 0, 90f * fall);
        body.localPosition = new Vector3(0, 0.32f * fall + bounceY, 0);
    }
}
