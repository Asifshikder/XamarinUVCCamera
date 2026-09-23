using System;
using System.Collections.Generic;
using System.Linq;
using Android.Hardware.Usb;
using CameraUVC.Droid.Renderers;
using CameraUVC.Interfaces;
using CameraUVC.Models;
using Com.Jiangdg.Usbcamera;
using Com.Serenegiant.Usb;
using Com.Serenegiant.Usb.Common;
using Com.Serenegiant.Usb.Encoder;
using Xamarin.Forms.Platform.Android;

namespace CameraUVC.Droid
{
    /// <summary>
    /// Android platform implementation of ICameraHelper managing USB device enumeration,
    /// hot-plug event broadcasts via USBMonitor, and resolution interrogation.
    /// </summary>
    public class CameraHelperImpl : ICameraHelper
    {
        private readonly USBMonitor _usbMonitor;
        private readonly Dictionary<int, CameraSize[]> _cameraSizesCache = new Dictionary<int, CameraSize[]>();

        public CameraHelperImpl()
        {
            var listener = new DevConnectListener(this);
            _usbMonitor = new USBMonitor(MainActivity.Instance, listener);
            _usbMonitor.Register();
        }

        public string PhotoRootDir { get; set; }

        public string VideoRootDir { get; set; }

        public event EventHandler<UsbDeviceInfo> UsbDeviceAttached;

        public event EventHandler<UsbDeviceInfo> UsbDeviceDetached;

        /// <summary>
        /// Lists all attached USB video devices filtered by UVC descriptors.
        /// </summary>
        public UsbDeviceInfo[] ListUsbDevices()
        {
            return _usbMonitor.DeviceList
                .Where(UvcCameraHelper.IsValidCameraDevice)
                .Select(UvcCameraHelper.FromAUsbDevice)
                .ToArray();
        }

        /// <summary>
        /// Interrogates the camera hardware descriptor for all supported resolution sizes.
        /// Caches the results per deviceId for rapid subsequent lookups.
        /// </summary>
        public CameraSize[] GetCameraSupportedSizes(int deviceId)
        {
            Console.WriteLine($"[CameraHelper] Querying supported resolutions for device ID: {deviceId}");

            if (_cameraSizesCache.TryGetValue(deviceId, out var cachedSizes))
            {
                Console.WriteLine($"[CameraHelper] Cache hit: {cachedSizes.Length} resolutions found");
                return cachedSizes;
            }

            var usbDevice = _usbMonitor.DeviceList.FirstOrDefault(x => x.DeviceId == deviceId);
            if (usbDevice == null)
            {
                Console.WriteLine($"[CameraHelper] Device ID {deviceId} not found in USB device list");
                return GetFallbackResolutions();
            }

            try
            {
                var block = _usbMonitor.OpenDevice(usbDevice);
                if (block == null)
                {
                    Console.WriteLine("[CameraHelper] Could not obtain USB control block for device");
                    return GetFallbackResolutions();
                }

                var uvcCamera = new UVCCamera();
                uvcCamera.Open(block);

                var supportedSizes = uvcCamera.SupportedSizeList;
                if (supportedSizes == null || supportedSizes.Count == 0)
                {
                    Console.WriteLine("[CameraHelper] Hardware reported no size descriptors; using fallback list");
                    uvcCamera.Close();
                    return GetFallbackResolutions();
                }

                var sizes = supportedSizes
                    .Select(x => new CameraSize(x.Width, x.Height))
                    .ToArray();

                uvcCamera.Close();
                _cameraSizesCache[deviceId] = sizes;
                return sizes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CameraHelper] Exception querying supported sizes: {ex.Message}");
                return GetFallbackResolutions();
            }
        }

        private CameraSize[] GetFallbackResolutions()
        {
            return new[]
            {
                new CameraSize(640, 480),   // VGA
                new CameraSize(1280, 720),  // HD 720p
                new CameraSize(1920, 1080), // Full HD 1080p
                new CameraSize(800, 600),   // SVGA
                new CameraSize(1024, 768),  // XGA
                new CameraSize(320, 240)    // QVGA
            };
        }

        private class DevConnectListener : Java.Lang.Object, USBMonitor.IOnDeviceConnectListener
        {
            private readonly CameraHelperImpl _parent;

            public DevConnectListener(CameraHelperImpl parent)
            {
                _parent = parent;
            }

