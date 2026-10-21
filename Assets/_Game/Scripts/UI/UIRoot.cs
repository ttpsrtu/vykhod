using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vykhod
{
    /// <summary>Корень интерфейса: экраны меню/убежища/HUD/инвентаря/результатов, уведомления, загрузка.</summary>
    public class UIRoot : MonoBehaviour
    {
        MainMenuUI menu; HideoutUI hideout; HUD hud; RaidMenuUI raidMenu; ResultsUI results;
        Canvas loadingCanvas; Text loadingText; Canvas toastCanvas; RectTransform toastCol;
        readonly List<(Text t, float until)> toasts = new List<(Text, float)>();

        public bool IsMenuOpen => Game.State == GameState.Raid && raidMenu != null && raidMenu.Open || ChatOpen;
        public bool ChatOpen => hud != null && hud.ChatOpen;

        void Awake()
        {
            if (FindFirstObjectByType<EventSystem>() == null) { var es = new GameObject("EventSystem"); es.transform.SetParent(transform, false); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>(); }
            toastCanvas = UIKit.NewCanvas("Toasts", 100);
            toastCol = UIKit.Column(toastCanvas.transform, 4, 0);
            toastCol.anchorMin = new Vector2(1, 1); toastCol.anchorMax = new Vector2(1, 1); toastCol.pivot = new Vector2(1, 1); toastCol.anchoredPosition = new Vector2(-16, -16); toastCol.sizeDelta = new Vector2(520, 400);
            toastCol.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperRight;
            loadingCanvas = UIKit.NewCanvas("Loading", 90); UIKit.Panel(loadingCanvas.transform, UIKit.Bg);
            loadingText = UIKit.Label(loadingCanvas.transform, "", 34, TextAnchor.MiddleCenter, UIKit.Accent, true); UIKit.Stretch(loadingText.rectTransform);
            loadingCanvas.gameObject.SetActive(false);
        }

        void HideAll()
        {
            menu?.Hide(); hideout?.Hide(); hud?.Hide(); raidMenu?.Hide(); results?.Hide(); loadingCanvas.gameObject.SetActive(false);
        }

        public void ShowMainMenu() { HideAll(); (menu ?? (menu = new MainMenuUI())).Show(); }
        public void ShowHideout() { HideAll(); (hideout ?? (hideout = new HideoutUI())).Show(); }
        public void ShowLoading(string text) { HideAll(); loadingCanvas.gameObject.SetActive(true); loadingText.text = text; }
        public void ShowHUD() { HideAll(); (hud ?? (hud = new HUD())).Show(); (raidMenu ?? (raidMenu = new RaidMenuUI())).Hide(); }
        public void ShowResults(bool extracted) { HideAll(); (results ?? (results = new ResultsUI())).Show(extracted); }

        public void OnNotification(string msg)
        {
            var t = UIKit.Label(toastCol, msg, 18, TextAnchor.MiddleRight, UIKit.TextC);
            var bg = t.gameObject.AddComponent<Outline>(); bg.effectColor = Color.black; bg.effectDistance = new Vector2(1, -1);
            toasts.Add((t, Time.unscaledTime + 5f));
        }

        void Update()
        {
            for (int i = toasts.Count - 1; i >= 0; i--) if (Time.unscaledTime > toasts[i].until) { if (toasts[i].t != null) Destroy(toasts[i].t.gameObject); toasts.RemoveAt(i); }
            if (Game.State == GameState.Raid)
            {
                hud?.Tick(); raidMenu?.Tick();
                bool open = IsMenuOpen; Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = open;
            }
            else if (Game.State == GameState.Hideout) hideout?.Tick();
        }

        // --- делегаты в HUD / меню рейда
        public void OnContainerContents(int id, List<ItemInstance> items) => raidMenu?.SetContainer(id, items);
        public void OnInventoryChanged() { raidMenu?.Refresh(); hud?.RefreshAmmo(); }
        public void OnChat(string who, string msg) => hud?.AddChat(who, msg);
        public void OnKillFeed(string text) => hud?.AddKill(text);
        public void SetHint(string text) => hud?.SetHint(text);
        public void OnDamaged(string part, float dmg, string attacker) => hud?.Damaged(part, dmg);
        public void OnDeath(string attacker, string weapon) { raidMenu?.Hide(); hud?.ShowDeath(attacker, weapon); }
        public void OnHealthChanged() => hud?.RefreshHealth();
        public void SixthSense() => hud?.SixthSense();
        public void HitMarker(bool head) => hud?.HitMarker(head);
        public void Flash(float power) => hud?.Flash(power);
        public void SetNightVision(bool on) => hud?.SetNV(on);
        public void OpenContainer(LootContainer c) => raidMenu?.OpenWith(c);
        public void ToggleInventory() { if (raidMenu == null) return; if (raidMenu.Open) raidMenu.Hide(); else raidMenu.ShowInventory(); }
        public void TogglePause() { if (raidMenu == null) return; if (raidMenu.Open) raidMenu.Hide(); else raidMenu.ShowPause(); }
        public void OpenChat() => hud?.OpenChat();
    }
}
