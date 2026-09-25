using System.Collections.Generic;
using UnityEngine;

public partial class GameMain : MonoBehaviour
{
    enum State { Select, Menu, Shop, Fight, Result, StoryMap }

    const long StartCoins = 999999999L;

    static readonly Color[] Hairs =
    {
        new Color(0.15f, 0.1f, 0.08f), new Color(0.85f, 0.7f, 0.3f),
        new Color(0.7f, 0.15f, 0.1f), new Color(0.86f, 0.86f, 0.9f),
    };

    State state = State.Select;
    Camera cam;
    Fighter player, enemy, prevM, prevF;
    Texture2D white;
    long coins, lastReward;
    HashSet<string> owned = new HashSet<string>();
    string eqW, eqH, eqA;
    bool female, won;
    int oppFaction = -1;                 // -1 = random
    const int MaxLevel = 30;
    static readonly string[] OppNames = { "Случайный", "Легион", "Династия", "Вестники", "Гильдия Теней" };
    int hairIdx, level = 1;
    int eqSkin, arenaSel = -1, curArena = -1;
    Light sun, fill;
    GameObject arenaRoot;
    float slowMo, koZoom;
    bool skinTab;
    float countdown, roundTime, koTimer;
    enum RState { Intro, Fight, KO }
    RState rs;
    int round = 1, pWins, eWins;
    string koText = "K.O.";
    bool lastRoundPlayer;
    Slot tab = Slot.Weapon;
    int factionFilter = -1;
    Vector2 scroll;
    Faction enemyFaction;

