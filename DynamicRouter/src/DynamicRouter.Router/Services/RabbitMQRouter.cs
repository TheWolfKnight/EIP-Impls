using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace DynamicRouter.Router.Services;

public class RabbitMQRouter : IDisposable
{
  private readonly DestinationService _service;
  private IConnection? _conn;

  public RabbitMQRouter(DestinationService service)
  {
    _service = service;
  }

	public void Dispose() {
		_conn?.Dispose();
	}

	public async Task<IChannel> StartRouterAsync(CancellationToken cancellationToken = default)
  {
    var connectionFactory = new ConnectionFactory
    {
      HostName = "localhost",
      UserName = "user",
      Password = "password"
    };

    var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
    _conn = connection;
    var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

    await channel.QueueDeclareAsync("message-in", true, false, false, cancellationToken: cancellationToken);

    var listner = new AsyncEventingBasicConsumer(channel);

    listner.ReceivedAsync += async (sender, ea) =>
    {
      if (ea.BasicProperties.Headers?.TryGetValue("Send-To", out var value) is not true || value is null)
      {
        //TODO: invalid queue
        Console.WriteLine("Could not find the destination");
        return;
      }

        var sendTo = Encoding.UTF8.GetString((value as byte[])!);

      var dests = _service.GetDestinations();

      if (!dests.TryGetValue(sendTo, out var registerdDests))
      {
        // TODO: dead letter
        Console.WriteLine("Could not find destination");
        return;
      }

      var dest = registerdDests.FirstOrDefault(dest => !dest.Working);
      if (dest is null)
      {
        //TODO: check amt times send, send to dead letter if more than 5?
        Console.WriteLine(" [WARN] Letter could not be routed within 5 tries, sending to dead letter");
        return;
      }

      await channel.BasicPublishAsync(string.Empty, routingKey: dest.QueueName, body: ea.Body);
      dest.Working = true;
    };

    await channel.BasicConsumeAsync("message-in", true, consumer: listner, cancellationToken: cancellationToken);
    return channel;
  }
}
