using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using HarmonyLib;
namespace SinglePlayerLibrary
{
    internal sealed partial class LibraryRuntime
    {
        bool creatingFromLibrary,creatorSeen;
        GameObject loading;
        float loadingStarted;
        int loadingReadyFrames;
        IntPtr resultButton;
        internal void CreationStarted()
        {
            if(creatingFromLibrary)BeginLoading();
            creatingFromLibrary=false;creatorSeen=false;
        }
        internal void UpdateNavigation()
        {
            if(DetailsOpen)UpdateDetailsLayout();
            if(!creatingFromLibrary || !UIManager.Exists)return;
            string screen=UIManager.Instance.CurrentScreen.ToString();
            if(screen=="GameMode" || screen=="TribePicker" || screen=="GameSetup")creatorSeen=true;
            else if(screen=="StartScreen" && creatorSeen && !PageOpen)
            {creatingFromLibrary=false;creatorSeen=false;OpenPage(false);}
        }
        internal void BeginLoading()
        {
            if(loading!=null)return;
            if(thinFont==null)DiscoverAppearance();
            loading=new GameObject("SinglePlayerLibrary.Loading");UnityEngine.Object.DontDestroyOnLoad(loading);
            var canvas=loading.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            var scaler=loading.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=UIManager.Canvas.scaleFactor;
            loading.AddComponent<GraphicRaycaster>();
            var background=Panel(loading.transform,"Loading cover",Color.black);Stretch(background,0,0,0,0);
            var text=Label(background,"LOADING…",30,thinFont);text.alignment=TMPro.TextAlignmentOptions.Center;Stretch(text.rectTransform,24,24,0,0);
            loadingStarted=Time.unscaledTime;loadingReadyFrames=0;
            Canvas.ForceUpdateCanvases();
        }
        internal void UpdateLoading()
        {
            if(loading==null)return;
            loading.GetComponent<CanvasScaler>().scaleFactor=UIManager.Canvas.scaleFactor;
            var manager=GameManager.instance;var client=manager?.client;
            bool ready=client!=null && client.IsReady && manager.isLevelLoaded && !manager.isLoadingGame
                && (UIManager.Instance.CurrentScreen.ToString()=="Hud" || PopupManager.PopupShowing);
            loadingReadyFrames=ready?loadingReadyFrames+1:0;
            // A welcome/reward popup may be waiting on the HUD: it must become clickable.
            if(loadingReadyFrames>=2 && Time.unscaledTime-loadingStarted>=.15f)EndLoading();
            else if(Time.unscaledTime-loadingStarted>45 || (manager!=null && !manager.isLoadingGame && PopupManager.PopupShowing && UIManager.Instance.CurrentScreen.ToString()=="StartScreen"))EndLoading();
        }
        internal void EndLoading()
        {
            if(loading==null)return;
            loading.SetActive(false);UnityEngine.Object.Destroy(loading);loading=null;loadingReadyFrames=0;
        }
        internal void ShowReplayResult(bool fallback,ScoreDetails score,byte winner)
        {
            if(ResultScreen.IsShowing())return;
            var state=GameManager.Client.GameState;var human=state.GetFirstHumanPlayer();
            if(NativeSession.IsTerminatedReplay)score=new ScoreDetails();else if(human!=null)score=PlayerExtensions.GetScore(human,state);
            PlayerState actualWinner;if(state.TryGetWinner(out actualWinner) && actualWinner!=null)winner=actualWinner.Id;
            resultButton=IntPtr.Zero;
            ResultScreen.Show(NativeSession.ReplayHumanWon(fallback),score,winner);
            if(NativeSession.IsTerminatedReplay)ApplyTerminationResult(ResultScreen.instance);
            UpdateReplayResultControls();
        }
        internal void UpdateReplayResultControls()
        {
            if(!NativeSession.OwnsReplay(GameManager.instance?.client) || !ResultScreen.IsShowing())return;
            var result=ResultScreen.instance;if(result==null)return;
            result.highscoreEnabled=false;
            var button=result.buttonRow?.buttonComp;if(button==null)return;
            if(button.text!="RETURN TO DIRECTORY")button.text="RETURN TO DIRECTORY";
            if(resultButton==button.Pointer)return;
            button.ClearCallbacks();
            button.add_OnClicked((UIButtonBase.ButtonAction)(Action<int,BaseEventData>)((id,ev)=>GameManager.ReturnToMenu()));
            resultButton=button.Pointer;
        }
    }
    [HarmonyPatch(typeof(UIManager),"ShowScreen",new Type[]{typeof(UIConstants.Screens),typeof(bool),typeof(UIDeepLinkData)})]
    internal static class LibraryCreationReturn
    {static void Postfix(){LibraryScript.Runtime?.UpdateNavigation();}}
    [HarmonyPatch(typeof(GameManager),"LoadLevel",new Type[]{})]
    internal static class LibraryLoadCover
    {
        static void Prefix()
        {
            var client=GameManager.instance?.client;
            if(NativeSession.Supported(client) || NativeSession.OwnsReplay(client))LibraryScript.Runtime?.BeginLoading();
        }
    }
    [HarmonyPatch(typeof(ResultScreen),"IsHighscoreEnabled")]
    internal static class LocalReplayHighscoreButton
    {
        static bool Prefix(ref bool __result)
        {if(!NativeSession.OwnsReplay(GameManager.instance?.client) && LibraryScript.Runtime?.MinimalInfinityResult!=true)return true;__result=false;return false;}
    }
}
