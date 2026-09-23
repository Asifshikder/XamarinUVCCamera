namespace CameraUVC.Models
{
    /// <summary>
    /// Metadata describing an attached USB video peripheral.
    /// </summary>
    public class UsbDeviceInfo
    {
        public int VendorId { get; set; }

        public string ProductName { get; set; }

        public int ProductId { get; set; }

        public string ManufacturerName { get; set; }

        public string DeviceName { get; set; }

        public int DeviceId { get; set; }

        /// <summary>
        /// User-friendly display title, defaulting to product name if available.
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(ProductName) ? ProductName : DeviceName;

        public override string ToString()
        {
            return $"{DisplayName} (ID: {DeviceId}, VID: {VendorId:X4}, PID: {ProductId:X4})";
        }
    }
}
