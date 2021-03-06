﻿using Remotely.ScreenCast.Core.Capture;
using Remotely.ScreenCast.Core.Services;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Remotely.ScreenCast.Win.Capture
{
    public class DXCapture : ICapturer
    {
        private Adapter1 adapter;
        private SharpDX.Direct3D11.Device device;
        private OutputDuplication duplicatedOutput;
        private Factory1 factory;
        private int height;
        private Output output;
        private Output1 output1;
        private Texture2D screenTexture;
        private Texture2DDescription textureDesc;
        private int width;
        public bool CaptureFullscreen { get; set; } = true;
        public Bitmap CurrentFrame { get; set; }
        public Rectangle CurrentScreenBounds { get; private set; }
        public bool NeedsInit { get; set; } = true;
        public Bitmap PreviousFrame { get; set; }
        public event EventHandler<Rectangle> ScreenChanged;
        public int SelectedScreen { get; private set; } = 0;

        public DXCapture()
        {
            Init();
        }

        public void Capture()
        {
            try
            {
                if (NeedsInit)
                {
                    duplicatedOutput?.Dispose();
                    Init();
                    NeedsInit = false;
                }

                PreviousFrame = (Bitmap)CurrentFrame.Clone();

                SharpDX.DXGI.Resource screenResource;
                OutputDuplicateFrameInformation duplicateFrameInformation;

                // Try to get duplicated frame within given time is ms
                duplicatedOutput.AcquireNextFrame(50, out duplicateFrameInformation, out screenResource);

                while (duplicateFrameInformation.AccumulatedFrames < 1)
                {
                    duplicatedOutput.ReleaseFrame();
                    duplicatedOutput.AcquireNextFrame(50, out duplicateFrameInformation, out screenResource);
                }

                // copy resource into memory that can be accessed by the CPU
                using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                    device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);

                // Get the desktop capture texture
                var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                // Create Drawing.Bitmap
                using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                {
                    var boundsRect = new Rectangle(0, 0, width, height);

                    // Copy pixels from screen capture Texture to GDI bitmap
                    var mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                    var sourcePtr = mapSource.DataPointer;
                    var destPtr = mapDest.Scan0;
                    for (int y = 0; y < height; y++)
                    {
                        // Copy a single line 
                        SharpDX.Utilities.CopyMemory(destPtr, sourcePtr, width * 4);

                        // Advance pointers
                        sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                        destPtr = IntPtr.Add(destPtr, mapDest.Stride);
                    }

                    // Release source and dest locks
                    bitmap.UnlockBits(mapDest);
                    device.ImmediateContext.UnmapSubresource(screenTexture, 0);

                    screenResource.Dispose();
                    duplicatedOutput.ReleaseFrame();

                    CurrentFrame = (Bitmap)bitmap.Clone();
                }
            }
            catch (SharpDXException e)
            {
                if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                {
                    Logger.Write(e);
                    NeedsInit = true;
                }
            }
            catch (Exception e)
            {
                Logger.Write(e);
                NeedsInit = true;
            }
        }

        public void Dispose()
        {
            duplicatedOutput?.Dispose();
            output1?.Dispose();
            output?.Dispose();
            device?.Dispose();
            adapter?.Dispose();
            factory?.Dispose();
            CurrentFrame?.Dispose();
            PreviousFrame?.Dispose();
        }

        public int GetScreenCount()
        {
            return Screen.AllScreens.Length;
        }

        public Rectangle GetVirtualScreenBounds()
        {
            return SystemInformation.VirtualScreen;
        }

        public void Init()
        {
            factory = new Factory1();

            //Get first adapter
            adapter = factory.Adapters1.FirstOrDefault(x => x.Outputs.Length > 0);
            //Get device from adapter
            device = new SharpDX.Direct3D11.Device(adapter);
            //Get front buffer of the adapter
            if (adapter.GetOutputCount() < SelectedScreen + 1)
            {
                SelectedScreen = 0;
            }
            output = adapter.GetOutput(SelectedScreen);
            output1 = output.QueryInterface<Output1>();

            // Width/Height of desktop to capture
            var bounds = output1.Description.DesktopBounds;
            var newWidth = bounds.Right - bounds.Left;
            var newHeight = bounds.Bottom - bounds.Top;
            CurrentScreenBounds = new Rectangle(bounds.Left, bounds.Top, newWidth, newHeight);
            if (newWidth != width || newHeight != height)
            {
                ScreenChanged?.Invoke(this, CurrentScreenBounds);
            }
            width = newWidth;
            height = newHeight;

            CurrentFrame = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            PreviousFrame = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            // Create Staging texture CPU-accessible
            textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            screenTexture = new Texture2D(device, textureDesc);
            duplicatedOutput = output1.DuplicateOutput(device);
        }

        public void SetSelectedScreen(int screenNumber)
        {
            if (screenNumber == SelectedScreen)
            {
                return;
            }
            if (adapter == null)
            {
                SelectedScreen = 0;
            }
            else
            {
                if (adapter.Outputs.Length >= screenNumber + 1)
                {
                    SelectedScreen = screenNumber;
                }
                else
                {
                    SelectedScreen = 0;
                }
            }
            CaptureFullscreen = true;
            NeedsInit = true;
        }
    }
}
