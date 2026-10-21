using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>Конструктор uGUI из кода: панели, текст, кнопки, списки, поля ввода, полосы.</summary>
    public static class UIKit
    {
        public static readonly Color Bg = new Color(0.07f, 0.08f, 0.09f, 0.96f), Panel2 = new Color(0.13f, 0.14f, 0.16f, 0.95f), Accent = new Color(0.95f, 0.65f, 0.2f),
            Good = new Color(0.35f, 0.8f, 0.4f), Bad = new Color(0.9f, 0.3f, 0.25f), Muted = new Color(0.6f, 0.62f, 0.65f), TextC = new Color(0.92f, 0.92f, 0.9f), BtnC = new Color(0.22f, 0.24f, 0.28f);
        static Font F => AssetCatalog.Font;

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
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        public static Image Img(Transform parent, Color c, string name = "img")
        {
            var rt = Rect(parent, name); var im = rt.gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im;
        }

        public static RectTransform Panel(Transform parent, Color c, string name = "panel")
        {
            var im = Img(parent, c, name); im.raycastTarget = true; Stretch(im.rectTransform); return im.rectTransform;
        }

        public static RectTransform Box(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax, Color c, string name = "box")
        {
            var im = Img(parent, c, name); im.raycastTarget = true; var rt = im.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offMin; rt.offsetMax = offMax; return rt;
        }

        public static Text Label(Transform parent, string text, int size = 20, TextAnchor align = TextAnchor.UpperLeft, Color? color = null, bool bold = false, float minH = 0)
        {
            var rt = Rect(parent, "label"); var t = rt.gameObject.AddComponent<Text>();
            t.font = F; t.fontSize = size; t.text = text; t.alignment = align; t.color = color ?? TextC; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; t.supportRichText = true;
            if (minH > 0) { var le = rt.gameObject.AddComponent<LayoutElement>(); le.minHeight = minH; }
            return t;
        }

        public static Button Btn(Transform parent, string text, UnityAction cb, float w = 0, float h = 38, Color? bg = null, int fontSize = 18)
        {
            var rt = Rect(parent, "btn_" + text); var im = rt.gameObject.AddComponent<Image>(); im.color = bg ?? BtnC;
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = im;
            var cols = b.colors; cols.highlightedColor = new Color(1.2f, 1.2f, 1.2f); cols.pressedColor = new Color(0.7f, 0.7f, 0.7f); b.colors = cols;
            if (cb != null) b.onClick.AddListener(() => { Fx.Click(); cb(); });
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h; if (w > 0) { le.minWidth = w; le.preferredWidth = w; } else le.flexibleWidth = 1;
            var t = Label(rt, text, fontSize, TextAnchor.MiddleCenter); Stretch(t.rectTransform, 8, 2, 8, 2);
            return b;
        }

        public static RectTransform Column(Transform parent, float spacing = 6, int pad = 8, bool fitHeight = false, string name = "col")
        {
            var rt = Rect(parent, name); var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = new RectOffset(pad, pad, pad, pad); v.childForceExpandHeight = false; v.childForceExpandWidth = true; v.childControlHeight = true; v.childControlWidth = true;
            if (fitHeight) { var f = rt.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
            return rt;
        }

        public static RectTransform Row(Transform parent, float spacing = 6, float h = 40, int pad = 0, string name = "row")
        {
            var rt = Rect(parent, name); var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = spacing; hl.padding = new RectOffset(pad, pad, pad, pad); hl.childForceExpandWidth = false; hl.childForceExpandHeight = true; hl.childControlWidth = true; hl.childControlHeight = true; hl.childAlignment = TextAnchor.MiddleLeft;
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h;
            return rt;
        }

        public static Text Cell(Transform row, string text, float w, int size = 17, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var t = Label(row, text, size, align, color); var le = t.gameObject.AddComponent<LayoutElement>();
            if (w > 0) { le.minWidth = w; le.preferredWidth = w; } else le.flexibleWidth = 1;
            return t;
        }

        /// <summary>Прокручиваемая колонка. Возвращает content, куда добавлять элементы.</summary>
        public static RectTransform Scroll(Transform parent, out ScrollRect sr)
        {
            var root = Rect(parent, "scroll"); sr = root.gameObject.AddComponent<ScrollRect>();
            var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.25f);
            var vp = Rect(root, "viewport"); Stretch(vp, 2, 2, 2, 2); var mask = vp.gameObject.AddComponent<RectMask2D>(); vp.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            var content = Column(vp, 4, 6, true, "content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.offsetMin = new Vector2(0, 0); content.offsetMax = new Vector2(0, 0);
            sr.content = content; sr.viewport = vp; sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 35; sr.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        public static InputField Field(Transform parent, string placeholder, string value = "", float w = 0, float h = 38, UnityAction<string> onEnd = null)
        {
            var rt = Rect(parent, "field"); var im = rt.gameObject.AddComponent<Image>(); im.color = new Color(0.05f, 0.05f, 0.06f, 0.95f);
            var f = rt.gameObject.AddComponent<InputField>(); f.targetGraphic = im;
            var t = Label(rt, "", 18, TextAnchor.MiddleLeft); Stretch(t.rectTransform, 8, 2, 8, 2); t.supportRichText = false; f.textComponent = t;
            var ph = Label(rt, placeholder, 18, TextAnchor.MiddleLeft, Muted); Stretch(ph.rectTransform, 8, 2, 8, 2); f.placeholder = ph;
            f.text = value; f.caretColor = TextC; f.selectionColor = Accent * 0.6f;
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h; if (w > 0) { le.minWidth = w; le.preferredWidth = w; } else le.flexibleWidth = 1;
            if (onEnd != null) f.onEndEdit.AddListener(onEnd);
            return f;
        }

        public static Slider SliderRow(Transform parent, string label, float min, float max, float val, UnityAction<float> cb, string fmt = "0.0")
        {
            var row = Row(parent, 10, 36); Cell(row, label, 220);
            var srt = Rect(row, "slider"); var s = srt.gameObject.AddComponent<Slider>(); var le = srt.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1;
            var bg = Img(srt, new Color(0.2f, 0.2f, 0.22f)); Stretch(bg.rectTransform, 0, 12, 0, 12);
            var fillArea = Rect(srt, "fill"); Stretch(fillArea, 0, 12, 0, 12); var fill = Img(fillArea, Accent); Stretch(fill.rectTransform); s.fillRect = fill.rectTransform;
            var handle = Img(srt, TextC); handle.rectTransform.sizeDelta = new Vector2(14, 0); s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.minValue = min; s.maxValue = max; s.value = val;
            var vt = Cell(row, val.ToString(fmt), 70, 17, TextAnchor.MiddleRight);
            s.onValueChanged.AddListener(v => { vt.text = v.ToString(fmt); cb(v); });
            return s;
        }

        /// <summary>Горизонтальная полоса-индикатор. fill.fillAmount 0..1</summary>
        public static Image Bar(Transform parent, Color fg, float h = 14, float w = 0)
        {
            var bgI = Img(parent, new Color(0, 0, 0, 0.5f), "bar"); var le = bgI.gameObject.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h; if (w > 0) { le.minWidth = w; le.preferredWidth = w; } else le.flexibleWidth = 1;
            var fill = Img(bgI.transform, fg, "fill"); Stretch(fill.rectTransform, 1, 1, 1, 1); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 1;
            return fill;
        }

        public static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject); }

        public static Text Header(Transform parent, string text) => Label(parent, text, 26, TextAnchor.MiddleLeft, Accent, true, 40);

        public static string RarityColor(int r) => r >= 5 ? "#ffb347" : r == 4 ? "#c38cff" : r == 3 ? "#5fb3ff" : r == 2 ? "#7ee07e" : "#dddddd";
        public static string ItemLine(ItemInstance it)
        {
            var info = it.Info; if (info == null) return it.id;
            string s = $"<color={RarityColor(info.rarity)}>{info.name}</color>";
            if (it.count > 1) s += $" ×{it.count}";
            if (it.IsWeapon) s += $"  <color=#aaaaaa>{it.ammoInMag}/{Database.Weapon(it.id)?.magCap} · {it.durability:0}%{(it.mods.Count > 0 ? " · моды " + it.mods.Count : "")}</color>";
            else if (it.durability >= 0 && info.item != null && info.item.Has("durability")) s += $"  <color=#aaaaaa>{it.durability:0}/{info.item.P("durability"):0}</color>";
            if (it.uses >= 0 && info.item != null && info.item.Has("uses")) s += $"  <color=#aaaaaa>{it.uses} исп.</color>";
            if (it.insured) s += " <color=#5fb3ff>[страх.]</color>";
            if (it.found) s += " <color=#7ee07e>[найдено]</color>";
            return s;
        }
    }
}
