using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// The five fight maps. Like the fighters, everything is built from primitives at runtime.
public static class Arena
{
    public static readonly string[] Names = { "Колизей", "Бамбуковый храм", "Небесное святилище", "Теневой склеп", "Вулкан" };

    static Transform root;
    static ArenaAnim anim;
    const PrimitiveType Cube = PrimitiveType.Cube, Sph = PrimitiveType.Sphere, Cyl = PrimitiveType.Cylinder;

    public static GameObject Build(int idx, Light sun, Light fill, Camera cam)
    {
        var go = new GameObject("Arena_" + Names[idx]);
        root = go.transform;
        anim = go.AddComponent<ArenaAnim>();
        Random.State saved = Random.state;
        Random.InitState(1234 + idx);   // same layout every time
        switch (idx)
        {
            case 0: Colosseum(sun, fill, cam); break;
            case 1: Bamboo(sun, fill, cam); break;
            case 2: Sky(sun, fill, cam); break;
            case 3: Crypt(sun, fill, cam); break;
            default: Volcano(sun, fill, cam); break;
        }
        Random.state = saved;
        return go;
    }

    // ---------- helpers ----------
    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
    static Color C(float r, float g, float b) { return new Color(r, g, b); }

    static Transform P(string n, PrimitiveType t, Vector3 pos, Vector3 scale, Color c, Vector3 euler)
    {
        return Fighter.Part(root, n, t, pos, scale, c, euler);
    }

    static Transform P(string n, PrimitiveType t, Vector3 pos, Vector3 scale, Color c) { return P(n, t, pos, scale, c, Vector3.zero); }

    static Transform Glow(string n, PrimitiveType t, Vector3 pos, Vector3 scale, Color c, Vector3 euler)
    {
        var tr = P(n, t, pos, scale, c, euler);
        var r = tr.GetComponent<Renderer>();
        r.sharedMaterial = Fighter.GlowOf(c);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return tr;
    }

    static Transform Glow(string n, PrimitiveType t, Vector3 pos, Vector3 scale, Color c) { return Glow(n, t, pos, scale, c, Vector3.zero); }

    static Transform Shiny(Transform t, float metal, float gloss)
    {
        var r = t.GetComponent<Renderer>();
        r.sharedMaterial = Fighter.MatX(r.sharedMaterial.color, metal, gloss, false);
        return t;
    }

    static Light Lamp(Vector3 pos, Color c, float range, float intensity, bool flicker)
    {
        var g = new GameObject("lamp");
        g.transform.SetParent(root, false);
        g.transform.position = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity;
        l.shadows = LightShadows.None;
        if (flicker) anim.Flicker(l);
        return l;
    }

    static void Env(Camera cam, Color bg, Color fog, float density, Color skyAmb, Color eqAmb, Color gndAmb)
    {
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = bg;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = density;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyAmb;
        RenderSettings.ambientEquatorColor = eqAmb;
        RenderSettings.ambientGroundColor = gndAmb;
    }

    static void Aim(Light l, Color c, float intensity, Vector3 euler)
    {
        l.color = c;
        l.intensity = intensity;
        l.transform.rotation = Quaternion.Euler(euler);
    }

    // checkered tiles with a slight colour variation; darker grout shows between them
    static void Floor(Color a, Color b, float gloss, float tile, float x0 = -22f, float x1 = 22f, float z0 = -6f, float z1 = 12f)
    {
        float[] shade = { 0.94f, 1f, 1.05f };
        for (float x = x0; x < x1 - 0.01f; x += tile)
            for (float z = z0; z < z1 - 0.01f; z += tile)
            {
                int k = Mathf.RoundToInt(x / tile) + Mathf.RoundToInt(z / tile);
                Color c = ((k & 1) == 0 ? a : b) * shade[Random.Range(0, 3)];
                c.a = 1f;
                var t = P("tile", Cube, V(x + tile / 2f, -0.25f, z + tile / 2f), V(tile - 0.04f, 0.5f, tile - 0.04f), c);
                if (gloss > 0.2f) Shiny(t, 0f, gloss);
            }
        P("grout", Cube, V((x0 + x1) / 2f, -0.3f, (z0 + z1) / 2f), V(x1 - x0, 0.48f, z1 - z0), Color.Lerp(a, Color.black, 0.6f));
    }

