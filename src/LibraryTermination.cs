using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PolytopiaBackendBase.Game;
namespace SinglePlayerLibrary
{
    internal sealed partial class LibraryRuntime
    {
        SettingsButtonContainer terminateRow;
        IntPtr terminateOwner;
        string terminatingId,completedTerminationId;
        bool terminateSent,returningTerminated;
        GameObject minimalResultRoot;
        IntPtr minimalResultOwner;
        internal bool TerminatingCurrentGame => terminatingId!=null && GameManager.instance?.client!=null
            && !GameManager.Client.IsReplay && GameManager.Client.gameId.ToString()==terminatingId;
        internal bool MinimalInfinityResult => TerminatingCurrentGame || NativeSession.IsTerminatedReplay;
        internal static bool IsLiveInfinity => NativeSession.Supported(GameManager.instance?.client)
            && GameManager.Client.GameState.Settings.RulesGameMode==GameMode.Sandbox
            && GameManager.Client.GameState.CurrentState.ToString()=="Started" && GameManager.Instance.isLevelLoaded;
        internal void SetupTerminateButton(IngameMenuScreen screen)
        {
            if(terminateRow!=null && terminateOwner!=screen.Pointer)DisposeTerminateButton();
            bool show=IsLiveInfinity;
            if(!show){if(terminateRow!=null)terminateRow.gameObject.SetActive(false);return;}
            if(terminateRow==null)
            {
                terminateRow=UnityEngine.Object.Instantiate(screen.singleButtonContainerPrefab,screen.verticalList.transform);
                terminateOwner=screen.Pointer;terminateRow.name="SinglePlayerLibrary.Terminate";
                var button=terminateRow.button;button.ClearCallbacks();button.text="TERMINATE";
                var red=new Color(.85f,.12f,.09f);
                button.BGColors=new UIButtonBase.ColorStates {defaultColor=red,hoverColor=new Color(.95f,.2f,.15f),highlightedColor=red,highlightedHoverColor=red,disabledColor=red};
                button.LabelColorStates=new UIButtonBase.ColorStates {defaultColor=Color.white,hoverColor=Color.white,highlightedColor=Color.white,highlightedHoverColor=Color.white,disabledColor=Color.white};
                button.UpdateColors();
                button.add_OnClicked((UIButtonBase.ButtonAction)(Action<int,BaseEventData>)((id,ev)=>ConfirmTermination()));
            }
            terminateRow.gameObject.SetActive(true);terminateRow.transform.SetAsLastSibling();terminateRow.button.ButtonEnabled=!TerminatingCurrentGame;
            LayoutRebuilder.ForceRebuildLayoutImmediate(screen.container);screen.OnScreenUpdated();
        }
        internal void DisposeTerminateButton()
        {if(terminateRow!=null){terminateRow.gameObject.SetActive(false);UnityEngine.Object.Destroy(terminateRow.gameObject);}terminateRow=null;terminateOwner=IntPtr.Zero;}
        internal void HideTerminateButton(){if(terminateRow!=null)terminateRow.gameObject.SetActive(false);}
        internal void ConfirmTermination()
        {
            if(!IsLiveInfinity || TerminatingCurrentGame || PopupManager.PopupShowing)return;
            string id=GameManager.Client.gameId.ToString();
            prompt=PopupManager.GetBasicPopup();prompt.IsUnskippable=true;
            prompt.SetHeader("Terminate game?").SetDescription("End this Infinity game now?\nYou will be shown as the winner and can choose whether to save the replay.");
            prompt.SetMainButton("TERMINATE",(Il2CppSystem.Action)(Action)(()=>
            {
                if(!IsLiveInfinity || GameManager.Client.gameId.ToString()!=id)return;
                terminatingId=id;completedTerminationId=null;terminateSent=false;
                prompt.Hide();prompt=null;UIManager.Instance.ShowScreen(UIConstants.Screens.Hud,false,null);
            }));
            prompt.SetSecondaryButton("CANCEL",(Il2CppSystem.Action)(Action)(()=>{prompt.Hide();prompt=null;}));
            LayoutPrompt();prompt.Show();
        }
        internal void UpdateTermination()
        {
            if(returningTerminated && UIManager.Instance.CurrentScreen.ToString()=="StartScreen")
            {returningTerminated=false;terminatingId=null;completedTerminationId=null;OpenPage(false);}
            if(!TerminatingCurrentGame)return;
            var c=GameManager.Client;
            if(c.GameState.CurrentState.ToString()=="Ended")return;
            if(terminateSent || PopupManager.PopupShowing || GameManager.Instance.isPaused || c.ActionManager==null || c.ActionManager.IsPaused
                || c.HasQueuedActions() || UIManager.Instance.CurrentScreen.ToString()!="Hud")return;
            var human=c.GameState.GetFirstHumanPlayer();if(human==null || c.GameState.CurrentPlayer!=human.Id)return;
            terminateSent=true;
            try{c.SendCommand(new EndMatchCommand(human.Id));}
            catch(Exception e){terminateSent=false;terminatingId=null;Error(e);}
        }
        internal void CompleteTermination()
        {
            if(!TerminatingCurrentGame || completedTerminationId==terminatingId || GameManager.Client.GameState.CurrentState.ToString()!="Ended"
                || GameManager.Client.GameState.CommandStack.Count!=GameManager.Client.GameState.CurrentCommand)return;
            completedTerminationId=terminatingId;
            try{CaptureEnd(true);}catch(Exception e){Error(e);}
            var c=GameManager.Client;var human=c.GameState.GetFirstHumanPlayer();
            ResultScreen.Show(true,new ScoreDetails(),human.Id);
            ApplyTerminationResult(ResultScreen.instance);
        }
        internal void ApplyTerminationResult(ResultScreen result)
        {
            if(result==null || !MinimalInfinityResult)return;
            result.highscoreEnabled=false;result.headerContainer.headerText.text="YOU WIN";
            // Keep the native result backdrop / lifecycle, and provide a minimal
            // content layer directly beneath the native popup layer.
            foreach(var row in result.rows)row.SetActive(false);
            result.headerContainer.gameObject.SetActive(false);result.buttonRow.gameObject.SetActive(false);
            if(minimalResultRoot!=null && minimalResultOwner!=result.bg.Pointer)ClearMinimalResult();
            if(minimalResultRoot==null)
            {
                if(thinFont==null)DiscoverAppearance();
                var root=Rect("SinglePlayerLibrary.InfinityResult",result.bg.transform);minimalResultRoot=root.gameObject;
                minimalResultOwner=result.bg.Pointer;Stretch(root,0,0,0,0);
                var title=Label(root,"YOU WIN",60,thinFont);title.alignment=TMPro.TextAlignmentOptions.Center;
                var titleRect=title.rectTransform;titleRect.anchorMin=titleRect.anchorMax=new Vector2(.5f,.5f);titleRect.pivot=new Vector2(.5f,.5f);titleRect.sizeDelta=new Vector2(500,100);titleRect.anchoredPosition=new Vector2(0,45);
                var back=ActionButton(root,"RETURN TO DIRECTORY",()=>
                {if(!NativeSession.IsTerminatedReplay)returningTerminated=true;ClearMinimalResult();GameManager.ReturnToMenu();},new Color(0,.62f,.93f),Color.white);
                var br=back.GetComponent<RectTransform>();br.anchorMin=br.anchorMax=new Vector2(.5f,.5f);br.pivot=new Vector2(.5f,.5f);br.sizeDelta=new Vector2(260,48);br.anchoredPosition=new Vector2(0,-70);
                ResultScreen.DefaultSelectable=back;
            }
        }
        internal void ClearMinimalResult()
        {if(minimalResultRoot!=null){minimalResultRoot.SetActive(false);UnityEngine.Object.Destroy(minimalResultRoot);}minimalResultRoot=null;minimalResultOwner=IntPtr.Zero;}
    }
    [HarmonyPatch(typeof(IngameMenuScreen),"Show")]
    internal static class InfinitySettingsButton
    {static void Postfix(IngameMenuScreen __instance){LibraryScript.Runtime?.SetupTerminateButton(__instance);}}
    [HarmonyPatch(typeof(SettingsScreen),"Show")]
    internal static class InfinityMainSettingsHidden
    {static void Postfix(SettingsScreen __instance){if(__instance.TryCast<IngameMenuScreen>()==null)LibraryScript.Runtime?.HideTerminateButton();}}
    [HarmonyPatch]
    internal static class InfinityEndCommand
    {
        static MethodBase TargetMethod()=>AccessTools.DeclaredMethod(typeof(EndMatchCommand),"IsValid",new[]{typeof(GameState),typeof(string).MakeByRefType()});
        static bool Prefix(GameState __0,ref string __1,ref bool __result)
        {
            var c=GameManager.instance?.client;
            if(c==null || __0.Pointer!=c.GameState.Pointer || __0.Settings.RulesGameMode!=GameMode.Sandbox || __0.CurrentState.ToString()!="Started"
                || !(LibraryScript.Runtime?.TerminatingCurrentGame==true || NativeSession.IsTerminatedReplay))return true;
            __1=null;__result=true;return false;
        }
    }
    [HarmonyPatch(typeof(ResultScreen),"ShowInternal")]
    internal static class InfinityResultHeader
    {static void Postfix(ResultScreen __instance){LibraryScript.Runtime?.ApplyTerminationResult(__instance);}}
    [HarmonyPatch(typeof(ResultScreen),"Update")]
    internal static class InfinityResultVisibility
    {static void Postfix(ResultScreen __instance){if(ResultScreen.IsShowing())LibraryScript.Runtime?.ApplyTerminationResult(__instance);}}
    [HarmonyPatch(typeof(ResultScreen),"AddSingleplayerResults")]
    internal static class InfinitySkipScoreRows
    {static bool Prefix()=>LibraryScript.Runtime?.MinimalInfinityResult!=true;}
}
