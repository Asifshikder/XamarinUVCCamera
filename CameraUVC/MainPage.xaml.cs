using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CameraUVC.Interfaces;
using CameraUVC.Models;
using Xamarin.Forms;

namespace CameraUVC
{
    public partial class MainPage : ContentPage
    {
        private readonly ICameraHelper _cameraHelper = CameraHelper.Instance;
        private bool _isConnected = false;
        private bool _isRecording = false;
        private UsbDeviceInfo _selectedDevice = null;
        private CameraSize _defaultResolution = new CameraSize { Width = 640, Height = 480 };
        private List<CameraSize> _availableResolutions = new List<CameraSize>();

        public MainPage()
        {
            InitializeComponent();

            if (_cameraHelper != null)
            {
                _cameraHelper.UsbDeviceAttached += OnUsbDeviceAttached;
                _cameraHelper.UsbDeviceDetached += OnUsbDeviceDetached;
            }

            UvcCameraView.CameraOpen += OnCameraOpen;
            UvcCameraView.CameraClose += OnCameraClose;
            UvcCameraView.RecordingStarted += OnRecordingStarted;
            UvcCameraView.RecordingStopped += OnRecordingStopped;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!await CheckPermissions())
                return;

            // Auto-select first available camera
            AutoSelectCamera();
        }

        private void AutoSelectCamera()
        {
            if (_cameraHelper == null) 
            {
                UpdateStatusLabel("Camera helper not available");
                return;
            }

            var usbDevices = _cameraHelper.ListUsbDevices();
            if (usbDevices.Length > 0)
            {
                _selectedDevice = usbDevices[0];
                LoadResolutions();
                UpdateStatusLabel($"Found camera: {_selectedDevice.DisplayName}");
                Console.WriteLine($"Auto-selected camera: {_selectedDevice.DisplayName}");
            }
            else
            {
                UpdateStatusLabel("No USB cameras found. Please connect a camera.");
            }
        }

        private void LoadResolutions()
        {
            if (_selectedDevice == null || _cameraHelper == null)
                return;

            _availableResolutions.Clear();
            var supportedSizes = _cameraHelper.GetCameraSupportedSizes(_selectedDevice.DeviceId);
            
            if (supportedSizes != null && supportedSizes.Length > 0)
            {
                _availableResolutions.AddRange(supportedSizes);
            }
            else
            {
                _availableResolutions.Add(_defaultResolution);
            }

            Device.BeginInvokeOnMainThread(() =>
            {
                ResolutionPicker.ItemsSource = _availableResolutions;
                ResolutionPicker.SelectedItem = _availableResolutions.FirstOrDefault();
            });
        }