            public void OnAttach(UsbDevice device)
            {
                Console.WriteLine($"[USBMonitor] OnAttach: {device.DeviceName}");
                if (!UvcCameraHelper.IsValidCameraDevice(device)) return;

                _parent._usbMonitor.RequestPermission(device);
            }

            public void OnCancel(UsbDevice device)
            {
                Console.WriteLine($"[USBMonitor] OnCancel permission: {device.DeviceName}");
            }

            public void OnConnect(UsbDevice device, USBMonitor.UsbControlBlock block, bool createNew)
            {
                Console.WriteLine($"[USBMonitor] OnConnect: {device.DeviceName}");
                if (!UvcCameraHelper.IsValidCameraDevice(device)) return;

                _parent.UsbDeviceAttached?.Invoke(_parent, UvcCameraHelper.FromAUsbDevice(device));
            }

            public void OnDetach(UsbDevice device)
            {
                Console.WriteLine($"[USBMonitor] OnDetach: {device.DeviceName}");
                if (!UvcCameraHelper.IsValidCameraDevice(device)) return;

                _parent.UsbDeviceDetached?.Invoke(_parent, UvcCameraHelper.FromAUsbDevice(device));
            }

            public void OnDisconnect(UsbDevice device, USBMonitor.UsbControlBlock block)
            {
                Console.WriteLine($"[USBMonitor] OnDisconnect: {device.DeviceName}");
            }
        }
    }

    /// <summary>
    /// Controller managing low-level UVCCameraHelper native session lifecycle,
    /// stream initiation, frame transforms, and capture pipelines.
    /// </summary>
    public static class UvcCameraHelper
    {
        internal static UVCCameraHelper ACameraHelper;

        /// <summary>
        /// Configures preview resolution, orientation angles, frame format, and USB monitor attachment.
        /// </summary>
        public static void Setup(UvcCameraView cameraView, UvcCameraSetupOptions options)
        {
            Console.WriteLine($"[UvcCameraHelper] Configuring camera stream at {options.PreviewWidth}x{options.PreviewHeight}");

            var renderer = cameraView.GetRenderer() as UvcCameraRenderer;
            if (renderer == null)
            {
                Console.WriteLine("[UvcCameraHelper] UvcCameraRenderer instance not found");
                return;
            }

            try
            {
                ACameraHelper = UVCCameraHelper.GetInstance(options.PreviewWidth, options.PreviewHeight);

                ACameraHelper.CameraAngle = options.VideoRotation > 0
                    ? 360 - options.VideoRotation
                    : 0;

                ACameraHelper.FlipVertically = options.FlipVertically;
                ACameraHelper.FlipHorizontally = options.FlipHorizontally;
                ACameraHelper.SetDefaultFrameFormat(UVCCameraHelper.FrameFormatMjpeg);

                var listener = new UvcDeviceConnectListener(cameraView);
                ACameraHelper.InitUSBMonitor(MainActivity.Instance, renderer.UvcCamera, listener);
                ACameraHelper.RegisterUSB();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UvcCameraHelper] Error in Setup: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts live preview on the attached native UVCCameraTextureView surface.
        /// </summary>
        public static void StartPreview(UvcCameraView view)
        {
            var renderer = view.GetRenderer() as UvcCameraRenderer;
            if (renderer?.UvcCamera == null || ACameraHelper == null)
            {
                Console.WriteLine("[UvcCameraHelper] Cannot start preview: Renderer or ACameraHelper is null");
                return;
            }

            try
            {
                ACameraHelper.StartPreview(renderer.UvcCamera);
                Console.WriteLine("[UvcCameraHelper] Preview pipeline started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UvcCameraHelper] Error starting preview: {ex.Message}");
            }
        }

        /// <summary>
        /// Halts live preview streaming.
        /// </summary>
        public static void StopPreview()
        {
            ACameraHelper?.StopPreview();
        }

        /// <summary>
        /// Initiates MP4 video recording via hardware media pusher.
        /// </summary>
        public static void StartRecording(string videoPath)
        {
            var param = new RecordParams
            {
                RecordPath = videoPath,
                RecordDuration = 0,
                VoiceClose = true
            };
            ACameraHelper?.StartPusher(param, null);
        }

        /// <summary>
        /// Halts MP4 video recording.
        /// </summary>
        public static void StopRecording()
        {
            ACameraHelper?.StopPusher();
        }

        /// <summary>
        /// Captures a JPEG snapshot from the video stream.
        /// </summary>
        public static void CapturePicture(string imageFile, Action<string> onResult)
        {
            ACameraHelper?.CapturePicture(imageFile, new OnCaptureListener(onResult));
        }

        public static void RequestPermission(int deviceId)
        {
            ACameraHelper?.RequestPermission(deviceId);
        }

        /// <summary>
        /// Validates whether a connected USB device is a video device (UVC compliant).
        /// Checks standard USB Class 14 (Video), interface descriptors, and known vendor IDs.
        /// </summary>
        public static bool IsValidCameraDevice(UsbDevice device)
        {
            // USB Video Class (Class 14 = 0x0E)
            if (device.DeviceClass == UsbClass.Video)
                return true;

            // Miscellaneous class with Video subclass
            if (device.DeviceClass == UsbClass.Misc &&
                (device.DeviceSubclass == UsbClass.Comm || (int)device.DeviceSubclass == 2))
                return true;

            // Interface Association Descriptor (IAD) - Class 239
            if ((int)device.DeviceClass == 239)
                return true;

            // Known camera vendors (Logitech, Microsoft, Creative, Apple, Sunplus, Realtek)
            var knownCameraVendors = new[] { 1133, 1118, 1054, 7119, 1452, 1367 };
            if (knownCameraVendors.Contains(device.VendorId))
                return true;

            // Inspect individual interface descriptors for Video class
            for (int i = 0; i < device.InterfaceCount; i++)
            {
                var usbInterface = device.GetInterface(i);
                if (usbInterface.InterfaceClass == UsbClass.Video)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Maps an Android UsbDevice to a cross-platform UsbDeviceInfo domain model.
        /// </summary>
        public static UsbDeviceInfo FromAUsbDevice(UsbDevice device)
        {
            return new UsbDeviceInfo
            {
                DeviceId = device.DeviceId,
                DeviceName = device.DeviceName,
                ProductId = device.ProductId,
                VendorId = device.VendorId,
                ManufacturerName = device.ManufacturerName,
                ProductName = device.ProductName
            };
        }
    }

    /// <summary>
    /// Listener bridging native Android USB connection lifecycle with UVC camera preview start.
    /// </summary>
    internal class UvcDeviceConnectListener : Java.Lang.Object, UVCCameraHelper.IOnMyDevConnectListener
    {
        private readonly UvcCameraView _cameraView;

        public UvcDeviceConnectListener(UvcCameraView cameraView)
        {
            _cameraView = cameraView;
        }

        public void OnAttachDev(UsbDevice device)
        {
            if (!UvcCameraHelper.IsValidCameraDevice(device)) return;
            UvcCameraHelper.RequestPermission(device.DeviceId);
        }

        public void OnConnectDev(UsbDevice device, bool isConnected)
        {
            if (!UvcCameraHelper.IsValidCameraDevice(device) || !isConnected)
                return;

            // Wait for native camera handler initialization (up to 10 seconds)
            if (!UvcCameraHelper.ACameraHelper.WaitCameraCreated(10000))
            {
                Console.WriteLine("[UvcDeviceConnectListener] Camera creation timed out");
                return;
            }

            try
            {
                UvcCameraHelper.StartPreview(_cameraView);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UvcDeviceConnectListener] Error starting preview: {ex.Message}");
            }
        }

        public void OnDetachDev(UsbDevice device)
        {
            _cameraView.OnCameraDisconnected();
        }

        public void OnDisConnectDev(UsbDevice device)
        {
            Console.WriteLine($"[UvcDeviceConnectListener] Disconnected device: {device.DeviceName}");
        }
    }

    internal class OnCaptureListener : Java.Lang.Object, AbstractUVCCameraHandler.IOnCaptureListener
    {
        private readonly Action<string> _onResult;

        public OnCaptureListener(Action<string> onResult)
        {
            _onResult = onResult;
        }

        public void OnCaptureResult(string path)
        {
            _onResult?.Invoke(path);
        }
    }

    /// <summary>
    /// Configuration options for configuring UVC camera hardware preview.
    /// </summary>
    public class UvcCameraSetupOptions
    {
        public int PreviewWidth { get; set; }

        public int PreviewHeight { get; set; }

        public int VideoRotation { get; set; }

        public bool FlipHorizontally { get; set; }

        public bool FlipVertically { get; set; }
    }
}
