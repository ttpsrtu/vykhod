using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>
    /// Поиск скачанных ассетов (Resources/Imported/asset_index.json) по подсказкам + плейсхолдеры из примитивов,
    /// если ассет не найден. Игра всегда работает, даже без единого скачанного файла.
    /// </summary>
    public static class AssetCatalog
    {
        class Pack { public string name, license; public List<string> models = new List<string>(), audio = new List<string>(), textures = new List<string>(); }
        static readonly List<Pack> packs = new List<Pack>();
        static readonly List<string> allModels = new List<string>(), allAudio = new List<string>(), allTextures = new List<string>();
        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

        /// <summary>Текстура/спрайт по подсказке (имя файла содержит hint), packHint — альтернативы через '|'.</summary>
        public static Texture2D FindTexture(string hint, string packHint = null)
        {
            Load();
            if (string.IsNullOrEmpty(hint) || allTextures.Count == 0) return null;
            string key = hint + "@" + packHint;
            if (texCache.TryGetValue(key, out var cached)) return cached;
            IEnumerable<string> pool = allTextures;
            if (!string.IsNullOrEmpty(packHint))
            {
                var alts = packHint.ToLowerInvariant().Split('|');
                var pp = allTextures.Where(m => { var ml = m.ToLowerInvariant(); foreach (var a in alts) if (ml.Contains(a)) return true; return false; }).ToList();
                if (pp.Count > 0) pool = pp;
            }
            string h = hint.ToLowerInvariant();
            string path = pool.FirstOrDefault(m => System.IO.Path.GetFileName(m).ToLowerInvariant() == h)
                       ?? pool.FirstOrDefault(m => System.IO.Path.GetFileName(m).ToLowerInvariant().StartsWith(h))
                       ?? pool.FirstOrDefault(m => System.IO.Path.GetFileName(m).ToLowerInvariant().Contains(h));
            var tex = path != null ? Resources.Load<Texture2D>(path) : null;
            texCache[key] = tex;
            return tex;
        }
        static readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        static readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
        static bool loaded;
        static Font font;

        static readonly Dictionary<string, string[]> synonyms = new Dictionary<string, string[]>
        {
            ["pistol"] = new[] { "pistol", "handgun", "revolver", "glock" }, ["revolver"] = new[] { "revolver", "pistol" },
            ["smg"] = new[] { "smg", "submachine", "uzi", "mp5", "machine-pistol" }, ["rifle"] = new[] { "rifle", "ak", "assault", "ar-", "m4", "carbine" },
            ["rifle_short"] = new[] { "carbine", "rifle", "ak", "assault" }, ["shotgun"] = new[] { "shotgun" }, ["sniper"] = new[] { "sniper", "rifle" },
            ["knife"] = new[] { "knife", "dagger", "blade" }, ["axe"] = new[] { "axe", "hatchet" }, ["crowbar"] = new[] { "crowbar", "bar" }, ["hammer"] = new[] { "hammer", "mallet" },
            ["suppressor"] = new[] { "suppressor", "silencer" }, ["scope"] = new[] { "scope", "sight" }, ["reddot"] = new[] { "red-dot", "reddot", "sight", "dot" },
            ["grip"] = new[] { "grip" }, ["mag"] = new[] { "magazine", "clip" }, ["muzzle"] = new[] { "muzzle", "brake", "compensator" },
            ["tree"] = new[] { "tree", "pine", "oak", "birch" }, ["bush"] = new[] { "bush", "shrub" }, ["rock"] = new[] { "rock", "stone" },
            ["car"] = new[] { "car", "sedan", "van", "truck", "vehicle" }, ["crate"] = new[] { "crate", "box", "chest" }, ["barrel"] = new[] { "barrel", "drum" },
            ["locker"] = new[] { "locker", "cabinet", "wardrobe" }, ["safe"] = new[] { "safe", "vault" }, ["table"] = new[] { "table", "desk" },
            ["chair"] = new[] { "chair", "stool" }, ["shelf"] = new[] { "shelf", "bookcase", "rack" }, ["bed"] = new[] { "bed", "bunk" },
            ["lamp"] = new[] { "lamp", "light" }, ["fence"] = new[] { "fence", "wall" }, ["pallet"] = new[] { "pallet" }, ["container"] = new[] { "container" },
            ["character"] = new[] { "character", "man", "soldier", "zombie", "human", "survivor" },
            ["bag"] = new[] { "bag", "backpack" }, ["medkit"] = new[] { "medkit", "medical", "first-aid", "aid" }, ["bottle"] = new[] { "bottle", "can", "drink" },
            ["food"] = new[] { "food", "can", "bread", "burger" }, ["valuable"] = new[] { "gem", "gold", "coin", "jewel", "trophy" },
            ["key"] = new[] { "key" }, ["ammo"] = new[] { "ammo", "bullet", "magazine", "clip" }, ["armor"] = new[] { "armor", "vest" },
            ["helmet"] = new[] { "helmet", "hat", "cap" }, ["throwable"] = new[] { "grenade" }, ["tool"] = new[] { "tool", "wrench", "hammer" },
            ["barter"] = new[] { "bolt", "screw", "tool", "wire", "box" }, ["med"] = new[] { "medkit", "medical", "bandage", "aid" },
        };

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            var ta = Resources.Load<TextAsset>("Imported/asset_index");
            if (ta == null) { Debug.LogWarning("[Assets] asset_index.json не найден — используем плейсхолдеры. Запустите Tools/fetch_assets.py"); return; }
            var root = Json.Parse(ta.text);
            var list = root is List<object> ? J.DL(root) : J.DL(J.D(root), "packs");
            foreach (var d in list)
            {
                var p = new Pack { name = J.S(d, "pack"), license = J.S(d, "license"), models = J.SL(d, "models"), audio = J.SL(d, "audio"), textures = J.SL(d, "textures") };
                packs.Add(p); allModels.AddRange(p.models); allAudio.AddRange(p.audio); allTextures.AddRange(p.textures);
            }
            Debug.Log($"[Assets] пакетов {packs.Count}, моделей {allModels.Count}, звуков {allAudio.Count}");
        }

        public static int ModelCount => allModels.Count;

        /// <summary>Ищет путь модели по подсказке. packHint — часть имени пакета (например "weapon").</summary>
        public static string FindModel(string hint, string packHint = null)
        {
            Load();
            if (string.IsNullOrEmpty(hint) || allModels.Count == 0) return null;
            IEnumerable<string> pool = allModels;
            if (!string.IsNullOrEmpty(packHint))
            {
                var alts = packHint.ToLowerInvariant().Split('|');
                var pp = allModels.Where(m => { var ml = m.ToLowerInvariant(); foreach (var a in alts) if (ml.Contains(a)) return true; return false; }).ToList();
                if (pp.Count > 0) pool = pp;
            }
            string h = hint.ToLowerInvariant();
            // подсказка с '/' — часть пути (например "guns_oga/machinegun/MachineGun"), чтобы различать одноимённые файлы разных паков
            if (h.Contains("/")) return pool.FirstOrDefault(m => m.ToLowerInvariant().EndsWith(h)) ?? pool.FirstOrDefault(m => m.ToLowerInvariant().Contains(h));
            var keys = synonyms.TryGetValue(h, out var s) ? s : new[] { h };
            foreach (var k in keys)
            {
                var exact = pool.FirstOrDefault(m => System.IO.Path.GetFileName(m).ToLowerInvariant() == k);
                if (exact != null) return exact;
                var starts = pool.FirstOrDefault(m => System.IO.Path.GetFileName(m).ToLowerInvariant().StartsWith(k));
                if (starts != null) return starts;
                var any = pool.FirstOrDefault(m => System.IO.Path.GetFileName(m).ToLowerInvariant().Contains(k));
                if (any != null) return any;
            }
            return null;
        }

        public static List<string> FindModels(string nameContains, string packContains = null)
        {
            Load();
            string n = nameContains?.ToLowerInvariant() ?? "", p = packContains?.ToLowerInvariant() ?? "";
            return allModels.Where(m => (p == "" || m.ToLowerInvariant().Contains(p)) && (n == "" || System.IO.Path.GetFileName(m).ToLowerInvariant().Contains(n))).ToList();
        }

        public static GameObject LoadPrefab(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (prefabCache.TryGetValue(path, out var g)) return g;
            g = Resources.Load<GameObject>(path);
            prefabCache[path] = g;
            return g;
        }

        /// <summary>Создаёт экземпляр модели, нормализуя размер (targetSize = макс. габарит в метрах; 0 = как есть).</summary>
        public static GameObject Spawn(string path, Transform parent, float targetSize = 0, bool colliders = false, bool center = false, int variant = 0)
        {
            var prefab = LoadPrefab(path);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            if (path.Contains("polyhaven_models")) ApplyPolyhavenTextures(go, path);
            else if (path.Contains("guns_oga") || path.Contains("guns_flat") || path.Contains("quaternius_nature") || path.Contains("quaternius_guns")) FixImportedMaterials(go, path, variant);
            Normalize(go, targetSize, colliders);
            if (center)
            {
                // центр модели — в точку привязки (у многих FBX пивот сбоку)
                var b = MeshBounds(go);
                if (b.size.sqrMagnitude > 0) go.transform.position += go.transform.position - b.center;
            }
            return go;
        }

        /// <summary>
        /// Ориентирует модель оружия: самая длинная ось → вперёд (+Z), самая тонкая → вбок (+X), средняя → вверх (+Y).
        /// Если меш читаемый, определяет знаки: рукоять/магазин висят вниз, дуло — тонкий конец.
        /// </summary>
        public static void OrientGun(GameObject go)
        {
            var mfs = go.GetComponentsInChildren<MeshFilter>();
            if (mfs.Length == 0) return;
            // границы в локальном пространстве go
            var toLocal = go.transform.worldToLocalMatrix; bool any = false; var b = new Bounds();
            var pts = new List<Vector3>();
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                var m = toLocal * mf.transform.localToWorldMatrix;
                if (mf.sharedMesh.isReadable) { var vs = mf.sharedMesh.vertices; for (int i = 0; i < vs.Length; i += Mathf.Max(1, vs.Length / 4000)) pts.Add(m.MultiplyPoint3x4(vs[i])); }
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++) { var c = m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z)); if (!any) { b = new Bounds(c, Vector3.zero); any = true; } else b.Encapsulate(c); }
            }
            if (!any) return;
            var size = b.size; int longest = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            int shortest = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            if (shortest == longest) shortest = (longest + 1) % 3;
            int middle = 3 - longest - shortest;
            Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            Vector3 fwd = Axis(longest), up = Axis(middle);
            if (pts.Count > 50)
            {
                // вверх: центр масс ниже центра габаритов (рукоять и магазин снизу)
                float meanUp = 0; foreach (var p in pts) meanUp += Vector3.Dot(p - b.center, up); meanUp /= pts.Count;
                if (meanUp < 0) up = -up; // магазин и рукоять тянут центр масс вниз относительно оси ствола
                // вперёд: конец с меньшим разбросом по вертикали — дуло
                float frontSpread = 0, backSpread = 0; int nf = 0, nb = 0; float half = Vector3.Dot(size, Vector3.Scale(fwd, fwd)) * 0.5f;
                foreach (var p in pts) { float a = Vector3.Dot(p - b.center, fwd); float h = Mathf.Abs(Vector3.Dot(p - b.center, up)); if (a > half * 0.5f) { frontSpread += h; nf++; } else if (a < -half * 0.5f) { backSpread += h; nb++; } }
                if (nf > 0 && nb > 0 && frontSpread / nf < backSpread / nb) fwd = -fwd; // дуло — тонкий конец, приклад/рукоять — широкий
            }
            var rot = Quaternion.LookRotation(fwd, up);           // поворачивает Z→fwd, Y→up
            go.transform.localRotation = Quaternion.Inverse(rot); // значит обратный поворот ставит fwd→Z, up→Y
        }

        static readonly Dictionary<string, Material> phMatCache = new Dictionary<string, Material>();
        /// <summary>Модели Poly Haven: FBX не всегда подхватывает текстуры из textures/ — назначаем диффуз/нормаль/шероховатость сами.</summary>
        static void ApplyPolyhavenTextures(GameObject go, string modelPath)
        {
            string dir = modelPath.Substring(0, modelPath.LastIndexOf('/') + 1).ToLowerInvariant();
            if (!phMatCache.TryGetValue(dir, out var mat))
            {
                var texs = allTextures.Where(t => t.ToLowerInvariant().StartsWith(dir)).ToList();
                string diff = texs.FirstOrDefault(t => t.ToLowerInvariant().Contains("_diff")), nor = texs.FirstOrDefault(t => t.ToLowerInvariant().Contains("_nor")), rough = texs.FirstOrDefault(t => t.ToLowerInvariant().Contains("_rough"));
                if (diff == null) { phMatCache[dir] = null; return; }
                mat = new Material(Shader.Find("Standard")) { enableInstancing = true };
                mat.mainTexture = Resources.Load<Texture2D>(diff);
                var n = nor != null ? Resources.Load<Texture2D>(nor) : null; if (n != null) { mat.EnableKeyword("_NORMALMAP"); mat.SetTexture("_BumpMap", n); }
                mat.SetFloat("_Glossiness", rough != null ? 0.35f : 0.2f); mat.SetFloat("_Metallic", 0f);
                phMatCache[dir] = mat;
            }
            if (mat == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; }
        }

        /// <summary>Путь модели оружия: сначала конкретная модель из данных (АК → AK, M4A1 → M4A1…), затем запасная по классу.</summary>
        public static string GunModel(WeaponDef def)
        {
            if (def == null) return null;
            var p = !string.IsNullOrEmpty(def.model) ? FindModel(def.model, "guns|weapon") : null;
            if (p == null && !string.IsNullOrEmpty(def.modelClass)) p = FindModel(def.modelClass, "weapon|guns");
            return p;
        }

        static string FileStem(string path) { int i = path.LastIndexOf('/'); return (i >= 0 ? path.Substring(i + 1) : path).ToLowerInvariant(); }
        static readonly Dictionary<string, Material> fixMatCache = new Dictionary<string, Material>();
        static Material gunmetal;

        /// <summary>
        /// Материалы импортированных моделей: текстуры из папки пакета (Albedo/Diffuse + Normal, или по имени материала как у Quaternius Nature),
        /// прозрачность листвы (cutout), а у нетекстурированных моделей (byzmod, Glock, Soviet_*) вместо белого «Default-Material» — воронёный металл.
        /// </summary>
        public static void FixImportedMaterials(GameObject go, string modelPath, int variant = 0)
        {
            string lp = modelPath.ToLowerInvariant();
            string dir = lp.Substring(0, lp.LastIndexOf('/') + 1);
            string baseDir = dir;
            foreach (var sub in new[] { "/fbx/", "/fbx (unity)/", "/obj/" }) if (baseDir.EndsWith(sub)) { baseDir = baseDir.Substring(0, baseDir.Length - sub.Length + 1); break; }
            var texs = allTextures.Where(t => t.ToLowerInvariant().StartsWith(baseDir)).ToList();
            if (gunmetal == null) gunmetal = Mat(new Color(0.16f, 0.17f, 0.19f), 0.45f);
            bool gunPack = lp.Contains("guns_");
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < ms.Length; i++)
                {
                    var m = ms[i]; string mn = m != null ? m.name.ToLowerInvariant().Replace(" (instance)", "") : "";
                    bool isDefault = m == null || mn.StartsWith("default") || mn == "no name" || mn.Length == 0;
                    string key = lp + "|" + mn + "|" + variant;
                    if (!fixMatCache.TryGetValue(key, out var nm))
                    {
                        nm = null; string tex = null, nor = null;
                        if (!isDefault && texs.Count > 0)
                        {
                            // по имени материала: Bark_NormalTree → Bark_NormalTree.png, Leaves_NormalTree → Leaves_NormalTree_C.png
                            tex = texs.FirstOrDefault(t => FileStem(t) == mn + "_c") ?? texs.FirstOrDefault(t => FileStem(t) == mn) ?? texs.FirstOrDefault(t => FileStem(t) == mn + "_diffuse");
                            nor = texs.FirstOrDefault(t => FileStem(t) == mn + "_normal");
                        }
                        if (tex == null && texs.Count > 0 && gunPack)
                        {
                            // по папке модели: ak/Textures/AK_1/AK_1_Albedo (variant выбирает набор), m4a1/M4A1Diffuse
                            var sets = texs.Where(t => { var f = FileStem(t); return (f.Contains("albedo") || f.Contains("diffuse") || f.EndsWith("_color") || f.Contains("basecolor")) && !t.ToLowerInvariant().Contains("ammunition"); }).OrderBy(t => t).ToList();
                            if (sets.Count > 0)
                            {
                                tex = sets[Mathf.Abs(variant) % sets.Count];
                                string tdir = tex.Substring(0, tex.LastIndexOf('/') + 1).ToLowerInvariant();
                                nor = texs.FirstOrDefault(t => t.ToLowerInvariant().StartsWith(tdir) && FileStem(t).EndsWith("_normal"));
                            }
                        }
                        if (tex != null)
                        {
                            var t2d = Resources.Load<Texture2D>(tex);
                            if (t2d != null)
                            {
                                nm = new Material(Shader.Find("Standard")) { name = mn + "_fx", enableInstancing = true, mainTexture = t2d };
                                nm.SetFloat("_Glossiness", gunPack ? 0.35f : 0.1f); nm.SetFloat("_Metallic", 0f);
                                var n2d = nor != null ? Resources.Load<Texture2D>(nor) : null; if (n2d != null) { nm.EnableKeyword("_NORMALMAP"); nm.SetTexture("_BumpMap", n2d); }
                                bool foliage = !gunPack && (mn.Contains("leaves") || mn.Contains("leaf") || mn.Contains("grass") || mn.Contains("flower") || mn.Contains("clover") || mn.Contains("petal") || mn.Contains("fern") || mn.Contains("plant") || mn.Contains("mushroom"));
                                if (foliage && UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(t2d.graphicsFormat))
                                { nm.SetFloat("_Mode", 1); nm.EnableKeyword("_ALPHATEST_ON"); nm.SetFloat("_Cutoff", 0.4f); nm.renderQueue = 2450; }
                            }
                        }
                        else if (gunPack && (isDefault || ((m.mainTexture == null || m.mainTexture.name.ToLowerInvariant().Contains("uv")) && m.HasProperty("_Color") && m.color.r > 0.85f && m.color.g > 0.85f && m.color.b > 0.85f))) nm = gunmetal;
                        fixMatCache[key] = nm;
                    }
                    if (nm != null) { ms[i] = nm; changed = true; }
                }
                if (changed) r.sharedMaterials = ms;
            }
        }

        /// <summary>Сдвинуть модель так, чтобы центр её габаритов оказался в точке привязки.</summary>
        public static void Recenter(GameObject go)
        {
            var b = MeshBounds(go);
            if (b.size.sqrMagnitude > 0) go.transform.position += go.transform.position - b.center;
        }

        /// <summary>Границы модели по мешам (надёжнее renderer.bounds сразу после Instantiate), в мировых координатах.</summary>
        public static Bounds MeshBounds(GameObject go)
        {
            bool any = false; var b = new Bounds();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                    var w = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w);
                }
            }
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>()) { if (!any) { b = smr.bounds; any = true; } else b.Encapsulate(smr.bounds); }
            return b;
        }

        public static void Normalize(GameObject go, float targetSize, bool colliders)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            var b = MeshBounds(go);
            float max = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (targetSize > 0 && max > 0.0001f) go.transform.localScale = go.transform.localScale * (targetSize / max);
            foreach (var r in rends) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; foreach (var m in r.sharedMaterials) if (m != null) m.enableInstancing = true; }
            if (colliders && go.GetComponentInChildren<Collider>() == null)
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) { var bc = mf.gameObject.AddComponent<BoxCollider>(); }
            }
            else if (!colliders) foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        }

        /// <summary>Модель по подсказке или плейсхолдер.</summary>
        public static GameObject SpawnOrPlaceholder(string hint, Transform parent, float size, Color fallbackColor, PrimitiveType prim = PrimitiveType.Cube, string packHint = null, bool colliders = false)
        {
            var path = FindModel(hint, packHint);
            var go = path != null ? Spawn(path, parent, size, colliders) : null;
            if (go == null)
            {
                go = Prim(prim, parent, Vector3.zero, Vector3.one * size, fallbackColor, colliders);
                go.name = "ph_" + hint;
            }
            return go;
        }

        // ------------------------------------------------------------ звук
        public static AudioClip FindClip(params string[] hints)
        {
            Load();
            foreach (var h in hints)
            {
                string k = h.ToLowerInvariant();
                if (clipCache.TryGetValue(k, out var c)) { if (c != null) return c; else continue; }
                var path = allAudio.FirstOrDefault(a => System.IO.Path.GetFileName(a).ToLowerInvariant().Contains(k));
                c = path != null ? Resources.Load<AudioClip>(path) : null;
                clipCache[k] = c;
                if (c != null) return c;
            }
            return null;
        }

        public static List<AudioClip> FindClips(string hint, int max = 6)
        {
            Load();
            string k = hint.ToLowerInvariant();
            var r = new List<AudioClip>();
            foreach (var a in allAudio.Where(a => System.IO.Path.GetFileName(a).ToLowerInvariant().Contains(k)).Take(max))
            { var c = Resources.Load<AudioClip>(a); if (c != null) r.Add(c); }
            return r;
        }

        // ------------------------------------------------------------ шрифт
        public static Font Font
        {
            get
            {
                if (font != null) return font;
                foreach (var n in new[] { "Imported/fonts/RussoOne-Regular", "Imported/fonts/Montserrat-ExtraBold", "Imported/fonts/Rubik-Variable", "Imported/fonts/Rubik", "Imported/fonts/Rubik-Regular", "Imported/fonts/Inter", "Imported/fonts/Roboto-Regular" })
                { font = Resources.Load<Font>(n); if (font != null) return font; }
                var fonts = Resources.LoadAll<Font>("Imported/fonts");
                if (fonts != null && fonts.Length > 0) { font = fonts[0]; return font; }
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        // ------------------------------------------------------------ материалы и примитивы
        static Shader standard;
        public static Material Mat(Color c, float smooth = 0.05f)
        {
            if (matCache.TryGetValue(c, out var m)) return m;
            if (standard == null) standard = Shader.Find("Standard");
            m = new Material(standard) { color = c, enableInstancing = true };
            m.SetFloat("_Glossiness", smooth); m.SetFloat("_Metallic", 0f);
            matCache[c] = m;
            return m;
        }

        // ------------------------------------------------------------ текстурированные материалы (триплaнар)
        static readonly Dictionary<string, Material> texMatCache = new Dictionary<string, Material>();
        static Shader triplanar;
        public static bool HasTexture(string key) => FindTexture(key + "_Color", "textures_cc0") != null || FindTexture(key, "textures_cc0") != null;

        /// <summary>Материал с CC0-текстурой по ключу (grass, asphalt, concrete, bricks, facade…). scale — метров на тайл. Без текстуры — плоский цвет.</summary>
        public static Material TexMat(string key, float scale = 2f, Color? tint = null, Color? fallback = null, int variant = 0)
        {
            Load();
            string ck = key + "|" + scale + "|" + (tint ?? Color.white) + "|" + variant;
            if (texMatCache.TryGetValue(ck, out var m)) return m;
            Texture2D tex = null;
            // ключи → реальные имена ambientCG
            string k = key.ToLowerInvariant();
            if (k == "metal") k = "corrugated"; else if (k == "woodplanks") k = "planks"; else if (k == "ground" || k == "dirt") k = "ground048"; else if (k == "sand") k = "ground033"; else if (k == "paving") k = "pavingstones";
            var candidates = allTextures.Where(t => t.ToLowerInvariant().Contains("textures_cc0") && t.ToLowerInvariant().Contains(k) && t.ToLowerInvariant().EndsWith("_color")).OrderBy(t => t).ToList();
            if (candidates.Count > 0) tex = Resources.Load<Texture2D>(candidates[Mathf.Abs(variant) % candidates.Count]);
            if (tex == null) { m = Mat(fallback ?? (tint ?? new Color(0.6f, 0.6f, 0.6f))); texMatCache[ck] = m; return m; }
            if (triplanar == null) triplanar = Shader.Find("Vykhod/Triplanar") ?? Resources.Load<Shader>("Shaders/Triplanar");
            if (triplanar == null) { m = Mat(fallback ?? Color.gray); texMatCache[ck] = m; return m; }
            m = new Material(triplanar) { enableInstancing = true };
            m.SetTexture("_MainTex", tex); m.SetFloat("_Scale", scale); m.SetColor("_Color", tint ?? Color.white);
            string colorPath = candidates[Mathf.Abs(variant) % candidates.Count];
            var nrm = Resources.Load<Texture2D>(colorPath.Substring(0, colorPath.Length - "_Color".Length) + "_Normal");
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); } else m.SetFloat("_BumpScale", 0f);
            // земля/трава: подмешиваем вторую вариацию по шуму, чтобы не было видимого тайлинга
            if (candidates.Count > 1 && (k == "grass" || k == "asphalt" || k == "concrete" || k == "ground048" || k == "gravel"))
            {
                var t2 = Resources.Load<Texture2D>(candidates[(Mathf.Abs(variant) + 1) % candidates.Count]);
                if (t2 != null) { m.SetTexture("_Tex2", t2); m.SetFloat("_Blend", 0.7f); }
            }
            texMatCache[ck] = m;
            return m;
        }

        /// <summary>Расставить готовую модель из пака как пропс (с коллайдером), с нормализацией размера.</summary>
        public static GameObject Prop(string nameHint, string packHint, Transform parent, Vector3 pos, float yaw, float size, bool collider = true)
        {
            var path = FindModel(nameHint, packHint);
            if (path == null) return null;
            var go = Spawn(path, parent, size, collider);
            if (go == null) return null;
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            // Kenney-модели стоят на своём пивоте снизу; на всякий случай опустим на землю по границам
            var b = MeshBounds(go);
            if (b.size.sqrMagnitude > 0) go.transform.position += Vector3.up * (pos.y - b.min.y);
            go.isStatic = true;
            return go;
        }

        public static Material Emissive(Color c, float power = 1.5f)
        {
            if (standard == null) standard = Shader.Find("Standard");
            var m = new Material(standard) { color = c, enableInstancing = true };
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * power);
            return m;
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 scale, Color color, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(t);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            if (!collider) { var c = go.GetComponent<Collider>(); if (c != null) Object.Destroy(c); }
            return go;
        }

        /// <summary>Плейсхолдер оружия из примитивов (используется и как база для показа модов).</summary>
        public static GameObject GunPlaceholder(WeaponDef def, Transform parent)
        {
            var root = new GameObject("gun_" + def.id); root.transform.SetParent(parent, false);
            Color body = new Color(0.12f, 0.12f, 0.13f), wood = new Color(0.45f, 0.28f, 0.14f);
            float len = def.cls == "pistol" ? 0.2f : def.cls == "smg" ? 0.45f : def.cls == "sniper" || def.cls == "lmg" ? 1.05f : def.cls == "melee" ? 0.3f : 0.75f;
            if (def.cls == "melee")
            {
                Prim(PrimitiveType.Cube, root.transform, new Vector3(0, 0, len * 0.5f), new Vector3(0.02f, 0.005f, len), new Color(0.75f, 0.75f, 0.78f), false);
                Prim(PrimitiveType.Cube, root.transform, new Vector3(0, 0, -0.05f), new Vector3(0.03f, 0.03f, 0.1f), body, false);
                return root;
            }
            Prim(PrimitiveType.Cube, root.transform, new Vector3(0, 0, len * 0.35f), new Vector3(0.04f, 0.06f, len * 0.7f), body, false); // ресивер
            Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.015f, len * 0.85f), new Vector3(0.025f, len * 0.3f, 0.025f), body, false).transform.localRotation = Quaternion.Euler(90, 0, 0); // ствол
            Prim(PrimitiveType.Cube, root.transform, new Vector3(0, -0.07f, 0.05f), new Vector3(0.03f, 0.1f, 0.04f), def.family == "ak" ? wood : body, false); // рукоять
            if (def.cls != "pistol") Prim(PrimitiveType.Cube, root.transform, new Vector3(0, -0.02f, -0.18f), new Vector3(0.035f, 0.07f, 0.3f), def.family == "ak" ? wood : body, false); // приклад
            if (def.cls != "pistol" && def.cls != "shotgun") Prim(PrimitiveType.Cube, root.transform, new Vector3(0, -0.08f, len * 0.3f), new Vector3(0.03f, 0.12f, 0.06f), body, false); // магазин
            return root;
        }
    }
}
