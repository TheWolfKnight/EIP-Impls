using System.Text;
using System.Text.Json;
using DynamicRouter.Router.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DynamicRouter.Router.Services;

public class DestinationService: IDisposable
{
  private readonly Dictionary<string, RegisterdDestination> _destinations = [];
  private readonly object _destinationLock = new();
  private IConnection? _conn;
  private AsyncEventingBasicConsumer? _cons;

  public async Task<IChannel> StartDestinationGatheringAsync(CancellationToken cancellationToken=default)
  {
    var connectionFactory = new ConnectionFactory
    {
      HostName = "localhost",
      UserName = "user",
      Password = "password"
    };

    var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
    var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

    await channel.QueueDeclareAsync("control_queue", durable: true, exclusive: false, autoDelete: false);

    var listnerEvent = new AsyncEventingBasicConsumer(channel);

    listnerEvent.ReceivedAsync += async (sender, ea) =>
    {
      Console.WriteLine(" [*] Message received");
      var message = Encoding.UTF8.GetString(ea.Body.ToArray());
      Console.WriteLine(message);
      var destination = JsonSerializer.Deserialize<Destination>(message);

      if (destination is null)
      {
        //TODO: better log later
        Console.WriteLine("Recived message, but could not deserialize destination");
        return;
      }

      lock (_destinationLock)
        _destinations.Add(
          destination.SortKey,
          new RegisterdDestination
          {
            QueueName = destination.QueueName,
            Expiration = DateTime.Now.AddMinutes(30)
          }
        );

      await Task.CompletedTask;
      return;
    };

    await channel.BasicConsumeAsync("control_queue", autoAck: true, consumer: listnerEvent, cancellationToken: cancellationToken);

    _cons = listnerEvent;
    _conn = connection;
    return channel;
  }

  public Dictionary<string, RegisterdDestination> GetDestinations()
  {
    lock (_destinationLock)
      return _destinations;
  }

	public void Dispose() {
        _conn?.Dispose();
	}
}
