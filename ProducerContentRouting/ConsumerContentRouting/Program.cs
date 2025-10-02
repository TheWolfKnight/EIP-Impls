using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var factory = new ConnectionFactory
{
    HostName = "localhost",
    UserName = "guest",
    Password = "guest",
};

var conn = await factory.CreateConnectionAsync();
await using var ch = await conn.CreateChannelAsync();


await ch.ExchangeDeclareAsync(exchange: "headersexchange", type: ExchangeType.Headers, durable: false, autoDelete: true);
await ch.QueueDeclareAsync(queue: "letterbox", durable: false, exclusive: false, autoDelete: false);

var bindingArgs = new Dictionary<string, object> {
    { "x-match", "all" }, 
    { "name", "Erik" },
    { "age",  "28" }
};

await ch.QueueBindAsync(queue: "letterbox", exchange: "headersexchange", routingKey: "", arguments: bindingArgs);

var consumer = new AsyncEventingBasicConsumer(ch);
consumer.ReceivedAsync += async (_, ea) =>
{
    var msg = Encoding.UTF8.GetString(ea.Body.ToArray());
    Console.WriteLine($"Received new message: {msg}");
    await ch.BasicAckAsync(ea.DeliveryTag, multiple: false);
};

await ch.BasicConsumeAsync(queue: "letterbox", autoAck: false, consumer: consumer);

Console.WriteLine("Consuming (press Ctrl+C to exit)");
await Task.Delay(-1);
