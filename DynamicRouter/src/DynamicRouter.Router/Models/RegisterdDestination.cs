
namespace DynamicRouter.Router.Models;

public class RegisterdDestination
{
  public required Guid DestinationId { get; set; }
  public required string QueueName { get; set; }
  public required DateTime Expiration { get; set; }

  public bool Working { get; set; }
}
