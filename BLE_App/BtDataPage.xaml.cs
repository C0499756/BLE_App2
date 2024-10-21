//This code is adapted from MoThunderz's Xamarin BLE App for Android (versions 12 and higher). 
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

    // Create an instance of MQTTServer
    private MQTTServer mqttServer;
    // Add a boolean flag to track if the request has been sent
    private bool _hasSentPidRequest = false;
    private bool _hasReceivedPidRequest = false;
    private bool _visiblePlusButton; //make the plus button visible because we have received appropriate PIDs. 

    public BtDataPage(IDevice connectedDevice, IService selectedService)
    {
        InitializeComponent();

        _connectedDevice = connectedDevice;
        _selectedService = selectedService;
        _char = null;

        mqttServer = new MQTTServer(); //Initalize MQTT server

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
                    // Check if the request has already been sent
                    if (!_hasSentPidRequest)
                    {
                        SendBluetoothRequest("PIDs");
                        _hasSentPidRequest = true; // Set the flag to true after sending
                    }
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
                        // Receive the data as bytes
                        byte[] receivedBytes = args.Characteristic.Value;

                        // Convert the bytes to characters
                        char[] receivedChars = new char[receivedBytes.Length];
                        for (int i = 0; i < receivedBytes.Length; i++)
                        {
                            receivedChars[i] = (char)receivedBytes[i]; // Convert each byte to a char
                        }

                        // Convert the characters to a string
                        string receivedString = new string(receivedChars);

                        // Check if this is the first PID request
                        if (!_hasReceivedPidRequest)
                        {
                            // Set the flag to true after the first PID information is received
                            _hasReceivedPidRequest = true;

                            // Process the PID information
                            if (receivedBytes.Length == 4)
                            {
                                // Handle the 32-bit PID information (don't send to the server)
                                binaryString = string.Join("", receivedBytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
                                if (Is32BitBinary(binaryString))
                                {
                                    await MainThread.InvokeOnMainThreadAsync(() =>
                                    {
                                        Output.Text += "Received valid 32-bit binary (first PID request): " + binaryString + "\n";
                                    });
                                    ProcessPIDs(binaryString); // Process the 32-bit PID data
                                }
                                else
                                {
                                    await MainThread.InvokeOnMainThreadAsync(() =>
                                    {
                                        Output.Text += "Invalid 32-bit binary data (first PID request): " + binaryString + "\n";

                                    });
                                }
                            }

                            // Return early after processing the first PID request
                            return;
                        }

                        // For regular strings, send to the MQTT server
                        mqttServer.PublishMessage(receivedString);

                        // Update UI elements on the main thread
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            // Update a Label or other UI element
                            Output.Text += "Received String: " + receivedString + "\n";
                        });
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

    private async Task SendBluetoothRequest(string request)
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

    private Dictionary<string, string> optionHexMapping = new Dictionary<string, string>
{
    { "Monitor status since DTCs cleared", "01" },  // Mode 01 PID 01
    { "Freeze DTC", "02" },                         // Mode 01 PID 02
    { "Fuel system status", "03" },                 // Mode 01 PID 03
    { "Calculated engine load", "04" },             // Mode 01 PID 04
    { "Engine coolant temperature", "05" },         // Mode 01 PID 05
    { "Short term fuel trim (bank 1)", "06" },      // Mode 01 PID 06
    { "Long term fuel trim (bank 1)", "07" },       // Mode 01 PID 07
    { "Short term fuel trim (bank 2)", "08" },      // Mode 01 PID 08
    { "Long term fuel trim (bank 2)", "09" },       // Mode 01 PID 09
    { "Fuel pressure (gauge pressure)", "0A" },     // Mode 01 PID 0A
    { "Intake manifold absolute pressure", "0B" },  // Mode 01 PID 0B
    { "Engine speed", "0C" },                       // Mode 01 PID 0C
    { "Vehicle speed", "0D" },                      // Mode 01 PID 0D
    { "Timing advance", "0E" },                     // Mode 01 PID 0E
    { "Intake air temperature", "0F" },             // Mode 01 PID 0F
    { "Mass air flow sensor air flow rate", "10" }, // Mode 01 PID 10
    { "Throttle position", "11" },                  // Mode 01 PID 11
    { "Commanded secondary air status", "12" },     // Mode 01 PID 12
    { "Oxygen sensors present (2 banks)", "13" },   // Mode 01 PID 13
    { "Oxygen sensor 1 (voltage)", "14" },          // Mode 01 PID 14
    { "Oxygen sensor 2 (voltage)", "15" },          // Mode 01 PID 15
    { "Oxygen sensor 3 (voltage)", "16" },          // Mode 01 PID 16
    { "Oxygen sensor 4 (voltage)", "17" },          // Mode 01 PID 17
    { "Oxygen sensor 5 (voltage)", "18" },          // Mode 01 PID 18
    { "Oxygen sensor 6 (voltage)", "19" },          // Mode 01 PID 19
    { "Oxygen sensor 7 (voltage)", "1A" },          // Mode 01 PID 1A
    { "Oxygen sensor 8 (voltage)", "1B" },          // Mode 01 PID 1B
    { "OBD standards the vehicle conforms to", "1C" }, // Mode 01 PID 1C
    { "Oxygen sensors present (4 banks)", "1D" },   // Mode 01 PID 1D
    { "Auxiliary input status", "1E" },             // Mode 01 PID 1E
    { "Run time since engine start", "1F" },        // Mode 01 PID 1F
    { "PIDs supported [21 - 40]", "20" }            // Mode 01 PID 20
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

    // Dictionary to store checkbox states for each option
    private Dictionary<string, bool> checkboxStates = new Dictionary<string, bool>();

    // This function will be called when the plus button is clicked
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
            BackgroundColor = Colors.DarkBlue,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Start
        };

        // Event for the close button
        closeButton.Clicked += async (s, args) =>
        {
            // Close the modal when clicked
            await Navigation.PopModalAsync();

            // Start sending data for checked boxes
            _ = SendDataForCheckedBoxes();
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
                    new ColumnDefinition { Width = GridLength.Star }, // Label column
                    new ColumnDefinition { Width = GridLength.Auto }  // Checkbox column
                },
                    Padding = new Thickness(0, 5)
                };

                var label = new Label
                {
                    Text = option.Key,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Start
                };

                var checkBox = new CheckBox
                {
                    IsChecked = checkboxStates.ContainsKey(option.Key) && checkboxStates[option.Key], // Restore saved state
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.End
                };

                // Event handler for checkbox state changes
                checkBox.CheckedChanged += (s, args) =>
                {
                    // Save the checkbox state
                    checkboxStates[option.Key] = checkBox.IsChecked;
                };

                // Add label and checkbox to the Grid
                optionGrid.Children.Add(label);
                Grid.SetColumn(label, 0);
                optionGrid.Children.Add(checkBox);
                Grid.SetColumn(checkBox, 1);

                // Add the optionGrid to the scrollable layout
                scrollableLayout.Children.Add(optionGrid);
            }
        }

        // Create a ScrollView and set the content to the scrollable layout
        var scrollView = new ScrollView
        {
            Content = scrollableLayout,
            VerticalOptions = LayoutOptions.FillAndExpand
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

    // Function to send data based on the checked boxes
    private async Task SendDataForCheckedBoxes()
    {
        foreach (var option in checkboxStates)
        {
            if (option.Value) // If the checkbox is checked
            {
                // Get the corresponding hex value from the optionHexMapping dictionary
                string hexValue = optionHexMapping[option.Key]; // Already the correct hex value

                // Send the request
                await SendBluetoothRequest(hexValue);

                // Wait for the corresponding response
                await WaitForBluetoothResponse(hexValue);

            }
        }
    }

    private TaskCompletionSource<string> responseTaskCompletionSource;

    private async Task WaitForBluetoothResponse(string expectedResponseStart)
    {
        // Initialize a new TaskCompletionSource to wait for the response
        responseTaskCompletionSource = new TaskCompletionSource<string>();

        // Wait until the response starts with the expected value
        while (true)
        {
            string receivedResponse = await responseTaskCompletionSource.Task;

            if (receivedResponse.StartsWith(expectedResponseStart))
            {
                // Correct response received, exit the loop
                break;
            }
            else
            {
                // If response does not match, continue waiting (this may also handle retry logic)
                await Task.Delay(100); // Optional delay to avoid tight looping
            }
        }
    }

}