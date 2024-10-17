using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

namespace BLE_App
{
    public partial class BtDevices : ContentPage
    {
        private IAdapter _bluetoothAdapter;
        private ObservableCollection<IDevice> _gattDevices = new ObservableCollection<IDevice>();

        public BtDevices()
        {
            InitializeComponent();

            _bluetoothAdapter = CrossBluetoothLE.Current.Adapter;

            // Bind the ListView to the ObservableCollection
            foundBleDevicesListView.ItemsSource = _gattDevices;

            // Automatically start scanning when the page is loaded
            StartContinuousScan();
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

            while (true) // Infinite loop for continuous scanning
            {
                if (!_bluetoothAdapter.IsScanning)
                {
                    await _bluetoothAdapter.StartScanningForDevicesAsync();
                }

                // Wait for a while before rescanning
                await Task.Delay(5000); // Adjust the delay based on your needs
            }
        }

        // Handle discovered devices and update the list
        private void DeviceDiscoveredHandler(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs e)
        {
            if (e.Device != null && !string.IsNullOrEmpty(e.Device.Name) && e.Device.Name.StartsWith("WATS"))
            {
                // Add the device if it's not already in the list
                if (!_gattDevices.Contains(e.Device))
                {
                    _gattDevices.Add(e.Device);
                }
            }
        }

        // Dynamically remove devices that are no longer available
        private async void RemoveUnavailableDevices()
        {
            while (true)
            {
                bool listUpdated = false;  // Track if any updates are made to the list

                // Iterate through a copy of the list to safely remove disconnected devices
                foreach (var device in _gattDevices.ToList())
                {
                    if (device.State == DeviceState.Disconnected)
                    {
                        _gattDevices.Remove(device);  // Remove disconnected devices
                        listUpdated = true;  // Flag that an update was made
                    }
                }

                if (listUpdated)
                {
                    // Update the ListView's ItemsSource only if there are changes
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        foundBleDevicesListView.ItemsSource = null;  // Clear the list
                        foundBleDevicesListView.ItemsSource = _gattDevices.ToArray();  // Set updated list
                    });
                }

                await Task.Delay(5000); // Check for unavailable devices every 5 seconds
            }
        }



        // When an item is selected from the list
        private async void foundBleDevicesListView_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            IDevice selectedItem = e.Item as IDevice;

            if (selectedItem.State == DeviceState.Connected)
            {
                await Navigation.PushAsync(new BtServices(selectedItem));
            }
            else
            {
                try
                {
                    var connectParameters = new ConnectParameters(false, true);
                    await _bluetoothAdapter.ConnectToDeviceAsync(selectedItem, connectParameters);
                    await Navigation.PushAsync(new BtServices(selectedItem));
                }
                catch
                {
                    await DisplayAlert("Error connecting", $"Error connecting to BLE device: {selectedItem.Name ?? "Unknown device"}", "OK");
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Stop scanning when the page is closed to save resources
            if (_bluetoothAdapter.IsScanning)
            {
                _bluetoothAdapter.StopScanningForDevicesAsync();
            }
        }
    }
}