    // ---------- 0: Colosseum at sunset ----------
    static void Colosseum(Light sun, Light fill, Camera cam)
    {
        Env(cam, C(0.88f, 0.55f, 0.38f), C(0.82f, 0.55f, 0.4f), 0.016f, C(0.72f, 0.56f, 0.46f), C(0.45f, 0.34f, 0.27f), C(0.22f, 0.16f, 0.12f));
        Aim(sun, C(1f, 0.8f, 0.6f), 1.05f, V(28f, -35f, 0f));
        Aim(fill, C(0.5f, 0.6f, 0.95f), 0.35f, V(20f, 150f, 0f));
        Floor(C(0.68f, 0.56f, 0.39f), C(0.63f, 0.52f, 0.36f), 0.1f, 2.5f);

        Color stone = C(0.78f, 0.7f, 0.6f), stoneD = C(0.6f, 0.53f, 0.45f), red = C(0.7f, 0.1f, 0.08f), gold = C(0.92f, 0.76f, 0.3f);
        for (int i = -7; i <= 7; i++)
        {
            float x = i * 3.2f, z = 9f + i * i * 0.09f;
            P("pier", Cube, V(x, 3.5f, z), V(0.9f, 7f, 1.2f), stone);
            P("archTop", Cube, V(x + 1.6f, 6.6f, z), V(2.4f, 0.8f, 1.2f), stone);
            P("void", Cube, V(x + 1.6f, 3.1f, z + 0.5f), V(2.3f, 6.2f, 0.2f), C(0.27f, 0.19f, 0.15f));
            P("pier2", Cube, V(x, 9.4f, z + 1.4f), V(0.7f, 4.4f, 1f), stoneD);
            if (i % 2 == 0)
            {
                P("banner", Cube, V(x, 8.3f, z - 0.66f), V(0.8f, 3f, 0.05f), red);
                Shiny(P("bannerTrim", Cube, V(x, 6.9f, z - 0.69f), V(0.84f, 0.18f, 0.05f), gold), 0.8f, 0.7f);
            }
        }
        // spectators on the tiers
        Color[] shirts = { C(0.7f, 0.15f, 0.12f), C(0.85f, 0.8f, 0.7f), C(0.25f, 0.35f, 0.6f), C(0.55f, 0.45f, 0.25f), C(0.3f, 0.5f, 0.3f), C(0.6f, 0.3f, 0.5f) };
        Color[] skins = { C(0.92f, 0.74f, 0.57f), C(0.75f, 0.55f, 0.4f), C(0.5f, 0.35f, 0.25f) };
        for (int row = 0; row < 3; row++)
        {
            float y = 7.4f + row * 1.4f, zr = 13.5f + row * 1.6f;
            P("tier", Cube, V(0f, y - 0.6f, zr), V(60f, 0.3f, 1.6f), stoneD);
            for (int i = 0; i < 40; i++)
            {
                float x = -24f + i * 1.2f + Random.Range(-0.25f, 0.25f);
                P("fan", Cube, V(x, y, zr), V(0.55f, 0.9f, 0.4f), shirts[Random.Range(0, shirts.Length)]);
                P("fanHead", Sph, V(x, y + 0.65f, zr), V(0.38f, 0.38f, 0.38f), skins[Random.Range(0, skins.Length)]);
            }
        }
        P("backWall", Cube, V(0f, 8f, 20f), V(70f, 16f, 1f), stoneD);
        foreach (float x in new[] { -7.8f, 7.8f })
        {
            Shiny(P("stand", Cyl, V(x, 0.7f, 2.5f), V(0.25f, 0.7f, 0.25f), C(0.2f, 0.18f, 0.16f)), 0.7f, 0.5f);
            Shiny(P("bowl", Cyl, V(x, 1.45f, 2.5f), V(1.1f, 0.18f, 1.1f), C(0.6f, 0.4f, 0.2f)), 0.8f, 0.6f);
            Glow("fire", Sph, V(x, 1.75f, 2.5f), V(0.8f, 0.9f, 0.8f), C(1f, 0.55f, 0.15f));
            Glow("fireCore", Sph, V(x, 1.9f, 2.5f), V(0.45f, 0.7f, 0.45f), C(1f, 0.9f, 0.4f));
            Lamp(V(x, 2.6f, 2.2f), C(1f, 0.6f, 0.25f), 9f, 2.2f, true);
        }
        Glow("sun", Sph, V(14f, 7f, 28f), V(6f, 6f, 6f), C(1f, 0.8f, 0.5f));
        anim.particles = 1;
    }

