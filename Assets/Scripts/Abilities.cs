using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// Anything an ability leaves in the world: projectiles, meteors, clouds. Cleared between rounds.
public class Hazard : MonoBehaviour
{
    static readonly List<Hazard> live = new List<Hazard>();

    protected virtual void OnEnable() { live.Add(this); }
    protected virtual void OnDisable() { live.Remove(this); }

    public static void ClearAll()
    {
        for (int i = live.Count - 1; i >= 0; i--) if (live[i] != null) Destroy(live[i].gameObject);
        live.Clear();
    }
}

public class Projectile : Hazard
{
    Fighter target;
    Vector3 vel;
    float dmg, life = 3f, radius, size, fxT;
    Move move;
    Action<Fighter> onHit;
    bool falling;
    Color col;
    Transform spinner;
    bool quiet;     // shurikens: no glow trail of sparks

    // falling projectiles (meteors) explode on the ground; the others fly along the fight line
    public static Projectile Launch(Fighter target, Vector3 pos, Vector3 vel, float size, Color c, float dmg, Move move, Action<Fighter> onHit, bool falling, float radius)
    {
        var tr = Fighter.Part(null, "projectile", PrimitiveType.Sphere, pos, Vector3.one * size, c, Vector3.zero);
        var r = tr.GetComponent<Renderer>();
        r.sharedMaterial = Fighter.GlowOf(c);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (!falling && Mathf.Abs(vel.x) > 18f) tr.localScale = new Vector3(size * 2.8f, size * 0.4f, size * 0.4f);   // arrows are long and thin
        var p = tr.gameObject.AddComponent<Projectile>();
        p.target = target; p.vel = vel; p.size = size; p.col = c; p.dmg = dmg; p.move = move; p.onHit = onHit; p.falling = falling; p.radius = radius;
        var l = tr.gameObject.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = 5f; l.intensity = 2.5f;
        Fighter.MakeTrail(tr, Vector3.zero, c, size * 0.9f, 0.3f).emitting = true;
        return p;
    }

    // a spinning steel star thrown along the fight line
    public static Projectile Shuriken(Fighter target, Vector3 pos, Vector3 vel, float dmg)
    {
        var g = new GameObject("shuriken");
        g.transform.position = pos;
        var p = g.AddComponent<Projectile>();
        p.target = target; p.vel = vel; p.size = 0.5f; p.col = new Color(0.85f, 0.88f, 0.95f); p.dmg = dmg;
        p.move = Fighter.StarMove; p.radius = 0.8f; p.life = 1.2f; p.quiet = true;
        p.spinner = new GameObject("spin").transform;
        p.spinner.SetParent(g.transform, false);
        Material steel = Fighter.MatX(new Color(0.72f, 0.75f, 0.8f), 0.9f, 0.85f, false);
        for (int k = 0; k < 2; k++)
        {
            var blade = Fighter.Part(p.spinner, "blade", PrimitiveType.Cube, Vector3.zero, new Vector3(0.62f, 0.13f, 0.03f), Color.gray, new Vector3(0f, 0f, k * 90f + 45f));
            blade.GetComponent<Renderer>().sharedMaterial = steel;
        }
        Fighter.Part(p.spinner, "hub", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.14f, 0.02f, 0.14f), new Color(0.2f, 0.2f, 0.22f), new Vector3(90f, 0f, 0f));
        Fighter.MakeTrail(g.transform, Vector3.zero, new Color(0.9f, 0.95f, 1f), 0.25f, 0.12f).emitting = true;
        return p;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        life -= dt;
        transform.position += vel * dt;
        if (spinner != null) spinner.Rotate(0f, 0f, -Mathf.Sign(vel.x) * 1400f * dt, Space.Self);
        fxT -= dt;
        if (fxT <= 0f && !quiet)
        {
            fxT = 0.03f;
            Fx.Spawn(transform.position + Random.insideUnitSphere * size * 0.4f, col, size * 0.45f, 0.3f, -vel * 0.08f, 0f).Mode(1);
        }
        Vector3 p = transform.position;
        if (falling)
        {
            if (p.y <= 0.35f) { Explode(); return; }
        }
        else if (target != null && target.Hittable)
        {
            Vector3 tp = target.transform.position;
            if (Mathf.Abs(p.x - tp.x) < radius && p.y > tp.y - 0.3f && p.y < tp.y + target.headY + 0.3f) { Hit(vel.x >= 0f ? 1 : -1); return; }
        }
        if (life <= 0f || Mathf.Abs(p.x) > 14f) Destroy(gameObject);
    }

    void Hit(int dir)
    {
        Vector3 p = transform.position;
        bool landed = target.TakeHit(move, dmg, dir);
        if (landed && onHit != null) onHit(target);
        Fx.Sparks(p, col, 14, 7f);
        Fx.Flash(p, col, 6f, 0.3f);
        Destroy(gameObject);
    }

    void Explode()
    {
        Vector3 p = transform.position;
        p.y = 0.3f;
        Fx.Sparks(p, col, 20, 9f);
        Fx.Dust(p, 8, 2f);
        Fx.Flash(p + Vector3.up, col, 9f, 0.4f);
        Fighter.Shake = Mathf.Max(Fighter.Shake, 0.14f);
        GameAudio.Sfx("hit2");
        if (target != null && target.Hittable)
        {
            Vector3 tp = target.transform.position;
            if (Mathf.Abs(tp.x - p.x) < radius && tp.y < 1.8f)
            {
                bool landed = target.TakeHit(move, dmg, tp.x >= p.x ? 1 : -1);
                if (landed && onHit != null) onHit(target);
            }
        }
        Destroy(gameObject);
    }
}

public class PoisonCloud : Hazard
{
    Fighter target;
    float x, t = 5f, tick, fxT, dps;
    Color col;

    public static void Spawn(Fighter target, float x, float dps, Color c)
    {
        var g = new GameObject("poisonCloud");
        g.transform.position = new Vector3(x, 0.5f, 0f);
        var pc = g.AddComponent<PoisonCloud>();
        pc.target = target; pc.x = x; pc.dps = dps; pc.col = c;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = 6f; l.intensity = 1.8f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        t -= dt; tick -= dt; fxT -= dt;
        if (fxT <= 0f)
        {
            fxT = 0.04f;
            Color c = Color.Lerp(col, new Color(0.3f, 0.8f, 0.2f), Random.value * 0.5f);
            Fx.Spawn(new Vector3(x + Random.Range(-2f, 2f), Random.Range(0.2f, 3f), Random.Range(-0.6f, 0.6f)), c, Random.Range(0.4f, 0.8f), 0.9f, new Vector3(Random.Range(-0.3f, 0.3f), 0.6f, 0f), 0f).Mode(1);
        }
        if (tick <= 0f)
        {
            tick = 0.5f;
            if (target != null && !target.Dead && Mathf.Abs(target.transform.position.x - x) < 2.1f) target.ApplyStatus("poison", 1.2f, dps);
        }
        if (t <= 0f) Destroy(gameObject);
    }
}
