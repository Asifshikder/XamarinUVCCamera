using System;
using System.Collections.Generic;
using System.Linq;
using Android.Hardware.Usb;
using Com.Jiangdg.Usbcamera;
using Com.Serenegiant.Usb;
using Com.Serenegiant.Usb.Common;
using Com.Serenegiant.Usb.Encoder;
using CameraUVC.Droid.Renders;
using Xamarin.Forms.Platform.Android;

namespace CameraUVC.Droid
{
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

        public UsbDeviceInfo[] ListUsbDevices()
        {
            return _usbMonitor.DeviceList
                .Where(MyUvcCameraHelper.IsValidCameraDevice)
                .Select(MyUvcCameraHelper.FromAUsbDevice)
                .ToArray();
        }

        public CameraSize[] GetCameraSupportedSizes(int deviceId)
        {
            Console.WriteLine($"Getting camera supported sizes for device ID: {deviceId}");
            
            if (_cameraSizesCache.TryGetValue(deviceId, out var sizes))
            {
                Console.WriteLine($"Found cached sizes: {sizes.Length} resolutions");
                return sizes;
            }

            var usbDevice = _usbMonitor.DeviceList.FirstOrDefault(x => x.DeviceId == deviceId);
            if (usbDevice == null) 
            {
                Console.WriteLine($"ERROR: USB device with ID {deviceId} not found");
                return GetFallbackResolutions();
            }

            Console.WriteLine($"Attempting to get resolutions for device: {usbDevice.DeviceName}");

            try
            {
                Console.WriteLine("Opening USB device...");
                var block = _usbMonitor.OpenDevice(usbDevice);
                if (block == null)
                {
                    Console.WriteLine("ERROR: Failed to open USB device block");
                    return GetFallbackResolutions();
                }

                Console.WriteLine("Creating UVCCamera instance...");
                var uvcCamera = new UVCCamera();
                uvcCamera.Open(block);

                Console.WriteLine("Getting supported size list...");
                var supportedSizes = uvcCamera.SupportedSizeList;
                
                if (supportedSizes == null || supportedSizes.Count == 0)
                {
                    Console.WriteLine("WARNING: No supported sizes returned from camera, using fallback");
                    uvcCamera.Close();
                    return GetFallbackResolutions();
                }

                Console.WriteLine($"Found {supportedSizes.Count} supported resolutions");
                sizes = supportedSizes
                    .Select(x => new CameraSize
                    {
                        Width = x.Width,
                        Height = x.Height
                    }).ToArray();

                foreach (var size in sizes)
                {
                    Console.WriteLine($"  Resolution: {size.Width}x{size.Height}");
                }

                uvcCamera.Close();
                Console.WriteLine("Camera closed successfully");

                _cameraSizesCache[deviceId] = sizes;
                return sizes;
            }
            catch (Exception e)
            {
                Console.WriteLine($"ERROR in GetCameraSupportedSizes: {e.Message}");
                Console.WriteLine($"Stack trace: {e.StackTrace}");
                return GetFallbackResolutions();
            }
        }

        private CameraSize[] GetFallbackResolutions()
        {
            Console.WriteLine("Using fallback resolutions");
            return new CameraSize[]
            {
                new CameraSize { Width = 640, Height = 480 },   // VGA
                new CameraSize { Width = 1280, Height = 720 },  // HD 720p
                new CameraSize { Width = 1920, Height = 1080 }, // Full HD 1080p
                new CameraSize { Width = 800, Height = 600 },   // SVGA
                new CameraSize { Width = 1024, Height = 768 },  // XGA
                new CameraSize { Width = 320, Height = 240 }    // QVGA
            };
        }

        public event EventHandler<UsbDeviceInfo> UsbDeviceAttached;

        public event EventHandler<UsbDeviceInfo> UsbDeviceDetached;

        class DevConnectListener : Java.Lang.Object, USBMonitor.IOnDeviceConnectListener
        {
            private readonly CameraHelperImpl _parent;

            public DevConnectListener(CameraHelperImpl parent)
            {
                _parent = parent;
            }

            public void OnAttach(UsbDevice device)
            {
                Console.WriteLine("OnAttach:" + device);
                if (!MyUvcCameraHelper.IsValidCameraDevice(device)) return;

                _parent._usbMonitor.RequestPermission(device);
            }