    // ---------- 1: bamboo temple at night ----------
    static void Bamboo(Light sun, Light fill, Camera cam)
    {
        Env(cam, C(0.08f, 0.13f, 0.17f), C(0.12f, 0.2f, 0.23f), 0.028f, C(0.28f, 0.42f, 0.48f), C(0.18f, 0.26f, 0.27f), C(0.08f, 0.1f, 0.08f));
        Aim(sun, C(0.65f, 0.78f, 1f), 0.65f, V(50f, 25f, 0f));
        Aim(fill, C(0.45f, 1f, 0.75f), 0.25f, V(15f, 160f, 0f));

        Color[] woods = { C(0.42f, 0.28f, 0.17f), C(0.38f, 0.25f, 0.15f), C(0.46f, 0.31f, 0.19f) };
        int n = 0;
        for (float z = -6f; z < 9f; z += 0.9f, n++)
            Shiny(P("plank", Cube, V(0f, -0.2f, z), V(46f, 0.4f, 0.86f), woods[n % 3]), 0f, 0.35f);

        Color red = C(0.72f, 0.12f, 0.1f), roof = C(0.15f, 0.22f, 0.2f), gold = C(0.9f, 0.72f, 0.3f), paper = C(0.82f, 0.74f, 0.56f), dark = C(0.12f, 0.08f, 0.06f);
        P("deck", Cube, V(0f, 0.3f, 10.5f), V(32f, 0.6f, 4f), C(0.3f, 0.2f, 0.12f));
        P("wall", Cube, V(0f, 3.6f, 11.8f), V(30f, 6f, 0.4f), paper);
        for (float x = -14f; x <= 14f; x += 1.2f) P("lattice", Cube, V(x, 3.6f, 11.57f), V(0.08f, 6f, 0.05f), dark);
        P("latticeH", Cube, V(0f, 4.2f, 11.57f), V(30f, 0.08f, 0.05f), dark);
        for (float x = -12f; x <= 12f; x += 4f)
        {
            P("column", Cyl, V(x, 3.6f, 9.4f), V(0.55f, 3.2f, 0.55f), red);
            P("columnBase", Cube, V(x, 0.7f, 9.4f), V(0.9f, 0.3f, 0.9f), C(0.4f, 0.4f, 0.38f));
        }
        P("beam", Cube, V(0f, 6.9f, 9.4f), V(28f, 0.5f, 0.6f), red);
        P("roof", Cube, V(0f, 7.6f, 10.2f), V(31f, 0.5f, 4.6f), roof);
        P("eaveL", Cube, V(-16.2f, 8.1f, 10.2f), V(2.2f, 0.4f, 4.6f), roof, V(0f, 0f, -20f));
        P("eaveR", Cube, V(16.2f, 8.1f, 10.2f), V(2.2f, 0.4f, 4.6f), roof, V(0f, 0f, 20f));
        P("upper", Cube, V(0f, 8.8f, 11f), V(18f, 2f, 3f), paper);
        P("roof2", Cube, V(0f, 10f, 11f), V(21f, 0.45f, 3.8f), roof);
        P("eave2L", Cube, V(-11.2f, 10.45f, 11f), V(2f, 0.4f, 3.8f), roof, V(0f, 0f, -22f));
        P("eave2R", Cube, V(11.2f, 10.45f, 11f), V(2f, 0.4f, 3.8f), roof, V(0f, 0f, 22f));
        Shiny(P("ridge", Cube, V(0f, 10.35f, 11f), V(19f, 0.18f, 0.25f), gold), 0.8f, 0.7f);

        for (float x = -10f; x <= 10f; x += 5f)
        {
            Glow("lantern", Sph, V(x, 5.3f, 8.7f), V(0.7f, 0.85f, 0.7f), C(1f, 0.32f, 0.2f));
            Shiny(P("lanternCap", Cyl, V(x, 5.8f, 8.7f), V(0.4f, 0.05f, 0.4f), gold), 0.8f, 0.6f);
            P("lanternCord", Cyl, V(x, 6.3f, 8.7f), V(0.03f, 0.5f, 0.03f), dark);
            Lamp(V(x, 5.2f, 8.2f), C(1f, 0.45f, 0.25f), 7f, 1.6f, true);
        }

        for (int i = 0; i < 44; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * Random.Range(9f, 22f), z = Random.Range(0.5f, 16f), h = Random.Range(7f, 13f);
            Color g = Color.Lerp(C(0.3f, 0.55f, 0.22f), C(0.45f, 0.65f, 0.3f), Random.value);
            P("bamboo", Cyl, V(x, h / 2f, z), V(0.22f, h / 2f, 0.22f), g);
            P("node", Cyl, V(x, h * 0.35f, z), V(0.26f, 0.04f, 0.26f), g * 0.7f);
            P("node", Cyl, V(x, h * 0.7f, z), V(0.26f, 0.04f, 0.26f), g * 0.7f);
            P("leaf", Cube, V(x + 0.5f, h - 0.4f, z), V(1.3f, 0.06f, 0.35f), C(0.3f, 0.6f, 0.25f), V(0f, Random.Range(0f, 360f), Random.Range(-25f, 25f)));
        }
        foreach (float x in new[] { -12f, 12.5f })
        {
            P("trunk", Cyl, V(x, 2f, 6.5f), V(0.4f, 2f, 0.4f), C(0.32f, 0.2f, 0.16f));
            for (int k = 0; k < 5; k++)
                Glow("blossom", Sph, V(x + Random.Range(-1.3f, 1.3f), 4.4f + Random.Range(-0.5f, 0.8f), 6.5f + Random.Range(-0.8f, 0.8f)), Vector3.one * Random.Range(1.4f, 2f), C(0.95f, 0.45f, 0.68f));
        }
        Glow("moon", Sph, V(-10f, 12f, 22f), V(4f, 4f, 4f), C(0.9f, 0.95f, 1f));
        anim.particles = 2;
    }

