using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SinglePlayerLibrary
{
    public sealed class EntryInfo
    {
        public int Format { get; set; } = 1;
        public string GameVersion { get; set; } = "2.17.3.16375";
        public string Id { get; set; }
        public string Tribe { get; set; }
        public string Mode { get; set; }
        public uint Turn { get; set; }
        public DateTime SavedUtc { get; set; }
        public bool Finished { get; set; }
        public bool CompleteHistory { get; set; }
        public string NativeEncoding { get; set; } = "wrapped-client";
        public int NativeVersion { get; set; }
        public int CommandCount { get; set; }
        public string StateHash { get; set; }
        public string Outcome { get; set; }
        public bool Terminated { get; set; }
    }
    public sealed class LibraryItem
    {
        public EntryInfo Info { get; set; }
        public string Path { get; set; }
        public string Error { get; set; }
    }
    public sealed class LibraryStore
    {
        public const int MaxPayload = 64 * 1024 * 1024;
        public const string SupportedGame = "2.17.3.16375";
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("POLYRPL1");
        static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = true };
        public string Root { get; }
        public string Saves => System.IO.Path.Combine(Root, "Ongoing");
        public string Replays => System.IO.Path.Combine(Root, "Replays");
        public string Pending => System.IO.Path.Combine(Root, "Pending");
        public string Backups => System.IO.Path.Combine(Root, "Backups");
        public LibraryStore(string root)
        {
            Root = System.IO.Path.GetFullPath(root);
            foreach (var p in new[] { Saves, Replays, Pending, Backups }) Directory.CreateDirectory(p);
        }
        public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        public static string SafeId(string id) => Guid.Parse(id).ToString("D");
        public static void AtomicWrite(string path, byte[] data)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { f.Write(data, 0, data.Length); f.Flush(true); }
                for (int attempt = 0; ; attempt++)
                {
                    try { File.Move(temp, path, true); break; }
                    catch (IOException) when (attempt < 8) { System.Threading.Thread.Sleep(5); }
                    catch (UnauthorizedAccessException) when (attempt < 8 && !Directory.Exists(path)) { System.Threading.Thread.Sleep(5); }
                }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static byte[] Pack(EntryInfo info, byte[] payload)
        {
            Validate(info, payload);
            byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(info, Json);
            if (metadata.Length > 65536) throw new InvalidDataException("Metadata is too large.");
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream, Encoding.UTF8, true);
            w.Write(Magic); w.Write(metadata.Length); w.Write(payload.Length);
            w.Write(metadata); w.Write(payload);
            w.Flush();
            byte[] body = stream.ToArray();
            w.Write(SHA256.HashData(body)); w.Flush();
            return stream.ToArray();
        }
        public static (EntryInfo Info, byte[] Payload) Unpack(byte[] file)
        {
            if (file.Length < 50 || file.Length > MaxPayload + 65536 + 48) throw new InvalidDataException("Invalid or incomplete replay size.");
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(file.AsSpan(0, file.Length - 32)), file.AsSpan(file.Length - 32)))
                throw new InvalidDataException("File is incomplete or its checksum is invalid.");
            using var r = new BinaryReader(new MemoryStream(file, false), Encoding.UTF8);
            if (!r.ReadBytes(8).SequenceEqual(Magic)) throw new InvalidDataException("Unsupported file format.");
            int ml = r.ReadInt32(), pl = r.ReadInt32();
            if (ml <= 0 || ml > 65536 || pl <= 0 || pl > MaxPayload || (long)ml + pl + 48 != file.Length)
                throw new InvalidDataException("Invalid file lengths.");
            var info = JsonSerializer.Deserialize<EntryInfo>(r.ReadBytes(ml));
            byte[] payload = r.ReadBytes(pl); Validate(info, payload);
            return (info, payload);
        }
        public static void Validate(EntryInfo i, byte[] payload)
        {
            if (i == null || i.Format != 1 || i.GameVersion != SupportedGame || !Guid.TryParse(i.Id, out _))
                throw new InvalidDataException("Unsupported replay or game version.");
            if (i.Mode != "Domination" && i.Mode != "Perfection" && i.Mode != "Infinity") throw new InvalidDataException("Unsupported single-player mode.");
            if(i.Terminated && (i.Mode!="Infinity" || !i.Finished))throw new InvalidDataException("Invalid terminated-game metadata.");
            if (payload == null || payload.Length == 0 || payload.Length > MaxPayload) throw new InvalidDataException("Invalid native data size.");
            if (i.NativeEncoding != "wrapped-client" && i.NativeEncoding != "local-client") throw new InvalidDataException("Unsupported native encoding.");
            if (i.NativeVersion <= 0 || i.CommandCount < 0 || i.CommandCount > ushort.MaxValue)
                throw new InvalidDataException("Invalid native replay metadata.");
            if (i.Tribe == null || i.Tribe.Length > 128 || i.Outcome?.Length > 128) throw new InvalidDataException("Invalid metadata.");
        }
        public string Save(EntryInfo info, byte[] state)
        {
            var path = System.IO.Path.Combine(Saves, SafeId(info.Id) + ".polysave");
            AtomicWrite(path, Pack(info, state)); return path;
        }
        public string Stage(EntryInfo info, byte[] replay)
        {
            if (!info.Finished || !info.CompleteHistory || info.NativeEncoding != "wrapped-client") throw new InvalidDataException("Complete finished history is required.");
            var path = System.IO.Path.Combine(Pending, SafeId(info.Id) + ".pending");
            AtomicWrite(path, Pack(info, replay)); return path;
        }
        public string CommitPending(string id)
        {
            var path = System.IO.Path.Combine(Pending, SafeId(id) + ".pending");
            byte[] bytes = ReadBounded(path); var data = Unpack(bytes);
            if (!data.Info.Finished || !data.Info.CompleteHistory || data.Info.NativeEncoding != "wrapped-client") throw new InvalidDataException("Incomplete history.");
            var target = System.IO.Path.Combine(Replays, SafeId(id) + ".polyreplay");
            AtomicWrite(target, bytes);
            Decide(id, "saved"); return target;
        }
        public void Decide(string id, string decision)
        {
            id = SafeId(id);
            // Persist decision before deleting the staging file; a crash cannot prompt twice.
            AtomicWrite(System.IO.Path.Combine(Pending, id + ".decision"), Encoding.UTF8.GetBytes(decision));
            var path = System.IO.Path.Combine(Pending, id + ".pending");
            if (File.Exists(path)) File.Delete(path);
            // Keep finished saves as recovery data, outside the ongoing listing.
        }
        public bool Decided(string id) => File.Exists(System.IO.Path.Combine(Pending, SafeId(id) + ".decision"));
        public static byte[] ReadBounded(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length < 1 || stream.Length > MaxPayload + 65584) throw new InvalidDataException("Invalid file size.");
            byte[] data = new byte[checked((int)stream.Length)];
            int offset = 0;
            while (offset < data.Length) { int n = stream.Read(data, offset, data.Length - offset); if (n == 0) throw new EndOfStreamException(); offset += n; }
            return data;
        }
        string BoundItem(string path,bool replay)
        {
            string full=System.IO.Path.GetFullPath(path);
            string directory=System.IO.Path.GetFullPath(replay?Replays:Saves);
            if(!String.Equals(System.IO.Path.GetDirectoryName(full),directory,StringComparison.OrdinalIgnoreCase)
                || !String.Equals(System.IO.Path.GetExtension(full),replay?".polyreplay":".polysave",StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The selected file is outside the library.");
            if(new FileInfo(full).LinkTarget!=null)throw new IOException("Linked library files cannot be changed here.");
            return full;
        }
        public string CopySave(string path,string expectedHash)
        {
            path=BoundItem(path,false);byte[] bytes=ReadBounded(path);
            if(Hash(bytes)!=expectedHash)throw new IOException("This save changed. Open its details again.");
            var data=Unpack(bytes);if(data.Info.Finished)throw new InvalidDataException("Only ongoing games can be copied.");
            // Native client IDs are restored from envelope metadata. A fresh identity
            // makes subsequent play/save of the copy independent of the original.
            data.Info.Id=Guid.NewGuid().ToString("D");
            return Save(data.Info,data.Payload);
        }
        public void DeleteItem(string path,bool replay,string expectedHash)
        {
            path=BoundItem(path,replay);byte[] bytes=ReadBounded(path);
            if(Hash(bytes)!=expectedHash)throw new IOException("This file changed. Open its details again before deleting it.");
            Unpack(bytes);File.Delete(path);
        }
        public List<LibraryItem> List(bool replay)
        {
            var result = new List<LibraryItem>(); var seen = new HashSet<string>();
            foreach (string path in Directory.EnumerateFiles(replay ? Replays : Saves, replay ? "*.polyreplay" : "*.polysave"))
            {
                try
                {
                    var d = Unpack(ReadBounded(path));
                    if (replay && (!d.Info.Finished || !d.Info.CompleteHistory || d.Info.NativeEncoding != "wrapped-client")) throw new InvalidDataException("Incomplete replay history.");
                    if (!replay && d.Info.Finished) continue;
                    string identity = d.Info.Id + ":" + Hash(d.Payload);
                    if (seen.Add(identity)) result.Add(new LibraryItem { Info = d.Info, Path = path });
                }
                catch (Exception e) when (e is InvalidDataException || e is IOException || e is UnauthorizedAccessException || e is JsonException || e is ArgumentException)
                { result.Add(new LibraryItem { Path = path, Error = e.Message }); }
            }
            return result.OrderByDescending(x => x.Info?.SavedUtc ?? DateTime.MinValue).ToList();
        }
    }
}
