using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System.Diagnostics.CodeAnalysis;

namespace BLE_App
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Set AppShell as the main page
            MainPage = new AppShell();
        }

        protected override async void OnStart()
        {
            base.OnStart();

            // Check Bluetooth state when the app starts
            await CheckBluetoothStateAsync();
        }

        private async Task CheckBluetoothStateAsync()
        {
            // Check if Bluetooth is off
            if (CrossBluetoothLE.Current.State == BluetoothState.Off)
            {
                // Show an alert once if Bluetooth is off when the app starts
                await MainPage.DisplayAlert("Bluetooth Disabled", "Please turn Bluetooth on to use this app.", "OK");

                // Wait for Bluetooth to be enabled
                CrossBluetoothLE.Current.StateChanged += async (sender, args) =>
                {
                    if (CrossBluetoothLE.Current.State == BluetoothState.On)
                    {
                        // Once Bluetooth is enabled, allow access to the app and stop listening for changes
                        CrossBluetoothLE.Current.StateChanged -= null;
                    }
                };
            }
        }

        protected override async void OnResume()
        {
            base.OnResume();

            // Recheck Bluetooth state when the app resumes
            await CheckBluetoothStateAsync();
        }

        protected override void OnSleep()
        {
            // Logic for when the app goes into the background (optional)
        }
    }
}


