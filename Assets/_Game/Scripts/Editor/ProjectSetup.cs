using System;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vykhod.EditorTools
{
    /// <summary>
    /// Автонастройка проекта при открытии: сетевые префабы (нужны GlobalObjectIdHash), стартовая сцена, настройки плеера.
    /// Меню Vykhod → Setup Project / Build.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string PrefabDir = "Assets/Resources/Prefabs";
        const string ScenePath = "Assets/_Game/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () => { if (!AllPrefabsExist() || !File.Exists(ScenePath)) Setup(); };
        }

        static bool AllPrefabsExist() => new[] { "Player", "Enemy", "Session", "WorldItem" }.All(n => File.Exists($"{PrefabDir}/{n}.prefab"));

        [MenuItem("Vykhod/Setup Project (prefabs, scene, settings)")]
        public static void Setup()
        {
            Directory.CreateDirectory(PrefabDir); Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            MakePlayer(); MakeEnemy(); MakeSession(); MakeWorldItem();
            FixAllHashes();
            MakeScene();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Vykhod] Проект настроен. Откройте сцену " + ScenePath + " и нажмите Play.");
        }

        /// <summary>Для автоматизации: настройка + сборка Windows одной командой.</summary>
        public static void SetupAndBuildWindows() { Setup(); BuildWindows(); }

        static void SavePrefab(GameObject go, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            FixHash(path);
        }

        /// <summary>В batchmode OnValidate не вызывается — генерируем GlobalObjectIdHash вручную (как делает NGO).</summary>
        static void FixHash(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (prefab == null) return;
            var no = prefab.GetComponent<NetworkObject>(); if (no == null) return;
            var m = typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (m != null) m.Invoke(no, null);
            var so = new SerializedObject(no); var p = so.FindProperty("GlobalObjectIdHash");
            uint hash = p != null ? p.uintValue : 0;
            if (hash == 0)
            {
                var gid = GlobalObjectId.GetGlobalObjectIdSlow(no).ToString();
                hash = 2166136261; foreach (char c in gid) { hash ^= c; hash *= 16777619; }
            }
            // Записываем в файл префаба через контент (SetDirty на ассете в batchmode не сохраняется)
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var cno = contents.GetComponent<NetworkObject>();
                var cso = new SerializedObject(cno); var cp = cso.FindProperty("GlobalObjectIdHash");
                if (cp != null && cp.uintValue != hash) { cp.uintValue = hash; cso.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(contents, path); }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets();
            // Контроль по файлу: если на диске всё ещё 0 — правим YAML напрямую
            try
            {
                string text = File.ReadAllText(path);
                if (text.Contains("  GlobalObjectIdHash: 0\n") || text.Contains("  GlobalObjectIdHash: 0\r\n"))
                {
                    text = text.Replace("  GlobalObjectIdHash: 0", "  GlobalObjectIdHash: " + hash);
                    File.WriteAllText(path, text);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                string after = File.ReadAllText(path);
                int i = after.IndexOf("  GlobalObjectIdHash: ");
                Debug.Log($"[Vykhod] {path}: GlobalObjectIdHash = {hash}, на диске: {(i >= 0 ? after.Substring(i + 22, 12).Split('\n')[0].Trim() : "?")}, m_Script пустых: {CountMissing(after)}");
            }
            catch (Exception e) { Debug.LogWarning("hash yaml: " + e.Message); }
        }

        static int CountMissing(string yaml) { int n = 0, i = 0; while ((i = yaml.IndexOf("m_Script: {fileID: 0}", i)) >= 0) { n++; i += 10; } return n; }

        static readonly System.Collections.Generic.Dictionary<string, Type[]> prefabScripts = new System.Collections.Generic.Dictionary<string, Type[]>
        {
            ["Player"] = new[] { typeof(OwnerNetworkTransform), typeof(PlayerNet), typeof(FPSController), typeof(PlayerWeapons), typeof(PlayerInteract) },
            ["Enemy"] = new[] { typeof(EnemyAI) },
            ["Session"] = new[] { typeof(RaidSession) },
            ["WorldItem"] = new[] { typeof(WorldItem) },
        };

        static string ScriptGuid(Type t)
        {
            var files = Directory.GetFiles("Assets/_Game/Scripts", t.Name + ".cs", SearchOption.AllDirectories);
            if (files.Length == 0) { Debug.LogError($"[Vykhod] Нет файла {t.Name}.cs — имя файла должно совпадать с классом"); return null; }
            string p = files[0].Replace('\\', '/');
            string g = AssetDatabase.AssetPathToGUID(p);
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(p);
            Debug.Log($"[Vykhod] скрипт {p}: guid {g}, MonoScript {(ms != null ? "есть" : "НЕТ")}, класс {(ms != null && ms.GetClass() != null ? ms.GetClass().Name : "?")}");
            return string.IsNullOrEmpty(g) ? null : g;
        }

        /// <summary>В batchmode m_Script у собственных компонентов остаётся пустым — прописываем GUID скриптов в YAML по порядку компонентов.</summary>
        static void PatchScripts(string path, Type[] types)
        {
            if (!File.Exists(path)) return;
            string yaml = File.ReadAllText(path); const string marker = "m_Script: {fileID: 0}";
            var sb = new System.Text.StringBuilder(); int idx = 0, ti = 0;
            while (true)
            {
                int k = yaml.IndexOf(marker, idx, StringComparison.Ordinal);
                if (k < 0 || ti >= types.Length) { sb.Append(yaml.Substring(idx)); break; }
                sb.Append(yaml, idx, k - idx);
                string g = ScriptGuid(types[ti++]);
                sb.Append(g != null ? "m_Script: {fileID: 11500000, guid: " + g + ", type: 3}" : marker);
                idx = k + marker.Length;
            }
            File.WriteAllText(path, sb.ToString());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("Vykhod/Fix Network Prefabs (hashes + scripts)")]
        public static void FixAllHashes()
        {
            foreach (var kv in prefabScripts) { string p = $"{PrefabDir}/{kv.Key}.prefab"; PatchScripts(p, kv.Value); FixHash(p); }
        }

        static void MakePlayer()
        {
            if (File.Exists($"{PrefabDir}/Player.prefab")) return;
            var go = new GameObject("Player");
            var cc = go.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0);
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<OwnerNetworkTransform>(); nt.Interpolate = true; nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false; nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;
            var head = new GameObject("Head"); head.transform.SetParent(go.transform, false); head.transform.localPosition = new Vector3(0, 1.65f, 0);
            go.AddComponent<PlayerNet>(); go.AddComponent<FPSController>(); go.AddComponent<PlayerWeapons>(); go.AddComponent<PlayerInteract>();
            SavePrefab(go, "Player");
        }

        static void MakeEnemy()
        {
            if (File.Exists($"{PrefabDir}/Enemy.prefab")) return;
            var go = new GameObject("Enemy");
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>(); nt.Interpolate = true; nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false; nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;
            go.AddComponent<EnemyAI>();
            SavePrefab(go, "Enemy");
        }

        static void MakeSession()
        {
            if (File.Exists($"{PrefabDir}/Session.prefab")) return;
            var go = new GameObject("Session"); go.AddComponent<NetworkObject>(); go.AddComponent<RaidSession>();
            SavePrefab(go, "Session");
        }

        static void MakeWorldItem()
        {
            if (File.Exists($"{PrefabDir}/WorldItem.prefab")) return;
            var go = new GameObject("WorldItem"); go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>(); nt.Interpolate = false; nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            go.AddComponent<WorldItem>();
            SavePrefab(go, "WorldItem");
        }

        static void MakeScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var boot = new GameObject("Boot (игра стартует автоматически из GameManager)");
                var cam = new GameObject("EditorCamera"); var c = cam.AddComponent<Camera>(); c.backgroundColor = new Color(0.05f, 0.06f, 0.07f); c.clearFlags = CameraClearFlags.SolidColor; cam.AddComponent<AudioListener>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "VYKHOD"; PlayerSettings.companyName = "Vykhod Team";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.vykhod.game");
            // macOS: Metal, оба типа CPU (Apple Silicon + Intel)
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, new[] { UnityEngine.Rendering.GraphicsDeviceType.Metal });
            SetMacUniversal();
            // старый Input Manager (Input.GetAxis)
            try
            {
                var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
                if (ps != null) { var so = new SerializedObject(ps); var p = so.FindProperty("activeInputHandler"); if (p != null) { p.intValue = 0; so.ApplyModifiedPropertiesWithoutUndo(); } }
            }
            catch (Exception e) { Debug.LogWarning("activeInputHandler: " + e.Message); }
            // убрать всплывающее окно разрешения
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            QualitySettings.vSyncCount = 0;
        }

        static void SetMacUniversal()
        {
            try
            {
                var t = Type.GetType("UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandalone.Extensions");
                if (t == null) { Debug.Log("[Vykhod] Модуль macOS не установлен — архитектуру Mac выставим позже."); return; }
                var prop = t.GetProperty("architecture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var enumType = prop.PropertyType; var val = Enum.Parse(enumType, "x64ARM64");
                prop.SetValue(null, val);
            }
            catch (Exception e) { Debug.LogWarning("Mac architecture: " + e.Message); }
        }

        [MenuItem("Vykhod/Build/Windows x64")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/VYKHOD.exe");

        [MenuItem("Vykhod/Build/macOS (Apple Silicon + Intel)")]
        public static void BuildMac() { SetMacUniversal(); Build(BuildTarget.StandaloneOSX, "Builds/macOS/VYKHOD.app"); }

        [MenuItem("Vykhod/Build/Both")]
        public static void BuildBoth() { BuildWindows(); BuildMac(); }

        static void Build(BuildTarget target, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var opts = new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = path, target = target, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Vykhod] Сборка {target}: {report.summary.result}, {report.summary.totalSize / 1048576} МБ → {path}");
        }

        [MenuItem("Vykhod/Open Main Scene")]
        public static void OpenScene() { if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath); }

        [MenuItem("Vykhod/Regenerate Data (run Tools/gen_data.py)")]
        public static void RegenData()
        {
            var psi = new System.Diagnostics.ProcessStartInfo("python", "-I \"" + Path.GetFullPath("Tools/gen_data.py") + "\"") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            try { var p = System.Diagnostics.Process.Start(psi); Debug.Log(p.StandardOutput.ReadToEnd()); p.WaitForExit(); AssetDatabase.Refresh(); } catch (Exception e) { Debug.LogError("Нужен python в PATH: " + e.Message); }
        }
    }
}
