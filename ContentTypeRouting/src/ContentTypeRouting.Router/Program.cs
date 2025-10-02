using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ContentType.Router;

public static class Program
{
  public static async Task Main(string[] argv)
  {
    var factory = new ConnectionFactory
    {
      HostName = "localhost",
      UserName = "user",
      Password = "password",
    };

    var conn = await factory.CreateConnectionAsync();
    await using var ch = await conn.CreateChannelAsync();

    await ch.QueueDeclareAsync("contenttype.router.input", durable: false, exclusive: false, autoDelete: true);

    var listner = new AsyncEventingBasicConsumer(ch);

    listner.ReceivedAsync += async (sender, ea) =>
    {
      var type = ea.BasicProperties.ContentType ?? "Unknown";

      if (type is "Unknown")
      {
        //TODO: Throw into invalid letter queue
        Console.WriteLine(" [WARN] Content-Type is unknown");
        return;
      }

      type = type.Replace('/', '-');

      var queue = await ch.QueueDeclareAsync($"contenttype.router.{type}", durable: false, exclusive: false, autoDelete: true);

      var props = new BasicProperties(ea.BasicProperties);

      await ch.BasicPublishAsync(string.Empty, queue.QueueName, mandatory: false, basicProperties: props, ea.Body);

      await ch.BasicAckAsync(ea.DeliveryTag, multiple: false);
    };

    await ch.BasicConsumeAsync("contenttype.router.input", autoAck: false, consumer: listner);

    Console.WriteLine(" [*] Content-Type router is running, press any key to stop");
    Console.ReadKey();
  }
}
