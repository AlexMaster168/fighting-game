using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEngine;

// Everything you hear is synthesised here at startup, so the game ships without a single audio file.
public class GameAudio : MonoBehaviour
{
    const int SR = 44100;

    static GameAudio inst;
    AudioSource music, sfx, voice;
    AudioClip menuClip;
    readonly AudioClip[] arenaClips = new AudioClip[5];
    int playing = -2;
    readonly Dictionary<string, AudioClip> fx = new Dictionary<string, AudioClip>();
    readonly System.Random rng = new System.Random(7);
    bool muted;

    public static void Boot(GameObject host)
    {
        if (inst == null) inst = host.AddComponent<GameAudio>();
    }

    public static void Sfx(string name)
    {
        if (inst == null) return;
        AudioClip c;
        if (inst.fx.TryGetValue(name, out c)) inst.sfx.PlayOneShot(c, name.StartsWith("whoosh") ? 0.5f : 0.9f);
    }

    // a grunt of pain: low voice for the boy, high for the girl, a few variants each
    public static void Pain(bool female, bool heavy)
    {
        if (inst == null) return;
        string key = (female ? "pf_" : "pm_") + (heavy ? "h" : "l") + Random.Range(0, 3);
        AudioClip c;
        if (!inst.fx.TryGetValue(key, out c)) return;
        inst.voice.pitch = Random.Range(0.95f, 1.06f);
        inst.voice.PlayOneShot(c, 0.9f);
    }

    public static void Music(bool fight)
    {
        if (inst == null) return;
        inst.SwitchMusic(fight);
    }

    void Awake()
    {
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.volume = 0.5f;
        music.spatialBlend = 0f;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.spatialBlend = 0f;
        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 0f;

        menuClip = MakeMenu();
        fx["hit"] = MakeHit(false);
        fx["hit2"] = MakeHit(true);
        fx["block"] = MakeBlock();
        fx["whoosh"] = MakeWhoosh(0.22f, 0.5f);
        fx["whoosh2"] = MakeWhoosh(0.4f, 0.9f);
        fx["ko"] = MakeKo();
        fx["zap"] = MakeZap();
        fx["fire"] = MakeFire();
        fx["holy"] = MakeHoly();
        fx["dark"] = MakeDark();

        for (int i = 0; i < 3; i++)
        {
            fx["pm_l" + i] = MakeVoice("pm_l" + i, false, 105f + i * 14f, 0.24f);
            fx["pm_h" + i] = MakeVoice("pm_h" + i, false, 98f + i * 11f, 0.55f);
            fx["pf_l" + i] = MakeVoice("pf_l" + i, true, 255f + i * 28f, 0.24f);
            fx["pf_h" + i] = MakeVoice("pf_h" + i, true, 240f + i * 24f, 0.55f);
        }
        // real recorded-style voices (Windows speech, pitched per character) replace the synthesised fallback
        int loaded = 0;
        foreach (string k in new List<string>(fx.Keys))
        {
            if (!k.StartsWith("pm_") && !k.StartsWith("pf_")) continue;
            var c = Resources.Load<AudioClip>("Voice/" + k);
            if (c != null) { fx[k] = c; loaded++; }
        }
        Debug.Log("AUDIO voices loaded " + loaded + "/12");
        Debug.Log("AUDIO pain male " + Pitch(fx["pm_l0"]).ToString("0") + "Hz/" + Pitch(fx["pm_h1"]).ToString("0") + "Hz  female " + Pitch(fx["pf_l0"]).ToString("0") + "Hz/" + Pitch(fx["pf_h1"]).ToString("0")
                  + "Hz  peaks " + Stats(fx["pm_h0"]) + " | " + Stats(fx["pf_h0"]));
        Debug.Log("AUDIO menu " + Stats(menuClip));
        SwitchMusic(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            muted = !muted;
            music.mute = muted;
        }
    }

    void SwitchMusic(bool fight)
    {
        if (fight) { PlayArena(0); return; }
        if (playing == -1 && music.isPlaying) return;
        playing = -1;
        music.clip = menuClip;
        music.volume = 0.4f;
        music.Play();
    }

