using MQTTnet.Client;
using MQTTnet;

namespace BLE_App
{
    public partial class MQTTServer : ContentPage
    {
        int count = 0;
        private string MsgTx = ""; //message to broker
        string MsgRx = "";
        string CnctMsg = string.Empty;
        string MsgText = string.Empty;

        MqttFactory mqttFactory;
        IMqttClient client;
        MqttClientOptions options;

        public MQTTServer()
        {
            InitializeComponent();
            SetUpMqtt();
        }

        private async void SetUpMqtt()
        {
            mqttFactory = new MqttFactory(); //MQTT factory instance
            client = mqttFactory.CreateMqttClient();
            var options = new MqttClientOptionsBuilder()
                .WithClientId(Guid.NewGuid().ToString())
                .WithTcpServer("broker.emqx.io", 1883) //The name of the mqtt server
                .WithClientId("mqttx_6fa2fabc") //The client id of this project location
                .WithCleanSession()
                .Build();
            client.ConnectedAsync += Client_ConnectedAsync; //function for whne we are connected
            client.DisconnectedAsync += Client_DisconnectedAsync; //function if we get disconnected
            client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;  //fucntion for when we receive a message 
            await client.ConnectAsync(options);
        }


        private Task Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg) //function that handles receiving a message
        {
            MsgRx = arg.ApplicationMessage.ConvertPayloadToString();
            //MainThread.BeginInvokeOnMainThread(MainCode);
            return Task.CompletedTask;
        }

        private Task Client_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            Console.WriteLine("Disconnected from the broker successfully"); //show appropriate messages
            CnctMsg = $"Disconnected from the broker successfully.";
            return Task.CompletedTask;
        }

        private Task Client_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            Console.WriteLine("Connnected to the broker successfully"); //show appropriate messages
            CnctMsg = $"Connected to the broker successfully.";
            return Task.CompletedTask;

        }

        private void Send_clicked(object sender, EventArgs e)
        {
            PublishMessage(MsgTx);
        }

        public void PublishMessage(string msgTx)
        {
            var message = new MqttApplicationMessageBuilder() //new message
                .WithTopic("WATS") //the topic of our broker
                .WithPayload(msgTx) //sending this message
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            if (client.IsConnected)
            {
                client.PublishAsync(message);
                MsgText = $"Message sent: {msgTx}"; //store the message that was sent
                lblConnected.Text = CnctMsg; //show this when connected 
                lblMessage.Text = MsgText; //show the message sent

            }
        }


    }

}
