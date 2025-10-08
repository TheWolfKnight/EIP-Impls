using System.Text;
using System.Text.Json;
using Aggregator.Aggregator.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Aggregator.Aggregator;

public static class Program
{

  public static Dictionary<string, List<(int Nr, bool Final, Item Item)>> Items = [];

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
    await channel.QueueDeclareAsync("aggregated.input", durable: false, exclusive: false, autoDelete: true);

    var listner = new AsyncEventingBasicConsumer(channel);

    listner.ReceivedAsync += async (sender, ea) =>
    {
      Console.WriteLine(" [*] Message Received");
      var sequenceNr = null as object;
      if (ea.BasicProperties.CorrelationId is null || ea.BasicProperties.Headers?.TryGetValue("Sequence-Nr", out sequenceNr) is not true)
      {
        //TODO: Throw into invalid letter queue
        Console.WriteLine(" [WARN] Message does not contain correlation id or Sequence-Nr");
        Console.WriteLine($" [WARN] CorrelationId: \"{ea.BasicProperties.CorrelationId}\"");
        Console.WriteLine($" [WARN] Sequence-Nr: {Encoding.UTF8.GetString((sequenceNr as byte[]) ?? new byte[0])}");
        await channel.BasicAckAsync(ea.DeliveryTag, false);
        return;
      }

      if (!int.TryParse(sequenceNr?.ToString() ?? string.Empty, out var result))
      {
        //TODO: Throw into invalid letter queue
        Console.WriteLine(" [WARN] Message contains sequenceNr but it cannot be converted to nr");
        await channel.BasicAckAsync(ea.DeliveryTag, false);
        return;
      }

      var final = false;
      if (ea.BasicProperties.Headers.TryGetValue("Final", out var check) is true && Encoding.UTF8.GetString((check as byte[])!) is "true")
        final = true;

      if (!Items.ContainsKey(ea.BasicProperties.CorrelationId))
        Items.Add(ea.BasicProperties.CorrelationId, new());

      var message = Encoding.UTF8.GetString(ea.Body.ToArray());
      var obj = JsonSerializer.Deserialize<Item>(message);

      if (obj is null)
      {
        //TODO: Throw into invalid letter queue
        Console.WriteLine(" [WARN] Could not deserialize message into Item object");
        await channel.BasicAckAsync(ea.DeliveryTag, false);
        return;
      }

      Items[ea.BasicProperties.CorrelationId].Add((result, final, obj));

      var items = Items[ea.BasicProperties.CorrelationId];
      if (items.Any(item => item.Final) && items.Count() == items.First(item => item.Final).Nr)
      {
        var order = new Order
        {
          Items = items.Select(item => item.Item).ToList()
        };

        message = JsonSerializer.Serialize(order);
        var body = Encoding.UTF8.GetBytes(message);

        var props = new BasicProperties
        {
          CorrelationId = ea.BasicProperties.CorrelationId
        };

        Console.WriteLine($" [*] Forward correlation_id: {ea.BasicProperties.CorrelationId}");
        await channel.BasicPublishAsync(string.Empty, "aggregated.order", mandatory: false, basicProperties: props, body: body);

        items.Clear();
        Items.Remove(ea.BasicProperties.CorrelationId);
      }

      await channel.BasicAckAsync(ea.DeliveryTag, false);
    };

    await channel.BasicConsumeAsync("aggregated.input", autoAck: false, consumer: listner);

    Console.WriteLine(" [*] Aggregator running (press Ctrl-c to exit)");
    await Task.Delay(-1);
  }
}
