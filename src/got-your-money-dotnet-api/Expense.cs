using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace got_your_money_dotnet_api
{
  public class Expense
  {
    public string Id { get; set; }
    public string Name { get; set; }
    public double Cost { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime CreatedTimestamp { get; set; }
  }
}
