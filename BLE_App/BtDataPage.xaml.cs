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
    { "Monitor status since DTCs cleared", 0 },  // Bit 0
    { "Freeze DTC", 1 },                        // Bit 1
    { "Fuel system status", 2 },                // Bit 2
    { "Calculated engine load", 3 },            // Bit 3
    { "Engine coolant temperature", 4 },         // Bit 4
    { "Short term fuel trim (bank 1)", 5 },     // Bit 5
    { "Long term fuel trim (bank 1)", 6 },      // Bit 6
    { "Short term fuel trim (bank 2)", 7 },     // Bit 7
    { "Long term fuel trim (bank 2)", 8 },      // Bit 8
    { "Fuel pressure (gauge pressure)", 9 },    // Bit 9
    { "Intake manifold absolute pressure", 10 }, // Bit 10
    { "Engine speed", 11 },                      // Bit 11
    { "Vehicle speed", 12 },                     // Bit 12
    { "Timing advance", 13 },                    // Bit 13
    { "Intake air temperature", 14 },            // Bit 14
    { "Mass air flow sensor air flow rate", 15 },// Bit 15
    { "Throttle position", 16 },                 // Bit 16
    { "Commanded secondary air status", 17 },    // Bit 17
    { "Oxygen sensors present (2 banks)", 18 },  // Bit 18
    { "Oxygen sensor 1 (voltage)", 19 },         // Bit 19
    { "Oxygen sensor 2 (voltage)", 20 },         // Bit 20
    { "Oxygen sensor 3 (voltage)", 21 },         // Bit 21
    { "Oxygen sensor 4 (voltage)", 22 },         // Bit 22
    { "Oxygen sensor 5 (voltage)", 23 },         // Bit 23
    { "Oxygen sensor 6 (voltage)", 24 },         // Bit 24
    { "Oxygen sensor 7 (voltage)", 25 },         // Bit 25
    { "Oxygen sensor 8 (voltage)", 26 },         // Bit 26
    { "OBD standards the vehicle conforms to", 27 }, // Bit 27
    { "Oxygen sensors present (4 banks)", 28 },   // Bit 28
    { "Auxiliary input status", 29 },             // Bit 29
    { "Run time since engine start", 30 },        // Bit 30
    { "PIDs supported [21 - 40]", 31 }            // Bit 31
};


    private void ProcessPIDs(string binaryString)
    {
        if (binaryString.Length < 32) // Ensure it's long enough for 32 bits
        {
            Console.WriteLine("Binary string is too short.");
            return;
        }

        // Invert the binaryString
        string invertedBinaryString = new string(binaryString.Reverse().ToArray());

        // Store the available options based on the inverted binary string
        foreach (var option in optionMapping)
        {
            var isAvailable = (invertedBinaryString[invertedBinaryString.Length - 1 - option.Value] == '1') ? "yes" : "no";
            Console.WriteLine($"{option.Key}: {isAvailable}");
        }
    }

    private async void plusButton_Clicked(object sender, EventArgs e)
    {
        // Create the main layout
        var layout = new StackLayout
        {
            Padding = new Thickness(10),
        };

        // Create the "X" button for closing the modal
        var closeButton = new Button
        {
            Text = "X",
            BackgroundColor = Colors.DarkBlue, // Optional: make it transparent
            TextColor = Colors.White, // Change this to your preferred color
            VerticalOptions = LayoutOptions.Start // Place it at the top
        };

        // Event for the close button
        closeButton.Clicked += async (s, args) =>
        {
            // Close the modal when clicked
            await Navigation.PopModalAsync();
        };

        // Add the close button to the layout
        layout.Children.Add(closeButton);

        // Create a StackLayout for the scrollable options
        var scrollableLayout = new StackLayout();

        // Iterate through the options and create checkboxes for available ones
        foreach (var option in optionMapping)
        {
            // Check if the corresponding bit in binaryString is set
            if ((binaryString[binaryString.Length - 1 - option.Value] == '1'))
            {
                // Create a Grid for each option
                var optionGrid = new Grid
                {
                    ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star }, // Label column (auto-expand)
                    new ColumnDefinition { Width = GridLength.Auto }  // Checkbox column (fixed size)
                },
                    Padding = new Thickness(0, 5) // Add padding for better spacing between rows
                };

                var label = new Label
                {
                    Text = option.Key,
                    VerticalOptions = LayoutOptions.Center, // Align label to the center vertically
                    HorizontalOptions = LayoutOptions.Start // Left align the label
                };

                var checkBox = new CheckBox
                {
                    IsChecked = false,
                    VerticalOptions = LayoutOptions.Center, // Align checkbox to the center vertically
                    HorizontalOptions = LayoutOptions.End   // Align checkbox to the right
                };

                // Add label and checkbox to the Grid
                optionGrid.Children.Add(label);    // Add label to the Grid
                Grid.SetColumn(label, 0);          // Set label column to 0
                Grid.SetRow(label, 0);             // Set label row to 0

                optionGrid.Children.Add(checkBox); // Add checkbox to the Grid
                Grid.SetColumn(checkBox, 1);       // Set checkbox column to 1
                Grid.SetRow(checkBox, 0);          // Set checkbox row to 0

                // Add the optionGrid to the scrollable layout
                scrollableLayout.Children.Add(optionGrid);
            }
        }

        // Create a ScrollView and set the content to the scrollable layout
        var scrollView = new ScrollView
        {
            Content = scrollableLayout,
            VerticalOptions = LayoutOptions.FillAndExpand // Allow it to fill the available vertical space
        };

        // Add the ScrollView to the main layout
        layout.Children.Add(scrollView);

        // Create a modal content page
        var modalPage = new ContentPage
        {
            Content = layout
        };

        // Show the modal
        await Navigation.PushModalAsync(modalPage);
    }

}
