[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('BrowserSwitchAssets.IconGenerator' -as [type])) {
    $source = @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
namespace BrowserSwitchAssets {
    public static class IconGenerator {
        static byte[] Encode(Bitmap bitmap) {
            using (MemoryStream stream = new MemoryStream()) {
                // PNG is supported by Windows for the 256 px frame; classic DIB frames
                // keep smaller sizes compatible with .NET Framework's Icon decoder.
                if (bitmap.Width == 256) {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    return stream.ToArray();
                }
                int size = bitmap.Width, maskStride = ((size + 31) / 32) * 4;
                byte[] row = new byte[size * 4], mask = new byte[maskStride * size];
                using (BinaryWriter writer = new BinaryWriter(stream)) {
                    writer.Write((uint)40); writer.Write(size); writer.Write(size * 2);
                    writer.Write((ushort)1); writer.Write((ushort)32);
                    writer.Write((uint)0); writer.Write((uint)(size * size * 4 + mask.Length));
                    writer.Write(0); writer.Write(0); writer.Write((uint)0); writer.Write((uint)0);
                    System.Drawing.Imaging.BitmapData data = bitmap.LockBits(new Rectangle(0,0,size,size), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try {
                        for (int y = size - 1; y >= 0; y--) {
                            Marshal.Copy(IntPtr.Add(data.Scan0,y * data.Stride),row,0,row.Length);
                            writer.Write(row);
                            for (int x = 0; x < size; x++)
                                if (row[x * 4 + 3] == 0) mask[(size - 1 - y) * maskStride + x / 8] |= (byte)(128 >> (x % 8));
                        }
                    } finally { bitmap.UnlockBits(data); }
                    writer.Write(mask);
                    return stream.ToArray();
                }
            }
        }
        public static void Save(string path, string previewPath) {
            List<byte[]> frames = new List<byte[]>();
            int[] sizes = new int[] {16, 24, 32, 48, 64, 128, 256};
            foreach (int size in sizes) {
                using (Bitmap bitmap = new Bitmap(size, size))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                using (SolidBrush background = new SolidBrush(Color.FromArgb(39, 90, 196)))
                using (Pen pen = new Pen(Color.White, 2.8f)) {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.Clear(Color.Transparent);
                    graphics.ScaleTransform(size / 32f, size / 32f);
                    graphics.FillEllipse(background, 1, 1, 30, 30);
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                    graphics.DrawLines(pen, new PointF[] {new PointF(7,11),new PointF(24,11),new PointF(20,7)});
                    graphics.DrawLine(pen,24,11,20,15);
                    graphics.DrawLines(pen, new PointF[] {new PointF(25,21),new PointF(8,21),new PointF(12,17)});
                    graphics.DrawLine(pen,8,21,12,25);
                    frames.Add(Encode(bitmap));
                }
            }
            using (BinaryWriter writer = new BinaryWriter(File.Create(path))) {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)frames.Count);
                uint offset = (uint)(6 + 16 * frames.Count);
                for (int i = 0; i < frames.Count; i++) {
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)0); writer.Write((byte)0);
                    writer.Write((ushort)1); writer.Write((ushort)32);
                    writer.Write((uint)frames[i].Length); writer.Write(offset); offset += (uint)frames[i].Length;
                }
                foreach (byte[] frame in frames) writer.Write(frame);
            }
            File.WriteAllBytes(previewPath, frames[frames.Count - 1]);
        }
    }
}
"@
    . (Join-Path $PSScriptRoot 'Common.ps1')
    $framework = Get-BrowserSwitchFramework
    $artifacts = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts'
    [IO.Directory]::CreateDirectory($artifacts) | Out-Null
    $sourceFile = Join-Path $artifacts 'IconGenerator.cs'
    $library = Join-Path $artifacts 'IconGenerator.dll'
    [IO.File]::WriteAllText($sourceFile,$source,[Text.UTF8Encoding]::new($true))
    & (Join-Path $framework 'csc.exe') /nologo /noconfig /target:library (('/out:' + $library)) (('/reference:' + (Join-Path $framework 'System.dll'))) (('/reference:' + (Join-Path $framework 'System.Drawing.dll'))) $sourceFile
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el generador de iconos.' }
    Add-Type -Path $library
}
$repo = Split-Path -Parent $PSScriptRoot
$folder = Join-Path $repo 'src\BrowserSwitch\Assets'
[IO.Directory]::CreateDirectory($folder) | Out-Null
$path = Join-Path $folder 'BrowserSwitch.ico'
$previewFolder = Join-Path $repo 'docs\assets'
[IO.Directory]::CreateDirectory($previewFolder) | Out-Null
$preview = Join-Path $previewFolder 'browserswitch.png'
[BrowserSwitchAssets.IconGenerator]::Save($path,$preview)
Write-Host ('Icono generado (16–256 px): ' + $path)