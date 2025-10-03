using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class Program
{
    private const string Host = "localhost";
    private const string User = "user";
    private const string Pass = "password";

    private const string ControlQueue = "control_queue";

    private static readonly ConcurrentDictionary<string, HashSet<string>> Rules =
        new(StringComparer.OrdinalIgnoreCase);

    public static async Task Main(string[] argv)
    {
      if (argv.Length is not 1)
        throw new InvalidOperationException("missing sort key");

        var factory = new ConnectionFactory
        {
            HostName = Host,
            UserName = User,
            Password = Pass
        };

        using var conn = await factory.CreateConnectionAsync();
        await using var controlCh = await conn.CreateChannelAsync();

        var queue = await controlCh.QueueDeclareAsync(durable: false, exclusive: false, autoDelete: true);
        await RegisterWithRouterAsync(controlCh, queue.QueueName, argv[0]);

        var ctrlConsumer = new AsyncEventingBasicConsumer(controlCh);
        ctrlConsumer.ReceivedAsync += async (_, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            Console.WriteLine("Received: " + message);

            await controlCh.BasicAckAsync(ea.DeliveryTag, false);
        };

        await controlCh.BasicConsumeAsync(queue.QueueName, autoAck: false, ctrlConsumer);
        Console.WriteLine("[control] Control channel ready. Ctrl+C to exit.");

        var done = new TaskCompletionSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.TrySetResult(); };
        await done.Task;
    }

    private static async Task RegisterWithRouterAsync(IChannel controlCh, string queueName, string routeKey)
    {
        await controlCh.QueueDeclareAsync(ControlQueue, durable: true, exclusive: false, autoDelete: false);

        var destination = new
        {
            SortKey = routeKey,
            QueueName = queueName
        };

        var message = JsonSerializer.Serialize(destination);
        var body = Encoding.UTF8.GetBytes(message);

        await controlCh.BasicPublishAsync(string.Empty, routingKey: ControlQueue, body: body);
        Console.WriteLine(" [*] Send connection");
    }
}
