using System;
using CameraUVC.Models;

namespace CameraUVC.Interfaces
{
    /// <summary>
    /// Platform service contract for USB camera enumeration and hardware capabilities.
    /// </summary>
    public interface ICameraHelper
    {
        string PhotoRootDir { get; }

        string VideoRootDir { get; }

        UsbDeviceInfo[] ListUsbDevices();

        CameraSize[] GetCameraSupportedSizes(int deviceId);

        event EventHandler<UsbDeviceInfo> UsbDeviceAttached;

        event EventHandler<UsbDeviceInfo> UsbDeviceDetached;
    }

    /// <summary>
    /// Service locator / ambient provider for platform-specific ICameraHelper implementation.
    /// </summary>
    public static class CameraHelper
    {
        public static ICameraHelper Instance { get; set; }
    }
}
