using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;


namespace ConsumerContentRouting;

public static class Program
{
    public static async Task Main(string[] argv)
    {
        if (argv.Length is not 1)
        {
            Console.WriteLine(" [*] Please provide a header type for routing");
            return;
        }

        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = "user",
            Password = "password",
        };

        var conn = await factory.CreateConnectionAsync();
        await using var ch = await conn.CreateChannelAsync();

        var type = argv[0].Replace('/', '-');

        var queue = await ch.QueueDeclareAsync(queue: $"contenttype.router.{type}", durable: false, exclusive: false, autoDelete: true);

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var msg = Encoding.UTF8.GetString(ea.Body.ToArray());
            Console.WriteLine("Content-Type: " + ea.BasicProperties.ContentType ?? "Unkown");
            Console.WriteLine($"Received new message: {msg}");
            await ch.BasicAckAsync(ea.DeliveryTag, multiple: false);
        };

        await ch.BasicConsumeAsync(queue: queue.QueueName, autoAck: false, consumer: consumer);

        Console.WriteLine("Consuming (press Ctrl+C to exit)");
        await Task.Delay(-1);
    }
}
