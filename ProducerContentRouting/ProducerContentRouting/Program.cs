
using System.Text;
using RabbitMQ.Client;

var factory = new ConnectionFactory
{
    HostName = "localhost",
    UserName = "guest",
    Password = "guest",
   
};

var conn = await factory.CreateConnectionAsync();
await using var ch = await conn.CreateChannelAsync();

await ch.ExchangeDeclareAsync(exchange: "headersexchange", type: ExchangeType.Headers, durable: false, autoDelete: true);

var message = "This message will be sent with headers";
var body = Encoding.UTF8.GetBytes(message);

var props = new BasicProperties
{
    ContentType = "text/plain",
    Headers = new Dictionary<string, object> {
        { "name", "Erik" },
        { "age",  "28" } 
    }
};

await ch.BasicPublishAsync(exchange: "headersexchange", routingKey: "", mandatory: false, body: body, basicProperties: props);

Console.WriteLine($"Sent message: {message}");
