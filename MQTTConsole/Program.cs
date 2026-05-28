using MQTTnet;
using MQTTnet.Server;
using System.Text;

Console.Title = "MQTT Console Suite";

await new Application().RunAsync();

public class Application
{
    private readonly CancellationTokenSource _cts = new();

    public async Task RunAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            Console.Clear();

            DrawHeader();

            Console.WriteLine("[1] MQTT Broker");
            Console.WriteLine("[2] MQTT Client Listener");
            Console.WriteLine("[0] Exit");
            Console.WriteLine();

            Console.Write("Select option: ");
            var input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    await RunBrokerAsync();
                    break;
                case "2":
                    await RunClientAsync();
                    break;
                case "3":
                    _cts.Cancel();
                    break;

                default:
                    WriteError("Invalid selection.");
                    Pause();
                    break;
            }
        }
    }

    private async Task RunBrokerAsync()
    {
        Console.Clear();

        DrawHeader("Broker Mode");

        var topic = ReadRequired("Topic Name");
        var port = ReadInt("Broker Port", 1883);

        var options = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(port)
            .Build();

        var mqttFactory = new MqttServerFactory();

        var server = mqttFactory.CreateMqttServer(options);

        server.ClientConnectedAsync += e =>
        {
            WriteInfo($"Client connected: {e.ClientId}");
            return Task.CompletedTask;
        };

        server.ClientDisconnectedAsync += e =>
        {
            WriteWarning($"Client disconnected: {e.ClientId}");
            return Task.CompletedTask;
        };

        await server.StartAsync();

        WriteSuccess($"Broker started on port {port}");
        WriteInfo($"Publishing Topic: {topic}");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  /menu  => Return to menu");
        Console.WriteLine("  /clear => Clear console");
        Console.WriteLine();

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Publish > ");
            Console.ResetColor();

            var message = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(message))
                continue;

            switch (message.ToLower())
            {
                case "/menu":
                    await server.StopAsync();
                    return;

                case "/clear":
                    Console.Clear();
                    DrawHeader("Broker Mode");
                    continue;
            }

            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(message)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await server.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMessage));


            WriteSuccess($"Published => [{topic}] {message}");
        }
    }

    private async Task RunClientAsync()
    {
        Console.Clear();

        DrawHeader("Client Listener Mode");

        var topic = ReadRequired("Topic Name");
        var host = ReadRequired("Broker Host", "localhost");
        var port = ReadInt("Broker Port", 1883);

        var factory = new MqttClientFactory();

        using var client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId($"client-{Guid.NewGuid():N}")
            .Build();

        client.ApplicationMessageReceivedAsync += e =>
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            Console.ForegroundColor = ConsoleColor.Green;

            Console.WriteLine();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Message received");
            Console.WriteLine($"Topic   : {e.ApplicationMessage.Topic}");
            Console.WriteLine($"Payload : {payload}");
            Console.WriteLine();

            Console.ResetColor();

            return Task.CompletedTask;
        };

        client.ConnectedAsync += _ =>
        {
            WriteSuccess("Connected to broker.");
            return Task.CompletedTask;
        };

        client.DisconnectedAsync += _ =>
        {
            WriteWarning("Disconnected from broker.");
            return Task.CompletedTask;
        };

        await client.ConnectAsync(options);

        await client.SubscribeAsync(topic);

        WriteInfo($"Subscribed to topic: {topic}");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  /menu => Return to menu");
        Console.WriteLine();

        while (true)
        {
            var command = Console.ReadLine();

            if (command?.ToLower() == "/menu")
            {
                await client.DisconnectAsync();
                return;
            }
        }
    }

    private static void DrawHeader(string? subtitle = null)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;

        Console.WriteLine("===============================================");
        Console.WriteLine("              MQTT CONSOLE SUITE               ");
        Console.WriteLine("===============================================");

        Console.ResetColor();

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Mode: {subtitle}");
            Console.ResetColor();
            Console.WriteLine();
        }
    }


    private static string ReadRequired(string label, string? defaultValue = null)
    {
        while (true)
        {
            Console.Write($"{label}");

            if (!string.IsNullOrWhiteSpace(defaultValue))
                Console.Write($" ({defaultValue})");

            Console.Write(": ");

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                if (!string.IsNullOrWhiteSpace(defaultValue))
                    return defaultValue;

                WriteError($"{label} is required.");
                continue;
            }

            return input;
        }
    }

    private static int ReadInt(string label, int defaultValue)
    {
        while (true)
        {
            Console.Write($"{label} ({defaultValue}): ");

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                return defaultValue;

            if (int.TryParse(input, out var result))
                return result;

            WriteError("Invalid number.");
        }
    }


    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Press ENTER to continue...");
        Console.ReadLine();
    }

    private static void WriteInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[INFO] {message}");
        Console.ResetColor();
    }

    private static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] {message}");
        Console.ResetColor();
    }

    private static void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARNING] {message}");
        Console.ResetColor();
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }

}
