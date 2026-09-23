using System;

namespace CameraUVC.Models
{
    /// <summary>
    /// Represents a video resolution (frame dimension) supported by the UVC camera.
    /// </summary>
    public class CameraSize
    {
        public CameraSize()
        {
        }

        public CameraSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Frame width in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Frame height in pixels.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Human-readable label for resolution (e.g., "1920x1080 (16:9)").
        /// </summary>
        public string DisplayName => $"{Width}x{Height}";

        public override string ToString()
        {
            return DisplayName;
        }

        public override bool Equals(object obj)
        {
            if (obj is CameraSize other)
            {
                return Width == other.Width && Height == other.Height;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height);
        }
    }
}
