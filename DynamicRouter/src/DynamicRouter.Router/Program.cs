using DynamicRouter.Router.Services;

namespace DynamicRouter.Router;

public static class Program
{
  public static async Task Main(string[] argv)
  {
    var destinationService = new DestinationService();
    var rabbit = new RabbitMQRouter(destinationService);

    using var _chDest = await destinationService.StartDestinationGatheringAsync();
    using var _chRoute = await rabbit.StartRouterAsync();

    Console.WriteLine(" [*] Router reader. Press any key to exit");
    Console.ReadKey();
  }
}