    // ---------- 2: sky sanctum above the clouds ----------
    static void Sky(Light sun, Light fill, Camera cam)
    {
        Env(cam, C(0.5f, 0.7f, 0.98f), C(0.72f, 0.82f, 0.98f), 0.012f, C(0.66f, 0.72f, 0.86f), C(0.52f, 0.57f, 0.68f), C(0.36f, 0.36f, 0.42f));
        Aim(sun, C(1f, 0.96f, 0.88f), 1.1f, V(55f, -20f, 0f));
        Aim(fill, C(0.7f, 0.8f, 1f), 0.45f, V(20f, 150f, 0f));
        Floor(C(0.84f, 0.84f, 0.88f), C(0.72f, 0.74f, 0.8f), 0.85f, 2f, -16f, 16f, -6f, 7f);

        Color marble = C(0.92f, 0.92f, 0.95f), gold = C(0.95f, 0.8f, 0.35f);
        Shiny(P("inlay", Cube, V(0f, 0.01f, 0f), V(32f, 0.02f, 0.12f), gold), 0.9f, 0.8f);
        P("rim", Cube, V(0f, -0.9f, 7.2f), V(32.6f, 1.8f, 0.6f), C(0.75f, 0.76f, 0.8f));
        P("rimL", Cube, V(-16.3f, -0.9f, 0.5f), V(0.6f, 1.8f, 13.4f), C(0.75f, 0.76f, 0.8f));
        P("rimR", Cube, V(16.3f, -0.9f, 0.5f), V(0.6f, 1.8f, 13.4f), C(0.75f, 0.76f, 0.8f));
        foreach (float x in new[] { -12f, -6f, 6f, 12f })
        {
            P("base", Cube, V(x, 0.3f, 6f), V(1.4f, 0.6f, 1.4f), marble);
            Shiny(P("shaft", Cyl, V(x, 4.2f, 6f), V(0.8f, 3.6f, 0.8f), marble), 0f, 0.7f);
            Shiny(P("capital", Cube, V(x, 8f, 6f), V(1.4f, 0.4f, 1.4f), gold), 0.9f, 0.75f);
            Transform cr = Glow("crystal", Cube, V(x, 9.4f, 6f), V(0.45f, 0.9f, 0.45f), C(1f, 0.88f, 0.45f), V(0f, 45f, 0f));
            anim.Bob(cr, 0.25f);
        }
        P("lintelL", Cube, V(-9f, 8.5f, 6f), V(7.4f, 0.6f, 1f), marble);
        P("lintelR", Cube, V(9f, 8.5f, 6f), V(7.4f, 0.6f, 1f), marble);

        for (int i = 0; i < 36; i++)
        {
            Vector3 pos = V(Random.Range(-45f, 45f), Random.Range(-7f, -1.5f), Random.Range(8f, 45f));
            P("cloud", Sph, pos, V(Random.Range(6f, 14f), Random.Range(1.5f, 3f), Random.Range(4f, 8f)), C(0.97f, 0.98f, 1f));
        }
        for (int i = 0; i < 10; i++)
            P("cloudHigh", Sph, V(Random.Range(-40f, 40f), Random.Range(11f, 17f), Random.Range(30f, 50f)), V(Random.Range(8f, 16f), Random.Range(1.5f, 2.5f), 5f), C(1f, 1f, 1f));
        for (int i = 0; i < 5; i++)
        {
            Vector3 c = V(-20f + i * 10f + Random.Range(-2f, 2f), Random.Range(7f, 11f), Random.Range(18f, 30f));
            Transform isl = P("island", Sph, c, V(4.5f, 2.2f, 3.2f), C(0.55f, 0.5f, 0.48f));
            Transform grass = P("grass", Cube, c + V(0f, 0.9f, 0f), V(4f, 0.35f, 2.8f), C(0.45f, 0.75f, 0.35f));
            Transform shrine = Shiny(P("shrine", Cube, c + V(0f, 1.6f, 0f), V(0.8f, 1.1f, 0.8f), gold), 0.9f, 0.7f);
            anim.Bob(isl, 0.5f); anim.Bob(grass, 0.5f); anim.Bob(shrine, 0.5f);
        }
        Glow("sun", Sph, V(-16f, 16f, 45f), V(8f, 8f, 8f), C(1f, 0.97f, 0.8f));
        anim.particles = 3;
    }

