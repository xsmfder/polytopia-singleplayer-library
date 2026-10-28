using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace SinglePlayerLibrary
{
    public sealed class LibraryScript : PolyMod.Api.PolyScript
    {
        internal static LibraryRuntime Runtime;
        private GameObject host;
        public override void Load()
        {
            if (Runtime != null) throw new InvalidOperationException("Single Player Library is already loaded.");
            if (Application.version != LibraryStore.SupportedGame) throw new NotSupportedException("Unsupported game: " + Application.version);
            if (!ClassInjector.IsTypeRegisteredInIl2Cpp<LibraryLoop>()) ClassInjector.RegisterTypeInIl2Cpp<LibraryLoop>();
            var runtime = new LibraryRuntime();
            Runtime = runtime;
            try
            {
                runtime.Initialize();
                host = new GameObject("SinglePlayerLibrary"); UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<LibraryLoop>();
                Logger.LogInfo("Single Player Library 0.1.7 loaded");
            }
            catch { runtime.Dispose(); Runtime = null; throw; }
        }
        public override void Unload()
        {
            if (Runtime == null) return;
            if (NativeSession.Supported(GameManager.Client)) Runtime.Archive(GameManager.Client);
            Runtime.Dispose(); Runtime = null;
            if (host != null) UnityEngine.Object.Destroy(host);
        }
    }
    public sealed class LibraryLoop : MonoBehaviour
    {
        public LibraryLoop(IntPtr p) : base(p) { }
        void Update() { LibraryScript.Runtime?.Update(); }
        void OnApplicationQuit() { LibraryScript.Runtime?.OnQuit(); }
    }
    internal sealed partial class LibraryRuntime : IDisposable
    {
        internal LibraryStore Store;
        internal Harmony Patches;
        internal string LastError;
        internal bool Busy, AllowNewGame;
        private string currentUser;
        private float nextScan;
        private bool bridgeInstalled;
        internal void Initialize()
        {
            Patches = new Harmony("local.single_player_library");
            Patches.PatchAll(typeof(LibraryRuntime).Assembly);
            bridgeInstalled=BridgeCompatibility.Install(Patches);
            Log("Loaded " + Application.version);
        }
        internal static void Log(string text) => Debug.Log("[SinglePlayerLibrary] " + text);
        internal void Error(Exception e)
        {
            LastError = e.Message.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? e.GetType().Name; Log(e.ToString());
            try { if (Store != null) File.AppendAllText(Path.Combine(Store.Root, "errors.log"), DateTime.UtcNow.ToString("o") + " " + e + "\n"); }
            catch (Exception logError) { Log("Could not write error log: " + logError.Message); }
        }
        internal void EnsureStore()
        {
            string user = Paths.GetUserDirectory();
            if (String.IsNullOrEmpty(user)) throw new IOException("User data directory is not ready.");
            if (Store != null && currentUser == user) return;
            currentUser = user;
            Store = new LibraryStore(Path.Combine(user, "SinglePlayerLibrary"));
            ImportExisting();
        }
        internal void ImportExisting()
        {
            string marker = Path.Combine(Store.Backups, "migration-complete");
            string infinityMarker=Path.Combine(Store.Backups,"migration-infinity-v1");
            bool firstImport=!File.Exists(marker);
            if (!firstImport && File.Exists(infinityMarker)) return;
            var dir = LocalSaveFileUtils.GetSaveDirectoryPath(PolytopiaBackendBase.Game.GameType.SinglePlayer);
            if (Directory.Exists(dir)) foreach (string file in Directory.GetFiles(dir, "*.state"))
            {
                try
                {
                byte[] bytes = LibraryStore.ReadBounded(file);
                var client = NativeSession.Decode(bytes,false);
                if(!firstImport && client.GameState.Settings.RulesGameMode!=PolytopiaBackendBase.Game.GameMode.Sandbox)continue;
                string backup = Path.Combine(Store.Backups, Path.GetFileName(file));
                if (!File.Exists(backup)) LibraryStore.AtomicWrite(backup, bytes);
                client.gameId = Il2CppSystem.Guid.Parse(Path.GetFileNameWithoutExtension(file));
                if (NativeSession.Supported(client) && !File.Exists(Path.Combine(Store.Saves,LibraryStore.SafeId(client.gameId.ToString())+".polysave")))
                    Store.Save(NativeSession.Info(client, client.GameState.CurrentState.ToString() == "Ended"), bytes);
                }
                catch(Exception e) { Error(new IOException("Could not import "+Path.GetFileName(file)+"; original file was retained.",e)); }
            }
            if(firstImport)LibraryStore.AtomicWrite(marker, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")));
            LibraryStore.AtomicWrite(infinityMarker, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")));
        }
        internal void Archive(ClientBase client)
        {
            if (!NativeSession.Supported(client) || !client.IsReady || client.IsRecap
                || GameManager.Instance.client == null || client.Pointer != GameManager.Instance.client.Pointer
                || client.GameState.CurrentState.ToString() == "Unknown") return;
            if (client.GameState.CommandStack.Count != client.GameState.CurrentCommand)
                throw new IOException("The game is still replaying commands; wait before saving or switching games.");
            EnsureStore();
            Store.Save(NativeSession.Info(client, client.GameState.CurrentState.ToString() == "Ended"), NativeSession.Bytes(client, false));
        }
        // Retain the self-contained capture before any disk operation or native cleanup.
        // A full disk / denied directory must not destroy the only available transcript.
        readonly Dictionary<string, byte[]> captured = new Dictionary<string, byte[]>();
        private string pendingId;
        private string incompleteId;
        internal void CaptureEnd(bool won)
        {
            var client = GameManager.Client;
            if (!NativeSession.Supported(client)) return;
            var info = NativeSession.Info(client, true, won ? "Won" : "Lost");
            if (info.CompleteHistory && !captured.ContainsKey(info.Id))
            {
                var replayInfo=NativeSession.Info(client,true,won ? "Won" : "Lost");
                replayInfo.NativeEncoding="wrapped-client";
                captured[info.Id] = LibraryStore.Pack(replayInfo, NativeSession.Bytes(client, true));
            }
            pendingId = info.CompleteHistory ? info.Id : pendingId;
            if (!info.CompleteHistory) incompleteId = info.Id;
            EnsureStore();
            if (Store.Decided(info.Id)) { captured.Remove(info.Id); if(pendingId==info.Id)pendingId=null; return; }
            // Recovery saves are useful, but a failure here cannot prevent replay staging.
            try { Store.Save(info, NativeSession.Bytes(client, false)); } catch(Exception e) { Error(e); }
            if (info.CompleteHistory)
            {
                try { PersistCapture(info.Id); } catch(Exception e) { Error(e); }
            }
            else LastError = "This game has incomplete history; a complete replay cannot be saved.";
        }
        void PersistCapture(string id)
        {
            if(captured.TryGetValue(id,out var bytes))
                LibraryStore.AtomicWrite(Path.Combine(Store.Pending,LibraryStore.SafeId(id)+".pending"),bytes);
        }
        string CommitReplay(string id)
        {
            PersistCapture(id);
            var path=Store.CommitPending(id);
            captured.Remove(id); return path;
        }
        void DeclineReplay(string id)
        {
            Store.Decide(id,"declined"); captured.Remove(id);
        }
        internal void Update()
        {
            try
            {
                if(GameManager.instance==null)return;
                if (Time.unscaledTime >= nextScan)
                {
                    nextScan = Time.unscaledTime + .5f;
                    if (!bridgeInstalled) bridgeInstalled=BridgeCompatibility.Install(Patches);
                    if (UIManager.Exists) { EnsureStore(); UpdateMenu(); RecoverPending(); }
                }
                if(PageOpen) UpdatePageLayout();
                UpdateNavigation();
                NativeSession.CompleteOpening();
                UpdateLoading();
                UpdateReplayResultControls();
                UpdateTermination();
                if(MinimalInfinityResult && ResultScreen.IsShowing())ApplyTerminationResult(ResultScreen.instance);
                NativeSession.CorrectReplayIndices();
                UpdateDialog();
#if POLYLIB_TESTS
                TestTick();
#endif
            }
            catch (Exception e) { Error(e); nextScan = Time.unscaledTime + 5; }
        }
        void RecoverPending()
        {
            if (pendingId != null || Store == null) return;
            foreach (var id in captured.Keys.ToArray())
            {
                if(Store.Decided(id))captured.Remove(id);
                else { pendingId=id;return; }
            }
            foreach (var path in Directory.GetFiles(Store.Pending, "*.pending"))
            {
                var data = LibraryStore.Unpack(LibraryStore.ReadBounded(path));
                if (!Store.Decided(data.Info.Id)) { pendingId = data.Info.Id; break; }
            }
        }
        internal void OnQuit()
        {
            try { if (NativeSession.Supported(GameManager.Client)) Archive(GameManager.Client); }
            catch (Exception e) { Error(e); }
        }
        public void Dispose()
        {
            if (prompt != null && prompt.IsShowing()) prompt.Hide();
            ClosePage();EndLoading();DisposeTerminateButton();ClearMinimalResult(); Patches?.UnpatchSelf(); RestoreMenu();iconAssets?.Dispose();iconAssets=null;
        }
    }
    [HarmonyPatch(typeof(StartScreen_UI2), "ClickNewGame")]
    internal static class MenuClick
    {
        static bool Prefix()
        {
            var r = LibraryScript.Runtime;
            if (r == null || r.AllowNewGame || BridgeCompatibility.AutomationDepth > 0) return true;
            try { r.OpenPage(false); } catch (Exception e) { r.Error(e); }
            return false;
        }
    }
    [HarmonyPatch(typeof(ClientBase), "SaveSession")]
    internal static class Saved
    {
        static void Postfix(ClientBase __instance)
        { try { LibraryScript.Runtime?.Archive(__instance); } catch (Exception e) { LibraryScript.Runtime?.Error(e); } }
    }
    [HarmonyPatch(typeof(GameManager), "CreateSinglePlayerGame")]
    internal static class Creating
    {
        static bool Prefix()
        {
            try { LibraryScript.Runtime?.Archive(GameManager.Client);LibraryScript.Runtime?.CreationStarted(); return true; }
            catch (Exception e) { LibraryScript.Runtime?.Error(e); return false; }
        }
    }
    [HarmonyPatch(typeof(GameManager), "MatchEnded")]
    internal static class Ended
    {
        static bool Prefix(bool __0, ScoreDetails __1, byte __2)
        {
            if(LibraryScript.Runtime?.TerminatingCurrentGame==true){LibraryScript.Runtime.CompleteTermination();return false;}
            if (NativeSession.OwnsReplay(GameManager.Client)) { LibraryScript.Runtime?.ShowReplayResult(__0,__1,__2);return false; }
            try { LibraryScript.Runtime?.CaptureEnd(__0); } catch (Exception e) { LibraryScript.Runtime?.Error(e); }
            return true;
        }
    }
    [HarmonyPatch(typeof(GameManager), "TryCreateAndExecuteAICommands")]
    internal static class ReplayAiGuard
    {
        static bool Prefix() => !NativeSession.OwnsReplay(GameManager.Client);
    }
    [HarmonyPatch(typeof(ClientBase), "ShouldUploadHighScore")]
    internal static class ReplayScoreGuard
    {
        static bool Prefix(ClientBase __instance, ref bool __result)
        { if (!NativeSession.OwnsReplay(__instance) && !(LibraryScript.Runtime?.TerminatingCurrentGame==true && GameManager.Client?.Pointer==__instance.Pointer)) return true; __result = false; return false; }
    }
}
