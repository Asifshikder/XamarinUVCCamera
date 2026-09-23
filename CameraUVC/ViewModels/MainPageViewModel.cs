using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CameraUVC.Interfaces;
using CameraUVC.Models;
using Xamarin.Forms;

namespace CameraUVC.ViewModels
{
    /// <summary>
    /// ViewModel managing state, hardware discovery, and capture commands for the camera interface.
    /// Demonstrates clean MVVM separation of concerns in Xamarin.Forms.
    /// </summary>
    public class MainPageViewModel : INotifyPropertyChanged
    {
        private readonly ICameraHelper _cameraHelper;
        private UsbDeviceInfo _selectedDevice;
        private CameraSize _selectedResolution;
        private bool _isConnected;
        private bool _isRecording;
        private string _statusMessage = "Connect a USB camera to get started";

        public MainPageViewModel() : this(CameraHelper.Instance)
        {
        }

        public MainPageViewModel(ICameraHelper cameraHelper)
        {
            _cameraHelper = cameraHelper;
            AvailableDevices = new ObservableCollection<UsbDeviceInfo>();
            AvailableResolutions = new ObservableCollection<CameraSize>();

            // Initialize Commands
            ConnectCommand = new Command(async () => await ToggleConnectionAsync(), () => SelectedDevice != null);
            RecordCommand = new Command(ToggleRecording, () => IsConnected);
            SnapshotCommand = new Command(async () => await TakeSnapshotAsync(), () => IsConnected);
            RefreshDevicesCommand = new Command(RefreshDevices);

            if (_cameraHelper != null)
            {
                _cameraHelper.UsbDeviceAttached += OnUsbDeviceAttached;
                _cameraHelper.UsbDeviceDetached += OnUsbDeviceDetached;
                RefreshDevices();
            }
        }

        #region Observable Collections

        public ObservableCollection<UsbDeviceInfo> AvailableDevices { get; }

        public ObservableCollection<CameraSize> AvailableResolutions { get; }

        #endregion

        #region Properties

        public UsbDeviceInfo SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    ((Command)ConnectCommand).ChangeCanExecute();
                    LoadResolutions();
                }
            }
        }

        public CameraSize SelectedResolution
        {
            get => _selectedResolution;
            set => SetProperty(ref _selectedResolution, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    ((Command)RecordCommand).ChangeCanExecute();
                    ((Command)SnapshotCommand).ChangeCanExecute();
                }
            }
        }

        public bool IsRecording
        {
            get => _isRecording;
            set => SetProperty(ref _isRecording, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region Commands

        public ICommand ConnectCommand { get; }
        public ICommand RecordCommand { get; }
        public ICommand SnapshotCommand { get; }
        public ICommand RefreshDevicesCommand { get; }

        #endregion

        #region Public & Internal Methods

        public void RefreshDevices()
        {
            if (_cameraHelper == null) return;

            AvailableDevices.Clear();
            var devices = _cameraHelper.ListUsbDevices();
            foreach (var device in devices)
            {
                AvailableDevices.Add(device);
            }

            if (AvailableDevices.Count > 0 && SelectedDevice == null)
            {
                SelectedDevice = AvailableDevices.First();
                StatusMessage = $"Found camera: {SelectedDevice.DisplayName}";
            }
            else if (AvailableDevices.Count == 0)
            {
                StatusMessage = "No USB cameras found. Please connect a camera.";
            }
        }

        private void LoadResolutions()
        {
            AvailableResolutions.Clear();
            if (_selectedDevice == null || _cameraHelper == null) return;

            var supportedSizes = _cameraHelper.GetCameraSupportedSizes(_selectedDevice.DeviceId);
            if (supportedSizes != null && supportedSizes.Length > 0)
            {
                foreach (var size in supportedSizes)
                {
                    AvailableResolutions.Add(size);
                }
            }
            else
            {
                AvailableResolutions.Add(new CameraSize(640, 480));
            }

            SelectedResolution = AvailableResolutions.FirstOrDefault();
        }

        private Task ToggleConnectionAsync()
        {
            // Triggered via view bindings or code-behind event bridges
            return Task.CompletedTask;
        }

        private void ToggleRecording()
        {
            // Managed in conjunction with UvcCameraView session
        }

        private Task TakeSnapshotAsync()
        {
            return Task.CompletedTask;
        }

        private void OnUsbDeviceAttached(object sender, UsbDeviceInfo e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                if (!AvailableDevices.Any(d => d.DeviceId == e.DeviceId))
                {
                    AvailableDevices.Add(e);
                }
                if (SelectedDevice == null)
                {
                    SelectedDevice = e;
                    StatusMessage = $"Camera attached: {e.DisplayName}";
                }
            });
        }

        private void OnUsbDeviceDetached(object sender, UsbDeviceInfo e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                var match = AvailableDevices.FirstOrDefault(d => d.DeviceId == e.DeviceId);
                if (match != null)
                {
                    AvailableDevices.Remove(match);
                }

                if (SelectedDevice?.DeviceId == e.DeviceId)
                {
                    SelectedDevice = AvailableDevices.FirstOrDefault();
                    StatusMessage = "Camera detached.";
                    IsConnected = false;
                    IsRecording = false;
                }
            });
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        #endregion
    }
}
