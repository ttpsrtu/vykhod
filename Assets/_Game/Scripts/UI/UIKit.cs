using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>
    /// Конструктор яркого «аркадного» интерфейса: скруглённые карточки, градиенты, большие кнопки, плитки предметов, попапы.
    /// Все спрайты генерируются процедурно, иконки берутся из скачанных паков, если есть.
    /// </summary>
    public static class UIKit
    {
        // ---------------------------------------------------------------- палитра
        public static readonly Color BgTop = Hex("#3b0f7a"), BgBottom = Hex("#0b2f7a");
        public static readonly Color Yellow = Hex("#ffd400"), Cyan = Hex("#19d3ff"), Pink = Hex("#ff4fd8"), Green = Hex("#5cf25a"), Orange = Hex("#ff8a2a"), Purple = Hex("#8f4bff"), Red = Hex("#ff3b5c"), Blue = Hex("#2f7bff");
        public static readonly Color Panel = new Color(0.05f, 0.08f, 0.26f, 0.78f), PanelLight = new Color(1, 1, 1, 0.09f), PanelSolid = Hex("#141a4a"), Dark = Hex("#0d1033");
        public static readonly Color TextC = Color.white, Muted = Hex("#b9c3e6"), TextDark = Hex("#1a1033");
        // совместимость со старыми именами
        public static readonly Color Bg = Hex("#120a3a"), Panel2 = new Color(0.08f, 0.1f, 0.3f, 0.95f), Accent = Hex("#ffd400"), Good = Hex("#5cf25a"), Bad = Hex("#ff3b5c"), BtnC = Hex("#2f7bff");

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static Font F => AssetCatalog.Font;
        static readonly Dictionary<int, Sprite> rounded = new Dictionary<int, Sprite>();
        static Sprite circle; static Texture2D streakTex;

        // ---------------------------------------------------------------- спрайты
        /// <summary>Белый скруглённый прямоугольник с 9-slice границей.</summary>
        public static Sprite Rounded(int radius = 24)
        {
            if (rounded.TryGetValue(radius, out var s)) return s;
            int size = radius * 2 + 16; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            rounded[radius] = s; return s;
        }

        public static Sprite Circle()
        {
            if (circle != null) return circle;
            int size = 96; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size]; float r = size / 2f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)); px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255)); }
            tex.SetPixels32(px); tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f)); return circle;
        }

        public static Texture2D GradientTex(Color a, Color b, bool vertical = true)
        {
            var tex = new Texture2D(vertical ? 1 : 256, vertical ? 256 : 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int i = 0; i < 256; i++) { var c = Color.Lerp(a, b, i / 255f); if (vertical) tex.SetPixel(0, 255 - i, c); else tex.SetPixel(i, 0, c); }
            tex.Apply(); return tex;
        }

        // ---------------------------------------------------------------- базовые элементы
        public static Canvas NewCanvas(string name, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(Game.Manager.transform, false);
            var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var sc = go.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(Transform parent, string name = "rt")
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t); }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        { rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size; }

        public static Image Img(Transform parent, Color c, string name = "img")
        { var rt = Rect(parent, name); var im = rt.gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im; }

        /// <summary>Скруглённая карточка (с перехватом кликов, чтобы не кликать сквозь).</summary>
        public static Image Card(Transform parent, Color? c = null, int radius = 24, string name = "card", bool raycast = true)
        {
            var im = Img(parent, c ?? Panel, name); im.sprite = Rounded(radius); im.type = Image.Type.Sliced; im.raycastTarget = raycast; return im;
        }

        public static RectTransform Fill(Transform parent, Color c, string name = "panel")
        { var im = Card(parent, c, 0, name); Stretch(im.rectTransform); return im.rectTransform; }

        public static RectTransform Box(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax, Color c, string name = "box", int radius = 24)
        {
            var im = Card(parent, c, radius, name); var rt = im.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offMin; rt.offsetMax = offMax; return rt;
        }

        /// <summary>Фон лобби: градиент + косые светлые полосы + тёмное основание.</summary>
        public static Image Background(Transform parent)
        {
            var bg = Img(parent, Color.white, "bg"); bg.sprite = Sprite.Create(GradientTex(BgTop, BgBottom), new Rect(0, 0, 1, 256), new Vector2(0.5f, 0.5f)); bg.type = Image.Type.Simple; Stretch(bg.rectTransform);
            var rnd = new System.Random(7);
            for (int i = 0; i < 7; i++)
            {
                var s = Img(bg.transform, new Color(1, 1, 1, 0.045f + (float)rnd.NextDouble() * 0.04f), "streak"); s.sprite = Rounded(40); s.type = Image.Type.Sliced;
                Place(s.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-900 + i * 300 + (float)rnd.NextDouble() * 120, (float)rnd.NextDouble() * 300 - 150), new Vector2(120 + (float)rnd.NextDouble() * 160, 2600));
                s.rectTransform.localRotation = Quaternion.Euler(0, 0, -28);
            }
            var g1 = Img(bg.transform, new Color(1f, 0.3f, 0.9f, 0.35f), "glow"); g1.sprite = Glow(); Place(g1.rectTransform, new Vector2(0.85f, 0.75f), Vector2.zero, new Vector2(1500, 1500));
            var g2 = Img(bg.transform, new Color(0.1f, 0.9f, 1f, 0.3f), "glow2"); g2.sprite = Glow(); Place(g2.rectTransform, new Vector2(0.1f, 0.2f), Vector2.zero, new Vector2(1600, 1600));
            var bottom = Img(bg.transform, Color.white, "bottom"); bottom.sprite = Sprite.Create(GradientTex(new Color(0, 0, 0, 0), new Color(0, 0, 0.05f, 0.55f)), new Rect(0, 0, 1, 256), new Vector2(0.5f, 0.5f)); bottom.rectTransform.anchorMin = new Vector2(0, 0); bottom.rectTransform.anchorMax = new Vector2(1, 0.35f); bottom.rectTransform.offsetMin = Vector2.zero; bottom.rectTransform.offsetMax = Vector2.zero;
            return bg;
        }

        public static Text Label(Transform parent, string text, int size = 22, TextAnchor align = TextAnchor.UpperLeft, Color? color = null, bool bold = true, float minH = 0, bool outline = false)
        {
            var rt = Rect(parent, "label"); var t = rt.gameObject.AddComponent<Text>();
            t.font = F; t.fontSize = size; t.text = text; t.alignment = align; t.color = color ?? TextC; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; t.supportRichText = true;
            if (minH > 0) { var le = rt.gameObject.AddComponent<LayoutElement>(); le.minHeight = minH; }
            if (outline) { var o = rt.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0.1f, 0.8f); o.effectDistance = new Vector2(2, -2); }
            return t;
        }

        public static Text Header(Transform parent, string text, int size = 34) => Label(parent, text.ToUpperInvariant(), size, TextAnchor.MiddleLeft, Yellow, true, size + 14, true);

        /// <summary>Большая цветная кнопка в стиле аркады: скруглённая, жирный текст с обводкой, увеличение при наведении.</summary>
        public static Button BigButton(Transform parent, string text, Color bg, UnityAction cb, float w = 0, float h = 72, int fontSize = 32, Color? textColor = null, string icon = null)
        {
            var rt = Rect(parent, "big_" + text); var im = rt.gameObject.AddComponent<Image>(); im.color = bg; im.sprite = Rounded(22); im.type = Image.Type.Sliced;
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = im;
            var cols = b.colors; cols.highlightedColor = new Color(1.15f, 1.15f, 1.15f); cols.pressedColor = new Color(0.8f, 0.8f, 0.8f); cols.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f); b.colors = cols;
            rt.gameObject.AddComponent<HoverScale>();
            if (cb != null) b.onClick.AddListener(() => { Fx.Click(); cb(); });
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h; if (w > 0) { le.minWidth = w; le.preferredWidth = w; } else le.flexibleWidth = 1;
            // нижняя тень-полоска для объёма
            var shade = Img(rt, new Color(0, 0, 0, 0.18f), "shade"); shade.sprite = Rounded(22); shade.type = Image.Type.Sliced; shade.rectTransform.anchorMin = new Vector2(0, 0); shade.rectTransform.anchorMax = new Vector2(1, 0.3f); shade.rectTransform.offsetMin = Vector2.zero; shade.rectTransform.offsetMax = Vector2.zero;
            if (icon != null) { var ic = Icon(rt, icon, textColor ?? Color.white, h * 0.55f); Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(h * 0.3f, 0), new Vector2(h * 0.55f, h * 0.55f)); }
            var t = Label(rt, text.ToUpperInvariant(), fontSize, TextAnchor.MiddleCenter, textColor ?? Color.white, true, 0, true); Stretch(t.rectTransform, icon != null ? h * 0.8f : 10, 2, 10, 2);
            return b;
        }

        public static Button Btn(Transform parent, string text, UnityAction cb, float w = 0, float h = 44, Color? bg = null, int fontSize = 20)
            => BigButton(parent, text, bg ?? Blue, cb, w, h, fontSize, Color.white);

        public static RectTransform Column(Transform parent, float spacing = 8, int pad = 10, bool fitHeight = false, string name = "col")
        {
            var rt = Rect(parent, name); var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = new RectOffset(pad, pad, pad, pad); v.childForceExpandHeight = false; v.childForceExpandWidth = true; v.childControlHeight = true; v.childControlWidth = true;
            if (fitHeight) { var f = rt.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
            return rt;
        }

        public static RectTransform Row(Transform parent, float spacing = 8, float h = 44, int pad = 0, string name = "row", TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Rect(parent, name); var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing; hl.padding = new RectOffset(pad, pad, pad, pad); hl.childForceExpandWidth = false; hl.childForceExpandHeight = true; hl.childControlWidth = true; hl.childControlHeight = true; hl.childAlignment = align;
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h; le.flexibleHeight = 0; // flexibleHeight=0 не даёт строке растягиваться в колонке
            return rt;
        }

        public static RectTransform Grid(Transform parent, Vector2 cell, float spacing = 12, int pad = 10, string name = "grid")
        {
            var rt = Rect(parent, name); var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell; g.spacing = new Vector2(spacing, spacing); g.padding = new RectOffset(pad, pad, pad, pad); g.childAlignment = TextAnchor.UpperLeft;
            var f = rt.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        public static Text Cell(Transform row, string text, float w, int size = 18, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var t = Label(row, text, size, align, color, false); var le = t.gameObject.AddComponent<LayoutElement>();
            if (w > 0) { le.minWidth = w; le.preferredWidth = w; } else le.flexibleWidth = 1;
            return t;
        }

        /// <summary>Прокручиваемая область. grid=true — сетка плиток.</summary>
        public static RectTransform Scroll(Transform parent, out ScrollRect sr, bool grid = false, Vector2 cell = default)
        {
            var root = Rect(parent, "scroll"); sr = root.gameObject.AddComponent<ScrollRect>();
            var vp = Rect(root, "viewport"); Stretch(vp); vp.gameObject.AddComponent<RectMask2D>(); var vi = vp.gameObject.AddComponent<Image>(); vi.color = new Color(1, 1, 1, 0.01f);
            var content = grid ? Grid(vp, cell == default ? new Vector2(150, 170) : cell, 12, 8, "content") : Column(vp, 8, 8, true, "content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            sr.content = content; sr.viewport = vp; sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 40; sr.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        public static InputField Field(Transform parent, string placeholder, string value = "", float w = 0, float h = 48, UnityAction<string> onEnd = null)
        {
            var rt = Rect(parent, "field"); var im = rt.gameObject.AddComponent<Image>(); im.color = new Color(0, 0, 0.1f, 0.55f); im.sprite = Rounded(16); im.type = Image.Type.Sliced;
            var f = rt.gameObject.AddComponent<InputField>(); f.targetGraphic = im;
            var t = Label(rt, "", 22, TextAnchor.MiddleLeft, TextC, true); Stretch(t.rectTransform, 16, 2, 16, 2); t.supportRichText = false; f.textComponent = t;
            var ph = Label(rt, placeholder, 22, TextAnchor.MiddleLeft, Muted, false); Stretch(ph.rectTransform, 16, 2, 16, 2); f.placeholder = ph;
            f.text = value; f.caretColor = Yellow; f.selectionColor = Yellow * 0.5f;
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h; if (w > 0) { le.minWidth = w; le.preferredWidth = w; } else le.flexibleWidth = 1;
            if (onEnd != null) f.onEndEdit.AddListener(onEnd);
            return f;
        }

        public static Slider SliderRow(Transform parent, string label, float min, float max, float val, UnityAction<float> cb, string fmt = "0.0")
        {
            var row = Row(parent, 14, 44); Cell(row, label, 260, 20);
            var srt = Rect(row, "slider"); var s = srt.gameObject.AddComponent<Slider>(); var le = srt.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1;
            var bg = Img(srt, new Color(0, 0, 0, 0.4f)); bg.sprite = Rounded(8); bg.type = Image.Type.Sliced; Stretch(bg.rectTransform, 0, 16, 0, 16);
            var fillArea = Rect(srt, "fill"); Stretch(fillArea, 0, 16, 0, 16); var fill = Img(fillArea, Yellow); fill.sprite = Rounded(8); fill.type = Image.Type.Sliced; Stretch(fill.rectTransform); s.fillRect = fill.rectTransform;
            var handle = Img(srt, Color.white); handle.sprite = Circle(); handle.rectTransform.sizeDelta = new Vector2(26, 0); s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.minValue = min; s.maxValue = max; s.value = val;
            var vt = Cell(row, val.ToString(fmt), 80, 20, TextAnchor.MiddleRight, Yellow);
            s.onValueChanged.AddListener(v => { vt.text = v.ToString(fmt); cb(v); });
            return s;
        }

        /// <summary>Скруглённая полоса прогресса. Возвращает fill (fillAmount 0..1).</summary>
        public static Image Bar(Transform parent, Color fg, float h = 16, float w = 0)
        {
            var bgI = Img(parent, new Color(0, 0, 0, 0.45f), "bar"); bgI.sprite = Rounded(10); bgI.type = Image.Type.Sliced;
            var le = bgI.gameObject.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h; if (w > 0) { le.minWidth = w; le.preferredWidth = w; } else le.flexibleWidth = 1;
            var fill = Img(bgI.transform, fg, "fill"); fill.sprite = Rounded(10); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 1; Stretch(fill.rectTransform, 2, 2, 2, 2);
            return fill;
        }

        /// <summary>Чип-метка (деньги, уровень, режим...).</summary>
        public static RectTransform Chip(Transform parent, string text, Color c, string icon = null, int size = 20, float h = 40, float w = 0)
        {
            var im = Card(parent, c, 20, "chip"); var rt = im.rectTransform;
            var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.padding = new RectOffset(14, 14, 4, 4); hl.spacing = 8; hl.childAlignment = TextAnchor.MiddleCenter; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false; hl.childControlWidth = true; hl.childControlHeight = true;
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h; le.flexibleHeight = 0;
            if (w > 0) { le.minWidth = w; le.preferredWidth = w; }
            else { var fit = rt.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; }
            if (icon != null) { var ic = Icon(rt, icon, Color.white, h * 0.6f); var il = ic.gameObject.AddComponent<LayoutElement>(); il.minWidth = h * 0.6f; il.minHeight = h * 0.6f; }
            var t = Label(rt, text, size, TextAnchor.MiddleCenter, Color.white, true, 0, true); t.gameObject.name = "chipText"; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return rt;
        }

        public static void SetChip(RectTransform chip, string text) { var t = chip.Find("chipText")?.GetComponent<Text>(); if (t != null) t.text = text; }

        // ---------------------------------------------------------------- иконки
        static readonly Dictionary<string, string[]> iconHints = new Dictionary<string, string[]>
        {
            ["pistol"] = new[] { "pistol", "gun", "handgun" }, ["smg"] = new[] { "uzi", "machinegun", "gun" }, ["ar"] = new[] { "machinegun", "rifle", "gun" }, ["rifle"] = new[] { "machinegun", "rifle", "gun" },
            ["shotgun"] = new[] { "shotgun", "gun" }, ["sniper"] = new[] { "sniper", "rifle", "gun" }, ["dmr"] = new[] { "sniper", "rifle" }, ["lmg"] = new[] { "machinegun", "gun" }, ["melee"] = new[] { "knife_sharp", "knife", "sword", "axe" },
            ["ammo"] = new[] { "ammo_pistol", "ammo", "bullet" }, ["mod"] = new[] { "scope", "gear", "wrench", "tool" }, ["med"] = new[] { "medkit", "medical", "health", "heart" }, ["food"] = new[] { "food", "drink", "bottle", "apple", "burger" },
            ["valuable"] = new[] { "diamond", "gem", "coin", "trophy" }, ["key"] = new[] { "keySmall", "key" }, ["armor"] = new[] { "shield", "armor", "vest" }, ["helmet"] = new[] { "helmet", "shield" }, ["backpack"] = new[] { "backpack", "bag" }, ["rig"] = new[] { "vest", "shield", "bag" },
            ["throwable"] = new[] { "grenade", "bomb" }, ["tool"] = new[] { "wrench", "tool", "gear" }, ["barter"] = new[] { "basket", "box", "crate", "gear" }, ["intel"] = new[] { "information", "document", "paper", "scroll" }, ["container"] = new[] { "save", "box", "chest", "crate" },
            ["money"] = new[] { "coin", "diamond", "cash", "dollar" }, ["level"] = new[] { "star", "medal1", "trophy" }, ["play"] = new[] { "arrowRight", "forward", "right", "play" }, ["settings"] = new[] { "gear", "cog", "wrench" }, ["exit"] = new[] { "exitRight", "power", "door", "cross" },
            ["join"] = new[] { "multiplayer", "share", "signal", "wifi", "network" }, ["map"] = new[] { "flag", "map", "pin", "compass" }, ["quest"] = new[] { "exclamation", "flag", "target", "scroll" }, ["skills"] = new[] { "star", "arrowUp", "up", "lightning" },
            ["shop"] = new[] { "cart", "basket", "coin", "shop" }, ["home"] = new[] { "home", "house", "hammer" }, ["gear"] = new[] { "singleplayer", "shield", "backpack" }, ["skull"] = new[] { "skull", "warning", "cross" }, ["clock"] = new[] { "clock", "timer", "hourglass", "pause" },
            ["enemy"] = new[] { "target", "skull", "crosshair" }, ["heart"] = new[] { "heart", "health", "medal2" }, ["coop"] = new[] { "multiplayer", "people", "group", "team" }, ["pvp"] = new[] { "target", "crossed", "swords", "skull" }, ["lock"] = new[] { "locked", "lock" }, ["check"] = new[] { "checkmark", "check", "tick" },
            ["trophy"] = new[] { "trophy", "medal1" }, ["info"] = new[] { "information", "question" }, ["cross"] = new[] { "cross" }, ["save"] = new[] { "save" }, ["trash"] = new[] { "trashcan" }, ["up"] = new[] { "arrowUp", "up" }, ["down"] = new[] { "arrowDown", "down" },
        };
        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        public static Sprite IconSprite(string hint)
        {
            if (string.IsNullOrEmpty(hint)) return null;
            if (spriteCache.TryGetValue(hint, out var s)) return s;
            var keys = iconHints.TryGetValue(hint, out var k) ? k : new[] { hint };
            s = null;
            foreach (var key in keys)
            {
                var tex = AssetCatalog.FindTexture(key, "weapon-pack/Sprites|PNG/White/2x|PNG/White/1x|ui-pack");
                if (tex != null) { var w = Whiten(tex); s = Sprite.Create(w, new Rect(0, 0, w.width, w.height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect); break; }
            }
            spriteCache[hint] = s; return s;
        }

        /// <summary>Любую иконку превращаем в белую маску (чёрные силуэты тоже), чтобы красить tint-ом.</summary>
        static Texture2D Whiten(Texture2D src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active; Graphics.Blit(src, rt); RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels32(); for (int i = 0; i < px.Length; i++) { px[i].r = 255; px[i].g = 255; px[i].b = 255; }
            tex.SetPixels32(px); tex.Apply();
            return tex;
        }

        /// <summary>Мягкое радиальное свечение.</summary>
        public static Sprite Glow()
        {
            if (glow != null) return glow;
            int size = 128; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size]; float r = size / 2f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r; float a = Mathf.Clamp01(1 - d); a = a * a; px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255)); }
            tex.SetPixels32(px); tex.Apply();
            glow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f)); return glow;
        }
        static Sprite glow;

        /// <summary>Иконка по подсказке; без ассета — цветной кружок с буквой.</summary>
        public static Image Icon(Transform parent, string hint, Color tint, float size, string fallbackText = null)
        {
            var sp = IconSprite(hint);
            var im = Img(parent, sp != null ? tint : new Color(1, 1, 1, 0.15f), "icon_" + hint); im.rectTransform.sizeDelta = new Vector2(size, size);
            if (sp != null) { im.sprite = sp; im.preserveAspect = true; }
            else
            {
                im.sprite = Circle();
                var t = Label(im.transform, (fallbackText ?? (hint.Length > 0 ? hint.Substring(0, 1).ToUpperInvariant() : "?")), Mathf.RoundToInt(size * 0.5f), TextAnchor.MiddleCenter, Color.white, true, 0, true); Stretch(t.rectTransform);
            }
            return im;
        }

        public static string IconHintFor(ItemInfo info)
        {
            if (info == null) return "barter";
            if (info.IsWeapon) return info.weapon.cls == "pistol" && info.weapon.model == "revolver" ? "pistol" : info.weapon.cls;
            if (info.IsAmmo) return "ammo"; if (info.IsMod) return "mod";
            return info.type;
        }

        public static Color RarityColor(int r) => r >= 5 ? Orange : r == 4 ? Purple : r == 3 ? Blue : r == 2 ? Green : new Color(0.55f, 0.6f, 0.75f);
        public static string RarityHex(int r) => "#" + ColorUtility.ToHtmlStringRGB(RarityColor(r));
        public static string RarityColorHex(int r) => RarityHex(r);
        public static string RarityName(int r) => r >= 5 ? "Легендарный" : r == 4 ? "Эпический" : r == 3 ? "Редкий" : r == 2 ? "Необычный" : "Обычный";

        /// <summary>Плитка предмета: цветная по редкости, иконка, название, счётчик.</summary>
        public static Button Tile(Transform parent, ItemInfo info, ItemInstance it, UnityAction cb, string badge = null, bool selected = false, string subtitle = null)
        {
            var rc = RarityColor(info?.rarity ?? 1);
            var card = Card(parent, Color.Lerp(rc, Dark, 0.35f), 18, "tile_" + (info?.id ?? "?"));
            var rt = card.rectTransform;
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = card; var cols = b.colors; cols.highlightedColor = new Color(1.2f, 1.2f, 1.2f); cols.pressedColor = new Color(0.8f, 0.8f, 0.8f); b.colors = cols;
            rt.gameObject.AddComponent<HoverScale>().Scale = 1.06f;
            if (cb != null) b.onClick.AddListener(() => { Fx.Click(); cb(); });
            // верхняя светлая зона под иконку
            var top = Img(rt, new Color(1, 1, 1, 0.12f), "top"); top.sprite = Rounded(18); top.type = Image.Type.Sliced; top.rectTransform.anchorMin = new Vector2(0, 0.38f); top.rectTransform.anchorMax = new Vector2(1, 1); top.rectTransform.offsetMin = new Vector2(6, 0); top.rectTransform.offsetMax = new Vector2(-6, -6);
            var ic = Icon(rt, IconHintFor(info), Color.white, 64, info?.name?.Substring(0, 1)); ic.rectTransform.anchorMin = new Vector2(0.5f, 0.69f); ic.rectTransform.anchorMax = new Vector2(0.5f, 0.69f); ic.rectTransform.anchoredPosition = Vector2.zero;
            var name = Label(rt, info?.name ?? "?", 15, TextAnchor.MiddleCenter, Color.white, true, 0, true); name.rectTransform.anchorMin = new Vector2(0, 0.1f); name.rectTransform.anchorMax = new Vector2(1, 0.38f); name.rectTransform.offsetMin = new Vector2(6, 0); name.rectTransform.offsetMax = new Vector2(-6, 0); name.horizontalOverflow = HorizontalWrapMode.Wrap; name.verticalOverflow = VerticalWrapMode.Truncate;
            if (subtitle != null) { var st = Label(rt, subtitle, 13, TextAnchor.MiddleCenter, Yellow, true, 0, true); st.rectTransform.anchorMin = new Vector2(0, 0); st.rectTransform.anchorMax = new Vector2(1, 0.12f); st.rectTransform.offsetMin = new Vector2(4, 2); st.rectTransform.offsetMax = new Vector2(-4, 0); }
            string badgeText = badge ?? (it != null && it.count > 1 ? "×" + it.count : null);
            if (badgeText != null) { var bd = Chip(rt, badgeText, new Color(0, 0, 0, 0.6f), null, 14, 26); Place(bd, new Vector2(1, 1), new Vector2(-6, -6), new Vector2(40, 26)); bd.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize; }
            if (it != null && it.insured) { var ins = Icon(rt, "check", Cyan, 18); Place(ins.rectTransform, new Vector2(0, 1), new Vector2(8, -8), new Vector2(18, 18)); }
            if (selected) { var frame = Img(rt, Yellow, "frame"); frame.sprite = Rounded(18); frame.type = Image.Type.Sliced; frame.color = new Color(1, 0.83f, 0, 0.9f); Stretch(frame.rectTransform, -3, -3, -3, -3); var inner = Img(frame.transform, Color.Lerp(rc, Dark, 0.35f)); inner.sprite = Rounded(18); inner.type = Image.Type.Sliced; Stretch(inner.rectTransform, 3, 3, 3, 3); frame.transform.SetAsFirstSibling(); }
            return b;
        }

        // ---------------------------------------------------------------- попапы
        static Canvas popupCanvas; static readonly List<GameObject> popups = new List<GameObject>();
        public static RectTransform Popup(string title, float w, float h, out RectTransform content, Color? accent = null)
        {
            if (popupCanvas == null) popupCanvas = NewCanvas("Popups", 80);
            var overlay = Img(popupCanvas.transform, new Color(0, 0, 0.05f, 0.7f), "popup"); overlay.raycastTarget = true; Stretch(overlay.rectTransform);
            var ob = overlay.gameObject.AddComponent<Button>(); ob.transition = Selectable.Transition.None; var go = overlay.gameObject; ob.onClick.AddListener(() => ClosePopup(go));
            var card = Card(overlay.transform, PanelSolid, 28, "popupCard"); Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // поглощает клик
            var stripe = Img(card.transform, accent ?? Yellow, "stripe"); stripe.sprite = Rounded(28); stripe.type = Image.Type.Sliced; stripe.rectTransform.anchorMin = new Vector2(0, 1); stripe.rectTransform.anchorMax = new Vector2(1, 1); stripe.rectTransform.offsetMin = new Vector2(0, -72); stripe.rectTransform.offsetMax = Vector2.zero;
            var tt = Label(stripe.transform, title.ToUpperInvariant(), 28, TextAnchor.MiddleLeft, TextDark, true); Stretch(tt.rectTransform, 24, 0, 80, 0);
            var close = BigButton(stripe.transform, "✕", new Color(0, 0, 0, 0.25f), () => ClosePopup(go), 52, 52, 26, TextDark); Place(close.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(52, 52));
            content = Column(card.transform, 10, 20); content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.offsetMin = new Vector2(0, 0); content.offsetMax = new Vector2(0, -76);
            popups.Add(go);
            return content;
        }
        public static void ClosePopup(GameObject go) { popups.Remove(go); if (go != null) UnityEngine.Object.Destroy(go); }
        public static void CloseAllPopups() { foreach (var p in popups) if (p != null) UnityEngine.Object.Destroy(p); popups.Clear(); }
        public static bool AnyPopup => popups.Count > 0;

        /// <summary>Горизонтальная панель вкладок.</summary>
        public static RectTransform TabBar(Transform parent, string[] names, string[] icons, int selected, Action<int> onSelect, float h = 64)
        {
            var row = Row(parent, 10, h, 0, "tabs", TextAnchor.MiddleCenter);
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i; bool sel = i == selected;
                var b = BigButton(row, names[i], sel ? Yellow : new Color(1, 1, 1, 0.12f), () => onSelect(idx), 0, h - 8, 16, sel ? TextDark : Color.white, icons != null && i < icons.Length ? icons[i] : null);
                b.GetComponentInChildren<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
                b.GetComponent<LayoutElement>().flexibleWidth = 1;
            }
            return row;
        }

        public static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject); }

        public static string ItemLine(ItemInstance it)
        {
            var info = it.Info; if (info == null) return it.id;
            string s = $"<color={RarityHex(info.rarity)}>{info.name}</color>";
            if (it.count > 1) s += $" ×{it.count}";
            if (it.IsWeapon) s += $"  <color=#b9c3e6>{it.ammoInMag}/{Database.Weapon(it.id)?.magCap} · {it.durability:0}%{(it.mods.Count > 0 ? " · моды " + it.mods.Count : "")}</color>";
            else if (it.durability >= 0 && info.item != null && info.item.Has("durability")) s += $"  <color=#b9c3e6>{it.durability:0}/{info.item.P("durability"):0}</color>";
            if (it.uses >= 0 && info.item != null && info.item.Has("uses")) s += $"  <color=#b9c3e6>{it.uses} исп.</color>";
            if (it.insured) s += " <color=#19d3ff>[страх.]</color>";
            if (it.found) s += " <color=#5cf25a>[найдено]</color>";
            return s;
        }

        /// <summary>Описание предмета для попапа.</summary>
        public static string Describe(ItemInstance it)
        {
            var info = it.Info; if (info == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color={RarityHex(info.rarity)}>{RarityName(info.rarity)}</color> · {info.desc}");
            if (it.IsWeapon)
            {
                var wc = WeaponStats.Compute(it); var def = info.weapon;
                sb.AppendLine($"Урон патрона {Database.AmmoDef(it.ammoId)?.dmg ?? 0:0} · Темп {wc.rpm:0} · Отдача {wc.recoilV:0}/{wc.recoilH:0} · Эргономика {wc.ergo:0}\nТочность {wc.moa:0.00} MOA · Магазин {it.ammoInMag}/{wc.magCap} · Прочность {it.durability:0}% · Вес {wc.weight:0.0} кг");
                if (it.mods.Count > 0) sb.AppendLine("Моды: " + string.Join(", ", System.Linq.Enumerable.Select(it.mods.Values, m => Database.NameOf(m))));
            }
            else if (info.IsMod) sb.AppendLine(string.Join("  ", System.Linq.Enumerable.Select(info.mod.stats, kv => kv.Key + " " + kv.Value.ToString("+0.##;-0.##"))));
            else if (info.item != null)
            {
                var d = info.item;
                if (d.Has("heal")) sb.Append($"Лечение {d.P("heal"):0} · ");
                if (d.Has("uses")) sb.Append($"Использований {it.uses}/{d.P("uses"):0} · ");
                if (d.Has("armorClass")) sb.Append($"Класс брони {d.P("armorClass"):0} · ");
                if (d.Has("durability")) sb.Append($"Прочность {it.durability:0}/{d.P("durability"):0} · ");
                if (d.Has("cellsW")) sb.Append($"Ячеек {d.P("cellsW"):0}×{d.P("cellsH"):0} · ");
                if (d.Has("energy")) sb.Append($"Энергия +{d.P("energy"):0} · Вода +{d.P("hydration"):0} · ");
                if (d.Has("dmg")) sb.Append($"Урон {d.P("dmg"):0} · Радиус {d.P("radius"):0} м · ");
                sb.AppendLine();
            }
            sb.Append($"Вес {it.Weight:0.0} кг · Размер {info.w}×{info.h} · Цена {it.Value:N0} ₽");
            return sb.ToString();
        }
    }
}