    // each arena has its own track, synthesised the first time it is needed
    public static void MusicArena(int idx) { if (inst != null) inst.PlayArena(idx); }

    void PlayArena(int idx)
    {
        idx = Mathf.Clamp(idx, 0, arenaClips.Length - 1);
        if (playing == idx && music.isPlaying) return;
        if (arenaClips[idx] == null)
        {
            float t0 = Time.realtimeSinceStartup;
            arenaClips[idx] = MakeTrack(idx);
            Debug.Log("AUDIO track " + idx + " " + Stats(arenaClips[idx]) + " built in " + (Time.realtimeSinceStartup - t0).ToString("0.00") + "s");
        }
        playing = idx;
        music.clip = arenaClips[idx];
        music.volume = 0.5f;
        music.Play();
    }

    // ---------- synthesis helpers ----------
    float N() { return (float)(rng.NextDouble() * 2.0 - 1.0); }
    static float Saw(float ph) { return 2f * (ph - Mathf.Floor(ph)) - 1f; }
    static float Sq(float ph) { return (ph - Mathf.Floor(ph)) < 0.5f ? 1f : -1f; }
    static float Sin(float ph) { return Mathf.Sin(ph * 6.2831853f); }

    static string Stats(AudioClip c)
    {
        float[] d = new float[c.samples];
        c.GetData(d, 0);
        float peak = 0f, sum = 0f;
        for (int i = 0; i < d.Length; i++) { float a = Mathf.Abs(d[i]); if (a > peak) peak = a; sum += a; }
        return (d.Length / (float)SR).ToString("0.0") + "s peak " + peak.ToString("0.00") + " avg " + (sum / d.Length).ToString("0.000");
    }

