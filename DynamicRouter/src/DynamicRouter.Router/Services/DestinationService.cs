using System.Text;
using System.Text.Json;
using DynamicRouter.Router.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DynamicRouter.Router.Services;

public class DestinationService : IDisposable
{
  private readonly Dictionary<string, IList<RegisterdDestination>> _destinations = [];
  private readonly object _destinationLock = new();
  private IConnection? _conn;
  private AsyncEventingBasicConsumer? _cons;

  public async Task<IChannel> StartDestinationGatheringAsync(CancellationToken cancellationToken = default)
  {
    var rabbitLoc = Environment.GetEnvironmentVariable("RABBIT_ADDRESS", EnvironmentVariableTarget.Process) ?? "localhost";
    var connectionFactory = new ConnectionFactory
    {
      HostName = rabbitLoc,
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
      Console.WriteLine("Deseri done, Reg type is: " + destination?.RegType);

      if (destination is null)
      {
        //TODO: better log later
        Console.WriteLine("Recived message, but could not deserialize destination");
        return;
      }

      if (destination.RegType is "Reg")
        RegisterNewDestination(destination);
      else if (destination.RegType is "Ready")
        MarkDestinationReady(destination);

      await Task.CompletedTask;
      return;
    };

    await channel.BasicConsumeAsync("control_queue", autoAck: true, consumer: listnerEvent, cancellationToken: cancellationToken);

    _cons = listnerEvent;
    _conn = connection;
    return channel;
  }

  public Dictionary<string, IList<RegisterdDestination>> GetDestinations()
  {
    lock (_destinationLock)
      return _destinations;
  }

  public void Dispose()
  {
    _conn?.Dispose();
  }

  private void RegisterNewDestination(Destination destination)
  {
    Console.WriteLine(" [*] Register with router");
    lock (_destinationLock)
    {
      if (!_destinations.ContainsKey(destination.SortKey))
        _destinations.Add(destination.SortKey, new List<RegisterdDestination>());

      var registredDest = new RegisterdDestination
      {
        DestinationId = destination.UnitId,
        QueueName = destination.QueueName,
        Expiration = DateTime.Now.AddMinutes(30)
      };


      _destinations[destination.SortKey].Add(registredDest);
    }
  }

  private void MarkDestinationReady(Destination destination)
  {
    Console.WriteLine(" [*] Ready with router");
    lock (_destinationLock)
    {
      if (!_destinations.ContainsKey(destination.SortKey) || !_destinations[destination.SortKey].Any(dest => dest.DestinationId.ToString() == destination.UnitId.ToString()))
      {
        //TODO: Throw into invalid letter queue
        Console.WriteLine(" [WARN] Received ready from unkown destination");
        return;
      }

      var dest = _destinations[destination.SortKey].First(dest => dest.DestinationId == destination.UnitId);
      dest.Working = false;
    }
  }
}
