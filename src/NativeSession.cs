using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PolytopiaBackendBase.Game;

namespace SinglePlayerLibrary
{
    internal static class NativeSession
    {
        internal static bool Supported(ClientBase c) => c != null && !c.IsReplay && c.clientType == ClientBase.ClientType.Local
            && c.GameState != null && !c.GameState.IsTutorial() && !c.GameState.IsWeeklyChallenge()
            && (c.GameState.Settings.RulesGameMode == GameMode.Domination || c.GameState.Settings.RulesGameMode == GameMode.Perfection || c.GameState.Settings.RulesGameMode == GameMode.Sandbox);

        internal static string ModeName(GameSettings settings) => settings.RulesGameMode==GameMode.Sandbox?"Infinity":settings.RulesGameMode.ToString();

        internal static byte[] Bytes(ClientBase client, bool replay)
        {
            if(!replay) return DiskSerializationHelpers.ToLZ4CompressedByteArray(client,client.GameState.Version).ToArray();
            var wrapper = new ClientSerializationWrapper(client);
            if (replay) wrapper.ForceSaveAsReplay();
            var bytes = DiskSerializationHelpers.ToLZ4CompressedByteArray(wrapper, client.GameState.Version);
            return bytes.ToArray();
        }
        internal static ClientBase Decode(byte[] bytes, bool wrapped)
        {
            ClientBase client; int version;
            if(wrapped)
            {
                ClientSerializationWrapper wrapper;
                if (!DiskSerializationHelpers.FromLZ4CompressedByteArray((Il2CppStructArray<byte>)bytes, out wrapper, out version) || wrapper == null)
                    throw new InvalidDataException("The game could not read the native replay session.");
                client = wrapper.GetDeserializedClient();
            }
            else
            {
                LocalClient local;
                if(!DiskSerializationHelpers.FromLZ4CompressedByteArray((Il2CppStructArray<byte>)bytes,out local,out version))
                    throw new InvalidDataException("The game could not read the native single-player save.");
                client=local;
            }
            if (client == null || client.GameState == null || client.initialGameState == null)
                throw new InvalidDataException("Native session data is incomplete.");
            return client;
        }
        internal static bool HistoryComplete(ClientBase client)
        {
            var initial = client.initialGameState; var state = client.GameState;
            return initial != null && state != null && initial.CurrentCommand == 0
                && state.CommandStack != null && state.CommandStack.Count == state.CurrentCommand
                && initial.CurrentTurn <= 1 && initial.Seed == state.Seed;
        }
        internal static EntryInfo Info(ClientBase client, bool finished, string outcome = null)
        {
            var state = client.GameState;
            return new EntryInfo
            {
                Id = client.gameId.ToString(), Mode = ModeName(state.Settings),
                Tribe = state.GetFirstHumanPlayer()?.tribe.ToString() ?? "Unknown", Turn = state.CurrentTurn,
                NativeEncoding = "local-client", NativeVersion = state.Version, CommandCount = state.CurrentCommand, SavedUtc = DateTime.UtcNow,
                CompleteHistory = HistoryComplete(client), Finished = finished, Outcome = outcome,
                Terminated = finished && state.Settings.RulesGameMode==GameMode.Sandbox && state.CommandStack.Count>0 && state.CommandStack[state.CommandStack.Count-1].GetIl2CppType().Name=="EndMatchCommand",
                StateHash = StateHash(state)
            };
        }
        internal static string StateHash(GameState state)
        {
            return LibraryStore.Hash(SerializationHelpers.ToByteArray(state, state.Version).ToArray());
        }
        static ClientBase opening;
        static ClientBase annotatedReplay;
        static string replayOutcome;
        static bool replayTerminated;
        internal static bool IsTerminatedReplay => replayTerminated && OwnsReplay(GameManager.instance?.client);
        static Dictionary<byte,int> eliminationIndices;
        internal static bool OwnsReplay(ClientBase client) => client!=null && client.IsReplay && annotatedReplay!=null && client.Pointer==annotatedReplay.Pointer;
        internal static bool ReplayHumanWon(bool fallback)
        {
            if(replayOutcome=="Won")return true;
            if(replayOutcome=="Lost")return false;
            var state=annotatedReplay?.GameState;var human=state?.GetFirstHumanPlayer();
            if(human==null)return fallback;
            if(human.wipedAtCommandIndex>=0 || human.resignedAtCommandIndex>=0)return false;
            PlayerState winner;
            return state.TryGetWinner(out winner) && winner!=null ? winner.Id==human.Id : fallback;
        }
        internal static void CorrectReplayIndices()
        {
            var client=GameManager.instance?.client;
            if(client==null || !client.IsReplay || annotatedReplay==null || client.Pointer!=annotatedReplay.Pointer || client.GameState==null)return;
            foreach(var player in client.GameState.PlayerStates)
            {
                // Native fast-forward records this UI annotation after advancing its
                // cursor; local live execution records the zero-based command index.
                // Preserve the original annotation only after elimination has occurred.
                if(eliminationIndices.TryGetValue(player.Id,out var index) && index>=0
                    && player.wipedAtCommandIndex>=0 && client.GameState.CurrentCommand>index)
                    player.wipedAtCommandIndex=index;
            }
        }

