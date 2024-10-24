using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace BLE_App
{
    public partial class BtDevices : ContentPage
    {
        private IAdapter _bluetoothAdapter;
        private ObservableCollection<IDevice> _gattDevices = new ObservableCollection<IDevice>();
        private IDevice _connectedDevice;

        public BtDevices()
        {
            InitializeComponent();

            _bluetoothAdapter = CrossBluetoothLE.Current.Adapter;

            // Bind the device ListView to the ObservableCollection
            foundBleDevicesListView.ItemsSource = _gattDevices;

            // Subscribe to DeviceDisconnected event
            _bluetoothAdapter.DeviceDisconnected += OnDeviceDisconnected;

            // Subscribe to Bluetooth state changes for the phone itself
            CrossBluetoothLE.Current.StateChanged += OnBluetoothStateChanged;
        }

        private async void OnBluetoothStateChanged(object sender, EventArgs e)
        {
            // Only allow scanning if Bluetooth is turned on
            if (CrossBluetoothLE.Current.State == BluetoothState.Off)
            {
                await DisplayAlert("Bluetooth Disabled", "Please turn Bluetooth back on to use this app.", "OK");

                // Stop scanning or interacting with Bluetooth when it's off
                if (_bluetoothAdapter.IsScanning)
                {
                    await _bluetoothAdapter.StopScanningForDevicesAsync();
                }

                // Optionally, navigate back to the root page
                await Navigation.PopToRootAsync();
            }
            else if (CrossBluetoothLE.Current.State == BluetoothState.On)
            {
                // Restart scanning when Bluetooth is turned back on
                StartContinuousScan();
            }
        }

        // Check for BLE permission
        private async Task<bool> PermissionsGrantedAsync()
        {
            var locationPermissionStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (locationPermissionStatus != PermissionStatus.Granted)
            {
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                return status == PermissionStatus.Granted;
            }

            return true;
        }

        // Start continuous scan for Bluetooth devices only if Bluetooth is on
        private async void StartContinuousScan()
        {
            if (CrossBluetoothLE.Current.State != BluetoothState.On)
            {
                // Show a message if the app tries to scan with Bluetooth off
                await DisplayAlert("Bluetooth Off", "Please turn Bluetooth on to scan for devices.", "OK");
                return;
            }

            if (!await PermissionsGrantedAsync())
            {
                await DisplayAlert("Permission Required", "Application Needs Location Permission", "OK");
                return;
            }

            _bluetoothAdapter.DeviceDiscovered += DeviceDiscoveredHandler;

            if (!_bluetoothAdapter.IsScanning)
            {
                await _bluetoothAdapter.StartScanningForDevicesAsync();
            }
        }

        // Handle discovered devices and update the list
        private void DeviceDiscoveredHandler(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs e)
        {
            if (e.Device != null && !string.IsNullOrEmpty(e.Device.Name) && e.Device.Name.StartsWith("WATS"))
            {
                if (!_gattDevices.Contains(e.Device))
                {
                    _gattDevices.Add(e.Device);
                }
            }
        }

        // When an item is selected from the devices list
        private async void foundBleDevicesListView_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            _connectedDevice = e.Item as IDevice;

            if (_connectedDevice.State == DeviceState.Connected)
            {
                await SelectUnknownService(_connectedDevice);
            }
            else
            {
                try
                {
                    var connectParameters = new ConnectParameters(false, true);
                    await _bluetoothAdapter.ConnectToDeviceAsync(_connectedDevice, connectParameters);
                    await SelectUnknownService(_connectedDevice);
                }
                catch
                {
                    await DisplayAlert("Error connecting", $"Error connecting to BLE device: {_connectedDevice.Name ?? "Unknown device"}", "OK");
                }
            }
        }

        // Select and navigate to the Unknown Service
        private async Task SelectUnknownService(IDevice connectedDevice)
        {
            try
            {
                var servicesListReadOnly = await connectedDevice.GetServicesAsync();

                IService unknownService = null;

                foreach (var service in servicesListReadOnly)
                {
                    // Automatically select "Unknown Service" if it exists
                    if (service.Name == "Unknown Service")
                    {
                        unknownService = service;
                        break;
                    }
                }

                if (unknownService != null)
                {
                    await NavigateToDataPage(unknownService);
                }
                else
                {
                    await DisplayAlert("Error", "Unknown Service not found.", "OK");
                }
            }
            catch
            {
                await DisplayAlert("Error", "Error retrieving services.", "OK");
            }
        }

        // Navigate to the data page for the Unknown Service
        private async Task NavigateToDataPage(IService service)
        {
            await Navigation.PushAsync(new BtDataPage(_connectedDevice, service));
        }


        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Disconnect the connected device when coming back to this page
            if (_connectedDevice != null && _connectedDevice.State == DeviceState.Connected)
            {
                await _bluetoothAdapter.DisconnectDeviceAsync(_connectedDevice);
                _connectedDevice = null;
            }

            // Clear the list of previously found devices
            _gattDevices.Clear();

            // Only start scanning if Bluetooth is on
            if (CrossBluetoothLE.Current.State == BluetoothState.On)
            {
                StartContinuousScan();
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();

            // Stop scanning if it is still active
            if (_bluetoothAdapter.IsScanning)
            {
                await _bluetoothAdapter.StopScanningForDevicesAsync();
            }

            // Unsubscribe from Bluetooth state changes
            CrossBluetoothLE.Current.StateChanged -= OnBluetoothStateChanged;
        }

        // Handle device disconnection
        private async void OnDeviceDisconnected(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs e)
        {
            if (_connectedDevice != null && e.Device.Id == _connectedDevice.Id)
            {
                // Show an alert when the connected device disconnects
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Bluetooth Device Disconnected", "Returned to Devices Page", "OK");
                    await Navigation.PopToRootAsync();  // Navigate back to the root page (BtDevices)
                });
            }
        }
    }
}