    // ---------- 3: shadow crypt ----------
    static void Crypt(Light sun, Light fill, Camera cam)
    {
        Env(cam, C(0.05f, 0.03f, 0.08f), C(0.1f, 0.05f, 0.15f), 0.042f, C(0.22f, 0.14f, 0.32f), C(0.13f, 0.09f, 0.2f), C(0.05f, 0.04f, 0.07f));
        Aim(sun, C(0.55f, 0.5f, 0.85f), 0.45f, V(60f, -40f, 0f));
        Aim(fill, C(0.6f, 0.3f, 0.9f), 0.3f, V(10f, 160f, 0f));
        Floor(C(0.18f, 0.16f, 0.22f), C(0.14f, 0.12f, 0.18f), 0.6f, 2f);

        Color wallC = C(0.16f, 0.14f, 0.2f), bone = C(0.85f, 0.82f, 0.72f), grave = C(0.35f, 0.33f, 0.38f);
        P("wall", Cube, V(0f, 6f, 10f), V(60f, 14f, 1f), wallC);
        for (float x = -16f; x <= 16f; x += 4f)
        {
            P("alcove", Cube, V(x, 3f, 9.45f), V(2f, 4.2f, 0.2f), C(0.05f, 0.04f, 0.07f));
            P("shelf", Cube, V(x, 1.35f, 9.2f), V(2f, 0.15f, 0.6f), wallC * 1.2f);
            P("skull", Sph, V(x, 1.65f, 9.1f), V(0.45f, 0.42f, 0.45f), bone);
            P("socket", Cube, V(x - 0.1f, 1.7f, 8.88f), V(0.09f, 0.09f, 0.05f), Color.black);
            P("socket", Cube, V(x + 0.1f, 1.7f, 8.88f), V(0.09f, 0.09f, 0.05f), Color.black);
            P("candle", Cyl, V(x + 0.6f, 1.6f, 9.1f), V(0.1f, 0.2f, 0.1f), C(0.9f, 0.88f, 0.8f));
            Glow("flame", Sph, V(x + 0.6f, 1.88f, 9.1f), V(0.1f, 0.18f, 0.1f), C(1f, 0.7f, 0.3f));
        }
        for (float x = -14f; x <= 14f; x += 7f)
        {
            P("pillar", Cube, V(x, 5f, 8.2f), V(0.9f, 10f, 0.9f), C(0.2f, 0.18f, 0.25f));
            P("spire", Cube, V(x, 10.2f, 8.2f), V(0.7f, 0.7f, 0.7f), C(0.2f, 0.18f, 0.25f), V(0f, 0f, 45f));
            P("chain", Cyl, V(x + 1.8f, 11f, 7.5f), V(0.05f, 3f, 0.05f), C(0.25f, 0.25f, 0.28f));
        }
        for (int i = 0; i < 12; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * Random.Range(8f, 17f), z = Random.Range(1f, 7f);
            P("tomb", Cube, V(x, 0.7f, z), V(0.9f, 1.4f, 0.25f), grave, V(0f, Random.Range(-15f, 15f), Random.Range(-8f, 8f)));
            if (i % 3 == 0) P("cross", Cube, V(x, 1.7f, z), V(0.6f, 0.15f, 0.2f), grave);
        }
        float[] cx = { -15f, -11f, -6f, 6f, 11f, 15f };
        for (int i = 0; i < cx.Length; i++)
        {
            float z = Random.Range(5.5f, 8f);
            for (int k = 0; k < 3; k++)
                Glow("crystal", Cube, V(cx[i] + Random.Range(-0.4f, 0.4f), 0.8f, z + Random.Range(-0.3f, 0.3f)), V(0.35f, Random.Range(1.5f, 2.8f), 0.35f), C(0.7f, 0.3f, 1f), V(0f, Random.Range(0f, 90f), Random.Range(-25f, 25f)));
            Lamp(V(cx[i], 1.8f, z - 0.8f), C(0.7f, 0.3f, 1f), 6f, 1.8f, true);
        }
        anim.particles = 4;
    }

