using System.Collections.Generic;
using UnityEngine;

// Story mode: campaign select, the world map with the hero walking along the path, dialogs before and after each fight.
public partial class GameMain
{
    StoryNode storyNode;              // non-null while a story fight is running
    int storyC, storyIdx, markerAt = -1, dialogIdx, demoStoryProg = -1;
    int winsNeeded = 2, enemyLevel;
    string enemyName = "";
    bool storyPicking = true, dialogOpen;
    float travelT = -1f;
    Vector2 markerFrom, markerTo;
    Texture2D mapTex, dotTex;

    // the dialog being shown: on the map before a fight, or on the result screen after it
    class DLine { public string who, text; public Color col; public Faction fac; public int side; public bool female, boss, hero; }
    List<DLine> dlg = new List<DLine>();
    int dlgI;
    float dlgStart;
    const float TypeSpeed = 60f;

    static readonly string[] DefeatTaunt =
    {
        "Легион не прощает слабости. Возвращайся, когда окрепнешь.",
        "Ты слишком медленный для пути дракона. Приходи ещё.",
        "Свет не на твоей стороне. Пока что.",
        "Тьма всегда побеждает. Попробуй ещё раз, если осмелишься.",
    };

    int StoryProgress(int c) { return demoStoryProg >= 0 ? demoStoryProg : PlayerPrefs.GetInt("story6_" + c, 0); }

    void SetStoryProgress(int c, int v)
    {
        if (demo) return;
        PlayerPrefs.SetInt("story6_" + c, v);
        PlayerPrefs.Save();
    }

    void OpenStory()
    {
        GameAudio.Music(false);
        ClearAll();
        state = State.StoryMap;
        storyNode = null;
        storyPicking = true;
        dialogOpen = false;
        travelT = -1f;
        EnsureMapTextures();
    }

    void OpenCampaign(int c)
    {
        storyC = c;
        storyPicking = false;
        dialogOpen = false;
        travelT = -1f;
        markerAt = Mathf.Min(StoryProgress(c), Story.Total) - 1;
    }

    void BackToMap()
    {
        ClearAll();
        GameAudio.Music(false);
        Time.timeScale = 1f;
        state = State.StoryMap;
        storyNode = null;
        EnsureMapTextures();
        OpenCampaign(storyC);
    }

    void StartStoryFight(int idx)
    {
        storyIdx = idx;
        storyNode = Story.Node(storyC, idx);
        dialogOpen = false;
        StartFight();
    }

    // called when a story match ends; sets up the after-fight dialog and returns the coin reward
    long StoryFinish(bool won)
    {
        Campaign cp = Story.Campaigns[storyC];
        int act = storyIdx / Story.NodesPerAct;
        if (!won)
        {
            SetDialog("E:" + DefeatTaunt[(int)storyNode.enemy] + "\nA:Не сдавайся. Держи дистанцию рывком (Shift) и сюрикенами (O), бей, когда враг открылся.", storyNode, cp);
            return 5000L;
        }
        string after = storyNode.post;
        if (storyNode.boss)
        {
            after += "\n" + cp.acts[act].outro;
            if (act == Story.Acts - 1) after += "\n" + cp.ending;
        }
        SetDialog(after, storyNode, cp);
        if (storyIdx == StoryProgress(storyC)) SetStoryProgress(storyC, storyIdx + 1);
        return storyNode.boss ? 400000L * (act + 1) : 60000L * (act + 1);
    }

    void OpenDialog(int idx)
    {
        Campaign cp = Story.Campaigns[storyC];
        StoryNode n = Story.Node(storyC, idx);
        dialogIdx = idx;
        bool firstOfAct = idx % Story.NodesPerAct == 0;
        SetDialog((firstOfAct ? cp.acts[idx / Story.NodesPerAct].intro + "\n" : "") + n.pre, n, cp);
        dialogOpen = true;
    }

