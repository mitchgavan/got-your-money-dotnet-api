using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace got_your_money_dotnet_api
{
  public class Expense
  {

    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; }
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }
    [JsonProperty(PropertyName = "cost")]
    public double Cost { get; set; }
    [JsonProperty(PropertyName = "purchaseDate")]
    public DateTime PurchaseDate { get; set; }
    [JsonProperty(PropertyName = "createdTimestamp")]
    public DateTime CreatedTimestamp { get; set; }
  }
}
