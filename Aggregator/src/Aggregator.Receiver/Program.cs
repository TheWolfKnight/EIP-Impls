using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Aggregator.Receiver;

public static class Program
{
  public static async Task Main(string[] argv)
  {
    var rabbitLoc = Environment.GetEnvironmentVariable("RABBIT_ADDRESS", EnvironmentVariableTarget.Process) ?? "localhost";
    var connectionFactory = new ConnectionFactory()
    {
      HostName = rabbitLoc,
      UserName = "user",
      Password = "password"
    };

    using var connection = await connectionFactory.CreateConnectionAsync();
    using var channel = await connection.CreateChannelAsync();

    await channel.QueueDeclareAsync("aggregated.order", durable: false, exclusive: false, autoDelete: true);

    var listner = new AsyncEventingBasicConsumer(channel);

    listner.ReceivedAsync += async (sender, ea) =>
    {
      Console.WriteLine(" [*] Message received");
      var message = Encoding.UTF8.GetString(ea.Body.ToArray());
      var obj = JsonSerializer.Deserialize<Order>(message);

      if (obj is null)
      {
        //TODO: Throw into invalid letter queue
        Console.WriteLine(" [WARN] Unable to deserialize message");
        return;
      }

      Console.WriteLine(" [*] Correclation Id: " + ea.BasicProperties.CorrelationId ?? "Unkown");
      Console.WriteLine(" [*] Body: " + message);

      await channel.BasicAckAsync(ea.DeliveryTag, false);
    };

    await channel.BasicConsumeAsync("aggregated.order", autoAck: false, consumer: listner);

    Console.WriteLine(" [*] Receiver running (press Ctrl-c to exit)");
    await Task.Delay(-1);
  }
}

public class Order
{
  public required List<Item> Items { get; set; }
}

public class Item
{
  public required string ItemName { get; set; }
  public required decimal ItemPrice { get; set; }
}
