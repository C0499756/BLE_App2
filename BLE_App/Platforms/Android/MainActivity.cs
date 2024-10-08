using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace BLE_App
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Only request Bluetooth permissions for Android 12 (API 31) and above
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
                RequestBluetoothPermissions();
            }
        }

        void RequestBluetoothPermissions()
        {
            // Request BLUETOOTH_SCAN and BLUETOOTH_CONNECT for Android 12+
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothScan) != Permission.Granted ||
                ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothConnect) != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this, new string[]
                {
                    Manifest.Permission.BluetoothScan, //Allows the app to find nearby Bluetooth devices
                    Manifest.Permission.BluetoothConnect //allows the app to connect to bluetooth devices. 
                }, 0);
            }
        }

        // Handle the result of the permission request
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
            {
                // Permission granted, proceed with Bluetooth operations
            }
            else
            {
                // Permission denied, handle it appropriately (e.g., show a message to the user)
            }
        }
    }
}
