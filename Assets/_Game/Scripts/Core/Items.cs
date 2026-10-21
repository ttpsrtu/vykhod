using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vykhod
{
    /// <summary>Экземпляр предмета: стак патронов, оружие с модами, контейнер с содержимым.</summary>
    public class ItemInstance
    {
        public string id;
        public int count = 1;
        public float durability = -1;          // оружие/броня; -1 = не применимо
        public int uses = -1;                   // медицина/ключи; -1 = не применимо
        public Dictionary<string, string> mods = new Dictionary<string, string>(); // слот -> id мода
        public string ammoId = "";              // патроны, заряженные в оружие
        public int ammoInMag;
        public List<ItemInstance> contents = new List<ItemInstance>(); // контейнеры
        public int x, y;                        // позиция в сетке
        public bool insured;
        public bool found;                      // найден в рейде (для квестов "найти")

        public ItemInstance() { }
        public ItemInstance(string id, int count = 1)
        {
            this.id = id; this.count = count;
            var info = Info;
            if (info != null)
            {
                if (info.IsWeapon) { durability = 100; ammoInMag = 0; }
                if (info.IsItem)
                {
                    if (info.item.Has("durability")) durability = info.item.P("durability");
                    if (info.item.Has("uses")) uses = (int)info.item.P("uses");
                }
            }
        }

        public ItemInfo Info => Database.Info(id);
        public string Name => Info?.name ?? id;
        public int W => Info?.w ?? 1;
        public int H => Info?.h ?? 1;
        public bool IsWeapon => Info != null && Info.IsWeapon;
        public bool IsAmmo => Info != null && Info.IsAmmo;
        public bool IsMod => Info != null && Info.IsMod;
        public string Type => Info?.type ?? "";
        public int MaxStack => Info?.stack ?? 1;
        public bool Stackable => MaxStack > 1;
        public bool IsContainer => Info != null && Info.IsItem && (Info.type == "backpack" || Info.type == "rig" || Info.type == "container");

        public float Weight
        {
            get
            {
                float w = (Info?.weight ?? 0.5f) * count;
                foreach (var m in mods.Values) w += Database.Mod(m)?.weight ?? 0;
                foreach (var c in contents) w += c.Weight;
                if (IsWeapon) w += ammoInMag * 0.012f;
                return w;
            }
        }

        public int Value
        {
            get
            {
                int v = (Info?.price ?? 0) * count;
                foreach (var m in mods.Values) v += Database.Mod(m)?.price ?? 0;
                foreach (var c in contents) v += c.Value;
                if (durability >= 0 && Info != null && (IsWeapon || Info.item != null && Info.item.Has("durability")))
                {
                    float max = IsWeapon ? 100 : Info.item.P("durability", 100);
                    v = (int)(v * Mathf.Lerp(0.3f, 1f, max > 0 ? durability / max : 1));
                }
                return v;
            }
        }

        public int ModCount => mods.Count;

        public ItemInstance Clone()
        {
            var c = new ItemInstance { id = id, count = count, durability = durability, uses = uses, ammoId = ammoId, ammoInMag = ammoInMag, x = x, y = y, insured = insured, found = found };
            foreach (var kv in mods) c.mods[kv.Key] = kv.Value;
            foreach (var it in contents) c.contents.Add(it.Clone());
            return c;
        }

        public Dictionary<string, object> ToJson()
        {
            var d = new Dictionary<string, object> { ["id"] = id, ["count"] = count, ["x"] = x, ["y"] = y };
            if (durability >= 0) d["dur"] = durability;
            if (uses >= 0) d["uses"] = uses;
            if (mods.Count > 0) d["mods"] = new Dictionary<string, object>(mods.ToDictionary(k => k.Key, k => (object)k.Value));
            if (!string.IsNullOrEmpty(ammoId)) { d["ammo"] = ammoId; d["mag"] = ammoInMag; }
            if (contents.Count > 0) d["contents"] = contents.Select(c => (object)c.ToJson()).ToList();
            if (insured) d["insured"] = true;
            if (found) d["found"] = true;
            return d;
        }

        public static ItemInstance FromJson(Dictionary<string, object> d)
        {
            if (d == null) return null;
            var it = new ItemInstance { id = J.S(d, "id"), count = J.I(d, "count", 1), x = J.I(d, "x"), y = J.I(d, "y") };
            it.durability = J.F(d, "dur", -1); it.uses = J.I(d, "uses", -1);
            it.mods = J.SD(d, "mods"); it.ammoId = J.S(d, "ammo"); it.ammoInMag = J.I(d, "mag");
            it.insured = J.B(d, "insured"); it.found = J.B(d, "found");
            foreach (var c in J.DL(d, "contents")) { var ci = FromJson(c); if (ci != null) it.contents.Add(ci); }
            return it;
        }

        public string ToJsonString() => Json.Serialize(ToJson());
        public static ItemInstance FromJsonString(string s) => FromJson(J.D(Json.Parse(s)));

        /// <summary>Сетка контейнера (рюкзак/разгрузка/кейс), построенная из contents.</summary>
        public Grid AsGrid()
        {
            var info = Info;
            int w = (int)(info?.item?.P("cellsW", 4) ?? 4), h = (int)(info?.item?.P("cellsH", 4) ?? 4);
            w += (int)Stats.Get(info?.type == "backpack" ? "bagBonus" : info?.item?.P("secure", 0) > 0 ? "secureBonus" : "none");
            var g = new Grid(w, h, contents);
            g.only = info?.item?.PS("only") ?? "";
            return g;
        }
    }

    /// <summary>Сетка ячеек с размещением предметов (как в Duckov/Tarkov).</summary>
    public class Grid
    {
        public int W, H;
        public List<ItemInstance> Items;
        public string only = "";

        public Grid(int w, int h, List<ItemInstance> backing = null) { W = w; H = h; Items = backing ?? new List<ItemInstance>(); }

        public bool Accepts(ItemInstance it)
        {
            if (string.IsNullOrEmpty(only)) return true;
            return it.Type == only || it.Info?.kind == only;
        }

        public bool Fits(ItemInstance it, int x, int y, ItemInstance ignore = null)
        {
            if (x < 0 || y < 0 || x + it.W > W || y + it.H > H) return false;
            foreach (var o in Items)
            {
                if (o == ignore || o == it) continue;
                if (x < o.x + o.W && x + it.W > o.x && y < o.y + o.H && y + it.H > o.y) return false;
            }
            return true;
        }

        public ItemInstance At(int x, int y)
        {
            foreach (var o in Items) if (x >= o.x && x < o.x + o.W && y >= o.y && y < o.y + o.H) return o;
            return null;
        }

        /// <summary>Пытается добавить: сначала в стаки, потом в первую свободную область. Возвращает true, если положили весь предмет.</summary>
        public bool TryAdd(ItemInstance it)
        {
            if (it == null || !Accepts(it)) return false;
            if (it.Stackable)
            {
                foreach (var o in Items)
                {
                    if (o.id != it.id || o.count >= o.MaxStack) continue;
                    int take = Math.Min(it.count, o.MaxStack - o.count);
                    o.count += take; it.count -= take;
                    if (it.count <= 0) return true;
                }
            }
            for (int y = 0; y <= H - it.H; y++)
                for (int x = 0; x <= W - it.W; x++)
                    if (Fits(it, x, y)) { it.x = x; it.y = y; Items.Add(it); return true; }
            return false;
        }

        public bool PlaceAt(ItemInstance it, int x, int y)
        {
            if (!Accepts(it) || !Fits(it, x, y)) return false;
            it.x = x; it.y = y;
            if (!Items.Contains(it)) Items.Add(it);
            return true;
        }

        public bool Remove(ItemInstance it) => Items.Remove(it);

        public int FreeCells
        {
            get { int used = 0; foreach (var o in Items) used += o.W * o.H; return W * H - used; }
        }

        public float Weight { get { float w = 0; foreach (var o in Items) w += o.Weight; return w; } }
        public int Value { get { int v = 0; foreach (var o in Items) v += o.Value; return v; } }

        public int CountOf(string id) { int n = 0; foreach (var o in Items) if (o.id == id) n += o.count; return n; }

        /// <summary>Удаляет n штук предмета id (учитывая стаки). Возвращает сколько реально удалено.</summary>
        public int Take(string id, int n)
        {
            int taken = 0;
            for (int i = Items.Count - 1; i >= 0 && taken < n; i--)
            {
                var o = Items[i];
                if (o.id != id) continue;
                int t = Math.Min(o.count, n - taken);
                o.count -= t; taken += t;
                if (o.count <= 0) Items.RemoveAt(i);
            }
            return taken;
        }

        public void Compact()
        {
            var all = Items.ToList(); Items.Clear();
            all.Sort((a, b) => (b.W * b.H).CompareTo(a.W * a.H));
            foreach (var it in all) if (!TryAdd(it)) Items.Add(it);
        }

        public Dictionary<string, object> ToJson() => new Dictionary<string, object> { ["w"] = W, ["h"] = H, ["items"] = Items.Select(i => (object)i.ToJson()).ToList() };
        public static Grid FromJson(Dictionary<string, object> d, int defW, int defH)
        {
            var g = new Grid(d != null ? J.I(d, "w", defW) : defW, d != null ? J.I(d, "h", defH) : defH);
            if (d != null) foreach (var it in J.DL(d, "items")) { var i = ItemInstance.FromJson(it); if (i != null) g.Items.Add(i); }
            return g;
        }
    }

    /// <summary>Слоты экипировки персонажа.</summary>
    public class Equipment
    {
        public static readonly string[] SlotNames = { "primary", "secondary", "holster", "melee", "helmet", "armor", "rig", "backpack", "secure", "headgear" };
        public static readonly Dictionary<string, string> SlotTitles = new Dictionary<string, string>
        {
            ["primary"] = "Основное", ["secondary"] = "Запасное", ["holster"] = "Кобура", ["melee"] = "Нож", ["helmet"] = "Шлем",
            ["armor"] = "Броня", ["rig"] = "Разгрузка", ["backpack"] = "Рюкзак", ["secure"] = "Контейнер", ["headgear"] = "Оптика/ПНВ"
        };
        public Dictionary<string, ItemInstance> slots = new Dictionary<string, ItemInstance>();

        public ItemInstance Get(string slot) => slots.TryGetValue(slot, out var i) ? i : null;
        public void Set(string slot, ItemInstance it) { if (it == null) slots.Remove(slot); else slots[slot] = it; }

        public static bool SlotAccepts(string slot, ItemInstance it)
        {
            if (it == null) return true;
            var info = it.Info; if (info == null) return false;
            switch (slot)
            {
                case "primary": case "secondary": return info.IsWeapon && info.weapon.cls != "pistol" && info.weapon.cls != "melee";
                case "holster": return info.IsWeapon && info.weapon.cls == "pistol";
                case "melee": return info.IsWeapon && info.weapon.cls == "melee";
                case "helmet": return info.type == "helmet";
                case "armor": return info.type == "armor";
                case "rig": return info.type == "rig";
                case "backpack": return info.type == "backpack";
                case "secure": return info.type == "container" && info.item.P("secure") > 0;
                case "headgear": return info.type == "tool" && info.item.P("night") > 0;
            }
            return false;
        }

        public float Weight { get { float w = 0; foreach (var i in slots.Values) w += i.Weight; return w; } }
        public int Value { get { int v = 0; foreach (var i in slots.Values) v += i.Value; return v; } }

        public Dictionary<string, object> ToJson() => slots.ToDictionary(k => k.Key, k => (object)k.Value.ToJson());
        public static Equipment FromJson(Dictionary<string, object> d)
        {
            var e = new Equipment();
            if (d != null) foreach (var kv in d) { var it = ItemInstance.FromJson(kv.Value as Dictionary<string, object>); if (it != null) e.slots[kv.Key] = it; }
            return e;
        }
    }

    /// <summary>Экипировка + карманы: всё, что персонаж несёт в рейд.</summary>
    public class Loadout
    {
        public Equipment eq = new Equipment();
        public Grid pockets = new Grid(4, 1);

        public Grid RigGrid => eq.Get("rig")?.AsGrid();
        public Grid BackpackGrid => eq.Get("backpack")?.AsGrid();
        public Grid SecureGrid => eq.Get("secure")?.AsGrid();

        public IEnumerable<KeyValuePair<string, Grid>> Grids()
        {
            yield return new KeyValuePair<string, Grid>("Карманы", pockets);
            var r = RigGrid; if (r != null) yield return new KeyValuePair<string, Grid>("Разгрузка", r);
            var b = BackpackGrid; if (b != null) yield return new KeyValuePair<string, Grid>("Рюкзак", b);
            var s = SecureGrid; if (s != null) yield return new KeyValuePair<string, Grid>("Контейнер", s);
        }

        public bool TryAdd(ItemInstance it)
        {
            foreach (var g in Grids()) if (g.Value.TryAdd(it)) return true;
            return false;
        }

        public IEnumerable<ItemInstance> AllItems()
        {
            foreach (var i in eq.slots.Values) yield return i;
            foreach (var g in Grids()) foreach (var i in g.Value.Items) yield return i;
        }

        public int CountOf(string id) { int n = 0; foreach (var g in Grids()) n += g.Value.CountOf(id); return n; }
        public int Take(string id, int n) { int t = 0; foreach (var g in Grids()) { t += g.Value.Take(id, n - t); if (t >= n) break; } return t; }
        public bool RemoveItem(ItemInstance it)
        {
            foreach (var g in Grids()) if (g.Value.Remove(it)) return true;
            foreach (var kv in eq.slots.ToList()) if (kv.Value == it) { eq.slots.Remove(kv.Key); return true; }
            return false;
        }
        public float Weight { get { float w = eq.Weight; w += pockets.Weight; return w; } }
        public int Value { get { int v = eq.Value + pockets.Value; return v; } }
        public bool HasKey(string keyId) => CountOf(keyId) > 0;

        public Dictionary<string, object> ToJson() => new Dictionary<string, object> { ["eq"] = eq.ToJson(), ["pockets"] = pockets.ToJson() };
        public static Loadout FromJson(Dictionary<string, object> d)
        {
            var l = new Loadout();
            if (d == null) return l;
            l.eq = Equipment.FromJson(J.D(d, "eq"));
            l.pockets = Grid.FromJson(J.D(d, "pockets"), 4, 1);
            return l;
        }
        public Loadout Clone() => FromJson(J.D(Json.Parse(Json.Serialize(ToJson()))));
    }
}