        private void UpdateStatusLabel(string message)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = message;
            });
        }

        private void OnUsbDeviceAttached(object sender, UsbDeviceInfo e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                if (_selectedDevice == null)
                {
                    _selectedDevice = e;
                    LoadResolutions();
                    UpdateStatusLabel($"Camera connected: {e.DisplayName}");
                    Console.WriteLine($"Camera attached: {e.DisplayName}");
                }
            });
        }

        private void OnUsbDeviceDetached(object sender, UsbDeviceInfo e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                if (_selectedDevice?.DeviceId == e.DeviceId)
                {
                    if (_isConnected)
                    {
                        UvcCameraView.Close();
                    }
                    _selectedDevice = null;
                    _availableResolutions.Clear();
                    ResolutionPicker.ItemsSource = null;
                    UpdateStatusLabel("Camera disconnected. Please reconnect your camera.");
                    Console.WriteLine($"Camera detached: {e.DisplayName}");
                }
            });
        }

        private void ResolutionPicker_OnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isConnected)
            {
                DisplayAlert("Resolution Change", 
                    "Please disconnect and reconnect the camera to apply the new resolution.", "OK");
            }
        }

        private void OnCameraOpen(object sender, EventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                _isConnected = true;
                BtnConnect.BackgroundColor = Color.FromHex("#FF9800");
                BtnRecord.IsEnabled = true;
                BtnPhoto.IsEnabled = true;
                BtnRecord.Opacity = 1.0;
                BtnPhoto.Opacity = 1.0;
                UpdateStatusLabel("Camera connected - Ready to record and take photos");
            });
        }

        private void OnCameraClose(object sender, EventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                _isConnected = false;
                _isRecording = false;
                BtnConnect.BackgroundColor = Color.FromHex("#2196F3");
                BtnRecord.IsEnabled = false;
                BtnPhoto.IsEnabled = false;
                BtnRecord.Opacity = 0.5;
                BtnPhoto.Opacity = 0.5;
                BtnRecord.Text = "⏺";
                BtnRecord.BackgroundColor = Color.FromHex("#E91E63");
                UpdateStatusLabel(_selectedDevice != null ? "Camera disconnected - Click Connect to start" : "No camera found");
            });
        }

        private void OnRecordingStarted(object sender, string videoFile)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                _isRecording = true;
                BtnRecord.Text = "⏹";
                BtnRecord.BackgroundColor = Color.FromHex("#9C27B0");
                UpdateStatusLabel($"Recording video...");
                Console.WriteLine($"Recording started: {videoFile}");
            });
        }

        private void OnRecordingStopped(object sender, EventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                _isRecording = false;
                BtnRecord.Text = "⏺";
                BtnRecord.BackgroundColor = Color.FromHex("#E91E63");
                UpdateStatusLabel("Recording stopped - Video saved successfully!");
                Console.WriteLine("Recording stopped");
            });
        }

        private async void BtnConnect_OnClicked(object sender, EventArgs e)
        {
            try
            {
                if (!_isConnected)
                {
                    // Connect to camera
                    if (_selectedDevice == null)
                    {
                        await DisplayAlert("No Camera", "No USB camera found. Please connect a USB camera and try again.", "OK");
                        return;
                    }

                    if (!await CheckPermissions())
                        return;

                    UpdateStatusLabel("Connecting to camera...");
                    Console.WriteLine($"Connecting to camera: {_selectedDevice.DisplayName}");
                    
                    // Set default camera settings
                    UvcCameraView.FlipHorizontally = false;
                    UvcCameraView.FlipVertically = false;
                    UvcCameraView.VideoRotation = 0;

                    // Get selected resolution or use default
                    var resolution = ResolutionPicker.SelectedItem as CameraSize ?? _defaultResolution;
                    Console.WriteLine($"Using resolution: {resolution.Width}x{resolution.Height}");
                    
                    UvcCameraView.Open(_selectedDevice.DeviceId, resolution.Width, resolution.Height);
                }
                else
                {
                    // Disconnect from camera
                    UpdateStatusLabel("Disconnecting camera...");
                    UvcCameraView.Close();
                    Console.WriteLine("Camera disconnected by user");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in BtnConnect_OnClicked: {ex.Message}");
                UpdateStatusLabel("Connection error occurred");
                await DisplayAlert("Connection Error", $"Failed to connect to camera: {ex.Message}", "OK");
            }
        }

        private async void BtnRecord_OnClicked(object sender, EventArgs e)
        {
            if (!_isConnected) return;

            try
            {
                if (!_isRecording)
                {
                    // Start recording
                    if (_cameraHelper == null) 
                    {
                        await DisplayAlert("Error", "Camera helper not available", "OK");
                        return;
                    }

                    // Ensure directory exists
                    if (!Directory.Exists(_cameraHelper.VideoRootDir))
                    {
                        Directory.CreateDirectory(_cameraHelper.VideoRootDir);
                        Console.WriteLine($"Created video directory: {_cameraHelper.VideoRootDir}");
                    }

                    var videoFile = Path.Combine(_cameraHelper.VideoRootDir, GetTimestampFileName() + ".mp4");
                    Console.WriteLine($"Starting video recording to: {videoFile}");
                    
                    UvcCameraView.StartRecording(videoFile);
                }
                else
                {
                    // Stop recording
                    Console.WriteLine("Stopping video recording");
                    UvcCameraView.StopRecording();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during recording: {ex.Message}");
                UpdateStatusLabel("Recording error occurred");
                await DisplayAlert("Recording Error", $"Error: {ex.Message}", "OK");
            }
        }

        private async void BtnPhoto_OnClicked(object sender, EventArgs e)
        {
            if (!_isConnected) return;

            try
            {
                if (_cameraHelper == null) 
                {
                    await DisplayAlert("Error", "Camera helper not available", "OK");
                    return;
                }

                // Ensure directory exists
                if (!Directory.Exists(_cameraHelper.PhotoRootDir))
                {
                    Directory.CreateDirectory(_cameraHelper.PhotoRootDir);
                    Console.WriteLine($"Created photo directory: {_cameraHelper.PhotoRootDir}");
                }

                var photoFile = Path.Combine(_cameraHelper.PhotoRootDir, GetTimestampFileName() + ".jpg");
                Console.WriteLine($"Taking photo to: {photoFile}");
                
                UpdateStatusLabel("Taking photo...");
                UvcCameraView.TakeSnapshot(photoFile);
                
                await Task.Delay(1000); // Give time for photo to save
                UpdateStatusLabel("Photo saved successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error taking photo: {ex.Message}");
                UpdateStatusLabel("Photo capture error occurred");
                await DisplayAlert("Photo Error", $"Error: {ex.Message}", "OK");
            }
        }

        private async Task<bool> CheckPermissions()
        {
            try
            {
                var status = await Xamarin.Essentials.Permissions.RequestAsync<Xamarin.Essentials.Permissions.StorageWrite>();
                if (status != Xamarin.Essentials.PermissionStatus.Granted)
                {
                    await DisplayAlert("Permission Required", "Storage permission is required to save photos and videos.", "OK");
                    return false;
                }

                status = await Xamarin.Essentials.Permissions.RequestAsync<Xamarin.Essentials.Permissions.Camera>();
                if (status != Xamarin.Essentials.PermissionStatus.Granted)
                {
                    await DisplayAlert("Permission Required", "Camera permission is required.", "OK");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Permission check failed: {ex.Message}");
                return false;
            }
        }

        private string GetTimestampFileName()
        {
            return DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }
    }
}