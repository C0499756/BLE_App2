using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;


namespace BLE_App;

public partial class Cloud : ContentPage
{
    private CancellationTokenSource _cts;

    public Cloud()
    {
        InitializeComponent();
        StartLocationUpdates();
        StartAccelerometer();
    }

    private void StartLocationUpdates()
    {
        _cts = new CancellationTokenSource();
        GetLocationAsync(_cts.Token);
    }

    private async Task GetLocationAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Request location updates
            while (!cancellationToken.IsCancellationRequested)
            {
                var location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5)));

                if (location != null)
                {
                    LocationLabel.Text = $"Latitude: {location.Latitude}, \nLongitude: {location.Longitude}";
                }
                else
                {
                    LocationLabel.Text = "Unable to get location.";
                }
            }
        }
        catch (FeatureNotSupportedException)
        {
            LocationLabel.Text = "Geolocation is not supported.";
        }
        catch (PermissionException)
        {
            LocationLabel.Text = "Permission to access location was denied.";
        }
        catch (Exception ex)
        {
            LocationLabel.Text = $"Unable to get location: {ex.Message}";
        }
    }

    private void StartAccelerometer()
    {
        try
        {
            Accelerometer.ReadingChanged += Accelerometer_ReadingChanged;
            Accelerometer.Start(SensorSpeed.UI);
        }
        catch (FeatureNotSupportedException)
        {
            AccelerometerLabel.Text = "Accelerometer not supported.";
        }
        catch (Exception ex)
        {
            AccelerometerLabel.Text = $"Unable to start accelerometer: {ex.Message}";
        }
    }

    private void Accelerometer_ReadingChanged(object sender, AccelerometerChangedEventArgs e)
    {
        var reading = e.Reading;
        AccelerometerLabel.Text = $"X: {reading.Acceleration.X:F2}, Y: {reading.Acceleration.Y:F2}, Z: {reading.Acceleration.Z:F2}";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopLocationUpdates();
        StopAccelerometer();
    }

    private void StopLocationUpdates()
    {
        _cts.Cancel();
    }

    private void StopAccelerometer()
    {
        Accelerometer.Stop();
        Accelerometer.ReadingChanged -= Accelerometer_ReadingChanged;
    }
}

