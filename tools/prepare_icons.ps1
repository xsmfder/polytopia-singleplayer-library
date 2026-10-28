param([int]$Size=128)
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$assets=Join-Path $project 'assets'
Add-Type -AssemblyName System.Drawing
$drawingReferences=@('System.Drawing.Common','System.Drawing.Primitives','System.Runtime','System.Runtime.InteropServices')
foreach($name in @('System.Private.Windows.GdiPlus','System.Private.Windows.Core')){if(Test-Path (Join-Path $PSHOME ($name+'.dll'))){$drawingReferences+=$name}}
Add-Type -ReferencedAssemblies $drawingReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class ImportReplayIcons {
 public static void Convert(string input,string output,int size) {
  using(var original=new Bitmap(input))
  using(var alpha=new Bitmap(original.Width,original.Height,PixelFormat.Format32bppArgb)) {
   using(var g=Graphics.FromImage(alpha))g.DrawImageUnscaled(original,0,0);
   var data=alpha.LockBits(new Rectangle(0,0,alpha.Width,alpha.Height),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
   int left=alpha.Width,top=alpha.Height,right=0,bottom=0;
   try {
    var bytes=new byte[data.Stride*alpha.Height];Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
    for(int y=0;y<alpha.Height;y++)for(int x=0;x<alpha.Width;x++) {
     int i=y*data.Stride+x*4;byte coverage=bytes[i+2];
     // Approved source is white over exact blue with R=0. Its R channel is
     // therefore precisely the glyph coverage, including the folder seam.
     bytes[i]=bytes[i+1]=bytes[i+2]=255;bytes[i+3]=coverage;
     if(coverage>8){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
    }
    Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
   } finally {alpha.UnlockBits(data);}
   if(left>=right || top>=bottom)throw new InvalidOperationException("Empty icon");
   int side=(int)Math.Ceiling(Math.Max(right-left+1,bottom-top+1)*1.12);
   var crop=new Rectangle((left+right+1-side)/2,(top+bottom+1-side)/2,side,side);
   using(var result=new Bitmap(size,size,PixelFormat.Format32bppArgb)) {
    using(var g=Graphics.FromImage(result)) {
     g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
     g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.Clear(Color.Transparent);
     g.DrawImage(alpha,new Rectangle(0,0,size,size),crop,GraphicsUnit.Pixel);
    }
    // GDI's resampling may round RGB at partly transparent edges. Keep RGB
    // white everywhere and retain only its antialiased coverage in alpha.
    var finalData=result.LockBits(new Rectangle(0,0,size,size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
    try {
     var pixels=new byte[finalData.Stride*size];Marshal.Copy(finalData.Scan0,pixels,0,pixels.Length);
     for(int y=0;y<size;y++)for(int x=0;x<size;x++){int i=y*finalData.Stride+x*4;pixels[i]=pixels[i+1]=pixels[i+2]=255;}
     Marshal.Copy(pixels,0,finalData.Scan0,pixels.Length);
    } finally {result.UnlockBits(finalData);}
    result.Save(output,ImageFormat.Png);
   }
  }
 }
}
'@
foreach($name in @('refresh','folder')) {
 [ImportReplayIcons]::Convert((Join-Path $assets "sources/$name-blue.png"),(Join-Path $assets "$name.png"),$Size)
}
Write-Output "Imported two approved icons as $Size x $Size transparent white PNGs."
