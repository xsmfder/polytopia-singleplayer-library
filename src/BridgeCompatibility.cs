using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
namespace SinglePlayerLibrary
{
    // Optional adapter over the existing Bridge's menu assumptions. No Bridge dependency
    // or protocol replacement: its original validation, calls and receipts still run.
    internal static class BridgeCompatibility
    {
        [ThreadStatic] internal static int AutomationDepth;
        sealed class ResumeScope
        {
            internal UISquareButton_UI2 Button;
            internal bool Active, Enabled;
        }
        internal static bool Install(Harmony patches)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("PolytopiaAgent.BridgeMod",false)).FirstOrDefault(t=>t!=null);
            if(type==null)return false;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var start=type.GetMethod("NativeSetupStep",flags);
            var load=type.GetMethod("GameLifecycleAction",flags);
            if(start!=null)patches.Patch(start,prefix:Hook(nameof(BeforeSetup)),finalizer:Hook(nameof(AfterSetup)));
            if(load!=null)patches.Patch(load,prefix:Hook(nameof(BeforeLifecycle)),finalizer:Hook(nameof(AfterLifecycle)));
            LibraryRuntime.Log("Optional Agent Bridge menu adapter installed");
            return true;
        }
        static HarmonyMethod Hook(string name)=>new HarmonyMethod(typeof(BridgeCompatibility).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static));
        static void BeforeSetup(string __0, out bool __state)
        {
            __state=LibraryScript.Runtime!=null && __0=="start";
            if(__state)AutomationDepth++;
        }
        static Exception AfterSetup(Exception __exception,bool __state)
        {if(__state)AutomationDepth=Math.Max(0,AutomationDepth-1);return __exception;}
        static void BeforeLifecycle(string __0,out ResumeScope __state)
        {
            __state=null;
            if(LibraryScript.Runtime==null||!LibraryScript.Runtime.NativeResumeAvailable||__0!="load_singleplayer"||!UIManager.Exists)return;
            var menu=UIManager.Instance.GetCurrentScreen()?.TryCast<StartScreen_UI2>();
            if(menu==null||menu.resumeButton==null)return;
            var b=menu.resumeButton;
            __state=new ResumeScope{Button=b,Active=b.IsActive(),Enabled=b.ButtonEnabled};
            b.SetActive(true);
        }
        static Exception AfterLifecycle(Exception __exception,ResumeScope __state)
        {
            try { if(__state?.Button!=null){__state.Button.ButtonEnabled=__state.Enabled;__state.Button.SetActive(__state.Active);} }
            catch(Exception e){LibraryRuntime.Log("Bridge menu restore: "+e.Message);}
            return __exception;
        }
    }
    [HarmonyPatch(typeof(GameManager),"ReturnToMenu")]
    internal static class LocalReplayReturn
    {
        static void Prefix()
        {
            var r=LibraryScript.Runtime;var c=GameManager.Client;
            if(r!=null&&c?.IsReplay==true&&r.OwnedReplayId==c.gameId.ToString())r.ReturningFromReplay=true;
        }
    }
    [HarmonyPatch(typeof(UIManager),"ShowScreen",new Type[]{typeof(UIConstants.Screens),typeof(bool),typeof(UIDeepLinkData)})]
    internal static class LocalReplayDestination
    {
        static void Prefix(ref UIConstants.Screens __0)
        {
            var r=LibraryScript.Runtime;
            if(r?.ReturningFromReplay==true&&(__0.ToString()=="MultiplayerScreen"||__0.ToString()=="ReplaysScreen"))
                __0=UIConstants.Screens.StartScreen;
        }
    }
}