    // ---------- 4: volcano ----------
    static void Volcano(Light sun, Light fill, Camera cam)
    {
        Env(cam, C(0.16f, 0.05f, 0.04f), C(0.32f, 0.1f, 0.05f), 0.026f, C(0.55f, 0.22f, 0.12f), C(0.32f, 0.13f, 0.08f), C(0.25f, 0.08f, 0.03f));
        Aim(sun, C(1f, 0.5f, 0.28f), 0.9f, V(35f, 30f, 0f));
        Aim(fill, C(1f, 0.45f, 0.15f), 0.55f, V(-25f, 160f, 0f));
        Floor(C(0.13f, 0.11f, 0.11f), C(0.17f, 0.13f, 0.12f), 0.35f, 2.5f, -22f, 22f, -6f, 6f);

        Color lava = C(1f, 0.38f, 0.06f), rock = C(0.18f, 0.08f, 0.07f);
        for (int i = 0; i < 25; i++)
            Glow("crack", Cube, V(Random.Range(-18f, 18f), 0.02f, Random.Range(-5f, 5.5f)), V(Random.Range(0.8f, 2.5f), 0.03f, 0.08f), C(1f, 0.45f, 0.1f), V(0f, Random.Range(0f, 180f), 0f));
        P("cliff", Cube, V(0f, -1.2f, 6.3f), V(50f, 2.4f, 0.8f), rock);
        Glow("lava", Cube, V(0f, -0.9f, 14f), V(90f, 0.3f, 15f), lava);
        for (int i = 0; i < 14; i++)
        {
            Transform crust = P("crust", Cube, V(Random.Range(-30f, 30f), -0.72f, Random.Range(8f, 20f)), V(Random.Range(1f, 3f), 0.12f, Random.Range(0.8f, 2f)), C(0.2f, 0.08f, 0.05f), V(0f, Random.Range(0f, 180f), 0f));
            anim.Bob(crust, 0.06f);
        }
        for (float x = -12f; x <= 12f; x += 6f) Lamp(V(x, 1.2f, 8f), C(1f, 0.45f, 0.12f), 11f, 2.2f, true);
        for (int k = 0; k < 8; k++)
        {
            float r = 14f - k * 1.6f;
            P("cone", Cyl, V(4f, k * 1.8f + 0.9f, 32f), V(r, 0.9f, r), rock);
        }
        Glow("crater", Cyl, V(4f, 14.5f, 32f), V(3.2f, 0.2f, 3.2f), lava);
        for (int k = 0; k < 4; k++)
            Glow("flow", Cube, V(4f + (k - 1.5f) * 2.5f, 8f, 27f), V(0.5f, 12f, 0.3f), lava, V(0f, 0f, (k - 1.5f) * 18f));
        for (int i = 0; i < 8; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            P("spire", Cube, V(side * Random.Range(9f, 18f), 2.5f, Random.Range(1f, 5f)), V(1.2f, Random.Range(4f, 9f), 1.2f), C(0.12f, 0.07f, 0.06f), V(0f, Random.Range(0f, 90f), Random.Range(-12f, 12f)));
        }
        anim.particles = 5;
    }
}

