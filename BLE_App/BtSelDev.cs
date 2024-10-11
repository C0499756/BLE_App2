// This code is adapted from MoThunderz's Xamarin BLE App for Android (versions 12 and higher). 
// Modified for .NET MAUI to meet the requirements of the Wireless Automotive Telemetry System. 
// Code modified by the Laplogic Team. For more details, see the video: https://www.youtube.com/watch?v=SfGuLsKeOeE

using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections;
using System.ComponentModel;
using System.Threading.Tasks;



namespace BLE_App
{
    public partial class MainPage : ContentPage
    {
        private IAdapter _bluetoothAdapter;
        private List<IDevice> _gattDevices = new List<IDevice>();

        public MainPage()
        {
            InitializeComponent();

            _bluetoothAdapter =CrossBluetoothLE.Current.Adapter;
            _bluetoothAdapter.DeviceDiscovered += (sender, foundBleDevice) =>
            {
                if (foundBleDevice.Device != null && !string.IsNullOrEmpty(foundBleDevice.Device.Name))
                {
                    _gattDevices.Add(foundBleDevice.Device);
                }
            };
        }

        //function because we need permission to use the BLE interface. Ask app user for permission. 
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


        //When Scan button is clicked function
        private async void ScanBtn_Clicked(object sender, EventArgs e)
        {
            IsBusyIndicator.IsVisible = IsBusyIndicator.IsRunning = !(ScanBtn.IsEnabled = false);
            foundBleDevicesListView.ItemsSource = null;

            if (!await PermissionsGrantedAsync())
            {
                await DisplayAlert("Permission Required", "Application Needs Location Permission", "OK");
                IsBusyIndicator.IsVisible = IsBusyIndicator.IsRunning = !(ScanBtn.IsEnabled = true);
                return;
            }
            _gattDevices.Clear();

            if (!_bluetoothAdapter.IsScanning)
            {
                await _bluetoothAdapter.StartScanningForDevicesAsync();
            }

            foreach (var device in _bluetoothAdapter.ConnectedDevices)
            {
                _gattDevices.Add(device);
            }

            foundBleDevicesListView.ItemsSource = _gattDevices.ToArray(); 
            IsBusyIndicator.IsVisible = IsBusyIndicator.IsRunning =!(ScanBtn.IsEnabled = true);
        }

        private async void foundBleDevicesListView_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            IsBusyIndicator.IsVisible = IsBusyIndicator.IsRunning = !(ScanBtn.IsEnabled = false);
            IDevice selectedItem = e.Item as IDevice;

            if(selectedItem.State == DeviceState.Connected)
            {
                await Navigation.PushAsync(new Display(selectedItem));
            }
            else
            {
                try
                {
                var connectParameters = new ConnectParameters(false, true);
                await _bluetoothAdapter.ConnectToDeviceAsync(selectedItem, connectParameters);
                await Navigation.PushAsync(new Display(selectedItem));
                }
                catch
                {
                await DisplayAlert("Error connecting", $"Error connecting to BLE device: {selectedItem.Name ?? "Unknown device"}", "OK");
                }

            }

            IsBusyIndicator.IsVisible = IsBusyIndicator.IsRunning = !(ScanBtn.IsEnabled = true);


        }
    }

}
