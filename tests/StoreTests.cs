using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using SinglePlayerLibrary;
public static class StoreTests
{
 static int assertions;
 static void Check(bool value,string label){assertions++;if(!value)throw new Exception(label);}
 static void Reject(Action f,string label){bool threw=false;try{f();}catch(Exception e)when(e is InvalidDataException||e is IOException||e is ArgumentException||e is UnauthorizedAccessException){threw=true;}Check(threw,label);}
 static EntryInfo Info(string id=null)=>new EntryInfo {Id=id??Guid.NewGuid().ToString(),Tribe="Imperius",Mode="Perfection",Turn=30,NativeVersion=123,CommandCount=36,Finished=true,CompleteHistory=true,SavedUtc=DateTime.UtcNow};
 public static string Run(string root)
 {
  var store=new LibraryStore(root);var i=Info();byte[] payload=Encoding.UTF8.GetBytes("native replay fixture");
  byte[] packed=LibraryStore.Pack(i,payload);var read=LibraryStore.Unpack(packed);
  Check(read.Info.Id==i.Id&&read.Payload.SequenceEqual(payload),"portable round trip");
  foreach(int n in new[]{0,1,8,16,packed.Length-1})Reject(()=>LibraryStore.Unpack(packed.Take(n).ToArray()),"truncated "+n);
  var corrupt=(byte[])packed.Clone();corrupt[20]^=1;Reject(()=>LibraryStore.Unpack(corrupt),"corrupt metadata");
  corrupt=(byte[])packed.Clone();corrupt[packed.Length-33]^=1;Reject(()=>LibraryStore.Unpack(corrupt),"corrupt payload");
  var unsupported=Info();unsupported.GameVersion="next";Reject(()=>LibraryStore.Pack(unsupported,payload),"version mismatch");
  unsupported=Info();unsupported.Format=2;Reject(()=>LibraryStore.Pack(unsupported,payload),"format mismatch");
  unsupported=Info();unsupported.Id="../escape";Reject(()=>LibraryStore.Pack(unsupported,payload),"unsafe id");
  unsupported=Info();unsupported.Mode="Multiplayer";Reject(()=>LibraryStore.Pack(unsupported,payload),"wrong mode");
  Reject(()=>LibraryStore.Pack(i,Array.Empty<byte>()),"empty native payload");
  unsupported=Info();unsupported.NativeEncoding="unknown";Reject(()=>LibraryStore.Pack(unsupported,payload),"unknown native encoding");
  var nativeSave=Info();nativeSave.NativeEncoding="local-client";
  Check(LibraryStore.Unpack(LibraryStore.Pack(nativeSave,payload)).Info.NativeEncoding=="local-client","native save encoding preserved");
  Reject(()=>store.Stage(nativeSave,payload),"local state cannot masquerade as replay payload");
  var a=Info();a.Finished=false;store.Save(a,payload);var b=Info();b.Finished=false;store.Save(b,payload);
  Check(store.List(false).Count==2,"two independent saves");
  a.Turn=12;store.Save(a,Encoding.UTF8.GetBytes("updated A"));Check(store.List(false).Count==2,"saving A preserves B");
  var completed=Info();store.Save(completed,payload);Check(store.List(false).Count==2,"finished save absent from ongoing");
  store.Stage(i,payload);Check(File.Exists(Path.Combine(store.Pending,i.Id+".pending")),"durable pending survives restart");
  var restarted=new LibraryStore(root);string rp=restarted.CommitPending(i.Id);Check(File.Exists(rp)&&restarted.Decided(i.Id),"commit survives restart");
  Check(!File.Exists(Path.Combine(store.Pending,i.Id+".pending")),"pending cleaned only after commit");
  File.Copy(rp,Path.Combine(store.Replays,"copy.polyreplay"));Check(store.List(true).Count==1,"duplicate replay deduplicated");
  File.WriteAllText(Path.Combine(store.Replays,"broken.polyreplay"),"broken");var rows=store.List(true);
  Check(rows.Count==2&&rows.Count(x=>x.Error!=null)==1,"bad replay doesn't hide valid replay");
  var decline=Info();store.Stage(decline,payload);store.Decide(decline.Id,"declined");Check(store.Decided(decline.Id)&&!File.Exists(Path.Combine(store.Replays,decline.Id+".polyreplay")),"decline creates no share file");
  var partial=Info();partial.CompleteHistory=false;Reject(()=>store.Stage(partial,payload),"partial replay rejected");
  var retry=Info();store.Stage(retry,payload);Directory.CreateDirectory(Path.Combine(store.Replays,retry.Id+".polyreplay"));
  Reject(()=>store.CommitPending(retry.Id),"write failure reported");
  Check(!store.Decided(retry.Id)&&File.Exists(Path.Combine(store.Pending,retry.Id+".pending")),"write failure retains pending and decision");
  var exported=LibraryStore.Unpack(File.ReadAllBytes(rp));Check(exported.Info.Id==i.Id&&exported.Payload.SequenceEqual(payload),"file is self contained");
  string original=Path.Combine(store.Saves,a.Id+".polysave");var originalBytes=File.ReadAllBytes(original);string originalHash=LibraryStore.Hash(originalBytes);
  string copied=store.CopySave(original,originalHash);var duplicate=LibraryStore.Unpack(File.ReadAllBytes(copied));var source=LibraryStore.Unpack(originalBytes);
  Check(duplicate.Info.Id!=a.Id&&duplicate.Payload.SequenceEqual(source.Payload),"copy has independent ID and exact native payload");
  Check(duplicate.Info.SavedUtc==source.Info.SavedUtc&&duplicate.Info.Turn==source.Info.Turn&&duplicate.Info.Tribe==source.Info.Tribe,"copy preserves displayed details");
  duplicate.Info.Turn++;store.Save(duplicate.Info,Encoding.UTF8.GetBytes("advanced copy"));Check(File.ReadAllBytes(original).SequenceEqual(originalBytes),"saving copy leaves original untouched");
  Reject(()=>store.CopySave(original,"stale hash"),"stale copy refused");
  Reject(()=>store.DeleteItem(original,false,"stale hash"),"stale deletion refused");
  Reject(()=>store.DeleteItem(rp,false,LibraryStore.Hash(File.ReadAllBytes(rp))),"wrong directory refused");
  store.DeleteItem(copied,false,LibraryStore.Hash(File.ReadAllBytes(copied)));Check(!File.Exists(copied)&&File.ReadAllBytes(original).SequenceEqual(originalBytes),"delete removes only selected save");
  store.DeleteItem(rp,true,LibraryStore.Hash(File.ReadAllBytes(rp)));Check(!File.Exists(rp)&&File.Exists(original),"replay deletion preserves ongoing");
  var infinity=Info();infinity.Mode="Infinity";infinity.Finished=false;string infinityPath=store.Save(infinity,payload);Check(LibraryStore.Unpack(File.ReadAllBytes(infinityPath)).Info.Mode=="Infinity","Infinity save is accepted and preserves mode");
  Check(File.Exists(store.CopySave(infinityPath,LibraryStore.Hash(File.ReadAllBytes(infinityPath)))),"Infinity can be copied independently");
  var terminated=Info();terminated.Mode="Infinity";terminated.Terminated=true;Check(LibraryStore.Unpack(LibraryStore.Pack(terminated,payload)).Info.Terminated,"manual Infinity end marker round trip");
  terminated.Mode="Perfection";Reject(()=>LibraryStore.Pack(terminated,payload),"termination marker restricted to Infinity");terminated.Mode="Infinity";terminated.Finished=false;Reject(()=>LibraryStore.Pack(terminated,payload),"termination marker requires finished game");
  return "PASS: "+assertions+" storage assertions";
 }
}
