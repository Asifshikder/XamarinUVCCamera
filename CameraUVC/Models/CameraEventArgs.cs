using System;

namespace CameraUVC.Models
{
    /// <summary>
    /// Event arguments for opening a camera stream at a specific resolution.
    /// </summary>
    public class RequestOpenArgs : EventArgs
    {
        public RequestOpenArgs(int deviceId, int previewWidth, int previewHeight)
        {
            DeviceId = deviceId;
            PreviewWidth = previewWidth;
            PreviewHeight = previewHeight;
        }

        public int DeviceId { get; }

        public int PreviewWidth { get; }

        public int PreviewHeight { get; }
    }

    /// <summary>
    /// Event arguments for initiating MP4 video recording.
    /// </summary>
    public class RequestStartRecordingArgs : EventArgs
    {
        public RequestStartRecordingArgs(string videoPath)
        {
            VideoPath = videoPath;
        }

        public string VideoPath { get; }
    }

    /// <summary>
    /// Event arguments for capturing a JPEG still snapshot.
    /// </summary>
    public class RequestTakeSnapshotArgs : EventArgs
    {
        public RequestTakeSnapshotArgs(string imagePath)
        {
            ImagePath = imagePath;
        }

        public string ImagePath { get; }
    }
}