    static AudioClip ToClip(string name, float[] buf, float gain)
    {
        for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i] * gain);   // soft clip, keeps loud hits from harshly distorting
        var clip = AudioClip.Create(name, buf.Length, 1, SR, false);
        clip.SetData(buf, 0);
        return clip;
    }

    static float Hz(int semitonesFromA) { return 55f * Mathf.Pow(2f, semitonesFromA / 12f); }   // A1 = 55 Hz

    // write a decaying note into the buffer
    void Note(float[] buf, float startSec, float lenSec, Func<float, float, float> osc, float freq, float amp, float decay)
    {
        int s0 = (int)(startSec * SR), n = (int)(lenSec * SR);
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            float t = i / (float)SR;
            float atk = Mathf.Min(1f, t / 0.004f);
            float rel = Mathf.Min(1f, (lenSec - t) / 0.02f);
            buf[s0 + i] += osc(freq, t) * amp * atk * rel * Mathf.Exp(-t / decay);
        }
    }

    // ---------- arena tracks: 16 bars each, intro / main theme / climax ----------
    class Track
    {
        public float bpm, pad, leadAmp;
        public int key, lead, drums, bass;
        public int[] roots, thirds;
        public int[][] riff;
    }

    static Track TrackDef(int idx)
    {
        switch (idx)
        {
            case 0: // Colosseum: heroic brass and war drums, D minor
                return new Track { bpm = 136, key = 5, lead = 1, drums = 1, bass = 0, pad = 0.08f, leadAmp = 0.14f,
                    roots = new[] { 0, -4, -2, -5 }, thirds = new[] { 3, 4, 4, 4 },
                    riff = new[] {
                        new[] { 0, -1, -1, 0, 3, -1, 7, -1, 5, -1, 3, -1, 2, -1, -1, -1 },
                        new[] { 0, -1, -1, 0, 3, -1, 7, -1, 10, -1, 8, -1, 7, -1, -1, -1 },
                        new[] { 12, -1, 10, -1, 8, -1, 7, -1, 8, -1, 10, -1, 12, -1, -1, -1 },
                        new[] { 7, -1, 5, -1, 3, -1, 2, -1, 3, -1, -1, 2, 0, -1, -1, -1 } } };
            case 1: // Bamboo temple: koto pluck and taiko, E pentatonic
                return new Track { bpm = 116, key = 7, lead = 2, drums = 2, bass = 1, pad = 0.06f, leadAmp = 0.22f,
                    roots = new[] { 0, -2, -4, -2 }, thirds = new[] { 3, 4, 4, 4 },
                    riff = new[] {
                        new[] { 12, -1, 10, -1, 7, -1, -1, -1, 5, -1, 7, -1, -1, -1, -1, -1 },
                        new[] { 10, -1, 12, -1, 15, -1, 12, -1, 10, -1, 7, -1, -1, -1, -1, -1 },
                        new[] { 7, -1, 5, -1, 3, -1, 5, -1, 7, -1, 10, -1, 12, -1, -1, -1 },
                        new[] { 15, -1, 12, 10, -1, -1, 7, -1, 5, -1, 3, -1, 0, -1, -1, -1 } } };
            case 2: // Sky sanctum: bells over a light groove, C major
                return new Track { bpm = 108, key = 3, lead = 3, drums = 3, bass = 2, pad = 0.09f, leadAmp = 0.2f,
                    roots = new[] { 0, 5, 7, -3 }, thirds = new[] { 4, 4, 4, 3 },
                    riff = new[] {
                        new[] { 0, -1, 4, -1, 7, -1, 11, -1, 12, -1, -1, -1, 11, -1, 7, -1 },
                        new[] { 9, -1, 7, -1, 4, -1, 7, -1, 5, -1, -1, -1, 4, -1, 2, -1 },
                        new[] { 0, -1, 4, -1, 7, -1, 12, -1, 14, -1, -1, -1, 12, -1, 11, -1 },
                        new[] { 9, -1, 11, -1, 12, -1, -1, -1, 7, -1, -1, -1, -1, -1, -1, -1 } } };
            case 3: // Shadow crypt: drone and eerie voice, F# phrygian
                return new Track { bpm = 90, key = -3, lead = 4, drums = 4, bass = 3, pad = 0.1f, leadAmp = 0.16f,
                    roots = new[] { 0, 1, 0, -2 }, thirds = new[] { 3, 3, 3, 3 },
                    riff = new[] {
                        new[] { 0, -1, -1, -1, -1, -1, -1, -1, 1, -1, -1, -1, -1, -1, -1, -1 },
                        new[] { 3, -1, -1, -1, -1, -1, 1, -1, 0, -1, -1, -1, -1, -1, -1, -1 },
                        new[] { 7, -1, -1, -1, 8, -1, -1, -1, 7, -1, -1, -1, 5, -1, -1, -1 },
                        new[] { 3, -1, -1, -1, 1, -1, -1, -1, 0, -1, -1, -1, -1, -1, -1, -1 } } };
            default: // Volcano: fast metal, E minor
                return new Track { bpm = 168, key = 7, lead = 5, drums = 0, bass = 4, pad = 0.04f, leadAmp = 0.12f,
                    roots = new[] { 0, 0, -4, -2 }, thirds = new[] { 3, 3, 4, 4 },
                    riff = new[] {
                        new[] { 0, 0, 12, 0, 0, 10, 0, 0, 7, 0, 6, 0, 5, 0, 3, 0 },
                        new[] { 0, 0, 12, 0, 0, 10, 0, 0, 7, -1, 8, -1, 7, -1, 6, -1 },
                        new[] { 12, -1, 10, -1, 12, -1, 15, -1, 13, -1, 12, -1, 10, -1, 8, -1 },
                        new[] { 7, -1, 6, -1, 5, -1, 3, -1, 0, -1, -1, -1, 12, -1, -1, -1 } } };
        }
    }

    AudioClip MakeTrack(int idx)
    {
        Track d = TrackDef(idx);
        float step = 60f / d.bpm / 4f;
        int bars = 16, steps = bars * 16;
        var buf = new float[(int)(steps * step * SR)];
        float[] leadLen = { 1.8f, 1.8f, 3f, 4f, 6f, 0.9f };
        float[] leadDecay = { 0.35f, 0.5f, 0.25f, 0.7f, 1.2f, 0.25f };
        for (int s = 0; s < steps; s++)
        {
            int bar = s / 16, st = s % 16, chord = bar % 4;
            int sec = bar < 4 ? 0 : bar < 12 ? 1 : 2;
            float t0 = s * step;
            Drums(d.drums, buf, t0, st, bar, sec);
            Bass(d.bass, buf, t0, st, Hz(d.roots[chord] + d.key), step);
            if (st == 0)
                foreach (int semi in new[] { 24, 24 + d.thirds[chord], 31 })
                    Note(buf, t0, step * 16f, (f, t) => Sin(f * t) * 0.6f + Sin(f * 1.004f * t) * 0.4f, Hz(d.roots[chord] + d.key + semi), d.pad, 3f);
            if (sec >= 1)
            {
                int deg = d.riff[bar % 4][st];
                if (deg >= 0)
                {
                    int type = d.lead;
                    Note(buf, t0, step * leadLen[type], (f, t) => LeadOsc(type, f, t), Hz(deg + 24 + d.key), d.leadAmp, leadDecay[type]);
                    if (sec == 2) Note(buf, t0, step * leadLen[type], (f, t) => LeadOsc(type, f, t), Hz(deg + 31 + d.key), d.leadAmp * 0.5f, leadDecay[type]);
                }
            }
        }
        return ToClip("arena" + idx, buf, 1.1f);
    }

    float LeadOsc(int type, float f, float t)
    {
        switch (type)
        {
            case 1: // brass
            {
                float vib = 1f + 0.005f * Mathf.Sin(t * 35f);
                return (Saw(f * vib * t) * 0.6f + Saw(f * 1.006f * vib * t) * 0.4f) * Mathf.Min(1f, t / 0.03f);
            }
            case 2: return Sin(f * t) * 0.7f + Sin(f * 2f * t) * 0.3f * Mathf.Exp(-t * 12f);          // koto pluck
            case 3: return Sin(f * t) * 0.7f + Sin(f * 2.76f * t) * 0.3f * Mathf.Exp(-t * 6f);        // bell
            case 4:                                                                                   // eerie voice
            {
                float vib = 1f + 0.012f * Mathf.Sin(t * 9f);
                return Sin(f * vib * t) * 0.6f + Sin(f * 0.5f * t) * 0.25f + Sin(f * 1.01f * vib * t) * 0.3f;
            }
            case 5: return (float)Math.Tanh((Saw(f * t) + Saw(f * 1.498f * t) + Saw(f * 0.5f * t) * 0.7f) * 2.2f) * 0.6f;   // power chord
            default: return Sq(f * t) * 0.5f + Saw(f * 1.005f * t) * 0.4f;
        }
    }

    void Kick(float[] b, float t0, float amp, float decay)
    {
        Note(b, t0, 0.3f, (f, t) => Sin(45f * t + (75f / 22f) * (1f - Mathf.Exp(-t * 22f))), 1f, amp, decay);
    }

    void Snare(float[] b, float t0, float amp)
    {
        Note(b, t0, 0.2f, (f, t) => N() * 0.9f, 1f, amp, 0.07f);
        Note(b, t0, 0.15f, (f, t) => Sin(185f * t), 1f, amp * 0.8f, 0.05f);
    }

    void Hat(float[] b, float t0, float amp, float decay)
    {
        Note(b, t0, 0.06f, (f, t) => N(), 1f, amp, decay);
    }

    void Tom(float[] b, float t0, float f0, float amp, float decay)
    {
        Note(b, t0, 0.5f, (f, t) => Sin(f0 * t + (f0 * 0.8f / 15f) * (1f - Mathf.Exp(-t * 15f))), 1f, amp, decay);
    }

    void Drums(int style, float[] b, float t0, int st, int bar, int sec)
    {
        bool fill = bar % 4 == 3 && st >= 12;
        switch (style)
        {
            case 0: // metal: double kick
                if (st % 2 == 0 || sec == 2) Kick(b, t0, 0.8f, 0.06f);
                if (st == 4 || st == 12) Snare(b, t0, 0.6f);
                Hat(b, t0, st % 2 == 0 ? 0.12f : 0.07f, 0.02f);
                if (fill) Snare(b, t0, 0.35f);
                break;
            case 1: // war drums
                if (st == 0 || st == 8 || st == 10) Kick(b, t0, 0.95f, 0.09f);
                if (st == 3 || st == 6 || st == 11 || st == 14) Tom(b, t0, 95f, 0.5f, 0.12f);
                if (st == 4 || st == 12) Snare(b, t0, 0.5f);
                if (fill || (sec == 2 && st % 2 == 1)) Snare(b, t0, 0.25f);
                if (st % 4 == 2) Hat(b, t0, 0.07f, 0.02f);
                break;
            case 2: // taiko
                if (st == 0 || st == 10) Tom(b, t0, 60f, 0.95f, 0.3f);
                if (st == 6 || st == 12 || st == 14) Tom(b, t0, 110f, 0.55f, 0.14f);
                if (st == 4 || st == 12) Hat(b, t0, 0.18f, 0.012f);
                if (fill) Tom(b, t0, 85f, 0.45f, 0.1f);
                break;
            case 3: // light groove
                if (st == 0 || st == 8) Kick(b, t0, 0.55f, 0.09f);
                if (st == 4 || st == 12) Snare(b, t0, 0.28f);
                Hat(b, t0, st % 2 == 0 ? 0.06f : 0.035f, 0.015f);
                break;
            default: // sparse and ominous
                if (st == 0) Kick(b, t0, 0.9f, 0.25f);
                if (st == 8 && bar % 2 == 1) Tom(b, t0, 50f, 0.7f, 0.4f);
                if (sec == 2 && (st == 4 || st == 12)) Snare(b, t0, 0.25f);
                if (st % 4 == 0) Hat(b, t0, 0.03f, 0.04f);
                break;
        }
    }

    void Bass(int style, float[] b, float t0, int st, float root, float step)
    {
        switch (style)
        {
            case 0: // driving eighths with octave jumps
                if (st % 2 == 0) Note(b, t0, step * 1.9f, (f, t) => Sin(f * t) * 0.7f + Saw(f * t) * 0.35f, root * ((st == 6 || st == 14) ? 2f : 1f), 0.45f, 0.25f);
                break;
            case 1: // plucked
                if (st == 0 || st == 8 || st == 11) Note(b, t0, step * 3f, (f, t) => Sin(f * t) * 0.8f + Sin(2f * f * t) * 0.25f * Mathf.Exp(-t * 10f), root, 0.5f, 0.35f);
                break;
            case 2: // soft quarters
                if (st % 4 == 0) Note(b, t0, step * 3.8f, (f, t) => Sin(f * t), root, 0.45f, 0.5f);
                break;
            case 3: // drone
                if (st == 0) Note(b, t0, step * 16f, (f, t) => Sin(f * t) * 0.8f + Saw(f * t) * 0.15f, root, 0.4f, 6f);
                break;
            default: // distorted chug
                Note(b, t0, step * 0.95f, (f, t) => (float)Math.Tanh(Saw(f * t) * 3f) * 0.6f, root, 0.35f, 0.2f);
                break;
        }
    }

    // ---------- menu track: 84 BPM, slow and moody ----------
    AudioClip MakeMenu()
    {
        const float bpm = 84f;
        float beat = 60f / bpm, step = beat / 2f;
        int bars = 8, steps = bars * 8;
        var buf = new float[(int)(steps * step * SR)];
        int[] roots = { 0, -4, -2, -5 };
        int[] thirds = { 3, 4, 4, 4 };
        int[] arp = { 0, 7, 12, 7, 15, 12, 7, 12 };

        for (int s = 0; s < steps; s++)
        {
            int bar = s / 8, st = s % 8;
            float t0 = s * step;
            int chord = bar % 4;
            if (st == 0)
            {
                foreach (int semi in new[] { roots[chord] + 12, roots[chord] + 12 + thirds[chord], roots[chord] + 19, roots[chord] + 24 })
                    Note(buf, t0, step * 8f, (f, t) => Sin(f * t) * 0.6f + Sin(f * 0.997f * t) * 0.4f, Hz(semi), 0.11f, 4f);
                Note(buf, t0, step * 4f, (f, t) => Sin(45f * t + (55f / 18f) * (1f - Mathf.Exp(-t * 18f))), 1f, 0.8f, 0.14f);
            }
            if (st == 4) Note(buf, t0, step * 4f, (f, t) => Sin(45f * t + (55f / 18f) * (1f - Mathf.Exp(-t * 18f))), 1f, 0.6f, 0.12f);
            Note(buf, t0, step * 1.6f, (f, t) => Sin(f * t) * 0.8f + Sin(f * 2f * t) * 0.2f, Hz(roots[chord] + arp[st] + 24), 0.14f, 0.3f);
            if (st % 2 == 1) Note(buf, t0, 0.05f, (f, t) => N(), 1f, 0.05f, 0.02f);
        }
        return ToClip("menuMusic", buf, 1.1f);
    }

    // ---------- sound effects ----------
    AudioClip MakeHit(bool heavy)
    {
        float len = heavy ? 0.4f : 0.2f;
        var buf = new float[(int)(len * SR)];
        Note(buf, 0f, len, (f, t) => N(), 1f, heavy ? 0.9f : 0.7f, heavy ? 0.06f : 0.035f);
        Note(buf, 0f, len, (f, t) => Sin((heavy ? 40f : 60f) * t + (heavy ? 70f : 50f) / 25f * (1f - Mathf.Exp(-t * 25f))), 1f, 1f, heavy ? 0.16f : 0.08f);
        if (heavy) Note(buf, 0.02f, 0.25f, (f, t) => Saw(55f * t) * N() * 0.6f, 1f, 0.6f, 0.08f);
        return ToClip(heavy ? "hit2" : "hit", buf, 1.4f);
    }

    AudioClip MakeBlock()
    {
        var buf = new float[(int)(0.45f * SR)];
        foreach (float f0 in new[] { 1180f, 1830f, 2650f, 3500f })
            Note(buf, 0f, 0.45f, (f, t) => Sin(f * t), f0, 0.3f, 0.12f);
        Note(buf, 0f, 0.05f, (f, t) => N(), 1f, 0.5f, 0.01f);
        return ToClip("block", buf, 1.2f);
    }

    AudioClip MakeWhoosh(float len, float amp)
    {
        var buf = new float[(int)(len * SR)];
        float y = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float u = i / (float)buf.Length;
            float a = Mathf.Lerp(0.03f, 0.42f, Mathf.Sin(u * Mathf.PI));   // cutoff opens then closes: the "swoosh"
            y += a * (N() - y);
            buf[i] = y * Mathf.Sin(u * Mathf.PI) * amp * 2.2f;
        }
        return ToClip("whoosh", buf, 1f);
    }

    AudioClip MakeZap()
    {
        var buf = new float[(int)(0.6f * SR)];
        float ph = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SR;
            ph += (2800f * Mathf.Exp(-t * 5f) + 120f) / SR;
            float chop = ((int)(t * 70f) % 2 == 0) ? 1f : 0.2f;
            buf[i] = (N() * 0.6f + Saw(ph) * 0.4f) * Mathf.Exp(-t * 4f) * chop;
        }
        return ToClip("zap", buf, 1.6f);
    }

    AudioClip MakeFire()
    {
        var buf = new float[(int)(0.95f * SR)];
        float y = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float u = i / (float)buf.Length;
            y += 0.08f * (N() - y);
            float env = Mathf.Sin(Mathf.Min(1f, u * 1.6f) * Mathf.PI * 0.5f) * (1f - u);
            float crackle = rng.NextDouble() < 0.002 ? N() * 1.2f : 0f;
            buf[i] = y * env * 3.2f + crackle * env;
        }
        return ToClip("fire", buf, 1.4f);
    }

    AudioClip MakeHoly()
    {
        var buf = new float[(int)(1.3f * SR)];
        foreach (float f0 in new[] { 523f, 784f, 1047f, 1568f })
            Note(buf, 0f, 1.3f, (f, t) => Sin(f * t) * (1f + 0.03f * Mathf.Sin(t * 30f)), f0, 0.22f, 0.5f);
        return ToClip("holy", buf, 1.3f);
    }

    AudioClip MakeDark()
    {
        var buf = new float[(int)(1.0f * SR)];
        Note(buf, 0f, 1.0f, (f, t) => Sin(f * t), 55f, 0.7f, 0.45f);
        Note(buf, 0f, 1.0f, (f, t) => Sin(f * t) * Sin(9f * t * 0.5f), 82.5f, 0.5f, 0.45f);
        float y = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float u = i / (float)buf.Length;
            y += 0.05f * (N() - y);
            buf[i] += y * (1f - u) * 1.8f;
        }
        return ToClip("dark", buf, 1.3f);
    }

    // two-pole resonator: shapes a buzzy source into a vowel
    class Reson
    {
        float a1, a2, g, y1, y2;
        public Reson(float f, float bw)
        {
            float r = Mathf.Exp(-Mathf.PI * bw / SR);
            a1 = 2f * r * Mathf.Cos(2f * Mathf.PI * f / SR);
            a2 = -r * r;
            g = 1f - r;
        }
        public float Step(float x)
        {
            float y = g * x + a1 * y1 + a2 * y2;
            y2 = y1; y1 = y;
            return y;
        }
    }

    // a short "aagh": glottal buzz through vowel formants, pitch falling, a little breath noise
    AudioClip MakeVoice(string name, bool female, float f0, float len)
    {
        var buf = new float[(int)(len * SR)];
        float f1 = female ? 850f : 680f, f2 = female ? 1450f : 1100f, f3 = female ? 2900f : 2500f;
        var r1 = new Reson(f1, 110f);
        var r2 = new Reson(f2, 130f);
        var r3 = new Reson(f3, 200f);
        float ph = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SR, u = t / len;
            float f = f0 * (1.35f - 0.5f * u);                       // pitch drops through the cry
            ph += f / SR;
            float src = Saw(ph) * 0.8f + N() * 0.18f;
            float v = r1.Step(src) + r2.Step(src) * 0.6f + r3.Step(src) * 0.25f;
            float env = Mathf.Min(1f, t / 0.012f) * Mathf.Exp(-t / (len * 0.5f)) * Mathf.Min(1f, (len - t) / 0.03f);
            buf[i] = v * env;
        }
        return ToClip(name, buf, 2.2f);
    }

    // rough pitch estimate (autocorrelation) so the log can prove the boy is lower than the girl
    static float Pitch(AudioClip c)
    {
        int n = Mathf.Min(c.samples, 4000);
        var d = new float[n];
        c.GetData(d, 0);
        int best = 0; float bestV = 0f;
        for (int lag = 60; lag < 700; lag++)
        {
            float sum = 0f;
            for (int i = 0; i + lag < n; i++) sum += d[i] * d[i + lag];
            if (sum > bestV) { bestV = sum; best = lag; }
        }
        return best > 0 ? SR / (float)best : 0f;
    }

    AudioClip MakeKo()
    {
        var buf = new float[(int)(1.0f * SR)];
        Note(buf, 0f, 1.0f, (f, t) => Sin(50f * t + (150f / 3f) * (1f - Mathf.Exp(-t * 3f))), 1f, 1f, 0.35f);
        Note(buf, 0f, 0.4f, (f, t) => N(), 1f, 0.6f, 0.12f);
        return ToClip("ko", buf, 1.3f);
    }
}