        internal static void CompleteOpening()
        {
            var c = opening;
            if (c == null) return;
            if (GameManager.Client == null || GameManager.Client.Pointer != c.Pointer) { opening = null; return; }
            if (!c.IsReady || !GameManager.Instance.isLevelLoaded || GameManager.Instance.isLoadingGame
                || UIManager.Instance.CurrentScreen.ToString() != "Hud" || PopupManager.PopupShowing || GameManager.Instance.isPaused) return;
            if (!c.IsReplay) c.ActionManager.Resume();
            opening = null;
        }
        internal static void Open(byte[] bytes, bool replay, EntryInfo info)
        {
            var client = Decode(bytes,info.NativeEncoding=="wrapped-client");
            client.gameId = Il2CppSystem.Guid.Parse(info.Id);
            if (client.GameState.Version != info.NativeVersion) throw new InvalidDataException("Native data version does not match metadata.");
            if (replay != client.IsReplay || (!replay && !Supported(client)))
                throw new InvalidDataException("Incorrect native session type.");
            if (replay)
            {
                if (!HistoryComplete(client) || client.GameState.CurrentState.ToString() != "Ended")
                    throw new InvalidDataException("The replay does not contain a complete finished history.");
                // Replay starts from a detached initial frame with the full recorded transcript.
                // Loading the terminal frame here leaks Ended into cached intermediate frames.
                eliminationIndices=new Dictionary<byte,int>();
                foreach(var player in client.GameState.PlayerStates)eliminationIndices[player.Id]=player.wipedAtCommandIndex;
                annotatedReplay=client;replayOutcome=info.Outcome;replayTerminated=info.Terminated;
                var transcript = client.GameState.CommandStack;
                GameState initialFrame;
                if (!SerializationHelpers.FromByteArray(SerializationHelpers.ToByteArray(client.initialGameState, client.initialGameState.Version), out initialFrame))
                    throw new InvalidDataException("Could not restore the initial replay frame.");
                initialFrame.CommandStack = transcript;
                client.currentGameState = initialFrame;
            }
            if(!replay){annotatedReplay=null;eliminationIndices=null;replayOutcome=null;replayTerminated=false;}
            GameManager.Instance.SetLoadingGame(true);
            if (replay)
            {
                // This local adapter never calls GameManager.OpenReplay or any backend lookup.
                GameManager.Instance.SetReplayClient(client.Cast<ReplayClient>());
            }
            else
            {
                GameManager.Instance.SetLocalClient();
                // Preserve all deserialized player/seen-command/session fields.
                GameManager.Instance.client = client;
            }
            client.lastSeenCommand = replay ? (ushort)0 : client.GameState.CurrentCommand;
            client.UpdateGameStateImmediate(client.GameState, StateUpdateReason.RecapSkipped);
            client.PrepareSession();
            opening = client;
            GameManager.Instance.LoadLevel();
        }
    }
}
