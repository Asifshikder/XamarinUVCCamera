using System;
using CameraUVC.Interfaces;
using CameraUVC.Models;
using Xamarin.Forms;

namespace CameraUVC
{
    /// <summary>
    /// Cross-platform Xamarin.Forms view component for rendering live USB UVC camera streams.
    /// Interacts with the platform-specific UvcCameraRenderer.
    /// </summary>
    public class UvcCameraView : Xamarin.Forms.View
    {
        public static readonly BindableProperty VideoRotationProperty =
            BindableProperty.Create(nameof(VideoRotation), typeof(int), typeof(UvcCameraView), 0);

        public static readonly BindableProperty FlipHorizontallyProperty =
            BindableProperty.Create(nameof(FlipHorizontally), typeof(bool), typeof(UvcCameraView), false);

        public static readonly BindableProperty FlipVerticallyProperty =
            BindableProperty.Create(nameof(FlipVertically), typeof(bool), typeof(UvcCameraView), false);

        public int PreviewHeight { get; private set; }

        public int PreviewWidth { get; private set; }

        public int DeviceId { get; private set; }

        public bool IsOpen { get; private set; }

        public bool IsRecording { get; private set; }

        public int VideoRotation
        {
            get => (int)GetValue(VideoRotationProperty);
            set => SetValue(VideoRotationProperty, value);
        }

        public bool FlipHorizontally
        {
            get => (bool)GetValue(FlipHorizontallyProperty);
            set => SetValue(FlipHorizontallyProperty, value);
        }

        public bool FlipVertically
        {
            get => (bool)GetValue(FlipVerticallyProperty);
            set => SetValue(FlipVerticallyProperty, value);
        }

        // Native Renderer Request Events
        public event EventHandler<RequestOpenArgs> OpenRequested;
        public event EventHandler CloseRequested;
        public event EventHandler<RequestStartRecordingArgs> StartRecordingRequested;
        public event EventHandler StopRecordingRequested;
        public event EventHandler<RequestTakeSnapshotArgs> TakeSnapshotRequested;

        // State Change Notification Events
        public event EventHandler<string> RecordingStarted;
        public event EventHandler RecordingStopped;
        public event EventHandler CameraOpen;
        public event EventHandler CameraClose;

        /// <summary>
        /// Commands the native renderer to initialize and start the camera stream at the designated resolution.
        /// </summary>
        public void Open(int deviceId, int width, int height)
        {
            DeviceId = deviceId;
            PreviewHeight = height;
            PreviewWidth = width;
            OpenRequested?.Invoke(this, new RequestOpenArgs(deviceId, width, height));
            IsOpen = true;
            CameraOpen?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Commands the native renderer to stop the camera preview and release hardware resources.
        /// </summary>
        public void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            IsOpen = false;
            CameraClose?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Begins MP4 video recording to the specified file path.
        /// </summary>
        public void StartRecording(string videoPath)
        {
            StartRecordingRequested?.Invoke(this, new RequestStartRecordingArgs(videoPath));
            IsRecording = true;
            RecordingStarted?.Invoke(this, videoPath);
        }

        /// <summary>
        /// Stops active video recording.
        /// </summary>
        public void StopRecording()
        {
            StopRecordingRequested?.Invoke(this, EventArgs.Empty);
            IsRecording = false;
            RecordingStopped?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Captures a single still JPEG frame to the specified file path.
        /// </summary>
        public void TakeSnapshot(string imgFilePath)
        {
            TakeSnapshotRequested?.Invoke(this, new RequestTakeSnapshotArgs(imgFilePath));
        }

        /// <summary>
        /// Called when the physical USB device is disconnected to safely teardown active sessions.
        /// </summary>
        public void OnCameraDisconnected()
        {
            if (IsRecording)
            {
                IsRecording = false;
                RecordingStopped?.Invoke(this, EventArgs.Empty);
            }

            if (IsOpen)
            {
                IsOpen = false;
                CameraClose?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}