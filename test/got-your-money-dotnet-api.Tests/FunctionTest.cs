using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Newtonsoft.Json;

using Xunit;

namespace got_your_money_dotnet_api.Tests
{
  public class FunctionTest : IDisposable
  {
    string TableName { get; }
    IAmazonDynamoDB DDBClient { get; }

    public FunctionTest()
    {
      this.TableName = "BlueprintBaseName-Expenses-" + DateTime.Now.Ticks;
      this.DDBClient = new AmazonDynamoDBClient(RegionEndpoint.USWest2);

      SetupTableAsync().Wait();
    }

    [Fact]
    public async Task ExpenseTestAsync()
    {
      TestLambdaContext context;
      APIGatewayProxyRequest request;
      APIGatewayProxyResponse response;

      Functions functions = new Functions(this.DDBClient, this.TableName);

      // Add a new expense
      Expense myExpense = new Expense();
      myExpense.Name = "The awesome post";
      myExpense.Cost = 3;
      myExpense.PurchaseDate = DateTime.Parse("06/08/2019");

      request = new APIGatewayProxyRequest
      {
        Body = JsonConvert.SerializeObject(myExpense)
      };
      context = new TestLambdaContext();
      response = await functions.AddExpenseAsync(request, context);
      Assert.Equal(200, response.StatusCode);

      var expenseId = response.Body;

      // Confirm we can get the expense back out
      request = new APIGatewayProxyRequest
      {
        PathParameters = new Dictionary<string, string> { { Functions.ID_QUERY_STRING_NAME, expenseId } }
      };
      context = new TestLambdaContext();
      response = await functions.GetExpenseAsync(request, context);
      Assert.Equal(200, response.StatusCode);

      Expense readExpense = JsonConvert.DeserializeObject<Expense>(response.Body);
      Assert.Equal(myExpense.Name, readExpense.Name);
      Assert.Equal(myExpense.Cost, readExpense.Cost);
      Assert.Equal(myExpense.PurchaseDate, readExpense.PurchaseDate);

      // List the expenses
      request = new APIGatewayProxyRequest
      {
      };
      context = new TestLambdaContext();
      response = await functions.GetExpensesAsync(request, context);
      Assert.Equal(200, response.StatusCode);

      Expense[] expense = JsonConvert.DeserializeObject<Expense[]>(response.Body);
      Assert.Single(expense);
      Assert.Equal(myExpense.Name, expense[0].Name);
      Assert.Equal(myExpense.Cost, expense[0].Cost);
      Assert.Equal(myExpense.PurchaseDate, expense[0].PurchaseDate);

      // Delete the expense
      request = new APIGatewayProxyRequest
      {
        PathParameters = new Dictionary<string, string> { { Functions.ID_QUERY_STRING_NAME, expenseId } }
      };
      context = new TestLambdaContext();
      response = await functions.RemoveExpenseAsync(request, context);
      Assert.Equal(200, response.StatusCode);

      // Make sure the post was deleted.
      request = new APIGatewayProxyRequest
      {
        PathParameters = new Dictionary<string, string> { { Functions.ID_QUERY_STRING_NAME, expenseId } }
      };
      context = new TestLambdaContext();
      response = await functions.GetExpenseAsync(request, context);
      Assert.Equal((int)HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExpensesFilterTestAsync()
    {
      TestLambdaContext context;
      APIGatewayProxyRequest request;
      APIGatewayProxyResponse response;

      Functions functions = new Functions(this.DDBClient, this.TableName);

      // Add a new expense
      Expense myExpense = new Expense();
      myExpense.Name = "Coffee";
      myExpense.Cost = 3;
      myExpense.PurchaseDate = DateTime.Parse("06/08/2019");

      request = new APIGatewayProxyRequest
      {
        Body = JsonConvert.SerializeObject(myExpense)
      };
      context = new TestLambdaContext();
      response = await functions.AddExpenseAsync(request, context);
      Assert.Equal(200, response.StatusCode);

      // Add second expense
      Expense myExpense2 = new Expense();
      myExpense2.Name = "Lunch";
      myExpense2.Cost = 3;
      myExpense2.PurchaseDate = DateTime.Parse("06/14/2019");

      request = new APIGatewayProxyRequest
      {
        Body = JsonConvert.SerializeObject(myExpense2)
      };
      await functions.AddExpenseAsync(request, context);

      // Add third expense
      Expense myExpense3 = new Expense();
      myExpense3.Name = "Lunch";
      myExpense3.Cost = 3;
      myExpense3.PurchaseDate = DateTime.Parse("06/20/2019");

      request = new APIGatewayProxyRequest
      {
        Body = JsonConvert.SerializeObject(myExpense3)
      };
      await functions.AddExpenseAsync(request, context);

      // List the expenses
      request = new APIGatewayProxyRequest
      {
      };
      context = new TestLambdaContext();
      response = await functions.GetExpensesAsync(request, context);
      Assert.Equal(200, response.StatusCode);

      Expense[] expense = JsonConvert.DeserializeObject<Expense[]>(response.Body);
      Assert.Equal(3, expense.Count());

      // List expenses with specified date range
      request = new APIGatewayProxyRequest
      {
        PathParameters = new Dictionary<string, string> {
          { Functions.DATE_FROM_QUERY_STRING_NAME, "06/13/2019" },
          { Functions.DATE_TO_QUERY_STRING_NAME, "06/19/2019"}
        }
      };
      context = new TestLambdaContext();
      response = await functions.GetExpensesAsync(request, context);
      Assert.Equal(200, response.StatusCode);

      expense = JsonConvert.DeserializeObject<Expense[]>(response.Body);
      Assert.Single(expense);
      Assert.Equal(myExpense2.Name, expense[0].Name);
      Assert.Equal(myExpense2.Cost, expense[0].Cost);
      Assert.Equal(myExpense2.PurchaseDate, expense[0].PurchaseDate);
    }


    /// <summary>
    /// Create the DynamoDB table for testing. This table is deleted as part of the object dispose method.
    /// </summary>
    /// <returns></returns>
    private async Task SetupTableAsync()
    {

      CreateTableRequest request = new CreateTableRequest
      {
        TableName = this.TableName,
        ProvisionedThroughput = new ProvisionedThroughput
        {
          ReadCapacityUnits = 2,
          WriteCapacityUnits = 2
        },
        KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        KeyType = KeyType.HASH,
                        AttributeName = Functions.ID_QUERY_STRING_NAME
                    }
                },
        AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = Functions.ID_QUERY_STRING_NAME,
                        AttributeType = ScalarAttributeType.S
                    }
                }
      };

      await this.DDBClient.CreateTableAsync(request);

      var describeRequest = new DescribeTableRequest { TableName = this.TableName };
      DescribeTableResponse response = null;
      do
      {
        Thread.Sleep(1000);
        response = await this.DDBClient.DescribeTableAsync(describeRequest);
      } while (response.Table.TableStatus != TableStatus.ACTIVE);
    }


    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          this.DDBClient.DeleteTableAsync(this.TableName).Wait();
          this.DDBClient.Dispose();
        }

        disposedValue = true;
      }
    }


    public void Dispose()
    {
      Dispose(true);
    }
    #endregion


  }
}