    bool bareShot;
    float resultShotT = -1f;
    bool resultShot;
    bool demo, demoFemale, demoNatural, menuShot, menuShotDone, demoFastKo, skinShot;
    float demoLog;
    float demoT;
    int demoShots;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<GameMain>() == null) new GameObject("Game").AddComponent<GameMain>();
    }

    // ---------- save ----------
    void Load()
    {
        coins = long.Parse(PlayerPrefs.GetString("coins", StartCoins.ToString()));
        level = Mathf.Clamp(PlayerPrefs.GetInt("level", 1), 1, MaxLevel);
        oppFaction = Mathf.Clamp(PlayerPrefs.GetInt("oppF", -1), -1, 3);
        foreach (string s in PlayerPrefs.GetString("owned", "W_0_0_1,H_0_0,A_0_0").Split(','))
            if (s.Length > 0) owned.Add(s);
        female = PlayerPrefs.GetInt("female", 0) == 1;
        hairIdx = PlayerPrefs.GetInt("hair", 0);
        eqW = PlayerPrefs.GetString("eqW", "W_0_0_1");
        eqH = PlayerPrefs.GetString("eqH", "H_0_0");
        eqA = PlayerPrefs.GetString("eqA", "A_0_0");
        // saves from older versions may reference gear that no longer exists
        if (eqW.StartsWith("FIST_")) Db.Fists((Faction)Mathf.Clamp(eqW[eqW.Length - 1] - '0', 0, 3));   // bare hands are saved as FIST_<faction>
        if (Db.Get(eqW) == null || Db.Get(eqW).slot != Slot.Weapon) eqW = "W_0_0_1";
        if (eqH != "" && (Db.Get(eqH) == null || Db.Get(eqH).slot != Slot.Helmet)) eqH = "H_0_0";
        if (eqA != "" && (Db.Get(eqA) == null || Db.Get(eqA).slot != Slot.Armor)) eqA = "A_0_0";
        owned.RemoveWhere(id => Db.Get(id) == null);
        owned.Add("W_0_0_1"); owned.Add("H_0_0"); owned.Add("A_0_0");
        owned.Add(eqW); owned.Add(eqH); owned.Add(eqA);
        eqSkin = Mathf.Clamp(PlayerPrefs.GetInt("skin", 0), 0, Db.Skins.Length - 1);
        arenaSel = Mathf.Clamp(PlayerPrefs.GetInt("arena", -1), -1, Arena.Names.Length - 1);
        owned.Add("S0");
        if (!owned.Contains(Db.Skins[eqSkin].id)) eqSkin = 0;
        state = PlayerPrefs.GetInt("chosen", 0) == 1 ? State.Menu : State.Select;
    }

    void Save()
    {
        if (demo || bareShot) return;   // test runs must never touch the real save
        PlayerPrefs.SetString("coins", coins.ToString());
        PlayerPrefs.SetInt("level", level);
        PlayerPrefs.SetInt("oppF", oppFaction);
        PlayerPrefs.SetString("owned", string.Join(",", new List<string>(owned).ToArray()));
        PlayerPrefs.SetInt("female", female ? 1 : 0);
        PlayerPrefs.SetInt("hair", hairIdx);
        PlayerPrefs.SetString("eqW", eqW);
        PlayerPrefs.SetString("eqH", eqH);
        PlayerPrefs.SetString("eqA", eqA);
        PlayerPrefs.SetInt("skin", eqSkin);
        PlayerPrefs.SetInt("arena", arenaSel);
        PlayerPrefs.SetInt("chosen", 1);
        PlayerPrefs.Save();
    }

    // ---------- setup ----------
    void Awake()
    {
        Load();
        white = new Texture2D(1, 1);
        white.SetPixel(0, 0, Color.white);
        white.Apply();

        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.orthographic = false;
        cam.fieldOfView = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);

        // lights are shared by every arena; each map sets their colour and angle
        sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        fill = new GameObject("Fill").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.shadows = LightShadows.None;

        QualitySettings.antiAliasing = 4;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance = 45f;
        QualitySettings.shadowCascades = 2;
        QualitySettings.pixelLightCount = 8;
        cam.allowMSAA = true;

        GameAudio.Boot(gameObject);
        string[] args = System.Environment.GetCommandLineArgs();
        demo = System.Array.IndexOf(args, "-demo") >= 0;
        demoFemale = System.Array.IndexOf(args, "-female") >= 0;
        demoNatural = System.Array.IndexOf(args, "-natural") >= 0;
        menuShot = System.Array.IndexOf(args, "-menushot") >= 0;
        if (System.Array.IndexOf(args, "-bare") >= 0) { bareShot = true; eqW = Db.Fists(Faction.Legion).id; eqH = ""; eqA = ""; }   // check: hero with everything taken off
        demoFastKo = System.Array.IndexOf(args, "-fastko") >= 0;
        skinShot = System.Array.IndexOf(args, "-skinshot") >= 0;
        foreach (string a in args)
            if (a.StartsWith("-storymap="))
            {
                // show a campaign map (optionally with fake progress and the next dialog open), then screenshot
                demo = true;
                string[] cv = a.Substring(10).Split(',');
                demoStoryProg = cv.Length > 1 ? int.Parse(cv[1]) : 0;
                OpenStory();
                if (int.Parse(cv[0]) >= 0) OpenCampaign(int.Parse(cv[0]));
                if (cv.Length > 2) { OpenDialog(demoStoryProg); dlgI = Mathf.Min(int.Parse(cv[2]), dlg.Count - 1); dlgStart = -999f; }
                menuShot = true;
                return;
            }
        if (skinShot)
        {
            // every skin side by side without armour, for checking how they look
            demo = true;
            LoadArena(2);
            state = State.Menu;
            for (int i = 0; i < Db.Skins.Length; i++)
            {
                Fighter f = MakeFighter("Skin" + i, false, -7f + i * 2f, Db.Get("W_2_5_1"), null, null, i % 2 == 1, Hairs[i % Hairs.Length], i);
                f.yawOffset = 60f;
            }
            AimCam(new Vector3(0f, 2.2f, 0f), 3f, -13f);
            menuShot = true;
            return;
        }
        if (demo)
        {
            Fighter.DebugLog = true;
            // AI vs AI in the best gear, screenshots on a timer: used to eyeball the animations without a human
            female = demoFemale;
            eqW = "W_0_6_3"; eqH = "H_0_3"; eqA = "A_0_3";
            foreach (string a in args)
            {
                if (a.StartsWith("-weapon=")) eqW = a.Substring(8);
                if (a.StartsWith("-arena=")) arenaSel = int.Parse(a.Substring(7));
                if (a.StartsWith("-skin=")) eqSkin = int.Parse(a.Substring(6));
                if (a.StartsWith("-storynode="))
                {
                    string[] cv = a.Substring(11).Split(',');
                    storyC = int.Parse(cv[0]);
                    storyIdx = int.Parse(cv[1]);
                    storyNode = Story.Node(storyC, storyIdx);
                }
            }
            StartFight();
            player.autoPlay = true;
            if (!demoNatural) { player.energy = 100f; player.dmgMul = 5f; }
            if (demoFastKo) player.dmgMul = 8f;
            level = 3;
            return;
        }
        LoadArena(arenaSel < 0 ? 0 : arenaSel);
        if (state == State.Select) ShowSelect(); else ShowMenu();
    }

    void LoadArena(int idx)
    {
        if (curArena == idx && arenaRoot != null) return;
        if (arenaRoot != null) Destroy(arenaRoot);
        curArena = idx;
        arenaRoot = Arena.Build(idx, sun, fill, cam);
    }

    Fighter MakeFighter(string name, bool isPlayer, float x, Item w, Item h, Item a, bool fem, Color hairCol, int skinIdx)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(x, 0, 0);
        var f = go.AddComponent<Fighter>();
        f.isPlayer = isPlayer;
        f.weapon = w; f.helmet = h; f.armor = a; f.female = fem; f.hair = hairCol; f.skinId = skinIdx;
        f.Build();
        return f;
    }

    void ClearAll()
    {
        foreach (Fighter f in new[] { player, enemy, prevM, prevF }) if (f != null) Destroy(f.gameObject);
        player = enemy = prevM = prevF = null;
    }

    void AimCam(Vector3 at, float height, float back)
    {
        cam.transform.position = new Vector3(at.x, height, back);
        cam.transform.rotation = Quaternion.LookRotation(at - cam.transform.position);
        cam.fieldOfView = 50f;
    }

    // ---------- hero select ----------
    void ShowSelect()
    {
        GameAudio.Music(false);
        ClearAll();
        state = State.Select;
        Item w = Db.Get(eqW), h = Db.Get(eqH), a = Db.Get(eqA);
        prevM = MakeFighter("PreviewM", false, -1.9f, w, h, a, false, Hairs[hairIdx], eqSkin);
        prevF = MakeFighter("PreviewF", false, 1.9f, w, h, a, true, Hairs[hairIdx], eqSkin);
        AimCam(new Vector3(0, 2f, 0), 2.9f, -8.5f);
    }

    void ShowMenu()
    {
        storyNode = null;
        GameAudio.Music(false);
        ClearAll();
        state = State.Menu;
        RebuildPreview();
    }

    void RebuildPreview()
    {
        if (player != null) Destroy(player.gameObject);
        player = MakeFighter("Player", true, -2.2f, Db.Get(eqW), Db.Get(eqH), Db.Get(eqA), female, Hairs[hairIdx], eqSkin);
        player.yawOffset = -25f;
        AimCam(new Vector3(-2.2f, 2f, 0), 2.9f, -7.5f);
    }

    // ---------- fight ----------
    void StartFight()
    {
        ClearAll();
        LoadArena(storyNode != null ? storyNode.arena : arenaSel < 0 ? Random.Range(0, Arena.Names.Length) : arenaSel);
        GameAudio.MusicArena(curArena);
        state = State.Fight;
        player = MakeFighter("Player", true, -3f, Db.Get(eqW), Db.Get(eqH), Db.Get(eqA), female, Hairs[hairIdx], eqSkin);
        player.maxHp = player.hp = 300f;

        bool isBoss = storyNode != null && storyNode.boss;
        enemyFaction = storyNode != null ? storyNode.enemy : oppFaction < 0 ? (Faction)Random.Range(0, 4) : (Faction)oppFaction;
        enemyLevel = storyNode != null ? storyNode.level : level;
        enemyName = storyNode != null ? storyNode.enemyName : "";
        winsNeeded = isBoss ? 3 : 2;
        int maxTier = isBoss ? 3 : Mathf.Min(3, 1 + enemyLevel / 2);
        bool enemyFemale = storyNode != null ? storyNode.female : Random.value < 0.5f;
        int enemySkin = storyNode != null && storyNode.skin >= 0 ? storyNode.skin : storyNode != null ? Random.Range(0, 4) : Random.Range(0, Db.Skins.Length);
        enemy = MakeFighter("Enemy", false, 3f, Pick(Slot.Weapon, maxTier), Pick(Slot.Helmet, maxTier), Pick(Slot.Armor, maxTier),
                            enemyFemale, Hairs[Random.Range(0, Hairs.Length)], enemySkin);
        enemy.maxHp = enemy.hp = 150f + 25f * enemyLevel;
        enemy.dmgMul = Mathf.Min(3f, 0.8f + 0.08f * enemyLevel);
        enemy.aiLevel = Mathf.Min(enemyLevel, 10);
        if (isBoss)
        {
            // bosses: bigger, glowing, tougher and at full cunning
            enemy.boss = true;
            enemy.Build();
            enemy.maxHp = enemy.hp = (150f + 25f * enemyLevel) * 1.6f;
            enemy.dmgMul = Mathf.Min(3.2f, 0.9f + 0.09f * enemyLevel);
            enemy.aiLevel = 10;
            enemy.bossPowers = storyNode.powers;
        }
        player.target = enemy;
        enemy.target = player;
        round = 1; pWins = eWins = 0;
        BeginRound();
    }

    void BeginRound()
    {
        Hazard.ClearAll();
        Fighter.Zoom = 0f;
        player.ResetForRound(-3.5f);
        enemy.ResetForRound(3.5f);
        if (demo) Debug.Log("BEGIN round " + round + " energy player=" + player.energy.ToString("0.0") + " enemy=" + enemy.energy.ToString("0.0"));
        rs = RState.Intro;
        countdown = 2.2f;
        roundTime = 60f;
    }

    Item Pick(Slot slot, int maxTier)
    {
        var list = new List<Item>();
        foreach (var it in Db.All) if (it.slot == slot && it.faction == enemyFaction && it.tier <= maxTier) list.Add(it);
        return list[Random.Range(0, list.Count)];
    }

    void Update()
    {
        if (Fighter.HitStop > 0f) { Fighter.HitStop -= Time.unscaledDeltaTime; Time.timeScale = 0.05f; }
        else if (slowMo > 0f) { slowMo -= Time.unscaledDeltaTime; Time.timeScale = 0.3f; }   // K.O. in slow motion
        else Time.timeScale = 1f;
        float dt = Time.deltaTime;
        if (demo) DemoTick();
        if (menuShot && !menuShotDone && Time.realtimeSinceStartup > 6f)
        {
            // the game renders and saves its own frame, so nothing else on the desktop is captured
            menuShotDone = true;
            ScreenCapture.CaptureScreenshot((System.Environment.GetEnvironmentVariable("SA_SHOTS") ?? ".") + "/menu.png");
        }
        if (menuShot && Time.realtimeSinceStartup > 9f) Application.Quit();
        if (state == State.Select)
        {
            if (prevM != null) prevM.yawOffset += dt * 22f;
            if (prevF != null) prevF.yawOffset -= dt * 22f;
        }
        else if (state == State.Menu && player != null)
        {
            if (Input.GetMouseButton(0)) player.yawOffset -= Input.GetAxis("Mouse X") * 6f;
            else player.yawOffset += dt * 12f;
        }
        else if (state == State.Fight)
        {
            if (rs == RState.Intro)
            {
                countdown -= dt;
                if (countdown <= 0f) { player.controlsEnabled = enemy.controlsEnabled = true; rs = RState.Fight; }
            }
            else if (rs == RState.Fight)
            {
                roundTime -= dt;
                bool timeUp = roundTime <= 0f;
                if (enemy.Dead || player.Dead || timeUp)
                {
                    player.controlsEnabled = enemy.controlsEnabled = false;
                    bool playerTakes = enemy.Dead != player.Dead ? enemy.Dead : player.hp / player.maxHp >= enemy.hp / enemy.maxHp;
                    if (playerTakes) pWins++; else eWins++;
                    lastRoundPlayer = playerTakes;
                    if (demo) Debug.Log("KO round " + round + " energy player=" + player.energy.ToString("0.0") + " enemy=" + enemy.energy.ToString("0.0"));
                    koText = (enemy.Dead || player.Dead) ? "K.O." : "ВРЕМЯ";
                    rs = RState.KO;
                    koTimer = 2.8f;
                    slowMo = (enemy.Dead || player.Dead) ? 1.3f : 0f;
                    (playerTakes ? player : enemy).victory = true;
                }
            }
            else
            {
                koTimer -= dt;
                if (koTimer <= 0f)
                {
                    if (pWins >= winsNeeded || eWins >= winsNeeded)
                    {
                        won = pWins >= winsNeeded;
                        if (storyNode != null) lastReward = StoryFinish(won);
                        else
                        {
                            lastReward = won ? 50000L * level : 5000L;
                            if (won) level = Mathf.Min(MaxLevel, level + 1);
                        }
                        coins += lastReward;
                        Save();
                        state = State.Result;
                    }
                    else { round++; BeginRound(); }
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { if (storyNode != null) BackToMap(); else ShowMenu(); }
        }
        else if (state == State.StoryMap) StoryUpdate(Time.unscaledDeltaTime);
    }

    void DemoTick()
    {
        demoT += Time.unscaledDeltaTime;
        if (demoNatural && rs == RState.Fight && demoT > demoLog) { demoLog = demoT + 3f; Debug.Log("ROUND " + round + " " + rs + " ENERGY t=" + demoT.ToString("0.0") + " player=" + player.energy.ToString("0.0") + " enemy=" + enemy.energy.ToString("0.0")); }
        if (!demoNatural && player != null && rs == RState.Fight && player.energy < 100f && Mathf.FloorToInt(demoT) % 4 == 0) player.energy = 100f;
        if (demoT > 3f + demoShots * 0.6f && demoShots < 60)
        {
            string dir = System.Environment.GetEnvironmentVariable("SA_SHOTS") ?? ".";
            ScreenCapture.CaptureScreenshot(dir + "/demo_" + (demoFemale ? "f" : "m") + curArena + "_" + demoShots + ".png");
            demoShots++;
        }
        if (demoT > 44f) Application.Quit();
        if (state == State.Result && storyNode != null)
        {
            // story demo: capture the after-fight dialog and stop
            if (resultShotT < 0f) resultShotT = demoT;
            dlgI = Mathf.Min(1, dlg.Count - 1); dlgStart = -999f;
            if (demoT - resultShotT > 1f && !resultShot)
            {
                resultShot = true;
                ScreenCapture.CaptureScreenshot((System.Environment.GetEnvironmentVariable("SA_SHOTS") ?? ".") + "/story_result.png");
            }
            if (demoT - resultShotT > 2f) Application.Quit();
            return;
        }
        if (state == State.Result) StartFight();
    }

    void LateUpdate()
    {
        if (state != State.Fight && state != State.Result) return;
        if (player == null || enemy == null) return;
        float mid = (player.transform.position.x + enemy.transform.position.x) * 0.5f;
        float sep = Mathf.Abs(player.transform.position.x - enemy.transform.position.x);
        koZoom = Mathf.MoveTowards(koZoom, rs == RState.KO && state == State.Fight ? 1f : 0f, Time.unscaledDeltaTime * 1.5f);
        Vector3 pos = new Vector3(mid * 0.6f, 3f - koZoom * 0.6f, -9f - sep * 0.4f + koZoom * 2.8f);
        cam.fieldOfView = Mathf.Lerp(50f, 40f, Fighter.Zoom);
        Fighter.Zoom = Mathf.MoveTowards(Fighter.Zoom, 0f, Time.unscaledDeltaTime * 0.9f);
        cam.transform.position = Vector3.Lerp(cam.transform.position, pos, Time.deltaTime * 4f);
        Quaternion rot = Quaternion.LookRotation(new Vector3(mid * 0.6f, 2.1f, 0f) - cam.transform.position);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, Time.deltaTime * 4f);
        if (Fighter.Shake > 0f)
        {
            cam.transform.position += Random.insideUnitSphere * Fighter.Shake;
            Fighter.Shake = Mathf.MoveTowards(Fighter.Shake, 0f, Time.unscaledDeltaTime * 1.2f);
        }
    }

    // ---------- UI helpers ----------
    void Rect(float x, float y, float w, float h, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new UnityEngine.Rect(x, y, w, h), white);
        GUI.color = Color.white;
    }

    void Bar(float x, float y, float w, float h, float frac, Color c)
    {
        Rect(x - 2, y - 2, w + 4, h + 4, new Color(0.7f, 0.65f, 0.4f, 0.9f));
        Rect(x, y, w, h, new Color(0.05f, 0.05f, 0.05f, 0.95f));
        Rect(x + 2, y + 2, (w - 4) * Mathf.Clamp01(frac), h - 4, c);
    }

    string OppLabel()
    {
        return oppFaction < 0 ? OppNames[0] : "<color=#" + Hex((Faction)oppFaction) + ">" + OppNames[oppFaction + 1] + "</color>";
    }

    static string Hex(Faction f) { return ColorUtility.ToHtmlStringRGB(Db.FactionColor(f)); }

    void OnGUI()
    {
        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float vw = Screen.width / s;
        GUI.skin.label.fontSize = 16;
        GUI.skin.label.richText = true;
        GUI.skin.button.fontSize = 18;
        GUI.skin.button.richText = true;

        var big = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        var mid = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };

        switch (state)
        {
            case State.Select: SelectGui(vw, big, mid); break;
            case State.Menu: MenuGui(vw, big, mid); break;
            case State.Shop: ShopGui(vw); break;
            case State.Fight: FightGui(vw, s, big, mid); break;
            case State.Result: if (storyNode != null) StoryResultGui(vw, big, mid); else ResultGui(vw, big, mid); break;
            case State.StoryMap: StoryGui(vw, big, mid); break;
        }
    }

    void SelectGui(float vw, GUIStyle big, GUIStyle mid)
    {
        GUI.Label(new UnityEngine.Rect(0, 30, vw, 70), "КОГО ОДЕВАЕМ?", big);
        GUI.Label(new UnityEngine.Rect(0, 100, vw, 40), "Выбери героя — дальше закупим ему экипировку", mid);
        float cx = vw / 2f;
        if (GUI.Button(new UnityEngine.Rect(cx - 330, 560, 300, 70), "МАЛЬЧИК")) { female = false; Save(); ShowMenu(); }
        if (GUI.Button(new UnityEngine.Rect(cx + 30, 560, 300, 70), "ДЕВОЧКА")) { female = true; Save(); ShowMenu(); }
        if (GUI.Button(new UnityEngine.Rect(cx - 150, 645, 300, 50), "Цвет волос"))
        {
            hairIdx = (hairIdx + 1) % Hairs.Length;
            ShowSelect();
        }
    }

    void MenuGui(float vw, GUIStyle big, GUIStyle mid)
    {
        Item w = Db.Get(eqW), h = Db.Get(eqH), a = Db.Get(eqA);
        GUI.Label(new UnityEngine.Rect(vw - 520, 26, 500, 70), "SHADOW <color=#c040ff>ARENA</color>", big);
        GUI.Label(new UnityEngine.Rect(vw - 520, 100, 500, 40), "Монеты: <color=#ffd040>" + coins.ToString("N0") + "</color>", mid);
        GUI.Label(new UnityEngine.Rect(vw - 520, 136, 500, 40), "Противник: " + OppLabel() + "  ·  ур. " + level, mid);
        if (GUI.Button(new UnityEngine.Rect(vw - 420, 200, 145, 70), "В БОЙ")) { storyNode = null; StartFight(); }
        if (GUI.Button(new UnityEngine.Rect(vw - 265, 200, 145, 70), "<b>СЮЖЕТ</b>")) OpenStory();
        if (GUI.Button(new UnityEngine.Rect(vw - 420, 285, 300, 70), "ОРУЖЕЙНАЯ")) { state = State.Shop; scroll = Vector2.zero; }
        if (GUI.Button(new UnityEngine.Rect(vw - 420, 370, 145, 50), "Сменить\nгероя")) ShowSelect();
        if (GUI.Button(new UnityEngine.Rect(vw - 265, 370, 145, 50), "Цвет\nволос"))
        {
            hairIdx = (hairIdx + 1) % Hairs.Length;
            Save();
            RebuildPreview();
        }
        if (GUI.Button(new UnityEngine.Rect(vw - 420, 435, 300, 50), "+100 000 000 монет")) { coins += 100000000L; Save(); }

        // choose who you fight and how strong they are
        var small = new GUIStyle(mid) { fontSize = 17 };
        GUI.Label(new UnityEngine.Rect(vw - 430, 498, 320, 24), "<b>ВЫБОР ПРОТИВНИКА</b>", small);
        if (GUI.Button(new UnityEngine.Rect(vw - 430, 528, 44, 42), "<")) { oppFaction = oppFaction <= -1 ? 3 : oppFaction - 1; Save(); }
        GUI.Label(new UnityEngine.Rect(vw - 384, 528, 228, 42), OppLabel(), small);
        if (GUI.Button(new UnityEngine.Rect(vw - 152, 528, 44, 42), ">")) { oppFaction = oppFaction >= 3 ? -1 : oppFaction + 1; Save(); }
        if (GUI.Button(new UnityEngine.Rect(vw - 430, 578, 44, 42), "-5")) { level = Mathf.Max(1, level - 5); Save(); }
        if (GUI.Button(new UnityEngine.Rect(vw - 384, 578, 40, 42), "-")) { level = Mathf.Max(1, level - 1); Save(); }
        GUI.Label(new UnityEngine.Rect(vw - 340, 578, 132, 42), "Уровень " + level, small);
        if (GUI.Button(new UnityEngine.Rect(vw - 204, 578, 40, 42), "+")) { level = Mathf.Min(MaxLevel, level + 1); Save(); }
        if (GUI.Button(new UnityEngine.Rect(vw - 160, 578, 52, 42), "+5")) { level = Mathf.Min(MaxLevel, level + 5); Save(); }
        if (GUI.Button(new UnityEngine.Rect(vw - 430, 628, 44, 42), "<")) { arenaSel = arenaSel <= -1 ? Arena.Names.Length - 1 : arenaSel - 1; Save(); if (arenaSel >= 0) LoadArena(arenaSel); }
        GUI.Label(new UnityEngine.Rect(vw - 384, 628, 228, 42), "Арена: " + (arenaSel < 0 ? "случайная" : Arena.Names[arenaSel]), small);
        if (GUI.Button(new UnityEngine.Rect(vw - 152, 628, 44, 42), ">")) { arenaSel = arenaSel >= Arena.Names.Length - 1 ? -1 : arenaSel + 1; Save(); if (arenaSel >= 0) LoadArena(arenaSel); }

        Rect(16, 16, 400, 176, new Color(0, 0, 0, 0.65f));
        GUI.Label(new UnityEngine.Rect(30, 24, 380, 30), (female ? "<b>ДЕВОЧКА</b>" : "<b>МАЛЬЧИК</b>") + "   ур. " + level);
        GUI.Label(new UnityEngine.Rect(30, 56, 380, 30), "Оружие: <color=#" + Hex(w.faction) + ">" + w.name + "</color>");
        GUI.Label(new UnityEngine.Rect(30, 84, 380, 30), "Шлем: " + (h != null ? "<color=#" + Hex(h.faction) + ">" + h.name + "</color>" : "<color=#888888>нет</color>"));
        GUI.Label(new UnityEngine.Rect(30, 112, 380, 30), "Броня: " + (a != null ? "<color=#" + Hex(a.faction) + ">" + a.name + "</color>" : "<color=#888888>нет</color>"));
        GUI.Label(new UnityEngine.Rect(30, 148, 380, 30), "УРОН " + w.damage + "    ЗАЩИТА " + ((h != null ? h.defense : 0) + (a != null ? a.defense : 0)) + "    скин: " + Db.Skins[eqSkin].name);
        GUI.Label(new UnityEngine.Rect(16, 686, vw, 30), "ЛКМ + движение мышью — покрутить героя");
    }

    void ShopGui(float vw)
    {
        Rect(vw - 560, 0, 560, 720, new Color(0, 0, 0, 0.78f));
        GUILayout.BeginArea(new UnityEngine.Rect(vw - 550, 10, 540, 700));
        GUILayout.Label("Монеты: <color=#ffd040>" + coins.ToString("N0") + "</color>   ·   одеваем: " + (female ? "девочку" : "мальчика"));
        GUILayout.BeginHorizontal();
        string[] tn = { "Оружие", "Шлемы", "Броня", "Скины" };
        for (int i = 0; i < 4; i++)
            if (GUILayout.Button(tn[i], GUILayout.Height(40)))
            {
                skinTab = i == 3;
                if (i < 3) tab = (Slot)i;
                scroll = Vector2.zero;
            }
        GUILayout.EndHorizontal();
        if (skinTab) SkinList();
        else
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Все", GUILayout.Height(34))) factionFilter = -1;
            for (int f = 0; f < 4; f++)
                if (GUILayout.Button("<color=#" + Hex((Faction)f) + ">" + (Faction)f + "</color>", GUILayout.Height(34))) factionFilter = f;
            GUILayout.EndHorizontal();

            if (factionFilter >= 0) GUILayout.Label("<i>" + (Faction)factionFilter + " — " + Db.Trait((Faction)factionFilter) + "</i>");
            // take off what is worn in this slot
            Item worn = Db.Get(tab == Slot.Weapon ? eqW : tab == Slot.Helmet ? eqH : eqA);
            bool bare = worn == null || Db.IsFist(worn);
            GUI.enabled = !bare;
            if (GUILayout.Button(bare ? (tab == Slot.Weapon ? "Без оружия: бой на кулаках" : tab == Slot.Helmet ? "Шлем не надет" : "Броня не надета")
                                      : "Снять: " + worn.name, GUILayout.Height(34)))
            {
                if (tab == Slot.Weapon) eqW = Db.Fists(worn.faction).id;
                else if (tab == Slot.Helmet) eqH = "";
                else eqA = "";
                Save();
                RebuildPreview();
            }
            GUI.enabled = true;
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(480));
            foreach (Item it in Db.All)
            {
                if (it.slot != tab || (factionFilter >= 0 && (int)it.faction != factionFilter)) continue;
                bool have = owned.Contains(it.id);
                bool eq = it.id == eqW || it.id == eqH || it.id == eqA;
                GUILayout.BeginHorizontal(GUI.skin.box);
                string stats = it.slot == Slot.Weapon
                    ? "УРОН " + it.damage + "  ДЛИНА " + it.reach + "  СКОР " + it.speed + "  КРИТ " + Mathf.Round(it.crit * 100f) + "%"
                    : "ЗАЩИТА " + it.defense;
                GUILayout.Label("<b><color=#" + Hex(it.faction) + ">" + it.name + "</color></b>\n" + it.faction + " · " + stats, GUILayout.Width(330));
                string label = eq ? "НАДЕТО" : have ? "Надеть" : "Купить\n" + it.price.ToString("N0");
                GUI.enabled = !eq && (have || coins >= it.price);
                if (GUILayout.Button(label, GUILayout.Width(150), GUILayout.Height(54))) BuyOrEquip(it, have);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }
        if (GUILayout.Button("Назад", GUILayout.Height(40))) ShowMenu();
        GUILayout.EndArea();
    }

    void SkinList()
    {
        GUILayout.Label("<i>Скин меняет одежду, кожу и детали героя. Броня и шлем надеваются поверх.</i>");
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(560));
        for (int i = 0; i < Db.Skins.Length; i++)
        {
            Skin sk = Db.Skins[i];
            bool have = owned.Contains(sk.id), eq = eqSkin == i;
            GUILayout.BeginHorizontal(GUI.skin.box);
            string col = ColorUtility.ToHtmlStringRGB(Color.Lerp(sk.cloth, Color.white, 0.4f));
            GUILayout.Label("<b><color=#" + col + ">" + sk.name + "</color></b>\n" + sk.desc, GUILayout.Width(330));
            string label = eq ? "НАДЕТО" : have ? "Надеть" : "Купить\n" + sk.price.ToString("N0");
            GUI.enabled = !eq && (have || coins >= sk.price);
            if (GUILayout.Button(label, GUILayout.Width(150), GUILayout.Height(54)))
            {
                if (!have) { coins -= sk.price; owned.Add(sk.id); }
                eqSkin = i;
                Save();
                RebuildPreview();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    void BuyOrEquip(Item it, bool have)
    {
        if (!have) { coins -= it.price; owned.Add(it.id); }
        if (it.slot == Slot.Weapon) eqW = it.id;
        else if (it.slot == Slot.Helmet) eqH = it.id;
        else eqA = it.id;
        Save();
        RebuildPreview();
    }

    void HeadBar(Fighter f, float s, Color c)
    {
        Vector3 sp = cam.WorldToScreenPoint(f.transform.position + new Vector3(0, f.headY, 0));
        if (sp.z <= 0) return;
        float x = sp.x / s - 60f, y = (Screen.height - sp.y) / s - 24f;
        Bar(x, y, 120, 12, f.hp / f.maxHp, c);
        string b = f.Buffs;
        if (b.Length > 0)
            GUI.Label(new UnityEngine.Rect(x - 80, y - 24, 280, 22), b, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, richText = true });
    }

    // energy bar split into three charges
    void SegBar(float x, float y, float w, float h, Fighter f)
    {
        Bar(x, y, w, h, f.energy / 100f, new Color(0.75f, 0.35f, 1f));
        for (int k = 1; k < 3; k++) Rect(x + w * k / 3f - 1f, y - 2f, 3f, h + 4f, new Color(0.7f, 0.65f, 0.4f, 0.95f));
    }

    void AbilityPanel()
    {
        int ch = player.Charges;
        var st = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        // shurikens: no cost, three in the pouch, they come back on their own
        {
            float x = 24 + 4 * 158, y = 598;
            bool ok = player.shuriken > 0;
            Rect(x, y, 150, 58, ok ? new Color(0.15f, 0.2f, 0.28f, 0.85f) : new Color(0f, 0f, 0f, 0.55f));
            Rect(x, y, 150, 3, new Color(0.8f, 0.85f, 0.95f));
            GUI.Label(new UnityEngine.Rect(x, y + 3, 150, 34), (ok ? "<color=#ffffff>" : "<color=#888888>") + "<b>[O]</b> Сюрикен</color>", st);
            for (int k = 0; k < Fighter.MaxShuriken; k++) Rect(x + 75 - Fighter.MaxShuriken * 9 + k * 18, y + 42, 14, 8, k < player.shuriken ? new Color(0.85f, 0.9f, 1f) : new Color(0.3f, 0.3f, 0.3f));
        }
        for (int i = 0; i < 4; i++)
        {
            int a = i + 1;
            int cost = a == 4 ? 3 : player.AbilityCost(a);
            string nm = a == 4 ? player.PowerName : player.AbilityName(a);
            bool ok = ch >= cost;
            float x = 24 + i * 158, y = 598;
            Rect(x, y, 150, 58, ok ? new Color(0.25f, 0.1f, 0.35f, 0.85f) : new Color(0f, 0f, 0f, 0.55f));
            Rect(x, y, 150, 3, ok ? Db.FactionColor(player.weapon.faction) : new Color(0.3f, 0.3f, 0.3f));
            GUI.Label(new UnityEngine.Rect(x, y + 3, 150, 34), (ok ? "<color=#ffffff>" : "<color=#888888>") + "<b>[" + (a == 4 ? "I" : a.ToString()) + "]</b> " + nm + "</color>", st);
            for (int k = 0; k < cost; k++) Rect(x + 75 - cost * 9 + k * 18, y + 42, 14, 8, ok ? new Color(0.8f, 0.45f, 1f) : new Color(0.3f, 0.3f, 0.3f));
        }
    }

    void FightGui(float vw, float s, GUIStyle big, GUIStyle mid)
    {
        Rect(0, 0, vw, 92, new Color(0, 0, 0, 0.45f));
        GUI.Label(new UnityEngine.Rect(24, 8, 500, 26), "<b>" + (female ? "ДЕВОЧКА" : "МАЛЬЧИК") + "</b>   " + player.weapon.name);
        Bar(24, 36, 480, 26, player.hp / player.maxHp, new Color(0.25f, 0.85f, 0.35f));
        SegBar(24, 68, 300, 12, player);
        GUI.Label(new UnityEngine.Rect(332, 62, 420, 24), "заряды <b>" + player.Charges + "/3</b>" + (player.Charges >= 3 ? "   <color=#ff80ff><b>УЛЬТА: " + player.PowerName + " (I)</b></color>" : ""));

        var right = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, alignment = TextAnchor.MiddleRight };
        GUI.Label(new UnityEngine.Rect(vw - 524, 8, 500, 26), (enemy.boss ? "<color=#ff5050><b>БОСС</b></color>  " : "") + "<b><color=#" + Hex(enemyFaction) + ">" + (enemyName != "" ? enemyName : enemyFaction.ToString()) + "</color></b>   " + enemy.weapon.name + "   ур. " + enemyLevel, right);
        Bar(vw - 504, 36, 480, 26, enemy.hp / enemy.maxHp, new Color(0.9f, 0.27f, 0.22f));
        SegBar(vw - 504, 68, 300, 12, enemy);

        HeadBar(player, s, new Color(0.25f, 0.85f, 0.35f));
        HeadBar(enemy, s, new Color(0.9f, 0.27f, 0.22f));

        var tm = new GUIStyle(GUI.skin.label) { fontSize = 38, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
        GUI.Label(new UnityEngine.Rect(vw / 2 - 60, 6, 120, 50), Mathf.CeilToInt(Mathf.Max(0f, roundTime)).ToString(), tm);
        GUI.Label(new UnityEngine.Rect(vw / 2 - 80, 52, 160, 24), "раунд " + round + " из " + (winsNeeded * 2 - 1), new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        for (int i = 0; i < winsNeeded; i++)
        {
            Rect(vw / 2 - 74 - i * 26, 78, 20, 10, i < pWins ? new Color(1f, 0.85f, 0.2f) : new Color(0.2f, 0.2f, 0.2f, 0.9f));
            Rect(vw / 2 + 54 + i * 26, 78, 20, 10, i < eWins ? new Color(1f, 0.85f, 0.2f) : new Color(0.2f, 0.2f, 0.2f, 0.9f));
        }
        if (Time.unscaledTime < Fighter.BossMsgT)
        {
            // a boss just used one of its strange powers
            var bm = new GUIStyle(big) { fontSize = 40, richText = true };
            GUI.Label(new UnityEngine.Rect(0, 110, vw, 60), "<color=#ff4a3a>" + (enemyName != "" ? enemyName.ToUpper() + ": " : "") + Fighter.BossMsg + "!</color>", bm);
        }
        if (Time.unscaledTime < Fighter.EventMsgT)
            GUI.Label(new UnityEngine.Rect(0, 170, vw, 50), "<color=#ffd040>" + Fighter.EventMsg + "</color>", new GUIStyle(big) { fontSize = 34, richText = true });
        Rect(0, 664, vw, 56, new Color(0, 0, 0, 0.55f));
        GUI.Label(new UnityEngine.Rect(20, 668, vw, 24),
            "A/D ход · <b>Shift</b> или A-A / D-D рывок (неуязвим) · W прыжок · <b>U</b> рука · <b>H</b> нога · <b>S+H</b> подсечка · <b>L</b> блок · <b>O</b> сюрикен · Esc меню");
        GUI.Label(new UnityEngine.Rect(20, 692, vw, 24),
            "<b>J</b>/ЛКМ рубящий · <b>K</b>/ПКМ тяжёлый (у каждой фракции свои) · <b>1 2 3</b> способности за заряды, можно подряд · <b>I</b> ульта (3 заряда)");
        AbilityPanel();
        if (rs == RState.Intro)
        {
            string t = countdown > 0.7f ? (pWins == winsNeeded - 1 && eWins == winsNeeded - 1 ? "ФИНАЛЬНЫЙ РАУНД" : "РАУНД " + round) : "БОЙ!";
            GUI.Label(new UnityEngine.Rect(0, 250, vw, 100), t, big);
            GUI.Label(new UnityEngine.Rect(0, 340, vw, 30), (enemy.boss && round == 1 ? "<color=#ff6050><b>БОСС: " + enemyName + "</b> · бой до 3 побед</color>   " : "") + Arena.Names[curArena], new GUIStyle(mid) { richText = true });
        }
        else if (rs == RState.KO)
        {
            GUI.Label(new UnityEngine.Rect(0, 200, vw, 120), "<color=#ff5040>" + koText + "</color>", new GUIStyle(big) { fontSize = 84, richText = true });
            string who = lastRoundPlayer
                ? "<color=#60ff80>РАУНД " + round + " — ТЫ ПОБЕДИЛ!</color>"
                : "<color=#ff6060>РАУНД " + round + " — ПОБЕДИЛ ПРОТИВНИК</color>";
            Rect(vw / 2 - 320, 320, 640, 64, new Color(0, 0, 0, 0.6f));
            GUI.Label(new UnityEngine.Rect(0, 326, vw, 36), who, new GUIStyle(mid) { fontSize = 30, fontStyle = FontStyle.Bold, richText = true });
            GUI.Label(new UnityEngine.Rect(0, 360, vw, 26), "счёт по раундам  " + pWins + " : " + eWins, mid);
        }
    }

    void ResultGui(float vw, GUIStyle big, GUIStyle mid)
    {
        Rect(0, 180, vw, 200, new Color(0, 0, 0, 0.6f));
        GUI.Label(new UnityEngine.Rect(0, 200, vw, 100), won ? "<color=#60ff80>ПОБЕДА</color>" : "<color=#ff6060>ПОРАЖЕНИЕ</color>", big);
        GUI.Label(new UnityEngine.Rect(0, 290, vw, 40), "Раунды " + pWins + " : " + eWins + "   ·   +" + lastReward.ToString("N0") + " монет", mid);
        if (GUI.Button(new UnityEngine.Rect(vw / 2 - 150, 400, 300, 60), "Ещё бой")) StartFight();
        if (GUI.Button(new UnityEngine.Rect(vw / 2 - 150, 475, 300, 60), "В меню")) ShowMenu();
    }
}
