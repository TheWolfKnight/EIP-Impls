using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DynamicRouter.Recipient;

public class Program
{
  private const string User = "user";
  private const string Pass = "password";

  private static Guid? _destId { get; set; }

  private const string ControlQueue = "control_queue";

  private static readonly ConcurrentDictionary<string, HashSet<string>> Rules =
      new(StringComparer.OrdinalIgnoreCase);

  public static async Task Main(string[] argv)
  {
    var sortKey = Environment.GetEnvironmentVariable("SORT_KEY", EnvironmentVariableTarget.Process);
    if (sortKey is null && argv.Length is not 1)
      throw new InvalidOperationException("missing sort key");
    else if (sortKey is null)
      sortKey = argv[0];

    var rabbitLoc = Environment.GetEnvironmentVariable("RABBIT_ADDRESS", EnvironmentVariableTarget.Process) ?? "localhost";
    var factory = new ConnectionFactory
    {
      HostName = rabbitLoc,
      UserName = User,
      Password = Pass
    };

    using var conn = await factory.CreateConnectionAsync();
    await using var controlCh = await conn.CreateChannelAsync();

    var queue = await controlCh.QueueDeclareAsync(durable: false, exclusive: false, autoDelete: true);
    await RegisterWithRouterAsync(controlCh, queue.QueueName, sortKey);

    var ctrlConsumer = new AsyncEventingBasicConsumer(controlCh);
    ctrlConsumer.ReceivedAsync += async (_, ea) =>
    {
      var message = Encoding.UTF8.GetString(ea.Body.ToArray());
      Console.WriteLine("Received: " + message);

      await controlCh.BasicAckAsync(ea.DeliveryTag, false);

      Console.WriteLine($" [*] Working for {10 * message.Length} ms");
      Thread.Sleep(10 * message.Length);
      await DeclareUnitReadyAsync(controlCh, queue.QueueName, sortKey);
    };

    await controlCh.BasicConsumeAsync(queue.QueueName, autoAck: false, ctrlConsumer);
    Console.WriteLine("[control] Control channel ready. (press Ctrl-c to exit)");
    await Task.Delay(-1);
  }

  private static async Task RegisterWithRouterAsync(IChannel controlCh, string queueName, string routeKey)
  {
    await controlCh.QueueDeclareAsync(ControlQueue, durable: true, exclusive: false, autoDelete: false);

    _destId = Guid.NewGuid();

    var destination = new
    {
      UnitId = _destId,
      SortKey = routeKey,
      QueueName = queueName,
      RegType = "Reg"
    };

    var message = JsonSerializer.Serialize(destination);
    var body = Encoding.UTF8.GetBytes(message);

    await controlCh.BasicPublishAsync(string.Empty, routingKey: ControlQueue, body: body);
    Console.WriteLine(" [*] Send connection");
  }

  private static async Task DeclareUnitReadyAsync(IChannel controlCh, string queueName, string routeKey)
  {
    await controlCh.QueueDeclareAsync(ControlQueue, durable: true, exclusive: false, autoDelete: false);

    var destination = new
    {
      UnitId = _destId,
      SortKey = routeKey,
      QueueName = queueName,
      RegType = "Ready"
    };

    var message = JsonSerializer.Serialize(destination);
    var body = Encoding.UTF8.GetBytes(message);

    await controlCh.BasicPublishAsync(string.Empty, routingKey: ControlQueue, body: body);
    Console.WriteLine(" [*] Send ready");
  }
}

