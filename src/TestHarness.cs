#if POLYLIB_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using HarmonyLib;
using UnityEngine;
using PolytopiaBackendBase.Common;
using PolytopiaBackendBase.Game;
namespace SinglePlayerLibrary
{
    internal sealed partial class LibraryRuntime
    {
        internal static string TestRoot
        {
            get
            {
                foreach (var a in Environment.GetCommandLineArgs())
                    if (a.StartsWith("--spl-test-root=", StringComparison.Ordinal)) return Path.GetFullPath(a.Substring(16));
                return null;
            }
        }
        string lastRequest;
        void TestTick()
        {
            if (TestRoot == null || !UIManager.Exists) return;
            Directory.CreateDirectory(TestRoot);
            string request = Path.Combine(TestRoot, "request.json");
            if (!File.Exists(request)) return;
            string text; using(var file = new FileStream(request,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) using(var reader=new StreamReader(file)) text=reader.ReadToEnd();
            if (text == lastRequest) return; lastRequest = text;
            string token = "";
            try
            {
                using var doc = JsonDocument.Parse(text); var q = doc.RootElement;
                token = q.GetProperty("id").GetString(); string op = q.GetProperty("op").GetString();
                switch (op)
                {
                    case "status": break;
                    case "terminate_click": terminateRow.button.OnButtonClicked();break;
                    case "return_terminate": minimalResultRoot.GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke();break;
                    case "settings_bottom":
                        var menuBottom=UIManager.Instance.GetCurrentScreen().Cast<IngameMenuScreen>();Canvas.ForceUpdateCanvases();menuBottom.scrollRect.verticalNormalizedPosition=0;break;
                    case "main_settings": UIManager.Instance.ShowScreen(UIConstants.Screens.Settings,false,null);break;
                    case "inspect_terminate":
                        var tr=ResultScreen.IsShowing()?ResultScreen.instance:null;
                        File.WriteAllText(Path.Combine(TestRoot,"termination-state.json"),JsonSerializer.Serialize(new {visible=terminateRow!=null && terminateRow.gameObject.activeInHierarchy,button=terminateRow!=null?terminateRow.button.text:null,color=terminateRow!=null?terminateRow.button.BGColors.defaultColor.ToString():null,terminating=TerminatingCurrentGame,sent=terminateSent,completed=completedTerminationId,showing=ResultScreen.IsShowing(),title=tr?.headerContainer?.headerText?.text,returnButton=tr?.buttonRow?.buttonComp?.text,activeResultText=tr?.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Select(t=>t.text).ToArray()}));break;
                    case "probe_terminate":
                        string endError;var endProbe=new EndMatchCommand(GameManager.Client.GameState.GetFirstHumanPlayer().Id);bool validEnd=endProbe.IsValid(GameManager.Client.GameState,out endError);
                        var settingsView=UIManager.Instance.GetCurrentScreen()?.TryCast<IngameMenuScreen>();
                        var nodes=settingsView?.verticalList?.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Select(t=>new {text=t.text,parent=t.transform.parent?.name,grand=t.transform.parent?.parent?.name}).ToArray();
                        File.WriteAllText(Path.Combine(TestRoot,"terminate-probe.json"),JsonSerializer.Serialize(new {validEnd,endError,nodes}));break;
                    case "inspect_settings":
                        var gs=GameManager.PreliminaryGameSettings;var setup=UIManager.Instance.GetCurrentScreen()?.TryCast<GameSetupScreen_UI2>();
                        File.WriteAllText(Path.Combine(TestRoot,"settings.json"),JsonSerializer.Serialize(new {baseMode=gs.BaseGameMode.ToString(),rulesMode=gs.RulesGameMode.ToString(),ids=setup?.gameModeData?.ids?.ToArray(),labels=setup?.gameModeData?.labels?.ToArray(),liveBase=GameManager.Client?.GameState?.Settings.BaseGameMode.ToString(),liveRules=GameManager.Client?.GameState?.Settings.RulesGameMode.ToString()}));break;
                    case "creative_rule":
                        var cs=UIManager.Instance.GetCurrentScreen().Cast<GameSetupScreen_UI2>();cs.OnGameModeChanged(cs.gameModeData.IndexFromId((int)Enum.Parse<GameMode>(q.GetProperty("mode").GetString())));break;
                    case "inspect_heads":
                        File.WriteAllText(Path.Combine(TestRoot,"heads.json"),JsonSerializer.Serialize(page.GetComponentsInChildren<UnityEngine.UI.Image>().Where(i=>i.name=="Tribe" && i.sprite!=null).Select(i=>new {name=i.sprite.name,packed=i.sprite.packed,packing=i.sprite.packingMode.ToString(),vertices=i.sprite.vertices.Length,triangles=i.sprite.triangles.Length,mesh=i.useSpriteMesh}).ToArray()));break;
                    case "unload_icons": ClosePage();GC.Collect();Resources.UnloadUnusedAssets();break;
                    case "destroy_icons": ClosePage();UnityEngine.Object.Destroy(iconAssets.Refresh);UnityEngine.Object.Destroy(iconAssets.Folder);break;
                    case "release_icons": ClosePage();iconAssets.Dispose();iconAssets=null;break;
                    case "icon_lifetime":
                        File.WriteAllText(Path.Combine(TestRoot,"icon-lifetime.json"),JsonSerializer.Serialize(new {cache=iconAssets!=null,refreshAlive=iconAssets!=null && iconAssets.Refresh!=null,folderAlive=iconAssets!=null && iconAssets.Folder!=null,
                            sprites=Resources.FindObjectsOfTypeAll<Sprite>().Count(s=>s.name.StartsWith("SinglePlayerLibrary.")),textures=Resources.FindObjectsOfTypeAll<Texture2D>().Count(t=>t.name.StartsWith("SinglePlayerLibrary."))}));break;
                    case "detail_assets":
                        var detailAssets=new System.Collections.Generic.List<object>();
                        foreach(var sp in Resources.FindObjectsOfTypeAll<Sprite>())detailAssets.Add(new {kind="sprite",name=sp.name});
                        foreach(var gm in Resources.FindObjectsOfTypeAll<GamemodeButton>())foreach(var data in gm.gamemodeData)detailAssets.Add(new {kind="mode",mode=data.gameMode.ToString(),id=data.id,icon=data.icon?.name});
                        foreach(var gi in Resources.FindObjectsOfTypeAll<GameInfoPopup>())detailAssets.Add(new {kind="popup",info=gi.moreInfoButton?.roundButton?.sprite?.name,mode=gi.gameModeButton?.roundButton?.sprite?.name});
                        File.WriteAllText(Path.Combine(TestRoot,"detail-assets.json"),JsonSerializer.Serialize(detailAssets));break;
                    case "back": UIManager.Instance.OnBack();break;
                    case "details_click":
                        (detailNotice??details).GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="Button "+q.GetProperty("name").GetString()).onClick.Invoke();break;
                    case "inspect_details":
                        Canvas.ForceUpdateCanvases();
                        File.WriteAllText(Path.Combine(TestRoot,"details.json"),JsonSerializer.Serialize(new {open=DetailsOpen,notice=detailNotice!=null,confirm=deleteConfirmation,replay=detailReplay,id=detailInfo?.Id,
                            images=details?.GetComponentsInChildren<UnityEngine.UI.Image>().Select(i=>new {name=i.name,parent=i.transform.parent?.name,sprite=i.sprite?.name}).ToArray(),
                            mappings=new {perfection=DetailModeIcon("Perfection"),domination=DetailModeIcon("Domination"),infinity=DetailModeIcon("Infinity")},
                            buttons=details?.GetComponentsInChildren<UnityEngine.UI.Button>().Select(b=>new {name=b.name,color=b.colors.normalColor.ToString(),bounds=TestRectBounds(page.GetComponent<RectTransform>(),b.GetComponent<RectTransform>()).ToString()}).ToArray(),
                            text=details?.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Select(t=>t.text).ToArray()}));break;
                    case "inspect_result":
                        File.WriteAllText(Path.Combine(TestRoot,"result.json"),JsonSerializer.Serialize(new {showing=ResultScreen.IsShowing(),text=ResultScreen.instance?.headerContainer?.headerText?.text,button=ResultScreen.instance?.buttonRow?.buttonComp?.text,highscore=ResultScreen.instance?.IsHighscoreEnabled(),humanWon=NativeSession.ReplayHumanWon(true)}));break;
                    case "tap_return": ResultScreen.instance.buttonRow.buttonComp.OnButtonClicked();break;
                    case "inspect_page":
                        Canvas.ForceUpdateCanvases();
                        var pageInfo=new System.Collections.Generic.List<object>();
                        if(page!=null)
                        {
                            if(listContent!=null && listViewport!=null)pageInfo.Add(new {kind="scroll",offsetY=listContent.anchoredPosition.y,contentHeight=listContent.rect.height,viewportHeight=listViewport.rect.height,normalized=listViewport.GetComponent<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition});
                            foreach(var image in page.GetComponentsInChildren<UnityEngine.UI.Image>())pageInfo.Add(new {kind="image",name=image.name,parent=image.transform.parent?.name,sprite=image.sprite?.name,enabled=image.enabled,color=image.color.ToString(),textureWidth=image.sprite?.texture?.width,textureHeight=image.sprite?.texture?.height});
                            foreach(var label in page.GetComponentsInChildren<TMPro.TextMeshProUGUI>())pageInfo.Add(new {kind="text",name=label.name,parent=label.transform.parent?.name,text=label.text,width=label.rectTransform.rect.width,height=label.rectTransform.rect.height,preferred=label.preferredHeight});
                        }
                        File.WriteAllText(Path.Combine(TestRoot,"page-info.json"),JsonSerializer.Serialize(pageInfo));break;
                    case "click_ui":
                        var namedButton=page.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name==q.GetProperty("name").GetString());namedButton.onClick.Invoke();break;
                    case "inspect_ui":
                        var nativeUI=new System.Collections.Generic.List<object>();
                        foreach(var label in Resources.FindObjectsOfTypeAll<TMPro.TextMeshProUGUI>())
                        {
                            if(label.font!=null)nativeUI.Add(new {kind="text",name=label.name,text=label.text,font=label.font.name,size=label.fontSize,style=label.fontStyle.ToString(),active=label.gameObject.activeInHierarchy});
                        }
                        foreach(var screen in Resources.FindObjectsOfTypeAll<MultiplayerScreen>())
                        {
                            var prefab=screen.gameInfoRowPrefab;
                            nativeUI.Add(new {kind="multiplayer",name=screen.name,width=prefab?.GetComponent<RectTransform>()?.rect.width,height=prefab?.GetComponent<RectTransform>()?.rect.height});
                            if(prefab!=null)foreach(var image in prefab.GetComponentsInChildren<UnityEngine.UI.Image>(true))nativeUI.Add(new {kind="rowImage",name=image.name,sprite=image.sprite?.name,type=image.type.ToString(),color=image.color.ToString()});
                        }
                        var startUI=UIManager.Instance.GetCurrentScreen()?.TryCast<StartScreen_UI2>();
                        if(startUI!=null)foreach(var btn in new[]{startUI.startButton,startUI.resumeButton,startUI.multiplayerButton,startUI.quitButton})nativeUI.Add(new {kind="mainButton",name=btn.Text,x=btn.GetX(),y=btn.GetY(),width=btn.GetWidth(),height=btn.GetHeight()});
                        nativeUI.Add(new {kind="canvas",scale=UIManager.Canvas.scaleFactor,reference=UIManager.CanvasScaler.referenceResolution.ToString(),mode=UIManager.CanvasScaler.uiScaleMode.ToString(),root=UIManager.Canvas.GetComponent<RectTransform>().rect.ToString()});
                        foreach(var image in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Image>())
                            if(image.sprite!=null && image.name.ToLowerInvariant().Contains("icon"))nativeUI.Add(new {kind="iconImage",name=image.name,parent=image.transform.parent?.name,sprite=image.sprite.name});
                        foreach(var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                        {
                            var sn=sprite.name.ToLowerInvariant();
                            if(new[]{"plus","folder","refresh","back","arrow","newgame","circle"}.Any(key=>sn.Contains(key)))nativeUI.Add(new {kind="sprite",name=sprite.name});
                        }
                        File.WriteAllText(Path.Combine(TestRoot,"native-ui.json"),JsonSerializer.Serialize(nativeUI));break;
                    case "tap_folder": OpenReplayFolder();break;
                    case "popup_layout":
                        var popupLayout=PopupManager.GetCurrentPopup()?.TryCast<BasicPopup>();
                        if(popupLayout==null)throw new InvalidOperationException("No basic popup.");
                        LayoutPrompt();Canvas.ForceUpdateCanvases();
                        var frame=popupLayout.background.rectTransform;
                        var mb=TestRectBounds(frame,popupLayout.MainButton.rectTransform);
                        var sb=TestRectBounds(frame,popupLayout.SecondaryButton.rectTransform);
                        var cr=frame.rect;
                        bool overlap=mb.min.x<sb.max.x && mb.max.x>sb.min.x && mb.min.y<sb.max.y && mb.max.y>sb.min.y;
                        bool popupInside=mb.min.x>=cr.xMin && mb.max.x<=cr.xMax && mb.min.y>=cr.yMin && mb.max.y<=cr.yMax
                            && sb.min.x>=cr.xMin && sb.max.x<=cr.xMax && sb.min.y>=cr.yMin && sb.max.y<=cr.yMax;
                        File.WriteAllText(Path.Combine(TestRoot,"popup-layout.json"),JsonSerializer.Serialize(new {overlap,inside=popupInside,mainX=popupLayout.MainButton.GetX(),mainY=popupLayout.MainButton.GetY(),secondaryX=popupLayout.SecondaryButton.GetX(),secondaryY=popupLayout.SecondaryButton.GetY(),mainWidth=popupLayout.MainButton.GetWidth(),secondaryWidth=popupLayout.SecondaryButton.GetWidth(),height=popupLayout.MainButton.GetHeight()}));break;
                    case "finish_replay": UnityEngine.Object.FindObjectOfType<HudScreen>().OnNextTurn(false);break;
                    case "show_multiplayer": UIManager.Instance.ShowScreen(UIConstants.Screens.MultiplayerScreen,false,null);break;
                    case "command_stats":
                        var stats=new System.Collections.Generic.Dictionary<string,int>();
                        foreach(var command in GameManager.Client.GameState.CommandStack)
                        {
                            var kind=command.GetIl2CppType().Name;
                            stats[kind]=stats.TryGetValue(kind,out var count)?count+1:1;
                        }
                        File.WriteAllText(Path.Combine(TestRoot,"command-stats.json"),JsonSerializer.Serialize(stats));break;
                    case "save_exit": UIManager.Instance.GetCurrentScreen().Cast<IngameMenuScreen>().OnSaveAndExitClicked(0,null);break;
                    case "repeat_end": CaptureEnd(q.GetProperty("won").GetBoolean());break;
                    case "game_menu":
                        UIManager.Instance.ShowScreen(UIConstants.Screens.IngameMenu,false,null);break;
                    case "resign":
                        var ingame=UIManager.Instance.GetCurrentScreen().Cast<IngameMenuScreen>();
                        if(!ingame.ShouldShowResignButton())throw new InvalidOperationException("Native menu does not offer resignation in this mode.");
                        ingame.ShowResignPopup();break;
                    case "bridge_start": TestBridge("NativeSetupStep",new object[]{"start","","","",""});break;
                    case "bridge_resume": TestBridge("GameLifecycleAction",new object[]{"load_singleplayer",Guid.NewGuid().ToString("N"),"","","1"});break;
                    case "tap_save": prompt.MainButton.OnButtonClicked();break;
                    case "tap_decline": prompt.SecondaryButton.OnButtonClicked();break;
                    case "view":
                        var view=UnityEngine.Object.FindObjectOfType<ReplayInterface>();
                        if(q.GetProperty("mode").GetString()=="all")view.SetNoFog();else view.SetNormalFog();
                        view.SwitchPlayerPerspective(q.GetProperty("playerIndex").GetInt32());break;
                    case "resolution": Screen.SetResolution(q.GetProperty("width").GetInt32(),q.GetProperty("height").GetInt32(),false);break;
                    case "ui_bounds":
                        Canvas.ForceUpdateCanvases();
                        if(page==null)throw new InvalidOperationException("Page is not open.");
                        var rootRect=page.GetComponent<RectTransform>();
                        var boundsRows=new System.Collections.Generic.List<object>();
                        foreach(var button in page.GetComponentsInChildren<UnityEngine.UI.Button>())
                        {
                            if(listContent!=null && button.transform.IsChildOf(listContent))continue;
                            var bounds=TestRectBounds(rootRect,button.GetComponent<RectTransform>());
                            bool inside=bounds.min.x>=rootRect.rect.xMin-1&&bounds.max.x<=rootRect.rect.xMax+1
                                &&bounds.min.y>=rootRect.rect.yMin-1&&bounds.max.y<=rootRect.rect.yMax+1;
                            boundsRows.Add(new {name=button.name,inside,width=bounds.size.x,height=bounds.size.y});
                            if(!inside)throw new InvalidDataException("Button outside canvas: "+button.name);
                        }
                        if(boundsRows.Count<(replayTab?5:4))throw new InvalidDataException("Missing navigation controls in bounds check.");
                        File.WriteAllText(Path.Combine(TestRoot,"ui-bounds-"+Screen.width+"x"+Screen.height+"-"+replayTab+".json"),JsonSerializer.Serialize(new {width=Screen.width,height=Screen.height,buttons=boundsRows}));break;
                    case "dump_native":
                        var symbols=new System.Collections.Generic.List<object>();
                        foreach(var ty in typeof(ClientBase).Assembly.GetTypes())
                        {
                            if(ty.Name!="ClientBase" && ty.Name!="GameManager" && !ty.Name.Contains("OpenReplay") && !ty.Name.Contains("OpenSession"))continue;
                            foreach(var field in ty.GetFields(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic))
                            {
                                if(!field.Name.StartsWith("NativeMethodInfoPtr"))continue;
                                var mi=(IntPtr)field.GetValue(null);if(mi==IntPtr.Zero)continue;
                                var addr=System.Runtime.InteropServices.Marshal.ReadIntPtr(mi);if(addr==IntPtr.Zero)continue;
                                string name=ty.Name+"."+field.Name;
                                symbols.Add(new {name,address=addr.ToInt64()});
                                if(ty.Name=="ClientBase"||ty.Name.Contains("OpenReplay")||ty.Name.Contains("OpenSession"))
                                {
                                    var code=new byte[1536];System.Runtime.InteropServices.Marshal.Copy(addr,code,0,code.Length);
                                    File.WriteAllBytes(Path.Combine(TestRoot,name+".bin"),code);
                                }
                            }
                        }
                        File.WriteAllText(Path.Combine(TestRoot,"native-symbols.json"),JsonSerializer.Serialize(symbols));break;
                    case "setup_loaded":
                        GameManager.Client.CreateOrResetActionManager(GameManager.Client.lastSeenCommand);
                        GameManager.Client.SessionOpened();GameManager.Instance.LoadLevel();break;
                    case "prepare_session": GameManager.Client.PrepareSession();break;
                    case "verify_replay":
                        var live=GameManager.Client;
                        var replayData=LibraryStore.Unpack(LibraryStore.ReadBounded(Path.Combine(Store.Replays,live.gameId.ToString()+".polyreplay")));
                        var expected=NativeSession.Decode(replayData.Payload,true).GameState;
                        var actual=live.GameState;
                        var expectedBytes=SerializationHelpers.ToByteArray(expected,expected.Version).ToArray();
                        var actualBytes=SerializationHelpers.ToByteArray(actual,actual.Version).ToArray();
                        File.WriteAllBytes(Path.Combine(TestRoot,"expected-final.bin"),expectedBytes);
                        File.WriteAllBytes(Path.Combine(TestRoot,"actual-final.bin"),actualBytes);
                        GameState expectedCopy,actualCopy;
                        SerializationHelpers.FromByteArray<GameState>((Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>)expectedBytes,out expectedCopy);
                        SerializationHelpers.FromByteArray<GameState>((Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>)actualBytes,out actualCopy);
                        int expectedQueue=expectedCopy.ActionStack.Count,actualQueue=actualCopy.ActionStack.Count;
                        expectedCopy.ActionStack.Clear();actualCopy.ActionStack.Clear();
                        string expectedNormalized=NativeSession.StateHash(expectedCopy),actualNormalized=NativeSession.StateHash(actualCopy);
                        File.WriteAllBytes(Path.Combine(TestRoot,"expected-normalized.bin"),SerializationHelpers.ToByteArray(expectedCopy,expectedCopy.Version).ToArray());
                        File.WriteAllBytes(Path.Combine(TestRoot,"actual-normalized.bin"),SerializationHelpers.ToByteArray(actualCopy,actualCopy.Version).ToArray());
                        var parts=new System.Collections.Generic.List<object>();
                        foreach(var property in typeof(GameState).GetProperties(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public))
                        {
                            if(property.GetIndexParameters().Length!=0 || !property.PropertyType.IsPrimitive && !property.PropertyType.IsEnum && property.PropertyType!=typeof(string))continue;
                            try {parts.Add(new {name=property.Name,expected=Convert.ToString(property.GetValue(expectedCopy)),actual=Convert.ToString(property.GetValue(actualCopy))});}catch{}
                        }
                        for(int pi=0;pi<expectedCopy.PlayerStates.Count;pi++)
                        {
                            var ep=expectedCopy.PlayerStates[pi];var ap=actualCopy.PlayerStates[pi];
                            foreach(var property in typeof(PlayerState).GetProperties(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public))
                            {
                                if(property.GetIndexParameters().Length!=0 || !property.PropertyType.IsPrimitive && !property.PropertyType.IsEnum && property.PropertyType!=typeof(string))continue;
                                try {parts.Add(new {name="Player "+ep.Id+"."+property.Name,expected=Convert.ToString(property.GetValue(ep)),actual=Convert.ToString(property.GetValue(ap))});}catch{}
                            }
                        }
                        File.WriteAllText(Path.Combine(TestRoot,"comparison-components.json"),JsonSerializer.Serialize(parts));
                        var comparison=new {expectedHash=LibraryStore.Hash(expectedBytes),actualHash=LibraryStore.Hash(actualBytes),
                            expectedQueue,actualQueue,expectedNormalized,actualNormalized,
                            expectedCommand=expected.CurrentCommand,actualCommand=actual.CurrentCommand,expectedTurn=expected.CurrentTurn,actualTurn=actual.CurrentTurn};
                        File.WriteAllText(Path.Combine(TestRoot,"replay-comparison.json"),JsonSerializer.Serialize(comparison));
                        if(expectedNormalized!=actualNormalized)throw new InvalidDataException("Replay final state mismatch: "+JsonSerializer.Serialize(comparison));
                        Log("Replay final rule state exact byte comparison PASS (detached copies, action queues normalized)");break;
                    case "roundtrip":
                        var original=GameManager.Client;
                        foreach(bool asReplay in new[]{false,true})
                        {
                            var restored=NativeSession.Decode(NativeSession.Bytes(original,asReplay),asReplay);
                            if(restored.GameState.Version!=original.GameState.Version || false
                               || restored.IsReplay!=asReplay || NativeSession.StateHash(restored.GameState)!=NativeSession.StateHash(original.GameState))
                                throw new InvalidDataException("Native serialization round trip failed: replay="+asReplay+", type="+restored.clientType+", version="+restored.GameState.Version+", id="+restored.gameId+", restored="+NativeSession.StateHash(restored.GameState)+", original="+NativeSession.StateHash(original.GameState));
                        }
                        Log("Native local/replay roundtrip PASS"); break;
                    case "resume_processing":
                        Application.runInBackground=true;GameManager.Instance.isPaused=false;GameManager.Client.ActionManager.Resume();break;
                    case "dismiss_popup":
                        var currentPopup=PopupManager.GetCurrentPopup();
                        var reward=currentPopup?.TryCast<UI.Popups.IconRewardPopup>();
                        if(reward!=null)
                        {
                            if(reward.RewardButton==null||!reward.RewardButton.ButtonEnabled)throw new InvalidOperationException("Native reward confirmation is not ready.");
                            reward.RewardButton.OnButtonClicked();break;
                        }
                        var popup=currentPopup?.TryCast<BasicPopup>();
                        if(popup==null && currentPopup!=null)
                        {
                            var confirm=currentPopup.GetDefaultSelectableOrFallback()?.GetComponent<UIButtonBase>();
                            if(confirm==null||!confirm.ButtonEnabled)throw new InvalidOperationException("No native confirmation button on "+currentPopup.GetIl2CppType().Name);
                            confirm.OnButtonClicked();break;
                        }
                        if(popup==null) throw new InvalidOperationException("Unsupported test popup; inspect its native buttons.");
                        if(popup.MainButton!=null)popup.MainButton.OnButtonClicked();
                        else if(popup.buttonData!=null && popup.buttonData.Length>0)
                        {
                            var button=popup.GetButtonById(popup.buttonData[0].id);
                            if(button==null || !button.ButtonEnabled)throw new InvalidOperationException("Popup button not ready.");
                            button.OnButtonClicked();
                        }
                        else throw new InvalidOperationException("No confirmation data.");
                        break;
                    case "open_page": OpenPage(q.TryGetProperty("replay", out var rp) && rp.GetBoolean()); break;
                    case "new_game": NewGame(); break;
                    case "tap_library": UIManager.Instance.GetCurrentScreen().Cast<StartScreen_UI2>().ClickNewGame();break;
                    case "mode":
                        var ms = UIManager.Instance.GetCurrentScreen().Cast<GameModeScreen_UI2>();
                        if(q.GetProperty("mode").GetString()=="Custom")ms.OnCustom();else if(q.GetProperty("mode").GetString()=="Domination") ms.OnDomination(); else ms.OnPerfection(); break;
                    case "tribe": UIManager.Instance.GetCurrentScreen().Cast<TribePickerScreen_UI2>().OnTribeButtonClicked(TribeType.Imperius, (TribeType)0); break;
                    case "confirm_tribe": UIManager.Instance.GetCurrentScreen().Cast<TribePickerScreen_UI2>().OnSelectTribeMainButtonPressed(); break;
                    case "start":
                        var settings=GameManager.PreliminaryGameSettings;
                        settings.OpponentCount=q.TryGetProperty("opponents",out var opponentSetting) ? opponentSetting.GetInt32() : 1; settings.MapSize=14; settings.Difficulty=q.TryGetProperty("difficulty",out var diff)?(BotDifficulty)Enum.Parse(typeof(BotDifficulty),diff.GetString()):BotDifficulty.Easy;
                        if(q.TryGetProperty("mapPreset",out var preset))settings.mapPreset=(MapPreset)Enum.Parse(typeof(MapPreset),preset.GetString());
                        UIManager.Instance.GetCurrentScreen().Cast<GameSetupScreen_UI2>().OnContinueClicked_StartSingleplayerGame(); break;
                    case "human_action":
                        var hc=GameManager.Client;var hs=hc.GameState;
                        if(hc.IsReplay||hc.HasQueuedActions()||hc.ActionManager.IsPaused||hs.CurrentPlayer!=1)throw new InvalidOperationException("Human action requires idle controlled turn.");
                        CommandBase selected=null;float distance=Single.MaxValue;
                        foreach(var fromTile in hs.Map.Tiles)
                        {
                            var unit=fromTile.unit;if(unit==null||unit.owner!=1)continue;
                            foreach(var targetTile in hs.Map.Tiles)
                            {
                                string validation;
                                if(targetTile.unit!=null&&targetTile.unit.owner!=1)
                                {
                                    var attack=new AttackCommand(1,unit,targetTile.coordinates);
                                    if(attack.IsValid(hs,out validation)){selected=attack;break;}
                                }
                                if(selected is AttackCommand)continue;
                                var move=new MoveCommand(1,unit,targetTile.coordinates);
                                if(!move.IsValid(hs,out validation))continue;
                                float score=WorldCoordinates.Distance(targetTile.coordinates,new WorldCoordinates(hs.Map.Width/2,hs.Map.Height/2));
                                if(score<distance){selected=move;distance=score;}
                            }
                            if(selected is AttackCommand)break;
                        }
                        if(selected!=null)hc.SendCommand(selected);break;
                    case "end_turn":
                        var c=GameManager.Client; var end=new EndTurnCommand(c.GameState.CurrentPlayer); string error;
                        if(c.IsReplay || c.ActionManager.IsPaused || c.GameState.CurrentCommand!=c.GameState.CommandStack.Count || c.HasQueuedActions() || !end.IsValid(c.GameState,out error)) throw new InvalidOperationException("End turn not valid or busy.");
                        c.SendCommand(end); break;
                    case "save": Archive(GameManager.Client); break;
                    case "menu": GameManager.ReturnToMenu(); break;
                    case "quit": Application.Quit(); break;
                    case "save_replay": CommitReplay(pendingId); pendingId=null; if(prompt!=null) {prompt.Hide();prompt=null;} break;
                    case "decline_replay": DeclineReplay(pendingId);pendingId=null; if(prompt!=null){prompt.Hide();prompt=null;} break;
                    case "load_save":
                        OpenItem(Path.Combine(Store.Saves, LibraryStore.SafeId(q.GetProperty("gameId").GetString())+".polysave"),false); break;
                    case "load_replay":
                        OpenItem(Path.Combine(Store.Replays, LibraryStore.SafeId(q.GetProperty("gameId").GetString())+".polyreplay"),true); break;
                    case "seek":
                        UnityEngine.Object.FindObjectOfType<Timeline>().SimulateToCommand(q.GetProperty("command").GetInt32()); break;
                    case "play": UnityEngine.Object.FindObjectOfType<Timeline>().Play(); break;
                    case "pause": UnityEngine.Object.FindObjectOfType<Timeline>().Pause(); break;
                    default: throw new ArgumentException("Unknown test operation.");
                }
                TestReply(token, null);
            }
            catch(Exception ex) { Error(ex); TestReply(token,ex.ToString()); }
        }
        static void TestBridge(string name,object[] args)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("PolytopiaAgent.AgentBridgeScript",false)).FirstOrDefault(t=>t!=null);
            if(type==null)throw new InvalidOperationException("Agent Bridge is not loaded.");
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            var script=type.GetField("ActiveScript",flags).GetValue(null);
            var bridge=type.GetField("runtime",flags).GetValue(script);
            var result=bridge.GetType().GetMethod(name,flags).Invoke(bridge,args);
            File.WriteAllText(Path.Combine(TestRoot,"bridge-"+name+".json"),(string)result);
            Log("Bridge test: "+result);
        }
        static Bounds TestRectBounds(RectTransform parent,RectTransform rect)
        {
            var corners=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
            rect.GetWorldCorners(corners);
            var bounds=new Bounds(parent.InverseTransformPoint(corners[0]),Vector3.zero);
            for(int i=1;i<4;i++)bounds.Encapsulate(parent.InverseTransformPoint(corners[i]));
            return bounds;
        }
        static string TestRuleHash(GameState state)
        {
            GameState copy;
            if(!SerializationHelpers.FromByteArray(SerializationHelpers.ToByteArray(state,state.Version),out copy))throw new InvalidDataException("Cannot normalize native snapshot.");
            copy.ActionStack.Clear();
            return NativeSession.StateHash(copy);
        }
        void TestReply(string token, string error)
        {
            var c=GameManager.Client; var s=c?.GameState;
            var visibleStart=UIManager.Instance.GetCurrentScreen()?.TryCast<StartScreen_UI2>();
            var info=s==null ? null : new {
                id=c.gameId.ToString(), replay=c.IsReplay, ready=c.IsReady,
                turn=s.CurrentTurn, player=s.CurrentPlayer, state=s.CurrentState.ToString(), command=s.CurrentCommand,
                stack=s.CommandStack?.Count, actions=c.ActionManager!=null && c.HasQueuedActions(), initialCommand=c.initialGameState?.CurrentCommand,
                initialTurn=c.initialGameState?.CurrentTurn, mode=s.Settings.RulesGameMode.ToString(),
                nativeVersion=s.Version, resultShowing=ResultScreen.IsShowing(), stateHash=NativeSession.StateHash(s), ruleHash=TestRuleHash(s), complete=NativeSession.HistoryComplete(c),
                type=c.clientType.ToString(), ended=c.isSessionEnded,
                managerPaused=GameManager.Instance.isPaused, levelLoaded=GameManager.Instance.isLevelLoaded, isLoading=GameManager.Instance.isLoadingGame, paused=c.ActionManager?.IsPaused,
                holding=c.ActionManager?.isHolding, processing=c.ActionManager?.IsProcessing,
                reaction=c.ActionManager?.currentReaction?.GetIl2CppType().Name,
                popup=PopupManager.GetCurrentPopup()?.Header, popupType=PopupManager.GetCurrentPopup()?.GetIl2CppType().Name,
                focus=Application.isFocused, timescale=Time.timeScale,
                runningBackground=Application.runInBackground,
                viewingPlayer=c.currentViewingPlayer, currentScreen=UIManager.Instance.CurrentScreen.ToString()
            };
            LibraryStore.AtomicWrite(Path.Combine(TestRoot,"reply.json"),JsonSerializer.SerializeToUtf8Bytes(new {
                id=token, error, lastError=LastError, pending=pendingId, page=PageOpen,details=DetailsOpen,loading=loading!=null,
                screen=UIManager.Instance.CurrentScreen.ToString(), store=Store?.Root,
                nativeResumeAvailable=NativeResumeAvailable, nativeResumeEnabled=visibleStart?.resumeButton?.ButtonEnabled,
                menuStart=visibleStart?.startButton != null ? visibleStart.startButton.Text : null, resumeVisible=visibleStart?.resumeButton != null ? (bool?)visibleStart.resumeButton.IsActive() : null,
                info, ongoing=Store?.List(false), replays=Store?.List(true), utc=DateTime.UtcNow
            },new JsonSerializerOptions{WriteIndented=true}));
        }
    }
    [HarmonyPatch(typeof(Paths),"GetUserDirectory")]
    internal static class TestProfile
    {
        static bool Prefix(ref string __result)
        { if(LibraryRuntime.TestRoot==null)return true; __result=Path.Combine(LibraryRuntime.TestRoot,"profile");Directory.CreateDirectory(__result);return false; }
    }
}
#endif
