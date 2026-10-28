using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Il2CppInterop.Runtime;
using TMPro;

namespace SinglePlayerLibrary
{
    internal sealed partial class LibraryRuntime
    {
        GameObject page;
        RectTransform listContent, listViewport;
        bool replayTab;
        StartScreen_UI2 menu;
        GameObject hiddenMenuHolder;
        bool holderWasActive;
        string oldStartText;
        bool oldResumeActive;
        internal bool NativeResumeAvailable;
        BasicPopup prompt;
        bool nativeInteractable = true;
        internal string OwnedReplayId;
        internal bool ReturningFromReplay;
        readonly List<SpriteHandle> pageSprites = new List<SpriteHandle>();
        TMP_FontAsset thinFont, bodyFont, rowFont;
        Sprite roundedSprite, circleSprite, backSprite, plusSprite, refreshSprite, folderSprite;
        LibraryIconAssets iconAssets;

        internal void UpdateMenu()
        {
            var start = UIManager.Instance.GetCurrentScreen()?.TryCast<StartScreen_UI2>();
            if (start == null) { if (page != null) ClosePage(); menu = null; return; }
            DecorateMenu(start, true);
            if (ReturningFromReplay && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "StartScene")
            {
                var client = GameManager.Instance.client;
                if (client?.IsReplay == true && client.gameId.ToString() == OwnedReplayId)
                { client.Disconnect(); GameManager.Instance.client = null; }
                ReturningFromReplay = false; OwnedReplayId = null; OpenPage(true);
            }
        }
        bool decoratingMenu;
        internal void DecorateMenu(StartScreen_UI2 start,bool updateLayout)
        {
            if(decoratingMenu || start==null || start.startButton==null || start.resumeButton==null)return;
            decoratingMenu=true;
            try
            {
                if (menu == null || menu.Pointer != start.Pointer)
                {
                    menu=start;oldStartText=start.startButton.Text;oldResumeActive=start.resumeButton.IsActive();
                    NativeResumeAvailable=oldResumeActive && start.resumeButton.ButtonEnabled;
                }
                bool changed=start.resumeButton.IsActive();start.resumeButton.SetActive(false);
                if(updateLayout && changed)start.UpdateLayout();
                start.resumeButton.SetActive(false);start.startButton.Text="SINGLE PLAYER";start.startButton.ButtonEnabled=true;
                // Native layout still reserves its resume slot. Keep a three-button stack
                // using the native multiplayer/quit pitch, independent of save presence.
                if(start.multiplayerButton!=null && start.quitButton!=null)
                {
                    float pitch=Math.Abs(start.multiplayerButton.GetY()-start.quitButton.GetY());
                    if(pitch>0)
                    {
                        start.startButton.SetX(start.multiplayerButton.GetX());
                        start.startButton.SetY(pitch-start.startButton.GetHeight()/2);
                        start.multiplayerButton.SetY(-start.multiplayerButton.GetHeight()/2);
                        start.quitButton.SetY(-pitch-start.quitButton.GetHeight()/2);
                    }
                }
            }
            finally { decoratingMenu=false; }
        }
        internal void RestoreMenu()
        {
            if(menu==null || menu.startButton==null)return;
            menu.startButton.Text=oldStartText;menu.resumeButton.SetActive(oldResumeActive);
            menu.UpdateButtons();menu.UpdateLayout();menu=null;
        }
        void DiscoverAppearance()
        {
            var texts=Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            thinFont=texts.FirstOrDefault(t=>t.font!=null && t.font.name=="JosefinSans-Thin")?.font;
            rowFont=texts.FirstOrDefault(t=>t.font!=null && t.font.name=="JosefinSans-Italic")?.font;
            bodyFont=texts.FirstOrDefault(t=>t.font!=null && t.font.name=="Nunito-ExtraLight")?.font ?? rowFont;
            if(thinFont==null || rowFont==null)throw new InvalidOperationException("Native UI fonts are not ready.");
            var sprites=Resources.FindObjectsOfTypeAll<Sprite>();
            Sprite Find(params string[] names)=>sprites.FirstOrDefault(s=>names.Any(n=>String.Equals(s.name,n,StringComparison.OrdinalIgnoreCase)));
            roundedSprite=Find("PopupBg");circleSprite=Find("circle_50","circle_64");
            backSprite=Find("UI_back","UI_BackArrow","back","BackArrow","arrow_left","UI_arrow_left");
            plusSprite=Find("UI_plus","plus","PlusIcon","UI_add","new_game");
            if(iconAssets==null || !iconAssets.IsValid){iconAssets?.Dispose();iconAssets=new LibraryIconAssets();}
            refreshSprite=iconAssets.Refresh;folderSprite=iconAssets.Folder;

        }
        internal void OpenPage(bool replay)
        {
            EnsureStore();ClosePage();DiscoverAppearance();replayTab=replay;
            page=new GameObject("SinglePlayerLibrary.Page");
            var canvas=page.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2000;
            var scaler=page.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor=UIManager.Canvas.scaleFactor;page.AddComponent<GraphicRaycaster>();
            var bg=Rect("Background",page.transform);Stretch(bg,0,0,0,0);bg.gameObject.AddComponent<Image>().color=Color.clear;
            nativeInteractable=UIManager.CanvasGroup.interactable;UIManager.CanvasGroup.interactable=false;
            var start=UIManager.Instance.GetCurrentScreen()?.TryCast<StartScreen_UI2>();
            if(start!=null){hiddenMenuHolder=start.screenHolder;holderWasActive=hiddenMenuHolder.activeSelf;hiddenMenuHolder.SetActive(false);}
            var header=Panel(bg,"Header",new Color(1,1,1,.08f));Top(header,0,74);
            var title=Label(header,"SINGLE PLAYER",30,thinFont);Stretch(title.rectTransform,70,70,0,0);title.alignment=TextAlignmentOptions.Center;
            var back=CircleButton(header,"Back",backSprite,ClosePage,false);Layout(back,12,12,48,48);
            var tabs=Panel(bg,"Tabs",new Color(0,0,0,.25f));Top(tabs,74,70);
            var tabGroup=Rect("Tab group",tabs);tabGroup.anchorMin=tabGroup.anchorMax=new Vector2(.5f,.5f);tabGroup.sizeDelta=new Vector2(276,44);
            var ongoing=TextButton(tabGroup,"Ongoing",()=>OpenPage(false),!replay,32);Layout(ongoing.GetComponent<RectTransform>(),0,0,134,44);
            var replays=TextButton(tabGroup,"Replays",()=>OpenPage(true),replay,32);Layout(replays.GetComponent<RectTransform>(),142,0,134,44);
            var section=Label(bg,replay?"Local Replays":"Ongoing Games",24,rowFont);Top(section.rectTransform,160,32);section.alignment=TextAlignmentOptions.Center;
            listViewport=Rect("List",bg);listViewport.anchorMin=new Vector2(.5f,0);listViewport.anchorMax=new Vector2(.5f,1);
            listViewport.offsetMin=new Vector2(-185,110);listViewport.offsetMax=new Vector2(185,-210);
            listViewport.gameObject.AddComponent<Image>().color=Color.clear;listViewport.gameObject.AddComponent<RectMask2D>();
            var scroll=listViewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=32;scroll.movementType=ScrollRect.MovementType.Clamped;
            listContent=Rect("Content",listViewport);listContent.anchorMin=new Vector2(0,1);listContent.anchorMax=new Vector2(1,1);listContent.pivot=new Vector2(.5f,1);listContent.sizeDelta=Vector2.zero;
            scroll.viewport=listViewport;scroll.content=listContent;
            var footer=Rect("Footer",bg);footer.anchorMin=footer.anchorMax=new Vector2(.5f,0);footer.pivot=new Vector2(.5f,0);footer.anchoredPosition=new Vector2(0,8);footer.sizeDelta=new Vector2(replay?176:88,92);
            if(replay)
            { FooterButton(footer,"Refresh",refreshSprite,0,RefreshRows,true);FooterButton(footer,"Open Folder",folderSprite,88,OpenReplayFolder,true); }
            else FooterButton(footer,"New Game",plusSprite,0,NewGame);
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(ongoing.gameObject);
            RefreshRows();UpdatePageLayout();
        }
        internal void UpdatePageLayout()
        {
            if(page==null)return;
            page.GetComponent<CanvasScaler>().scaleFactor=UIManager.Canvas.scaleFactor;
            if(listViewport!=null)
            {
                float width=Math.Min(370,Math.Max(220,page.GetComponent<RectTransform>().rect.width-32));
                listViewport.offsetMin=new Vector2(-width/2,110);listViewport.offsetMax=new Vector2(width/2,-210);
            }
        }
        internal void RefreshRows()
        {
            if(listContent==null)return;
            foreach(var handle in pageSprites)handle.SetCompletion(null);pageSprites.Clear();
            for(int i=listContent.childCount-1;i>=0;i--)UnityEngine.Object.Destroy(listContent.GetChild(i).gameObject);
            var rows=Store.List(replayTab);float y=0;
            foreach(var row in rows)
            {
                string path=row.Path;bool isReplay=replayTab;
                string title=row.Error!=null?Path.GetFileName(path):row.Info.Tribe+" · "+row.Info.Mode;
                var button=TextButton(listContent,"",()=>ShowItemDetails(path,isReplay),false,26);button.gameObject.name="Game "+(row.Info?.Id ?? Path.GetFileName(path));
                var rt=button.GetComponent<RectTransform>();Layout(rt,0,y,370,82);rt.anchorMax=new Vector2(1,1);rt.sizeDelta=new Vector2(0,82);
                var colors=button.colors;colors.normalColor=isReplay?new Color(0,0,0,.8f):new Color(.18f,.60f,.88f,.96f);colors.highlightedColor=new Color(.24f,.66f,.93f,.96f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.1f,.4f,.7f,1);button.colors=colors;button.interactable=row.Error==null;
                foreach(var l in button.GetComponentsInChildren<TextMeshProUGUI>())UnityEngine.Object.Destroy(l.gameObject);
                var face=Rect("Tribe",rt);Layout(face,12,13,48,56);var faceImage=face.gameObject.AddComponent<Image>();faceImage.preserveAspect=true;faceImage.useSpriteMesh=true;faceImage.raycastTarget=false;faceImage.enabled=false;
                if(row.Info!=null && Enum.TryParse<PolytopiaBackendBase.Common.TribeType>(row.Info.Tribe,out var tribe))
                {
                    var handle=new SpriteHandle();pageSprites.Add(handle);
                    handle.SetCompletion((SpriteHandleCallback)(Action<SpriteHandle>)(h=>{if(faceImage!=null && h.sprite!=null){faceImage.sprite=h.sprite;faceImage.enabled=true;}}));
                    handle.Request(SpriteData.GetHeadSpriteAddress(tribe));
                }
                var name=Label(rt,title,26,rowFont);Layout(name.rectTransform,70,5,288,29);name.rectTransform.anchorMax=new Vector2(1,1);name.rectTransform.sizeDelta=new Vector2(-82,29);name.enableAutoSizing=true;name.fontSizeMin=20;name.fontSizeMax=26;name.overflowMode=TextOverflowModes.Ellipsis;
                string line1=row.Error ?? ("Turn "+row.Info.Turn+(isReplay?(row.Info.Outcome=="Won"?" · Victory":row.Info.Outcome=="Lost"?" · Defeat":""):""));
                string line2=row.Error!=null?"":"Saved "+row.Info.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                foreach(var line in new[]{new{text=line1,y=34f},new{text=line2,y=55f}})
                {
                    var info=Label(rt,line.text,15,bodyFont);Layout(info.rectTransform,70,line.y,288,21);info.rectTransform.anchorMax=new Vector2(1,1);info.rectTransform.sizeDelta=new Vector2(-82,21);info.color=new Color(1,1,1,.72f);info.overflowMode=TextOverflowModes.Ellipsis;
                }
                y+=98;
            }
            if(rows.Count==0)
            {
                var empty=Label(listContent,replayTab?"No local replays yet.\nCopy .polyreplay files into the replay folder, then refresh.":"No ongoing games.\nStart a new game below.",20,bodyFont);Layout(empty.rectTransform,0,24,370,100);empty.rectTransform.anchorMax=new Vector2(1,1);empty.rectTransform.sizeDelta=new Vector2(0,100);empty.alignment=TextAlignmentOptions.Top;empty.enableWordWrapping=true;y=140;
            }
            if(!String.IsNullOrEmpty(LastError))
            {var error=Label(listContent,LastError,16,bodyFont);Layout(error.rectTransform,0,y,370,90);error.rectTransform.anchorMax=new Vector2(1,1);error.rectTransform.sizeDelta=new Vector2(0,90);error.enableWordWrapping=true;y+=100;}
            listContent.sizeDelta=new Vector2(0,Math.Max(y,140));
        }
        internal void OpenReplayFolder()
        {
            try
            {
                Directory.CreateDirectory(Store.Replays);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo{FileName=Path.GetFullPath(Store.Replays),UseShellExecute=true,Verb="open"});
                LastError=null;
            }
            catch(Exception e){Error(e);RefreshRows();}
        }
        internal void OpenItem(string path, bool replay)
        {
            if (Busy) return; Busy = true;
            try
            {
                if (NativeSession.Supported(GameManager.Client)) Archive(GameManager.Client);
                var data = LibraryStore.Unpack(LibraryStore.ReadBounded(path));
                if (replay && (!data.Info.Finished || !data.Info.CompleteHistory)) throw new InvalidDataException("Incomplete replay history.");
                CloseDetails();BeginLoading();
                NativeSession.Open(data.Payload, replay, data.Info); OwnedReplayId = replay ? data.Info.Id : null; ClosePage(); LastError = null;
            }
            catch (Exception e) { EndLoading();Error(e); RefreshRows(); }
            finally { Busy = false; }
        }
        internal void NewGame()
        {
            if (Busy) return;
            try
            {
                if (NativeSession.Supported(GameManager.Client)) Archive(GameManager.Client);
                creatingFromLibrary=true;creatorSeen=false;
                AllowNewGame = true; ClosePage();
                var start = UIManager.Instance.GetCurrentScreen()?.TryCast<StartScreen_UI2>();
                if (start == null) throw new InvalidOperationException("Main menu is not ready.");
                start.ClickNewGame();
            }
            catch (Exception e) { Error(e); }
            finally { AllowNewGame = false; }
        }
        internal void ClosePage()
        {
            CloseDetails();
            if (page == null) return;
            if (UIManager.Exists && UIManager.CanvasGroup != null) UIManager.CanvasGroup.interactable = nativeInteractable;
            foreach(var handle in pageSprites)handle.SetCompletion(null);pageSprites.Clear();
            if(hiddenMenuHolder!=null)hiddenMenuHolder.SetActive(holderWasActive);hiddenMenuHolder=null;
            UnityEngine.Object.Destroy(page);page=null;listContent=null;listViewport=null;
        }
        internal bool PageOpen => page != null;
        void UpdateDialog()
        {
            if(prompt!=null && prompt.IsShowing()){LayoutPrompt();return;}
            if ((pendingId == null && incompleteId == null) || Store == null || prompt != null && prompt.IsShowing() || !UIManager.Exists || PopupManager.PopupShowing) return;
            // Do not interrupt gameplay/recap: the original result screen or a menu must be visible.
            string screen = UIManager.Instance.CurrentScreen.ToString();
            if (!ResultScreen.IsShowing() && screen != "StartScreen") return;
            if(PageOpen) ClosePage();
            prompt = PopupManager.GetBasicPopup(); prompt.IsUnskippable = true;
            if(pendingId==null)
            {
                prompt.SetHeader("Replay Unavailable").SetDescription("本局存档缺少完整历史，无法生成完整回放。\nThe original save is retained; no complete replay will be created.");
                prompt.SetMainButton("OK",(Il2CppSystem.Action)(Action)(()=>{incompleteId=null;prompt.Hide();prompt=null;}));
                LayoutPrompt();prompt.Show();return;
            }
            prompt.SetHeader("Save Replay?").SetDescription("是否保存本局回放？\nSave a local replay file you can keep and share.");
            prompt.SetMainButton("Save Replay", (Il2CppSystem.Action)(Action)(() =>
            {
                try { var path = CommitReplay(pendingId); Log("Replay saved: " + path); pendingId = null; LastError = null; prompt.Hide(); prompt = null; }
                catch (Exception e) { Error(e); prompt.SetDescription("Could not save replay. Your replay is retained; press Save Replay to retry.\n" + e.Message); }
            }));
            prompt.SetSecondaryButton("Don't Save", (Il2CppSystem.Action)(Action)(() =>
            {
                try { DeclineReplay(pendingId); pendingId = null; LastError = null; prompt.Hide(); prompt = null; }
                catch (Exception e) { Error(e); }
            }));
            LayoutPrompt();prompt.Show();
        }
        internal void LayoutPrompt()
        {
            if(prompt==null)return;
            prompt.RunLayout();
            if(prompt.MainButton==null || prompt.center==null)return;
            var main=prompt.MainButton;var secondary=prompt.SecondaryButton;
            float gap=16;
            float mainX=secondary==null?0:(secondary.GetWidth()+gap)/2;
            PlacePromptButton(main,mainX);
            if(secondary!=null)PlacePromptButton(secondary,-(main.GetWidth()+gap)/2);
        }
        void PlacePromptButton(UISquareButton_UI2 button,float x)
        {
            // UI2 buttons and the legacy popup use different coordinate origins.
            // Anchor the native controls to the popup's own bottom edge explicitly.
            float width=button.GetWidth(),height=button.GetHeight();
            var rect=button.rectTransform;var frame=prompt.background!=null?prompt.background.rectTransform:prompt.center;rect.SetParent(frame,false);
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,0);rect.pivot=new Vector2(.5f,0);
            rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(x,16);
        }
        static RectTransform Rect(string name,Transform parent)
        {
            var go=new GameObject(name,new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Type>(new[]{Il2CppType.Of<RectTransform>()}));
            var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;
        }
        static void Stretch(RectTransform r,float left,float right,float top,float bottom)
        {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);}
        static void Layout(RectTransform r,float x,float y,float w,float h)
        {r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        static void Top(RectTransform r,float y,float height)
        {Layout(r,0,y,0,height);r.anchorMax=Vector2.one;r.sizeDelta=new Vector2(0,height);}
        static RectTransform Panel(Transform parent,string name,Color color)
        {var rect=Rect(name,parent);var image=rect.gameObject.AddComponent<Image>();image.color=color;return rect;}
        TextMeshProUGUI Label(Transform parent,string text,float size,TMP_FontAsset font)
        {
            var rect=Rect("Label",parent);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.fontSize=size;label.color=Color.white;label.text=text;label.richText=false;label.raycastTarget=false;label.enableWordWrapping=false;label.alignment=TextAlignmentOptions.Left;return label;
        }
        Button TextButton(Transform parent,string text,Action action,bool selected,float size)
        {
            var rect=Rect("Button "+text,parent);var image=rect.gameObject.AddComponent<Image>();image.sprite=roundedSprite;image.type=Image.Type.Sliced;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.normalColor=selected?Color.white:Color.clear;colors.highlightedColor=selected?Color.white:new Color(1,1,1,.18f);colors.selectedColor=colors.normalColor;button.colors=colors;
            button.onClick.AddListener((UnityAction)(Action)(()=>{try{action();}catch(Exception e){Error(e);}}));
            var label=Label(rect,text,size,thinFont);Stretch(label.rectTransform,8,8,0,0);label.alignment=TextAlignmentOptions.Center;label.color=selected?new Color(.3f,.3f,.3f):Color.white;
            return button;
        }
        RectTransform CircleButton(Transform parent,string name,Sprite icon,Action action,bool dark,Color? fill=null)
        {
            var rect=Rect("Button "+name,parent);var image=rect.gameObject.AddComponent<Image>();image.sprite=circleSprite;image.color=Color.white;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener((UnityAction)(Action)(()=>{try{action();}catch(Exception e){Error(e);}}));
            if(dark){var inner=Panel(rect,"Black",fill??Color.black);Stretch(inner,3,3,3,3);inner.GetComponent<Image>().sprite=circleSprite;inner.GetComponent<Image>().raycastTarget=false;}
            var glyph=Panel(rect,"Icon",dark?Color.white:Color.black);Stretch(glyph,13,13,13,13);var glyphImage=glyph.GetComponent<Image>();glyphImage.sprite=icon;glyphImage.enabled=icon!=null;glyphImage.preserveAspect=true;glyphImage.raycastTarget=false;
            return rect;
        }
        void FooterButton(RectTransform footer,string text,Sprite icon,float x,Action action,bool blue=false)
        {
            var button=CircleButton(footer,text,icon,action,true,blue?(Color?)new Color(0,.62f,.93f):null);Layout(button,x+12,0,64,64);
            var label=Label(footer,text,15,rowFont);Layout(label.rectTransform,x,70,88,20);label.alignment=TextAlignmentOptions.Center;
        }
    }
    [HarmonyLib.HarmonyPatch(typeof(StartScreen_UI2), "UpdateButtons")]
    internal static class MenuButtonsUpdated
    {
        static void Postfix(StartScreen_UI2 __instance)
        {
            var r=LibraryScript.Runtime;if(r==null)return;
            r.NativeResumeAvailable=__instance.resumeButton!=null && __instance.resumeButton.IsActive() && __instance.resumeButton.ButtonEnabled;
            r.DecorateMenu(__instance,false);
        }
    }
    [HarmonyLib.HarmonyPatch(typeof(StartScreen_UI2), "RunLayout")]
    internal static class MenuLaidOut
    {
        static void Postfix(StartScreen_UI2 __instance) => LibraryScript.Runtime?.DecorateMenu(__instance,false);
    }
    [HarmonyLib.HarmonyPatch(typeof(UIManager), "OnBack")]
    internal static class BackToMenu
    {
        static bool Prefix()
        { var r=LibraryScript.Runtime;if(r==null)return true;if(r.DetailsOpen){r.BackFromDetails();return false;}if(!r.PageOpen)return true;r.ClosePage();return false; }
    }
}
