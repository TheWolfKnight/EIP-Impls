
namespace DynamicRouter.Router.Models;

public class Destination
{
  public required Guid UnitId { get; set; }
  public required string SortKey { get; set; }
  public required string QueueName { get; set; }
  public string RegType {get; set; } = "Reg";
}