// Small life for the maps: flickering fires, bobbing islands, drifting particles.
public class ArenaAnim : MonoBehaviour
{
    readonly List<Light> lights = new List<Light>();
    readonly List<float> lightBase = new List<float>();
    readonly List<Transform> bobs = new List<Transform>();
    readonly List<Vector3> bobBase = new List<Vector3>();
    readonly List<float> bobAmp = new List<float>();
    public int particles;
    float t, spawnT;

    public void Flicker(Light l) { lights.Add(l); lightBase.Add(l.intensity); }
    public void Bob(Transform tr, float amp) { bobs.Add(tr); bobBase.Add(tr.position); bobAmp.Add(amp); }

    void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        for (int i = 0; i < lights.Count; i++)
            if (lights[i] != null) lights[i].intensity = lightBase[i] * (0.75f + 0.45f * Mathf.PerlinNoise(t * 5f, i * 7.3f));
        for (int i = 0; i < bobs.Count; i++)
            if (bobs[i] != null) bobs[i].position = bobBase[i] + Vector3.up * Mathf.Sin(t * 0.7f + i * 1.7f) * bobAmp[i];
        spawnT -= dt;
        if (particles > 0 && spawnT <= 0f) Spawn();
    }

    void Spawn()
    {
        switch (particles)
        {
            case 1: // dust motes in the evening sun
                spawnT = 0.15f;
                Fx.Spawn(new Vector3(Random.Range(-14f, 14f), Random.Range(0.5f, 7f), Random.Range(-2f, 8f)), new Color(1f, 0.85f, 0.6f), 0.08f, 5f, new Vector3(Random.Range(0.1f, 0.4f), Random.Range(-0.05f, 0.1f), 0f), 0f).Mode(1);
                break;
            case 2: // cherry petals
                spawnT = 0.09f;
                Fx.Spawn(new Vector3(Random.Range(-16f, 16f), 10f, Random.Range(-3f, 8f)), new Color(1f, 0.7f, 0.85f), 0.14f, 7f, new Vector3(Random.Range(0.3f, 0.9f), Random.Range(-1.3f, -0.8f), 0f), 0f).Mode(1);
                break;
            case 3: // golden sparkles
                spawnT = 0.07f;
                Fx.Spawn(new Vector3(Random.Range(-15f, 15f), Random.Range(0.5f, 9f), Random.Range(-2f, 8f)), new Color(1f, 0.92f, 0.55f), 0.1f, 2.5f, new Vector3(0f, 0.4f, 0f), 0f).Mode(1);
                break;
            case 4: // wisps rising from the crypt floor
                spawnT = 0.1f;
                Fx.Spawn(new Vector3(Random.Range(-16f, 16f), 0.2f, Random.Range(-2f, 8f)), new Color(0.6f, 0.25f, 0.9f), 0.3f, 3.5f, new Vector3(Random.Range(-0.2f, 0.2f), 0.7f, 0f), 0f).Mode(1);
                break;
            default: // embers
                spawnT = 0.04f;
                Fx.Spawn(new Vector3(Random.Range(-16f, 16f), 0.2f, Random.Range(-2f, 10f)), new Color(1f, 0.5f, 0.12f), 0.1f, 3f, new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(2f, 4f), 0f), 0f).Mode(1);
                break;
        }
    }
}
