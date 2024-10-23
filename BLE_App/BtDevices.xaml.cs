using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Timers;

namespace BLE_App
{
    public partial class BtDevices : ContentPage
    {
        private IAdapter _bluetoothAdapter;
        private ObservableCollection<IDevice> _gattDevices = new ObservableCollection<IDevice>();
        private IDevice _connectedDevice;
        private System.Timers.Timer _connectionCheckTimer;
        private DateTime _lastDataReceivedTime;
        private const int ConnectionCheckInterval = 6000; //check every 6 seconds
        private bool _hasAlertedDisconnection; // To track if alert has been shown

        public BtDevices()
        {
            InitializeComponent();

            _bluetoothAdapter = CrossBluetoothLE.Current.Adapter;

            // Bind the device ListView to the ObservableCollection
            foundBleDevicesListView.ItemsSource = _gattDevices;

            //Subscribe to DeviceDisconnected event
            _bluetoothAdapter.DeviceDisconnected += OnDeviceDisconnected;

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

        // Start continuous scan for Bluetooth devices
        private async void StartContinuousScan()
        {
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

        // Handle device disconnection
        private async void OnDeviceDisconnected(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs e)
        {
            if (_connectedDevice != null && e.Device.Id == _connectedDevice.Id)
            {
                // Stop the connection check timer
                _connectionCheckTimer?.Stop();

                // Navigate back to BtDevices page and display a message
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Bluetooth Device Disconnected", "Return to Devices Page", "OK");
                    await Navigation.PopToRootAsync();  // Navigate back to root page (BtDevices)
                });
            }
        }

        // Start the connection check timer
        private void StartConnectionCheckTimer()
        {
            _connectionCheckTimer = new System.Timers.Timer(ConnectionCheckInterval);
            _connectionCheckTimer.Elapsed += OnConnectionCheckTimerElapsed;
            _connectionCheckTimer.Start();
        }

        // Check if the device is still connected
        private void OnConnectionCheckTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_connectedDevice != null && (DateTime.Now - _lastDataReceivedTime).TotalMilliseconds > ConnectionCheckInterval)
            {
                // If no data received for the interval duration, consider it disconnected
                if (!_hasAlertedDisconnection)
                {
                    _hasAlertedDisconnection = true; //prevent multiple alerts
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                    await DisplayAlert("Bluetooth Device Disconnected", "No data received. Returning to Devices Page.", "OK");
                    await Navigation.PopToRootAsync();
                    });
                }

            }
            else
            {
                //reset the alert if data is received again
                _hasAlertedDisconnection= false;
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

        // Handle data received from the device (update this method according to your data reception logic)
        private void OnDataReceived(object sender, EventArgs e)
        {
            // Update the last data received time
            _lastDataReceivedTime = DateTime.Now;

        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // If there is a connected device, disconnect when coming back to this page
            if (_connectedDevice != null && _connectedDevice.State == DeviceState.Connected)
            {
                await _bluetoothAdapter.DisconnectDeviceAsync(_connectedDevice);
                _connectedDevice = null;
            }

            // Clear the list of previously found devices
            _gattDevices.Clear();

            // Restart the scanning process
            StartContinuousScan();

            //Start the connection chekc timer 
            StartConnectionCheckTimer();
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();

            // Stop scanning if it is still active
            if (_bluetoothAdapter.IsScanning)
            {
                await _bluetoothAdapter.StopScanningForDevicesAsync();
            }

            // No need to disconnect here, just clean up if necessary
        }
    }
}
