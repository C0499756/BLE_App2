// This code is adapted from MoThunderz's Xamarin BLE App for Android (versions 12 and higher). 
// Modified for .NET MAUI to meet the requirements of the Wireless Automotive Telemetry System. 
// Code modified by the Laplogic Team. For more details, see the video: https://www.youtube.com/watch?v=SfGuLsKeOeE

using Microsoft.Maui.Animations;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace BLE_App;

public partial class BtDataPage : ContentPage
{
    private readonly IDevice _connectedDevice;
    private readonly IService _selectedService;
    private readonly List<ICharacteristic> _charList = new List<ICharacteristic>();
    private ICharacteristic _char;

    public BtDataPage(IDevice connectedDevice, IService selectedService)
    {
        InitializeComponent();

        _connectedDevice = connectedDevice;
        _selectedService = selectedService;
        _char = null;

        bleDevice.Text = "Selected BLE device: " + _connectedDevice.Name;
        bleService.Text = "Selected BLE service: " + _selectedService.Name;

    }

    //called straight after the page is loaded
    protected async override void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (_selectedService != null)
            {
                var charListReadOnly = await _selectedService.GetCharacteristicsAsync();

                _charList.Clear();
                var charListStr = new List<String>();
                for (int i = 0; i < charListReadOnly.Count; i++)
                {
                    _charList.Add(charListReadOnly[i]);
                    charListStr.Add(charListReadOnly[i].Name);
                }
                foundBleChars.ItemsSource = charListStr;
            }
            else
            {

            }

        }
        catch
        {
            ErrorLabel.Text += GetTimeNow() + ": Error intializing UART GATT service.";
        }
    }

    private async void FoundBleChars_ItemTapped(object sender, ItemTappedEventArgs e)
    {
        if (_selectedService != null)
        {
            _char = _charList[e.ItemIndex];
            bleChar.Text = _char.Name + "\n" +
                "UUID: " + _char.Uuid.ToString() + "\n" +
                "Read: " + _char.CanRead + "\n" +
                "Write: " + _char.CanRead + "\n" +
                "Update: " + _char.CanUpdate;
            var charDescriptors = await _char.GetDescriptorsAsync();

            bleChar.Text += "\nDescriptors (" + charDescriptors.Count + "): ";
            for (int i = 0; i < charDescriptors.Count; i++)
                bleChar.Text += charDescriptors[i].Name + ", ";
        }
    }

    private async void RegisterButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_char != null)
            {
                _char.ValueUpdated += (o, args) =>
                {
                    var receivedBytes = args.Characteristic.Value;
                    Console.WriteLine("byte array: " + BitConverter.ToString(receivedBytes));

                    string charStr = "";
                    if (receivedBytes != null)
                    {
                        charStr = "Bytes: " + BitConverter.ToString(receivedBytes);
                        charStr += " | UTF8: " + Encoding.UTF8.GetString(receivedBytes); //this line may be wrong and you may need to look it up
                    }

                    if (receivedBytes.Length <= 4)
                    {
                        int charVal = 0;
                        for (int i = 0; i < receivedBytes.Length; i++)
                        {
                            charVal |= (receivedBytes[i] << (i * 8));
                        }
                        charStr += " | int: " + charVal.ToString();
                    }
                    charStr += Environment.NewLine;

                    // Update UI on the main thread
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Output.Text += charStr;
                    });
                };

                await _char.StartUpdatesAsync();
                ErrorLabel.Text = GetTimeNow() + ": Notify callback function registered successfully.";
            }
            else
            {
                ErrorLabel.Text = GetTimeNow() + ": UART GATT service not found";
            }
        }
        catch
        {
            ErrorLabel.Text = GetTimeNow() + ": Error initializing UART GATT service.";
        }
    }


    private async void SendButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_char != null)
            {
                byte[] array = Encoding.UTF8.GetBytes(CommandTxt.Text);
                await _char.WriteAsync(array);
            }
        }
        catch
        {
            ErrorLabel.Text = GetTimeNow() + ": Error receiving Characteristic.";
        }
    }

    private async void ReceiveButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_char != null)
            {
                // Assuming _char.ReadAsync() returns a tuple (byte[] data, int resultCode)
                var (receivedBytes, resultCode) = await _char.ReadAsync();

                // Ensure the receivedBytes is not null
                if (receivedBytes != null && receivedBytes.Length > 0)
                {
                    Output.Text += Encoding.UTF8.GetString(receivedBytes) + Environment.NewLine;
                }
                else
                {
                    Output.Text += "No data received." + Environment.NewLine;
                }
            }
            else
            {
                ErrorLabel.Text = GetTimeNow() + ": No Characteristic selected.";
            }
        }
        catch
        {
            ErrorLabel.Text = GetTimeNow() + ": Error receiving Characteristic. ";
        }
    }



    private string GetTimeNow()
    {
        var timestamp = DateTime.Now;
        return timestamp.Hour.ToString() + ":" + timestamp.Minute.ToString() + ":" + timestamp.Second.ToString();
    }

}