            public void OnCancel(UsbDevice device)
            {
                Console.WriteLine("OnCancel:" + device);
            }

            public void OnConnect(UsbDevice device, USBMonitor.UsbControlBlock block, bool createNew)
            {
                Console.WriteLine("OnConnect:" + device);
                if (!MyUvcCameraHelper.IsValidCameraDevice(device)) return;

                _parent.UsbDeviceAttached?.Invoke(_parent, MyUvcCameraHelper.FromAUsbDevice(device));
            }

            public void OnDetach(UsbDevice device)
            {
                Console.WriteLine("OnDetach:" + device);
                if (!MyUvcCameraHelper.IsValidCameraDevice(device)) return;
                _parent.UsbDeviceDetached?.Invoke(_parent, MyUvcCameraHelper.FromAUsbDevice(device));
            }

            public void OnDisconnect(UsbDevice device, USBMonitor.UsbControlBlock block)
            {
                Console.WriteLine("OnDisconnect:" + device);
            }
        }
    }

    public class MyUvcCameraHelper
    {
        internal static UVCCameraHelper ACameraHelper;
        public static void Setup(UvcCameraView cameraView, UvcCameraSetupOptions options)
        {
            Console.WriteLine($"Setting up UVC camera with resolution {options.PreviewWidth}x{options.PreviewHeight}");
            
            var render = cameraView.GetRenderer() as UvcCameraRender;
            if (render == null) 
            {
                Console.WriteLine("ERROR: Camera render is null");
                return;
            }

            try
            {
                ACameraHelper = UVCCameraHelper.GetInstance(options.PreviewWidth, options.PreviewHeight);
                Console.WriteLine("UVCCameraHelper instance created");
                
                if (options.VideoRotation > 0)
                {
                    ACameraHelper.CameraAngle = 360 - options.VideoRotation;
                }
                else
                {
                    ACameraHelper.CameraAngle = 0;
                }
                
                Console.WriteLine($"Camera angle set to: {ACameraHelper.CameraAngle}");

                ACameraHelper.FlipVertically = options.FlipVertically;
                ACameraHelper.FlipHorizontally = options.FlipHorizontally;
                Console.WriteLine($"Flip settings: V={options.FlipVertically}, H={options.FlipHorizontally}");

                ACameraHelper.SetDefaultFrameFormat(UVCCameraHelper.FrameFormatMjpeg);
                Console.WriteLine("Frame format set to MJPEG");
                
                var listener = new OnMyDevConnectListener(cameraView);
                ACameraHelper.InitUSBMonitor(MainActivity.Instance, render.UvcCamera, listener);
                Console.WriteLine("USB Monitor initialized");
                
                ACameraHelper.RegisterUSB();
                Console.WriteLine("USB registration completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in Setup: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        public static void StartPreview(UvcCameraView view)
        {
            Console.WriteLine("StartPreview called");
            
            var render = view.GetRenderer() as UvcCameraRender;
            if (render == null) 
            {
                Console.WriteLine("ERROR: UvcCameraRender is null");
                return;
            }
            
            if (render.UvcCamera == null)
            {
                Console.WriteLine("ERROR: UvcCamera TextureView is null");
                return;
            }
            
            if (ACameraHelper == null)
            {
                Console.WriteLine("ERROR: ACameraHelper is null");
                return;
            }

            try
            {
                Console.WriteLine("Attempting to start camera preview...");
                ACameraHelper.StartPreview(render.UvcCamera);
                Console.WriteLine("Camera preview started successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in StartPreview: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        public static void StopPreview()
        {
            ACameraHelper.StopPreview();
        }

        public static void StartRecording(string videoPath)
        {
            var param = new RecordParams();
            param.RecordPath = videoPath;
            param.RecordDuration = 0;
            param.VoiceClose = true;
            ACameraHelper.StartPusher(param, null);
        }

        public static void StopRecording()
        {
            ACameraHelper.StopPusher();
        }

        public static void CapturePicture(string imageFile, Action<string> onResult)
        {
            ACameraHelper.CapturePicture(imageFile, new OnCaptureListener(onResult));
        }

        public static void RequestPermission(int deviceId)
        {
            ACameraHelper.RequestPermission(deviceId);
        }

        public static bool IsValidCameraDevice(UsbDevice device)
        {
            Console.WriteLine($"Checking device: Class={device.DeviceClass}, Subclass={device.DeviceSubclass}, Protocol={device.DeviceProtocol}, VID={device.VendorId}, PID={device.ProductId}");
            
            // UVC Video Class (Class 14)
            if (device.DeviceClass == UsbClass.Video)
                return true;
                
            // Miscellaneous class with Video subclass
            if (device.DeviceClass == UsbClass.Misc && 
                (device.DeviceSubclass == UsbClass.Comm || (int)device.DeviceSubclass == 2))
                return true;
                
            // Interface Association Descriptor (IAD) - Class 239
            if ((int)device.DeviceClass == 239)
                return true;
                
            // Common camera vendor IDs
            var knownCameraVendors = new[] { 1133, 1118, 1054, 7119, 1452, 1367 }; // Logitech, Microsoft, Creative, etc.
            if (knownCameraVendors.Contains(device.VendorId))
                return true;
                
            // Check interfaces for UVC class
            for (int i = 0; i < device.InterfaceCount; i++)
            {
                var usbInterface = device.GetInterface(i);
                if (usbInterface.InterfaceClass == UsbClass.Video)
                    return true;
            }
            
            return false;
        }

        public static UsbDeviceInfo FromAUsbDevice(UsbDevice device)
        {
            var info = new UsbDeviceInfo
            {
                DeviceId = device.DeviceId,
                DeviceName = device.DeviceName,
                ProductId = device.ProductId,
                VendorId = device.VendorId,
                ManufacturerName = device.ManufacturerName,
                ProductName = device.ProductName
            };
            //try
            //{
            //    info.SerialNumber = device.SerialNumber;
            //}
            //catch
            //{
            //}

            return info;
        }
    }

    class OnCaptureListener : Java.Lang.Object, AbstractUVCCameraHandler.IOnCaptureListener
    {
        public OnCaptureListener(Action<string> onResult)
        {
            OnResult = onResult;
        }

        public Action<string> OnResult { get; }

        public void OnCaptureResult(string path)
        {
            OnResult?.Invoke(path);
        }
    }

    public class UvcCameraSetupOptions
    {
        public int PreviewWidth { get; set; }

        public int PreviewHeight { get; set; }

        public int VideoRotation { get; set; }

        public bool FlipHorizontally { get; set; }

        public bool FlipVertically { get; set; }
    }

    class OnMyDevConnectListener : Java.Lang.Object, UVCCameraHelper.IOnMyDevConnectListener
    {
        private readonly UvcCameraView _cameraView;
        public OnMyDevConnectListener(UvcCameraView cameraView)
        {
            _cameraView = cameraView;
        }

        public void OnAttachDev(UsbDevice device)
        {
            if (!MyUvcCameraHelper.IsValidCameraDevice(device)) return;
            MyUvcCameraHelper.RequestPermission(device.DeviceId);
        }

        public void OnConnectDev(UsbDevice device, bool isConnected)
        {
            Console.WriteLine($"OnConnectDev called: Device={device.DeviceName}, Connected={isConnected}");
            
            if (!MyUvcCameraHelper.IsValidCameraDevice(device)) 
            {
                Console.WriteLine("Device validation failed - not a valid camera device");
                return;
            }

            if (!isConnected) 
            {
                Console.WriteLine("Device not connected");
                return;
            }

            Console.WriteLine("Waiting for camera to be created...");
            
            // Increase timeout and add more logging
            if (!MyUvcCameraHelper.ACameraHelper.WaitCameraCreated(10000))
            {
                Console.WriteLine("ERROR: Camera creation timeout after 10 seconds");
                return;
            }
            
            Console.WriteLine("Camera created successfully, starting preview...");
            
            try
            {
                MyUvcCameraHelper.StartPreview(_cameraView);
                Console.WriteLine("Preview started successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR starting preview: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        public void OnDetachDev(UsbDevice device)
        {
            _cameraView.OnCameraDisconnected();
        }

        public void OnDisConnectDev(UsbDevice device)
        {
            Console.WriteLine("OnDisConnectDev");
        }
    }
}