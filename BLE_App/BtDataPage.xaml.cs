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
    string charStr;

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

                // Automatically select the "Unknown Characteristic" if found
                if (unknownCharacteristic != null)
                {
                    _char = unknownCharacteristic;
                    // Display the characteristic name and UUID
                    bleChar.Text = $"Selected BLE characteristic: {_char.Name}";
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
                        byte[] receivedBytes = args.Characteristic.Value;
                        Console.WriteLine("byte array: " + BitConverter.ToString(receivedBytes));

                        // Check if the length of received bytes is 4
                        if (receivedBytes.Length == 4)
                        {
                            // Convert byte array to a binary string
                            string binaryString = string.Join("", receivedBytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
                            Console.WriteLine("binary string: " + binaryString); // Output for debugging

                            // Check if the binary string is valid (32 bits)
                            if (Is32BitBinary(binaryString))
                            {
                                // Update Output on the main thread
                                await MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    Output.Text += "Received valid 32-bit binary: " + binaryString + "\n"; // Print the binary string
                                });

                                // Process the data if it's valid
                                ProcessPIDs(binaryString);
                            }
                            else
                            {
                                // Update Output on the main thread
                                await MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    Output.Text += "Invalid 32-bit binary data: " + binaryString + "\n"; // Print invalid message
                                });
                            }

                            // Scroll to the bottom of the ScrollView
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await Task.Delay(100); // Optional delay for better scrolling
                                await OutputScrollView.ScrollToAsync(0, Output.Height, true);
                            });
                        }
                        else
                        {
                            // Update Output on the main thread
                            await MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                Output.Text += "Received invalid length: " + receivedBytes.Length + "\n"; // Handle the invalid length case
                            });
                        }
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
        catch (Exception ex)
        {
            ErrorLabel.Text = GetTimeNow() + ": Error initializing UART GATT service. " + ex.Message;
        }
    }

    private bool Is32BitBinary(string data)
    {
        // Check if the data is exactly 32 characters long and consists of '0's and '1's
        return data.Length == 32 && data.All(c => c == '0' || c == '1');
    }

    private async void ShowError(string message)
    {
        ErrorLabel.Text = message;
        await Task.Delay(60000); // Wait for 60 seconds
        ErrorLabel.Text = ""; // Clear the error message
    }

    private string GetTimeNow()
    {
        var timestamp = DateTime.Now;
        return timestamp.Hour.ToString() + ":" + timestamp.Minute.ToString() + ":" + timestamp.Second.ToString();
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

    private void ProcessPIDs(string data)
    {

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
