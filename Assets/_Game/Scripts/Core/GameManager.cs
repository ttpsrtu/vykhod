using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    public enum GameState { Menu, Hideout, Loading, Raid, Results }

    /// <summary>Глобальный доступ к состоянию игры.</summary>
    public static class Game
    {
        public static PlayerProfile Profile;
        public static GameManager Manager;
        public static GameSettings Settings = new GameSettings();
        public static GameState State => Manager != null ? Manager.State : GameState.Menu;
        public static bool InRaid => State == GameState.Raid;
        public static void Notify(string msg) { Debug.Log("[Игра] " + msg); if (Manager != null) Manager.PushNotification(msg); }
    }

    public class GameSettings
    {
        public float sensitivity = 1.6f, fov = 85f, volume = 0.8f;
        public int quality = 2; // 0 низкое, 1 среднее, 2 высокое
        public bool invertY;
        public string playerName = "Боец";
        public string lastIp = "127.0.0.1";

        public void Load()
        {
            sensitivity = PlayerPrefs.GetFloat("sens", 1.6f); fov = PlayerPrefs.GetFloat("fov", 85f); volume = PlayerPrefs.GetFloat("vol", 0.8f);
            quality = PlayerPrefs.GetInt("quality", 2); invertY = PlayerPrefs.GetInt("invY", 0) == 1;
            playerName = PlayerPrefs.GetString("pname", "Боец"); lastIp = PlayerPrefs.GetString("lastIp", "127.0.0.1");
        }
        public void Save()
        {
            PlayerPrefs.SetFloat("sens", sensitivity); PlayerPrefs.SetFloat("fov", fov); PlayerPrefs.SetFloat("vol", volume);
            PlayerPrefs.SetInt("quality", quality); PlayerPrefs.SetInt("invY", invertY ? 1 : 0);
            PlayerPrefs.SetString("pname", playerName); PlayerPrefs.SetString("lastIp", lastIp); PlayerPrefs.Save();
        }
        public void Apply()
        {
            AudioListener.volume = volume;
            // Оптимизация под macOS/Metal и слабые машины: без лишних теней и постэффектов, фиксированный FPS-бюджет.
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            switch (quality)
            {
                case 0: QualitySettings.shadows = ShadowQuality.Disable; QualitySettings.shadowDistance = 0; QualitySettings.antiAliasing = 0; QualitySettings.lodBias = 0.6f; QualitySettings.pixelLightCount = 1; break;
                case 1: QualitySettings.shadows = ShadowQuality.HardOnly; QualitySettings.shadowDistance = 60; QualitySettings.antiAliasing = 2; QualitySettings.lodBias = 1f; QualitySettings.pixelLightCount = 2; break;
                default: QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowDistance = 160; QualitySettings.antiAliasing = 4; QualitySettings.lodBias = 1.3f; QualitySettings.pixelLightCount = 4; break;
            }
            QualitySettings.shadowCascades = quality >= 2 ? 4 : quality == 1 ? 2 : 1;
            QualitySettings.shadowResolution = quality >= 2 ? ShadowResolution.High : ShadowResolution.Medium;
            QualitySettings.shadowProjection = ShadowProjection.CloseFit;
            PostFx.Apply();
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.softParticles = false;
        }
    }

    /// <summary>Статистика текущего рейда.</summary>
    public class RaidStats
    {
        public int kills, headshots, bossKills, playerKills, questItems;
        public long xp, lootValue;
        public float damageDealt, startTime;
        public string extractName = "";
        public List<string> log = new List<string>();
    }

    public class GameManager : MonoBehaviour
    {
        public GameState State = GameState.Menu;
        public NetGame Net;
        public UIRoot UI;
        public RaidStats Raid;
        public string RaidMap = "mirea", RaidMode = "coop";
        public Loadout RaidLoadout;                 // живая копия экипировки в рейде
        public readonly Queue<string> Notifications = new Queue<string>();
        public bool RaidFinishing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Game.Manager != null) return;
            var go = new GameObject("GameRoot");
            go.AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Game.Manager != null && Game.Manager != this) { Destroy(gameObject); return; }
            Game.Manager = this;
            DontDestroyOnLoad(gameObject);
            Database.Load();
            AssetCatalog.Load();
            Game.Settings.Load(); Game.Settings.Apply();
            Net = gameObject.AddComponent<NetGame>();
            UI = gameObject.AddComponent<UIRoot>();
            if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            UI.ShowMainMenu();
            // Автотест: VYKHOD.exe -autoraid mirea -autoquit 40  → новая игра, рейд на карте, выход через N секунд
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autoraid" && i + 1 < args.Length) StartCoroutine(AutoRaid(args[i + 1]));
                if (args[i] == "-autojoin" && i + 1 < args.Length) StartCoroutine(AutoJoin(args[i + 1]));
                if (args[i] == "-shots" && i + 1 < args.Length) StartCoroutine(AutoShots(args[i + 1]));
                if (args[i] == "-gallery" && i + 1 < args.Length) StartCoroutine(Gallery(args[i + 1]));
                if (args[i] == "-autoquit" && i + 1 < args.Length && float.TryParse(args[i + 1], out var sec)) StartCoroutine(AutoQuit(sec));
            }
        }

        System.Collections.IEnumerator AutoRaid(string map)
        {
            yield return new WaitForSeconds(1f);
            NewGame("Автотест");
            yield return new WaitForSeconds(1f);
            StartRaidHost(map, "coop");
            float t = 0;
            while (State != GameState.Raid && t < 120) { t += Time.deltaTime; yield return null; }
            Debug.Log($"[AutoTest] состояние {State} через {t:0.0} с, мир готов: {World.IsReady}, контейнеров {World.Containers.Count}, игрок: {(Net.LocalPlayer != null ? Net.LocalPlayer.transform.position.ToString() : "нет")}");
            yield return new WaitForSeconds(15f);
            var lp = Net.LocalPlayer;
            Debug.Log($"[AutoTest] через 15 с: врагов {Net.Session?.EnemiesAlive.Value}, hp {lp?.Hp:0}, fps ~{1f / Time.smoothDeltaTime:0}, оружие {lp?.Weapons?.Current?.Name}");
            FinishRaid(true, "Автотест");
        }

        /// <summary>Автотест клиента: VYKHOD.exe -autojoin 127.0.0.1 -autoquit 40</summary>
        System.Collections.IEnumerator AutoJoin(string ip)
        {
            yield return new WaitForSeconds(1f);
            NewGame("Клиент");
            yield return new WaitForSeconds(1f);
            JoinRaid(ip, 7777);
            float t = 0;
            while (State != GameState.Raid && t < 60) { t += Time.deltaTime; yield return null; }
            Debug.Log($"[AutoTest] клиент: состояние {State} через {t:0.0} с, мир готов: {World.IsReady}, карта {RaidMap}, игроков в сессии {Net.Players.Count}, сид {Net.Session?.Seed.Value}");
            yield return new WaitForSeconds(10f);
            var lp = Net.LocalPlayer;
            Debug.Log($"[AutoTest] клиент через 10 с: игроков {Net.Players.Count}, врагов {Net.Session?.EnemiesAlive.Value}, hp {lp?.Hp:0}, позиция {lp?.transform.position}, контейнеров {World.Containers.Count}");
            if (Net.Session != null) Net.Session.ChatRpc("Привет от клиента!");
            yield return new WaitForSeconds(5f);
            Debug.Log($"[AutoTest] клиент: состояние {State}, соединение {(Net.IsRunning ? "есть" : "нет")}");
        }

        /// <summary>Скриншоты интерфейса: VYKHOD.exe -shots <папка> — меню, вкладки лобби, HUD в рейде, инвентарь.</summary>
        System.Collections.IEnumerator AutoShots(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); World.ForceDay = true;
            yield return new WaitForSeconds(2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "01_menu.png")); yield return new WaitForSeconds(1f);
            if (!ContinueGame()) NewGame("Боец");
            yield return new WaitForSeconds(1.5f);
            for (int t = 0; t < 6; t++) { UI.SetLobbyTab(t); yield return new WaitForSeconds(1f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"0{2 + t}_lobby_{t}.png")); yield return new WaitForSeconds(0.5f); }
            StartRaidHost("suburb", "coop");
            float w = 0; while (State != GameState.Raid && w < 60) { w += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(3f);
            LookAtCenter();
            yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "08_hud.png")); yield return new WaitForSeconds(1f);
            UI.ToggleInventory(); yield return new WaitForSeconds(1f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "09_inventory.png")); yield return new WaitForSeconds(1f);
            UI.ToggleInventory(); FinishRaid(true, "Скриншоты"); yield return new WaitForSeconds(2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "10_results.png")); yield return new WaitForSeconds(1.5f);
            // ещё две карты для оценки визуала
            int shot = 11;
            foreach (var mapId in new[] { "factory", "mirea", "mall" })
            {
                EnterHideout(); yield return new WaitForSeconds(1f);
                StartRaidHost(mapId, "coop");
                w = 0; while (State != GameState.Raid && w < 60) { w += Time.deltaTime; yield return null; }
                yield return new WaitForSeconds(3f); LookAtCenter(); yield return new WaitForSeconds(0.5f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{shot++}_raid_{mapId}.png")); yield return new WaitForSeconds(1f);
                FinishRaid(true, "Скриншоты"); yield return new WaitForSeconds(2f);
            }
            Debug.Log("[AutoTest] скриншоты готовы"); Application.Quit();
        }

        void LookAtCenter()
        {
            var lp = Net.LocalPlayer; if (lp == null) return;
            var dir = -lp.transform.position; dir.y = 0; if (dir.sqrMagnitude < 1) dir = Vector3.forward;
            lp.transform.rotation = Quaternion.LookRotation(dir.normalized); lp.Ctrl.Pitch = 2f;
        }

        System.Collections.IEnumerator AutoQuit(float sec) { yield return new WaitForSeconds(sec); Debug.Log("[AutoTest] выход"); Application.Quit(); }

        /// <summary>
        /// Галерея моделей: VYKHOD.exe -gallery <папка> — скриншоты всех моделей оружейных паков с именами файлов (20_models_N.png)
        /// и всех стволов из данных с их названиями и выбранной моделью (21_weapons_N.png). Для проверки «оружие совпадает с надписью».
        /// </summary>
        System.Collections.IEnumerator Gallery(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(1f);
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) c.enabled = false;
            SetSceneCameras(false);
            var root = new GameObject("Gallery").transform;
            var sun = new GameObject("sun").AddComponent<Light>(); sun.transform.SetParent(root, false); sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(35, -40, 0); sun.intensity = 1.3f; sun.color = new Color(1f, 0.97f, 0.92f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f); RenderSettings.skybox = null; RenderSettings.fog = false;
            var cam = new GameObject("gcam").AddComponent<Camera>(); cam.transform.SetParent(root, false); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.22f, 0.26f);
            cam.orthographic = true; cam.orthographicSize = 3.5f; cam.transform.position = new Vector3(0, 0, -10); cam.nearClipPlane = 0.1f; cam.farClipPlane = 50;
            // 1) все модели оружейных паков
            var models = AssetCatalog.FindModels("", "guns").Concat(AssetCatalog.FindModels("", "weapon-pack")).Distinct()
                .Where(m => { var l = m.ToLowerInvariant(); return !l.Contains("accessories") && !l.Contains("ammo") && !l.Contains("bullet") && !l.Contains("grenade") && !l.Contains("rocket") && !l.Contains("flamethrower") && !l.Contains("magazine"); })
                .Select(m => (path: m, label: System.IO.Path.GetFileNameWithoutExtension(m) + "\n" + PackOf(m), variant: 0)).ToList();
            yield return GalleryPages(cam, root, models, dir, "20_models");
            // 2) оружие из данных → выбранная модель
            var ws = Database.Weapons.Values.Where(w => !w.IsMelee).OrderBy(w => w.cls).ThenBy(w => w.lvl)
                .Select(w => { var p = AssetCatalog.GunModel(w); return (path: p, label: w.name + "\n" + (p != null ? System.IO.Path.GetFileNameWithoutExtension(p) : "—"), variant: w.modelVariant); }).ToList();
            yield return GalleryPages(cam, root, ws, dir, "21_weapons");
            Debug.Log("[Gallery] готово"); Application.Quit();
        }

        static string PackOf(string path) { int i = path.IndexOf("imported/", System.StringComparison.OrdinalIgnoreCase); var rest = i >= 0 ? path.Substring(i + 9) : path; int j = rest.IndexOf('/'); return j > 0 ? rest.Substring(0, j) : rest; }

        System.Collections.IEnumerator GalleryPages(Camera cam, Transform root, List<(string path, string label, int variant)> items, string dir, string prefix)
        {
            const int cols = 8, rows = 4; float cw = 1.55f, ch = 1.7f;
            var font = AssetCatalog.Font;
            for (int page = 0; page * cols * rows < items.Count; page++)
            {
                var pr = new GameObject("page").transform; pr.SetParent(root, false);
                for (int k = 0; k < cols * rows && page * cols * rows + k < items.Count; k++)
                {
                    var it = items[page * cols * rows + k]; int col = k % cols, row = k / cols;
                    var cell = new GameObject("cell").transform; cell.SetParent(pr, false); cell.position = new Vector3((col - (cols - 1) / 2f) * cw, ((rows - 1) / 2f - row) * ch + 0.25f, 0);
                    if (it.path != null)
                    {
                        var wrap = new GameObject("wrap").transform; wrap.SetParent(cell, false); wrap.localRotation = Quaternion.Euler(0, 90, 0);
                        var g = AssetCatalog.Spawn(it.path, wrap, 1.35f, false, false, it.variant);
                        if (g != null) { AssetCatalog.OrientGun(g); AssetCatalog.Recenter(g); }
                    }
                    var tgo = new GameObject("label"); tgo.transform.SetParent(cell, false); tgo.transform.localPosition = new Vector3(0, -0.62f, -0.5f);
                    var tm = tgo.AddComponent<TextMesh>(); tm.text = it.label; tm.fontSize = 48; tm.characterSize = 0.045f; tm.anchor = TextAnchor.UpperCenter; tm.alignment = TextAlignment.Center; tm.color = Color.white;
                    if (font != null) { tm.font = font; tgo.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
                }
                yield return new WaitForSeconds(0.8f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{prefix}_{page}.png"));
                yield return new WaitForSeconds(0.8f);
                Destroy(pr.gameObject);
                yield return null;
            }
        }

        /// <summary>Камеры и слушатели сцены (фон меню) выключаем на время рейда — у игрока своя камера.</summary>
        public void SetSceneCameras(bool on)
        {
            foreach (var c in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (c.gameObject.name == "PlayerCamera" || !c.gameObject.scene.IsValid()) continue;
                c.enabled = on; var al = c.GetComponent<AudioListener>(); if (al != null) al.enabled = on;
            }
            var mine = GetComponent<AudioListener>(); if (mine != null) mine.enabled = on && FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length <= 1;
        }

        public void PushNotification(string msg)
        {
            Notifications.Enqueue(msg);
            while (Notifications.Count > 6) Notifications.Dequeue();
            UI?.OnNotification(msg);
        }

        // ------------------------------------------------------------ меню
        public void NewGame(string playerName)
        {
            Game.Settings.playerName = playerName; Game.Settings.Save();
            Game.Profile = PlayerProfile.NewGame(playerName);
            SaveSystem.Save(Game.Profile);
            EnterHideout();
        }

        public bool ContinueGame()
        {
            var p = SaveSystem.Load();
            if (p == null) return false;
            Game.Profile = p;
            EnterHideout();
            return true;
        }

        public void EnterHideout()
        {
            State = GameState.Hideout;
            Stats.Rebuild(Game.Profile);
            HideoutSystem.TickOffline();
            int n = HideoutSystem.Collect(); if (n > 0) Game.Notify($"Готово крафтов: {n}");
            QuestSystem.Refresh();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            UI.ShowHideout();
            SaveSystem.Save(Game.Profile);
        }

        public void ToMainMenu()
        {
            if (Game.Profile != null) SaveSystem.Save(Game.Profile);
            Net.Shutdown(); World.Clear(); SetSceneCameras(true);
            State = GameState.Menu;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            UI.ShowMainMenu();
        }

        // ------------------------------------------------------------ рейд
        public void StartRaidHost(string mapId, string mode)
        {
            var map = Database.Map(mapId); if (map == null) { Game.Notify("Нет такой карты: " + mapId); return; }
            PrepareRaid(mapId, mode);
            UI.ShowLoading("Строим карту: " + map.name);
            int seed = Random.Range(1, int.MaxValue);
            StartCoroutine(HostRoutine(map, mode, seed));
        }

        System.Collections.IEnumerator HostRoutine(MapDef map, string mode, int seed)
        {
            yield return null;
            World.Build(map, seed, true);
            yield return null;
            if (!Net.Host(map.id, mode, seed)) { Game.Notify("Не удалось запустить хост"); EnterHideout(); }
        }

        public void JoinRaid(string ip, ushort port)
        {
            Game.Settings.lastIp = ip; Game.Settings.Save();
            PrepareRaid(Game.Profile.lastMap, Game.Profile.lastMode);
            UI.ShowLoading("Подключение к " + ip + "...");
            if (!Net.Join(ip, port)) { Game.Notify("Не удалось подключиться"); EnterHideout(); }
        }

        void PrepareRaid(string mapId, string mode)
        {
            State = GameState.Loading; RaidFinishing = false;
            RaidMap = mapId; RaidMode = mode;
            Game.Profile.lastMap = mapId; Game.Profile.lastMode = mode;
            Game.Profile.raids++;
            RaidLoadout = Game.Profile.loadout.Clone();
            foreach (var it in RaidLoadout.AllItems()) it.found = false;
            Raid = new RaidStats { startTime = Time.time };
            Stats.Rebuild(Game.Profile);
            SaveSystem.Save(Game.Profile);
        }

        /// <summary>Вызывается сетью, когда локальный игрок заспавнен и мир построен.</summary>
        public void OnRaidReady()
        {
            State = GameState.Raid;
            if (Net.Session != null) { RaidMap = Net.Session.MapId.Value.ToString(); RaidMode = Net.Session.Mode.Value == 1 ? "pvp" : "coop"; }
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            UI.ShowHUD();
            var map = Database.Map(RaidMap);
            Game.Notify($"Рейд: {map?.name}. Режим: {(RaidMode == "pvp" ? "Все против всех" : "Кооператив")}. Найдите выход.");
        }

        /// <summary>Конец рейда для локального игрока.</summary>
        public void FinishRaid(bool extracted, string reason)
        {
            if (RaidFinishing) return; RaidFinishing = true;
            var p = Game.Profile; var r = Raid; r.extractName = reason;
            var lootItems = RaidLoadout.AllItems().ToList();
            long lootValue = lootItems.Sum(i => (long)i.Value);
            if (extracted)
            {
                p.extractions++; p.extractStreak++;
                p.loadout = RaidLoadout;
                foreach (var it in lootItems) if (it.found) r.questItems++;
                long xpLoot = lootValue / 400; r.xp += xpLoot + 300;
                p.totalLootValue += lootValue;
                p.AddXp(r.xp, "raid");
                p.AddSkillXp("endurance", 60); p.AddSkillXp("attention", 20);
                QuestSystem.OnExtract(RaidMap, (int)lootValue, true);
                QuestSystem.OnFoundExtracted(lootItems);
            }
            else
            {
                p.deaths++; p.extractStreak = 0;
                var kept = new Loadout();
                var secure = RaidLoadout.eq.Get("secure"); if (secure != null) kept.eq.Set("secure", secure);
                if (Stats.Has("keepRig")) { var rig = RaidLoadout.eq.Get("rig"); if (rig != null) kept.eq.Set("rig", rig); }
                if (Stats.Has("medic_second_wind_used")) { }
                float insChance = Mathf.Clamp01(0.6f + Stats.Get("insurance"));
                foreach (var it in lootItems) if (it.insured && !kept.AllItems().Contains(it) && Random.value < insChance) p.insuranceReturns.Add(it.Clone());
                p.loadout = kept;
                p.AddXp(r.xp / 2, "raid");
                QuestSystem.OnExtract(RaidMap, 0, false);
            }
            foreach (var it in p.loadout.AllItems()) it.found = false;
            p.kills += r.kills; p.bossKills += r.bossKills;
            Stats.Rebuild(p);
            SaveSystem.Save(p);
            StartCoroutine(FinishRoutine(extracted));
        }

        System.Collections.IEnumerator FinishRoutine(bool extracted)
        {
            yield return new WaitForSeconds(0.2f);
            Net.Shutdown();
            World.Clear();
            SetSceneCameras(true);
            State = GameState.Results;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            UI.ShowResults(extracted);
        }

        /// <summary>Потеря связи с хостом: возвращаемся без наград, снаряжение остаётся.</summary>
        public void OnConnectionLost()
        {
            if (State != GameState.Raid && State != GameState.Loading) return;
            if (RaidFinishing) return; RaidFinishing = true;
            Game.Profile.loadout = RaidLoadout ?? Game.Profile.loadout;
            foreach (var it in Game.Profile.loadout.AllItems()) it.found = false;
            SaveSystem.Save(Game.Profile);
            Net.Shutdown(); World.Clear(); SetSceneCameras(true);
            Game.Notify("Связь с хостом потеряна. Снаряжение сохранено.");
            EnterHideout();
        }

        public void AbortRaid()
        {
            // Выход из рейда через меню = пропал без вести: теряем всё, кроме контейнера.
            FinishRaid(false, "Пропал без вести");
        }
    }
}
