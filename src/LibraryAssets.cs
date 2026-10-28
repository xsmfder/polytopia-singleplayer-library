using System;
using System.IO;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace SinglePlayerLibrary
{
    internal sealed class LibraryIconAssets : IDisposable
    {
        internal Sprite Refresh { get; private set; }
        internal Sprite Folder { get; private set; }
        Texture2D refreshTexture,folderTexture;
        internal bool IsValid => Refresh!=null && Folder!=null && refreshTexture!=null && folderTexture!=null;
        internal LibraryIconAssets()
        {
            try {Refresh=Read("refresh",out refreshTexture);Folder=Read("folder",out folderTexture);}
            catch {Dispose();throw;}
        }
        static Sprite Read(string name,out Texture2D texture)
        {
            texture=null;
            using var input=typeof(LibraryIconAssets).Assembly.GetManifestResourceStream("SinglePlayerLibrary.Assets."+name+".png");
            if(input==null || input.Length>256*1024)throw new InvalidDataException("Missing or oversized icon: "+name);
            using var memory=new MemoryStream();input.CopyTo(memory);
            texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(texture,(Il2CppStructArray<byte>)memory.ToArray(),true))throw new InvalidDataException("Cannot read icon: "+name);
            texture.name="SinglePlayerLibrary."+name+".texture";texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
            // Unity does not trace references held by the external managed mod
            // when unloading unused native assets between scenes/pages.
            texture.hideFlags=HideFlags.DontUnloadUnusedAsset;
            var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));
            sprite.name="SinglePlayerLibrary."+name;sprite.hideFlags=HideFlags.DontUnloadUnusedAsset;return sprite;
        }
        public void Dispose()
        {
            if(Refresh!=null)UnityEngine.Object.Destroy(Refresh);if(Folder!=null)UnityEngine.Object.Destroy(Folder);
            if(refreshTexture!=null)UnityEngine.Object.Destroy(refreshTexture);if(folderTexture!=null)UnityEngine.Object.Destroy(folderTexture);
            Refresh=Folder=null;refreshTexture=folderTexture=null;
        }
    }
}