    void SetDialog(string src, StoryNode n, Campaign cp)
    {
        dlg.Clear();
        foreach (string raw in src.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            char k = line.Length > 2 && line[1] == ':' && "NPAE".IndexOf(line[0]) >= 0 ? line[0] : 'N';
            string text = k == 'N' && !(line.Length > 2 && line[1] == ':') ? line : line.Substring(2).Trim();
            var d = new DLine { text = text };
            switch (k)
            {
                case 'P': d.who = "Ты"; d.col = Db.FactionColor(cp.faction); d.fac = cp.faction; d.side = 1; d.hero = true; d.female = female; break;
                case 'A':
                    d.who = cp.ally; d.col = Db.FactionColor(cp.faction); d.fac = cp.faction; d.side = 1;
                    d.female = "аяи".IndexOf(cp.ally[cp.ally.Length - 1]) >= 0;
                    break;
                case 'E': d.who = n.enemyName; d.col = Db.FactionColor(n.enemy); d.fac = n.enemy; d.side = 2; d.female = n.female; d.boss = n.boss; break;
                default: d.who = ""; d.col = Color.white; d.side = 0; break;
            }
            dlg.Add(d);
        }
        dlgI = 0;
        dlgStart = Time.unscaledTime;
    }

    bool LineTyped { get { return dlg.Count == 0 || (Time.unscaledTime - dlgStart) * TypeSpeed >= dlg[dlgI].text.Length; } }
    bool DialogDone { get { return dlg.Count == 0 || (dlgI >= dlg.Count - 1 && LineTyped); } }

    void DialogAdvance()
    {
        if (dlg.Count == 0) return;
        if (!LineTyped) dlgStart = -999f;
        else if (dlgI < dlg.Count - 1) { dlgI++; dlgStart = Time.unscaledTime; }
    }

    Vector2 MarkerNorm(int at) { return at < 0 ? Story.Campaigns[storyC].start : Story.NodePos(storyC, at); }

    void StoryUpdate(float dt)
    {
        if (travelT >= 0f)
        {
            travelT += dt / 0.9f;
            if (travelT >= 1f)
            {
                travelT = -1f;
                markerAt = dialogIdx;
                OpenDialog(dialogIdx);
            }
        }
        if (dialogOpen && Input.GetKeyDown(KeyCode.Escape)) dialogOpen = false;
    }

