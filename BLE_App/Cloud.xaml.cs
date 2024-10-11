using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace BLE_App;

public partial class Cloud : ContentPage
{
    public Cloud()
    {
        InitializeComponent();
        GetLocationAsync();
    }

    private async Task GetLocationAsync()
    {
        try
        {
            // Check if location services are enabled
            var location = await Geolocation.GetLastKnownLocationAsync();

            if (location != null)
            {
                // Update the label with the location data
                LocationLabel.Text = $"Latitude: {location.Latitude}, Longitude: {location.Longitude}";
            }
            else
            {
                // If no last known location, get the current location
                location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best));
                if (location != null)
                {
                    LocationLabel.Text = $"Latitude: {location.Latitude}, Longitude: {location.Longitude}";
                }
                else
                {
                    LocationLabel.Text = "Unable to get location.";
                }
            }
        }
        catch (FeatureNotSupportedException fnsEx)
        {
            LocationLabel.Text = "Geolocation is not supported.";
        }
        catch (PermissionException pEx)
        {
            LocationLabel.Text = "Permission to access location was denied.";
        }
        catch (Exception ex)
        {
            LocationLabel.Text = $"Unable to get location: {ex.Message}";
        }
    }
}
