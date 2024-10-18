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
using System.Timers;

namespace BLE_App;

public partial class BtDataPage : ContentPage
{
    private readonly IDevice _connectedDevice;
    private readonly IService _selectedService;
    private readonly List<ICharacteristic> _charList = new List<ICharacteristic>();
    private ICharacteristic _char;

    private System.Timers.Timer _rpmTimer;
    private System.Timers.Timer _speedTimer;
    private System.Timers.Timer _coolantTempTimer;
    private System.Timers.Timer _engineTempTimer;

    private bool _isRPMButtonClicked;
    private bool _isSpeedButtonClicked;
    private bool _isCoolantTempButtonClicked;
    private bool _isEngineTempButtonClicked;

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
                ICharacteristic unknownCharacteristic = null;

                for (int i = 0; i < charListReadOnly.Count; i++)
                {
                    _charList.Add(charListReadOnly[i]);
                    charListStr.Add(charListReadOnly[i].Name);

                    // Check for the "Unknown Characteristic"
                    if (charListReadOnly[i].Name == "Unknown characteristic")
                    {
                        unknownCharacteristic = charListReadOnly[i];
                        break; // Exit loop once found
                    }
                }

                foundBleChars.ItemsSource = charListStr;

                // Automatically select the "Unknown Characteristic" if found
                if (unknownCharacteristic != null)
                {
                    _char = unknownCharacteristic;
                    bleChar.Text = _char.Name + "\n" +
                        "UUID: " + _char.Uuid.ToString() + "\n" +
                        "Read: " + _char.CanRead + "\n" +
                        "Write: " + _char.CanWrite + "\n" +
                        "Update: " + _char.CanUpdate;

                    //Automaticlaly Call RegisterButton_Clicked after selecting the character
                    RegisterButton_Clicked(null, null);
                    await Task.Delay(250); // Wait for 250ms
                    SendBluetoothRequest("PIDs");
                }
            }
            else
            {
                ErrorLabel.Text += GetTimeNow() + ": Error initializing UART GATT service.";
            }
        }
        catch
        {
            ErrorLabel.Text += GetTimeNow() + ": Error initializing UART GATT service.";
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
                if (_char.CanUpdate)
                {
                    _char.ValueUpdated += async (o, args) =>
                    {
                        var receivedBytes = args.Characteristic.Value;
                        Console.WriteLine("byte array: " + BitConverter.ToString(receivedBytes));

                        string charStr = "";
                        if (receivedBytes != null)
                        {
                            charStr += Encoding.UTF8.GetString(receivedBytes);
                            charStr += Environment.NewLine; // Add a newline after the received data

                            // Update UI on the main thread
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                Output.Text += charStr; // Update the Output text

                                // Scroll to the bottom of the ScrollView
                                await Task.Delay(100); // Optional delay for better scrolling
                                await OutputScrollView.ScrollToAsync(0, Output.Height, true);
                            });
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
                    };

                    await _char.StartUpdatesAsync();
                    ErrorLabel.Text = GetTimeNow() + ": Notify callback function registered successfully.";
                }
                else
                {
                    ErrorLabel.Text = GetTimeNow() + ": Characteristic does not have a notify function.";
                }
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

    private async void ShowError(string message)
    {
        ErrorLabel.Text = message;
        await Task.Delay(60000); // Wait for 10 seconds
        ErrorLabel.Text = ""; // Clear the error message
    }

    private string GetTimeNow()
    {
        var timestamp = DateTime.Now;
        return timestamp.Hour.ToString() + ":" + timestamp.Minute.ToString() + ":" + timestamp.Second.ToString();
    }

    private void OnButtonClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        if (button == null) return;

        switch (button.Text)
        {
            case "RPM":
                _isRPMButtonClicked = !_isRPMButtonClicked;
                button.BackgroundColor = _isRPMButtonClicked ? Colors.Green : Colors.Gray;
                HandleButtonState(_isRPMButtonClicked, "Request RPM");
                break;
            case "Speed":
                _isSpeedButtonClicked = !_isSpeedButtonClicked;
                button.BackgroundColor = _isSpeedButtonClicked ? Colors.Green : Colors.Gray;
                HandleButtonState(_isSpeedButtonClicked, "Request Speed");
                break;
            case "Coolant Temp":
                _isCoolantTempButtonClicked = !_isCoolantTempButtonClicked;
                button.BackgroundColor = _isCoolantTempButtonClicked ? Colors.Green : Colors.Gray;
                HandleButtonState(_isCoolantTempButtonClicked, "Request CoolantTemp");
                break;
            case "Engine Temp":
                _isEngineTempButtonClicked = !_isEngineTempButtonClicked;
                button.BackgroundColor = _isEngineTempButtonClicked ? Colors.Green : Colors.Gray;
                HandleButtonState(_isEngineTempButtonClicked, "Request EngineTemp");
                break;
        }
    }

    private void HandleButtonState(bool isButtonClicked, string request)
    {
        switch (request)
        {
            case "Request RPM":
                if (isButtonClicked)
                {
                    _rpmTimer = new System.Timers.Timer(1000);
                    _rpmTimer.Elapsed += (sender, e) => SendBluetoothRequest(request);
                    _rpmTimer.Start();
                }
                else
                {
                    _rpmTimer?.Stop();
                    _rpmTimer?.Dispose();
                    _rpmTimer = null;
                }
                break;
            case "Request Speed":
                if (isButtonClicked)
                {
                    _speedTimer = new System.Timers.Timer(1000);
                    _speedTimer.Elapsed += (sender, e) => SendBluetoothRequest(request);
                    _speedTimer.Start();
                }
                else
                {
                    _speedTimer?.Stop();
                    _speedTimer?.Dispose();
                    _speedTimer = null;
                }
                break;
            case "Request CoolantTemp":
                if (isButtonClicked)
                {
                    _coolantTempTimer = new System.Timers.Timer(1000);
                    _coolantTempTimer.Elapsed += (sender, e) => SendBluetoothRequest(request);
                    _coolantTempTimer.Start();
                }
                else
                {
                    _coolantTempTimer?.Stop();
                    _coolantTempTimer?.Dispose();
                    _coolantTempTimer = null;
                }
                break;

            case "Request EngineTemp":
                if (isButtonClicked)
                {
                    _engineTempTimer = new System.Timers.Timer(1000);
                    _engineTempTimer.Elapsed += (sender, e) => SendBluetoothRequest(request);
                    _engineTempTimer.Start();
                }
                else
                {
                    _engineTempTimer?.Stop();
                    _engineTempTimer?.Dispose();
                    _engineTempTimer = null;
                }
                break;
        }
    }

    private async void SendBluetoothRequest(string request)
    {
        try
        {
            if (_char != null)
            {
                byte[] array = Encoding.UTF8.GetBytes(request);  // Convert the request string to bytes

                // Write the request to the characteristic (similar to SendButton_Clicked logic)
                await _char.WriteAsync(array);

                // Update the UI on the main thread
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    Output.Text += request + " sent via Bluetooth." + Environment.NewLine;

                    // Scroll to the bottom of the ScrollView on the main thread
                    await Task.Delay(100); // Optional delay for smooth scrolling
                    await OutputScrollView.ScrollToAsync(0, Output.Height, true);
                });
            }
            else
            {
                ShowError(GetTimeNow() + ": No BLE characteristic found.");
            }
        }
        catch
        {
            ShowError(GetTimeNow() + ": Error sending Bluetooth request.");
        }
    }

    private async void plusButton_Clicked(object sender, EventArgs e)
    {
        // Create the content for the modal
        var layout = new StackLayout();

        // Add a label and checkbox for "Monitor status since DTCs cleared"
        var monitorStatusCheckbox = new CheckBox { IsChecked = false };
        var monitorStatusLabel = new Label { Text = "Monitor status since DTCs cleared" };

        layout.Children.Add(monitorStatusLabel);
        layout.Children.Add(monitorStatusCheckbox);

        // Add a submit button
        var submitButton = new Button { Text = "Submit" };
        layout.Children.Add(submitButton);

        // Create a modal content page
        var modalPage = new ContentPage
        {
            Content = new StackLayout
            {
                Padding = new Thickness(10),
                Children = { layout }
            }
        };

        // When the submit button is clicked, check if the checkbox is selected
        submitButton.Clicked += (s, args) =>
        {
            if (monitorStatusCheckbox.IsChecked)
            {
                // Handle the request for "Monitor status since DTCs cleared"
                Console.WriteLine("Monitor status since DTCs cleared selected");
                // You can add your request handling logic here
            }

            // Close the modal
            Navigation.PopModalAsync();
        };

        // Show the modal
        await Navigation.PushModalAsync(modalPage);
    }

}