    // ---------- textures ----------
    void EnsureMapTextures()
    {
        if (dotTex == null)
        {
            const int n = 64;
            dotTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    dotTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(n / 2f - d)));
                }
            dotTex.Apply();
        }
        if (mapTex != null) return;

        // a parchment world map: four territories with soft, noisy borders
        const int W = 960, H = 540;
        mapTex = new Texture2D(W, H, TextureFormat.RGB24, false);
        Color parchment = new Color(0.87f, 0.79f, 0.61f);
        Color[] tint = { new Color(0.8f, 0.38f, 0.28f), new Color(0.38f, 0.66f, 0.4f), new Color(0.55f, 0.7f, 0.95f), new Color(0.3f, 0.16f, 0.42f) };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H;   // v grows downward, like the map coordinates
                Vector2 q = new Vector2(u + (Mathf.PerlinNoise(u * 6f, v * 6f) - 0.5f) * 0.12f, v + (Mathf.PerlinNoise(u * 6f + 7f, v * 6f + 3f) - 0.5f) * 0.12f);
                int best = 0;
                float bd = 9f, sd = 9f;
                for (int r = 0; r < 4; r++)
                {
                    Vector2 c = Story.Region[r];
                    float d = Vector2.Distance(new Vector2(q.x * 1.6f, q.y), new Vector2(c.x * 1.6f, c.y)) * (r == 3 ? 1.15f : 1f);
                    if (d < bd) { sd = bd; bd = d; best = r; }
                    else if (d < sd) sd = d;
                }
                float border = Mathf.Clamp01((sd - bd) / 0.02f);
                Color c0 = Color.Lerp(parchment, tint[best], best == 3 ? 0.62f : 0.48f);
                float shade = 0.82f + 0.18f * Mathf.PerlinNoise(u * 22f, v * 22f) + (Mathf.PerlinNoise(u * 80f, v * 80f) - 0.5f) * 0.06f;
                c0 *= shade;
                c0 = Color.Lerp(new Color(0.3f, 0.22f, 0.15f), c0, 0.35f + 0.65f * border);   // ink line on the borders
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                c0 *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(edge / 0.08f));                      // burnt edges
                c0.a = 1f;
                px[(H - 1 - y) * W + x] = c0;
            }
        mapTex.SetPixels(px);
        mapTex.Apply();
    }

    void Dot(Vector2 p, float r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new UnityEngine.Rect(p.x - r, p.y - r, r * 2f, r * 2f), dotTex);
        GUI.color = Color.white;
    }

    // a label on a parchment tag, so it stays readable over roads and borders
    void Tag(Vector2 topCenter, string text, GUIStyle st, List<UnityEngine.Rect> taken)
    {
        Vector2 sz = st.CalcSize(new GUIContent(text));
        var r = new UnityEngine.Rect(topCenter.x - sz.x / 2f - 4f, topCenter.y, sz.x + 8f, sz.y);
        Rect(r.x, r.y, r.width, r.height, new Color(0.95f, 0.88f, 0.7f, 0.82f));
        GUI.Label(r, text, st);
        taken.Add(r);
    }

    static bool Overlaps(UnityEngine.Rect r, List<UnityEngine.Rect> taken)
    {
        foreach (var t in taken) if (r.Overlaps(t)) return true;
        return false;
    }

    // ---------- GUI ----------
    void StoryGui(float vw, GUIStyle big, GUIStyle mid)
    {
        Rect(0, 0, vw, 720, new Color(0.06f, 0.05f, 0.07f, 1f));
        if (storyPicking) { CampaignSelect(vw, big, mid); return; }

        Campaign cp = Story.Campaigns[storyC];
        int prog = Mathf.Min(StoryProgress(storyC), Story.Total);
        float mx = vw / 2f - 520f, my = 58f, mw = 1040f, mh = 585f;
        GUI.DrawTexture(new UnityEngine.Rect(mx, my, mw, mh), mapTex);
        System.Func<Vector2, Vector2> M = n => new Vector2(mx + n.x * mw, my + n.y * mh);

        // the road: dotted, walked part in gold
        Vector2 prev = M(cp.start);
        for (int i = 0; i < Story.Total; i++)
        {
            Vector2 p = M(Story.NodePos(storyC, i));
            float len = Vector2.Distance(prev, p);
            for (float d = 8f; d < len - 8f; d += 11f)
                Dot(Vector2.Lerp(prev, p, d / len), 2.6f, i < prog ? new Color(0.85f, 0.65f, 0.15f) : new Color(0.3f, 0.22f, 0.15f, 0.7f));
            prev = p;
        }
        Vector2 sp = M(cp.start);
        Dot(sp, 11f, new Color(0.15f, 0.1f, 0.08f));
        Dot(sp, 8f, Db.FactionColor(cp.faction));

        // everything that labels must not cover
        var taken = new List<UnityEngine.Rect>();
        taken.Add(new UnityEngine.Rect(sp.x - 12, sp.y - 12, 24, 24));
        for (int i = 0; i < Story.Total; i++)
        {
            Vector2 p = M(Story.NodePos(storyC, i));
            float r = Story.Node(storyC, i).boss ? 22f : 16f;
            taken.Add(new UnityEngine.Rect(p.x - r, p.y - r, r * 2f, r * 2f));
        }

        var tiny = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.UpperCenter, richText = true, wordWrap = false };
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        bool idle = travelT < 0f && !dialogOpen;
        for (int i = 0; i < Story.Total; i++)
        {
            StoryNode n = Story.Node(storyC, i);
            Vector2 p = M(Story.NodePos(storyC, i));
            bool done = i < prog, next = i == prog, locked = i > prog;
            float r = n.boss ? 17f : 11f;
            Dot(p, r + 3f, new Color(0.15f, 0.1f, 0.08f, 0.85f));
            Color col = done ? Db.FactionColor(n.enemy) : next ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.3f), pulse) : new Color(0.45f, 0.42f, 0.38f);
            Dot(p, r, col);
            if (next) Dot(p, r + 6f + pulse * 4f, new Color(1f, 0.9f, 0.4f, 0.35f));
            if (n.boss || next)
            {
                string t = (n.boss ? (locked ? "<color=#5a4a3a><b>БОСС</b></color>  " : "<color=#8a1010><b>БОСС</b></color>  ") : "")
                         + "<color=#2a1d12>" + (next || done ? n.title : "???") + "</color>";
                Tag(new Vector2(p.x, p.y + r + 4f), t, tiny, taken);
            }
            if (!locked && idle && GUI.Button(new UnityEngine.Rect(p.x - r - 5, p.y - r - 5, r * 2 + 10, r * 2 + 10), GUIContent.none, GUIStyle.none))
            {
                dialogIdx = i;
                markerFrom = MarkerNorm(markerAt);
                markerTo = Story.NodePos(storyC, i);
                travelT = 0f;
            }
        }

        // the hero: a pin standing above the node
        Vector2 mk = travelT >= 0f ? Vector2.Lerp(markerFrom, markerTo, Mathf.SmoothStep(0f, 1f, travelT)) : MarkerNorm(markerAt);
        Vector2 foot = M(mk);
        Vector2 mp = foot + new Vector2(0f, -34f - (travelT >= 0f ? Mathf.Abs(Mathf.Sin(travelT * 14f)) * 6f : 0f));
        Rect(mp.x - 1.5f, mp.y, 3f, foot.y - mp.y - 8f, new Color(0.1f, 0.08f, 0.05f));
        Dot(mp, 12f, new Color(0.1f, 0.08f, 0.05f));
        Dot(mp, 9f, new Color(1f, 0.85f, 0.25f));
        GUI.Label(new UnityEngine.Rect(mp.x - 30, mp.y - 7, 60, 16), "<b><color=#1a1208>ТЫ</color></b>", new GUIStyle(tiny) { fontSize = 10 });
        taken.Add(new UnityEngine.Rect(mp.x - 14, mp.y - 14, 28, foot.y - mp.y + 14));

        // region names go wherever there is room for them
        var regionSt = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true, wordWrap = false };
        float[] ox = { 0f, -0.1f, 0.1f, 0f, -0.14f, 0.14f, 0f, 0f };
        float[] oy = { 0.13f, 0.13f, 0.13f, -0.12f, 0f, 0f, 0.22f, -0.2f };
        for (int r = 0; r < 4; r++)
        {
            string txt = "<color=#3a2a1c>" + Story.RegionName[r] + "</color>";
            Vector2 sz = regionSt.CalcSize(new GUIContent(txt)) + new Vector2(8f, 0f);
            UnityEngine.Rect best = default(UnityEngine.Rect);
            bool found = false;
            for (int k = 0; k < ox.Length && !found; k++)
            {
                Vector2 c = M(Story.Region[r] + new Vector2(ox[k], r == 2 ? -oy[k] * 0.6f : oy[k]));
                var cand = new UnityEngine.Rect(c.x - sz.x / 2f, c.y - sz.y / 2f, sz.x, sz.y);
                if (cand.xMin < mx + 16 || cand.xMax > mx + mw - 16 || cand.yMin < my + 10 || cand.yMax > my + mh - 10) continue;
                if (k == 0) best = cand;
                if (!Overlaps(cand, taken)) { best = cand; found = true; }
            }
            if (best.width <= 0f) continue;
            GUI.Label(best, txt, regionSt);
            taken.Add(best);
        }

        // top bar
        int act = Mathf.Min(prog, Story.Total - 1) / Story.NodesPerAct;
        GUI.Label(new UnityEngine.Rect(mx, 10, mw - 340, 40), "<b><color=#" + Hex(cp.faction) + ">" + cp.title + "</color></b>   ·   Акт " + (act + 1) + ": " + cp.acts[act].title + "   ·   пройдено " + prog + "/" + Story.Total, new GUIStyle(mid) { alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = false, fontSize = 19 });
        if (GUI.Button(new UnityEngine.Rect(mx + mw - 330, 12, 160, 36), "Кампании")) OpenStory();
        if (GUI.Button(new UnityEngine.Rect(mx + mw - 160, 12, 160, 36), "В меню")) ShowMenu();
        GUI.Label(new UnityEngine.Rect(mx, 650, mw, 30), prog >= Story.Total
            ? "<color=#ffd040><b>КАМПАНИЯ ПРОЙДЕНА!</b></color>  Пройденные бои можно переиграть."
            : "Нажми на мигающую точку, чтобы идти дальше. Пройденные бои можно переиграть.", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontSize = 15 });

        if (dialogOpen) StoryDialog(vw, cp);
    }

    void StoryDialog(float vw, Campaign cp)
    {
        StoryNode n = Story.Node(storyC, dialogIdx);
        int a = dialogIdx / Story.NodesPerAct;
        float x = vw / 2f - 500f, y = 372f, w = 1000f, h = 310f;
        string head = "Акт " + (a + 1) + " · " + cp.acts[a].title + "  —  <color=#ffd070>" + n.title + "</color>";
        string sub = "Противник: <b><color=#" + Hex(n.enemy) + ">" + n.enemyName + "</color></b>  ·  ур. " + n.level
            + (n.boss ? "  ·  <color=#ff5050><b>БОСС — бой до 3 побед</b></color>" : "  ·  бой до 2 побед") + "  ·  арена: " + Arena.Names[n.arena];
        DrawDialog(x, y, w, h, head, sub, n);
        if (!DialogDone)
        {
            if (GUI.Button(new UnityEngine.Rect(x + w - 190, y + h - 56, 170, 42), "Пропустить")) { dlgI = dlg.Count - 1; dlgStart = -999f; }
        }
        else
        {
            if (GUI.Button(new UnityEngine.Rect(x + w - 250, y + h - 60, 230, 46), "<b>В БОЙ!</b>")) StartStoryFight(dialogIdx);
            if (GUI.Button(new UnityEngine.Rect(x + w - 420, y + h - 60, 150, 46), "Назад")) dialogOpen = false;
        }
    }

    // the dialog box: portraits on both sides, the speaker's line typing out in the middle
    void DrawDialog(float x, float y, float w, float h, string head, string sub, StoryNode n)
    {
        Rect(x - 4, y - 4, w + 8, h + 8, new Color(0.55f, 0.42f, 0.2f, 0.95f));
        Rect(x, y, w, h, new Color(0.08f, 0.06f, 0.05f, 0.97f));
        var headSt = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true, fontStyle = FontStyle.Bold, wordWrap = false };
        var subSt = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = false };
        GUI.Label(new UnityEngine.Rect(x + 20, y + 10, w - 40, 28), head, headSt);
        GUI.Label(new UnityEngine.Rect(x + 20, y + 38, w - 40, 22), sub, subSt);
        Rect(x + 20, y + 64, w - 40, 2, new Color(0.55f, 0.42f, 0.2f, 0.6f));
        if (dlg.Count == 0) return;
        DLine cur = dlg[dlgI];

        // left: whoever of your side spoke last (you by default); right: the opponent
        DLine left = null;
        for (int i = dlgI; i >= 0 && left == null; i--) if (dlg[i].side == 1) left = dlg[i];
        if (left == null) left = new DLine { who = "Ты", col = Db.FactionColor(Story.Campaigns[storyC].faction), fac = Story.Campaigns[storyC].faction, side = 1, hero = true, female = female };
        var right = new DLine { who = n.enemyName, col = Db.FactionColor(n.enemy), fac = n.enemy, side = 2, female = n.female, boss = n.boss };
        const float ps = 120f;
        Portrait(x + 20, y + 80, ps, left, cur.side == 1);
        Portrait(x + w - 20 - ps, y + 80, ps, right, cur.side == 2);

        float tx = x + 40 + ps, tw = w - 80 - ps * 2;
        int shown = Mathf.Clamp((int)((Time.unscaledTime - dlgStart) * TypeSpeed), 0, cur.text.Length);
        string text = cur.text.Substring(0, shown);
        if (cur.side == 0)
        {
            var narr = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
            narr.normal.textColor = new Color(0.88f, 0.82f, 0.68f);
            GUI.Label(new UnityEngine.Rect(tx, y + 80, tw, 150), text, narr);
        }
        else
        {
            var nameSt = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = cur.side == 2 ? TextAnchor.UpperRight : TextAnchor.UpperLeft };
            nameSt.normal.textColor = Color.Lerp(cur.col, Color.white, 0.25f);
            GUI.Label(new UnityEngine.Rect(tx, y + 80, tw, 28), cur.who, nameSt);
            var body = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, alignment = cur.side == 2 ? TextAnchor.UpperRight : TextAnchor.UpperLeft };
            GUI.Label(new UnityEngine.Rect(tx, y + 110, tw, 125), "«" + text + (shown >= cur.text.Length ? "»" : ""), body);
        }

        var hint = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
        GUI.Label(new UnityEngine.Rect(x + 24, y + h - 44, 420, 22), "<color=#a08a60>" + (dlgI + 1) + " / " + dlg.Count + "</color>   " + (DialogDone ? "" : "<color=#806e50>пробел или клик — дальше</color>"), hint);

        // advance with a click on the text or with Space / Enter
        if (!DialogDone && GUI.Button(new UnityEngine.Rect(tx, y + 76, tw, 160), GUIContent.none, GUIStyle.none)) DialogAdvance();
        Event e = Event.current;
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
        {
            DialogAdvance();
            e.Use();
        }
    }

    static readonly Color[] SkinTones = { new Color(0.97f, 0.82f, 0.68f), new Color(0.88f, 0.69f, 0.53f), new Color(0.72f, 0.51f, 0.37f), new Color(0.5f, 0.35f, 0.24f) };
    static readonly Color[] HairTones = { new Color(0.1f, 0.08f, 0.07f), new Color(0.36f, 0.2f, 0.1f), new Color(0.86f, 0.7f, 0.36f), new Color(0.62f, 0.22f, 0.08f), new Color(0.86f, 0.86f, 0.86f) };

    static int NameHash(string s)
    {
        int h = 17;
        foreach (char c in s) h = h * 31 + c;
        return h & 0x7fffffff;
    }

    // a painted bust: every character gets its own face from its name, and the headgear of its faction
    void Portrait(float x, float y, float s, DLine d, bool active)
    {
        int h = NameHash(d.who);
        Color fc = d.col, dark = Color.Lerp(fc, Color.black, 0.5f), light = Color.Lerp(fc, Color.white, 0.35f);
        Color skinC = d.hero ? SkinTones[0] : SkinTones[h % 4];
        Color hairC = d.hero ? Hairs[hairIdx] : HairTones[(h / 4) % 5];
        int v = (h / 7) % 3;
        Color[] helmTones = { new Color(0.72f, 0.74f, 0.8f), new Color(0.8f, 0.55f, 0.28f), new Color(0.3f, 0.3f, 0.36f), new Color(0.9f, 0.75f, 0.3f) };
        Color helm = helmTones[(h / 13) % 4];
        bool beard = !d.female && !d.hero && (h / 60) % 3 == 0, scar = !d.hero && (h / 180) % 4 == 0;
        Color gold = new Color(1f, 0.82f, 0.3f);

        Rect(x - 3, y - 3, s + 6, s + 6, d.hero ? gold : fc);
        Rect(x, y, s, s, Color.Lerp(new Color(0.1f, 0.09f, 0.1f), fc, 0.18f));
        GUI.BeginGroup(new UnityEngine.Rect(x, y, s, s));
        float cx = s / 2f, hy = s * 0.47f, hr = s * 0.18f;
        System.Action<float, float, float, float, Color> R = (rx, ry, rw, rh, c) => Rect(cx + rx * s, ry * s, rw * s, rh * s, c);
        System.Action<float, float, float, Color> O = (ox, oy, r, c) => Dot(new Vector2(cx + ox * s, oy * s), r * s, c);

        if (d.boss) O(0f, 0.47f, 0.4f, new Color(0.9f, 0.1f, 0.05f, 0.35f));                 // a red glow behind every boss
        if (d.fac == Faction.Heralds) { O(0f, 0.36f, 0.27f, new Color(1f, 0.9f, 0.45f, 0.9f)); O(0f, 0.36f, 0.22f, Color.Lerp(new Color(0.1f, 0.09f, 0.1f), fc, 0.18f)); }   // halo
        if (d.female && d.fac != Faction.ShadowGuild) R(-0.2f, 0.36f, 0.4f, 0.36f, hairC);     // long hair behind the head

        // shoulders and armour
        O(0f, 1.1f, 0.47f, dark);
        O(0f, 1.04f, 0.31f, fc);
        if (d.fac == Faction.Legion) { O(-0.3f, 0.86f, 0.13f, light); O(0.3f, 0.86f, 0.13f, light); }       // pauldrons
        if (d.fac == Faction.Dynasty) R(-0.04f, 0.74f, 0.08f, 0.3f, gold);                                    // robe trim
        if (d.fac == Faction.Heralds) R(-0.2f, 0.8f, 0.4f, 0.05f, gold);
        R(-0.06f, 0.6f, 0.12f, 0.12f, skinC);                                                                  // neck

        // head
        O(0f, 0.47f, 0.18f, skinC);
        switch (d.fac)
        {
            case Faction.Legion:
                O(0f, 0.38f, 0.19f, helm);                                            // helmet dome
                R(-0.19f, 0.36f, 0.06f, 0.2f, helm); R(0.13f, 0.36f, 0.06f, 0.2f, helm);   // cheek guards
                if (v == 0) R(-0.03f, 0.12f, 0.06f, 0.16f, (h / 5) % 2 == 0 ? new Color(0.85f, 0.12f, 0.1f) : new Color(0.15f, 0.15f, 0.2f));   // crest
                else if (v == 1) R(-0.16f, 0.2f, 0.32f, 0.05f, new Color(0.85f, 0.12f, 0.1f));   // side plume
                else R(-0.2f, 0.34f, 0.4f, 0.04f, gold);                              // gilded brow
                break;
            case Faction.Dynasty:
                if (v == 0) { O(0f, 0.36f, 0.17f, hairC); O(0f, 0.2f, 0.07f, hairC); R(-0.03f, 0.25f, 0.06f, 0.03f, gold); }   // topknot
                else if (v == 1) { O(0f, 0.36f, 0.17f, hairC); R(-0.34f, 0.27f, 0.68f, 0.05f, new Color(0.75f, 0.62f, 0.35f)); O(0f, 0.25f, 0.13f, new Color(0.75f, 0.62f, 0.35f)); }   // straw hat
                else { O(0f, 0.36f, 0.17f, hairC); R(-0.18f, 0.34f, 0.36f, 0.05f, new Color(0.85f, 0.15f, 0.1f)); }   // headband
                break;
            case Faction.Heralds:
                if (v == 0) { O(0f, 0.37f, 0.18f, Color.Lerp(helm, Color.white, 0.6f)); R(-0.28f, 0.3f, 0.1f, 0.05f, Color.white); R(0.18f, 0.3f, 0.1f, 0.05f, Color.white); }   // winged helm
                else O(0f, 0.37f, 0.17f, hairC);
                R(-0.03f, 0.3f, 0.06f, 0.06f, gold);                                  // sun mark
                break;
            default:
                O(0f, 0.42f, 0.23f, v == 1 ? new Color(0.12f, 0.1f, 0.14f) : dark);   // hood
                O(0f, 0.5f, 0.16f, skinC);
                R(-0.17f, 0.52f, 0.34f, 0.14f, new Color(0.08f, 0.06f, 0.1f));         // mask over the mouth
                break;
        }
        if (d.hero) R(-0.18f, 0.28f, 0.36f, 0.05f, gold);                            // the hero's golden band

        // face
        Color eye = d.boss ? new Color(1f, 0.15f, 0.1f) : d.fac == Faction.ShadowGuild ? new Color(0.8f, 0.4f, 1f) : new Color(0.1f, 0.08f, 0.06f);
        float er = d.boss ? 0.035f : 0.024f;
        O(-0.07f, 0.47f, er, eye); O(0.07f, 0.47f, er, eye);
        if (d.fac != Faction.ShadowGuild)
        {
            R(-0.11f, 0.42f, 0.08f, 0.015f, hairC); R(0.03f, 0.42f, 0.08f, 0.015f, hairC);   // brows
            if (beard) { O(0f, 0.6f, 0.1f, hairC); R(-0.08f, 0.53f, 0.16f, 0.06f, hairC); }
            else R(-0.04f, 0.57f, 0.08f, 0.012f, new Color(0.45f, 0.22f, 0.18f));      // mouth
            if (d.female) { O(-0.11f, 0.53f, 0.022f, new Color(1f, 0.55f, 0.55f, 0.6f)); O(0.11f, 0.53f, 0.022f, new Color(1f, 0.55f, 0.55f, 0.6f)); }
        }
        if (scar) R(0.05f, 0.42f, 0.015f, 0.14f, new Color(0.6f, 0.2f, 0.2f));
        if (d.boss) { O(-0.15f, 0.22f, 0.05f, new Color(0.25f, 0.05f, 0.05f)); O(0.15f, 0.22f, 0.05f, new Color(0.25f, 0.05f, 0.05f)); O(-0.19f, 0.16f, 0.035f, new Color(0.25f, 0.05f, 0.05f)); O(0.19f, 0.16f, 0.035f, new Color(0.25f, 0.05f, 0.05f)); }   // horns
        GUI.EndGroup();
        if (!active) Rect(x, y, s, s, new Color(0f, 0f, 0f, 0.55f));
        var st = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.UpperCenter, wordWrap = true };
        GUI.Label(new UnityEngine.Rect(x - 10, y + s + 6, s + 20, 36), d.who, st);
    }

    void CampaignSelect(float vw, GUIStyle big, GUIStyle mid)
    {
        GUI.Label(new UnityEngine.Rect(0, 24, vw, 70), "СЮЖЕТ", big);
        GUI.Label(new UnityEngine.Rect(0, 90, vw, 30), "Выбери, за какую фракцию пройти историю. У каждой своя дорога, 4 акта и 4 босса.", mid);
        int nc = Story.Campaigns.Length;
        float gap = 20f, cw = Mathf.Min(320f, (vw - 60f - gap * (nc - 1)) / nc), x0 = vw / 2f - (cw * nc + gap * (nc - 1)) / 2f;
        var body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };
        var title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        for (int c = 0; c < nc; c++)
        {
            Campaign cp = Story.Campaigns[c];
            float x = x0 + c * (cw + gap), y = 140f;
            Color fc = Db.FactionColor(cp.faction);
            Rect(x - 3, y - 3, cw + 6, 466, fc);
            Rect(x, y, cw, 460, new Color(0.09f, 0.07f, 0.08f, 0.97f));
            GUI.Label(new UnityEngine.Rect(x, y + 14, cw, 30), "<color=#" + Hex(cp.faction) + ">" + OppNames[(int)cp.faction + 1].ToUpper() + "</color>", new GUIStyle(mid) { richText = true });
            GUI.Label(new UnityEngine.Rect(x, y + 46, cw, 36), cp.title, title);
            GUI.Label(new UnityEngine.Rect(x + 16, y + 92, cw - 32, 130), cp.blurb, body);
            GUI.Label(new UnityEngine.Rect(x + 16, y + 216, cw - 32, 22), "<color=#a09070>Спутник: " + cp.ally + "</color>", body);
            int prog = Mathf.Min(StoryProgress(c), Story.Total);
            string acts = "";
            for (int a = 0; a < Story.Acts; a++)
                acts += (prog >= (a + 1) * Story.NodesPerAct ? "<color=#ffd040>[+]</color> " : prog >= a * Story.NodesPerAct ? "<color=#ffffff>[>]</color> " : "<color=#666666>[ ]</color> ") + (a + 1) + ". " + cp.acts[a].title + "\n";
            GUI.Label(new UnityEngine.Rect(x + 16, y + 250, cw - 32, 110), acts, body);
            Rect(x + 16, y + 368, cw - 32, 8, new Color(0.2f, 0.2f, 0.2f));
            Rect(x + 16, y + 368, (cw - 32) * prog / Story.Total, 8, fc);
            if (GUI.Button(new UnityEngine.Rect(x + 30, y + 392, cw - 60, 50), prog == 0 ? "<b>Начать</b>" : prog >= Story.Total ? "Пройдено" : "<b>Продолжить</b>")) OpenCampaign(c);
        }
        if (GUI.Button(new UnityEngine.Rect(vw / 2f - 110, 632, 220, 46), "В меню")) ShowMenu();
    }

    void StoryResultGui(float vw, GUIStyle big, GUIStyle mid)
    {
        EnsureMapTextures();
        Campaign cp = Story.Campaigns[storyC];
        int a = storyIdx / Story.NodesPerAct;
        Rect(0, 0, vw, 720, new Color(0f, 0f, 0f, 0.6f));
        string title = !won ? "<color=#ff6060>ПОРАЖЕНИЕ</color>"
                     : storyNode.boss ? (a == Story.Acts - 1 ? "<color=#ffd040>КАМПАНИЯ ПРОЙДЕНА</color>" : "<color=#ffd040>АКТ " + (a + 1) + " ПРОЙДЕН</color>")
                     : "<color=#60ff80>ПОБЕДА</color>";
        GUI.Label(new UnityEngine.Rect(0, 40, vw, 90), title, big);
        GUI.Label(new UnityEngine.Rect(0, 124, vw, 34), "Раунды " + pWins + " : " + eWins + "   ·   +" + lastReward.ToString("N0") + " монет", mid);
        float x = vw / 2f - 500f, y = 180f, w = 1000f, h = 310f;
        DrawDialog(x, y, w, h, "<color=#ffd070>" + storyNode.title + "</color>", "Акт " + (a + 1) + " · " + cp.acts[a].title, storyNode);
        if (!DialogDone)
        {
            if (GUI.Button(new UnityEngine.Rect(x + w - 190, y + h - 56, 170, 42), "Пропустить")) { dlgI = dlg.Count - 1; dlgStart = -999f; }
            return;
        }
        if (won)
        {
            if (GUI.Button(new UnityEngine.Rect(vw / 2f - 150, 520, 300, 60), "<b>На карту</b>")) BackToMap();
        }
        else
        {
            if (GUI.Button(new UnityEngine.Rect(vw / 2f - 310, 520, 300, 60), "<b>Повторить</b>")) StartFight();
            if (GUI.Button(new UnityEngine.Rect(vw / 2f + 10, 520, 300, 60), "На карту")) BackToMap();
        }
    }
}
