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
    string binaryString;

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
                            binaryString = string.Join("", receivedBytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
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

    private Dictionary<string, int> optionMapping = new Dictionary<string, int>
{
    { "PIDs supported [21 - 40]", 0 },           // Bit 0
    { "Run time since engine start", 1 },         // Bit 1
    { "Auxiliary input status", 2 },               // Bit 2
    { "Oxygen sensors present (4 banks)", 3 },     // Bit 3
    { "OBD standards the vehicle conforms to", 4 }, // Bit 4
    { "Oxygen sensor 8 (voltage)", 5 },            // Bit 5
    { "Oxygen sensor 7 (voltage)", 6 },            // Bit 6
    { "Oxygen sensor 6 (voltage)", 7 },            // Bit 7
    { "Oxygen sensor 5 (voltage)", 8 },            // Bit 8
    { "Oxygen sensor 4 (voltage)", 9 },            // Bit 9
    { "Oxygen sensor 3 (voltage)", 10 },           // Bit 10
    { "Oxygen sensor 2 (voltage)", 11 },           // Bit 11
    { "Oxygen sensor 1 (voltage)", 12 },           // Bit 12
    { "Oxygen sensors present (2 banks)", 13 },     // Bit 13
    { "Commanded secondary air status", 14 },      // Bit 14
    { "Throttle position", 15 },                    // Bit 15
    { "Mass air flow sensor air flow rate", 16 },   // Bit 16
    { "Intake air temperature", 17 },               // Bit 17
    { "Timing advance", 18 },                       // Bit 18
    { "Vehicle speed", 19 },                        // Bit 19
    { "Engine speed", 20 },                         // Bit 20
    { "Intake manifold absolute pressure", 21 },    // Bit 21
    { "Fuel pressure (gauge pressure)", 22 },      // Bit 22
    { "Long term fuel trim (bank 2)", 23 },        // Bit 23
    { "Short term fuel trim (bank 2)", 24 },       // Bit 24
    { "Long term fuel trim (bank 1)", 25 },        // Bit 25
    { "Short term fuel trim (bank 1)", 26 },       // Bit 26
    { "Engine coolant temperature", 27 },           // Bit 27
    { "Calculated engine load", 28 },              // Bit 28
    { "Fuel system status", 29 },                   // Bit 29
    { "Freeze DTC", 30 },                          // Bit 30
    { "Monitor status since DTCs cleared", 31 }     // Bit 31
};

    private void ProcessPIDs(string binaryString)
    {
        if (binaryString.Length < 32) // Ensure it's long enough for 32 bits
        {
            Console.WriteLine("Binary string is too short.");
            return;
        }

        // Store the available options based on the binary string
        foreach (var option in optionMapping)
        {
            var isAvailable = (binaryString[binaryString.Length - 1 - option.Value] == '1') ? "yes" : "no";
            Console.WriteLine($"{option.Key}: {isAvailable}");
        }
    }

    private async void plusButton_Clicked(object sender, EventArgs e)
    {
        // Create a StackLayout for the checkboxes
        var layout = new StackLayout();

        // Iterate through the options and create checkboxes for available ones
        foreach (var option in optionMapping)
        {
            // Check if the corresponding bit in binaryString is set
            if ((binaryString[binaryString.Length - 1 - option.Value] == '1'))
            {
                var checkBox = new CheckBox { IsChecked = false };
                var label = new Label { Text = option.Key };

                layout.Children.Add(label);
                layout.Children.Add(checkBox);
            }
        }

        // Add a submit button
        var submitButton = new Button { Text = "Submit" };
        layout.Children.Add(submitButton);

        // Create a ScrollView to hold the layout
        var scrollView = new ScrollView
        {
            Content = layout,
            VerticalOptions = LayoutOptions.FillAndExpand // Allow it to fill the available space
        };

        // Create a modal content page with the ScrollView
        var modalPage = new ContentPage
        {
            Content = new StackLayout
            {
                Padding = new Thickness(10),
                Children = { scrollView }
            }
        };

        // When the submit button is clicked, check if any checkboxes are selected
        submitButton.Clicked += (s, args) =>
        {
            foreach (var child in layout.Children)
            {
                if (child is CheckBox cb && cb.IsChecked)
                {
                    var optionIndex = layout.Children.IndexOf(cb) / 2; // Every label is followed by its checkbox
                    Console.WriteLine($"{optionMapping.ElementAt(optionIndex).Key} selected");
                    // You can add your request handling logic here
                }
            }

            // Close the modal
            Navigation.PopModalAsync();
        };

        // Show the modal
        await Navigation.PushModalAsync(modalPage);
    }



}
