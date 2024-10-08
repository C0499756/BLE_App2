using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

namespace BLE_App;

public partial class Display : ContentPage
{
	public Display(IDevice _connectedDevice)
	{
		InitializeComponent();
	}
}