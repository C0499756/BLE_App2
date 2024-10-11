// This code is adapted from MoThunderz's Xamarin BLE App for Android (versions 12 and higher). 
// Modified for .NET MAUI to meet the requirements of the Wireless Automotive Telemetry System. 
// Code modified by the Laplogic Team. For more details, see the video: https://www.youtube.com/watch?v=SfGuLsKeOeE

using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace BLE_App;

public partial class BtServices : ContentPage
{
	private IDevice _connectedDevice; //BLE device we are connected to
	private List<IService> _servicesList = new List<IService>(); //list of services that BLE device has

	public BtServices(IDevice connectedDevice) //gets a parameter, the BLE device we connected to
	{
		InitializeComponent();

		_connectedDevice = connectedDevice;
		bleDevice.Text="Selected BLE Device: " + _connectedDevice.Name;

	}

	//called straight after the page is loaded
	protected async override void OnAppearing()
	{
		base.OnAppearing();

		try
		{
			var servicesListReadOnly = await _connectedDevice.GetServicesAsync();

			_servicesList.Clear();
			var _servicesListStr = new List<String>();
			for(int i = 0; i < servicesListReadOnly.Count; i++)
			{
				_servicesList.Add(servicesListReadOnly[i]);
				_servicesListStr.Add(servicesListReadOnly[i].Name + ", UUID: " + servicesListReadOnly[i].Id.ToString());
			}
			foundBleServs.ItemsSource = _servicesListStr;
		}
		catch
		{
			await DisplayAlert("Error initializing", $"Error initializing UART GATT service.", "OK");
		}
	}
    private async void foundBleServs_ItemTapped(object sender, ItemTappedEventArgs e)
    {
		var selectedService = _servicesList[e.ItemIndex];
		if(selectedService!= null)
		{
			await Navigation.PushAsync(new BtDataPage(_connectedDevice, selectedService));
		}
    }
}