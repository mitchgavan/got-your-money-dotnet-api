using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace got_your_money_dotnet_api
{
  public class Functions
  {
    // This const is the name of the environment variable that the serverless.template will use to set
    // the name of the DynamoDB table used to store expense.
    const string TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP = "ExpenseTable";

    public const string ID_QUERY_STRING_NAME = "Id";
    public const string DATE_FROM_QUERY_STRING_NAME = "DateFrom";
    public const string DATE_TO_QUERY_STRING_NAME = "DateTo";
    IDynamoDBContext DDBContext { get; set; }

    /// <summary>
    /// Default constructor that Lambda will invoke.
    /// </summary>
    public Functions()
    {
      // Check to see if a table name was passed in through environment variables and if so 
      // add the table mapping.
      var tableName = System.Environment.GetEnvironmentVariable(TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP);
      if (!string.IsNullOrEmpty(tableName))
      {
        AWSConfigsDynamoDB.Context.TypeMappings[typeof(Expense)] = new Amazon.Util.TypeMapping(typeof(Expense), tableName);
      }

      var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
      this.DDBContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);
    }

    /// <summary>
    /// Constructor used for testing passing in a preconfigured DynamoDB client.
    /// </summary>
    /// <param name="ddbClient"></param>
    /// <param name="tableName"></param>
    public Functions(IAmazonDynamoDB ddbClient, string tableName)
    {
      if (!string.IsNullOrEmpty(tableName))
      {
        AWSConfigsDynamoDB.Context.TypeMappings[typeof(Expense)] = new Amazon.Util.TypeMapping(typeof(Expense), tableName);
      }

      var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
      this.DDBContext = new DynamoDBContext(ddbClient, config);
    }

    /// <summary>
    /// A Lambda function that returns back a page worth of expenses.
    /// </summary>
    /// <param name="request"></param>
    /// <returns>The list of expenses</returns>
    public async Task<APIGatewayProxyResponse> GetExpensesAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      context.Logger.LogLine("Getting expenses");

      DateTime? dateFrom = GetDateParam(request, DATE_FROM_QUERY_STRING_NAME);

      var scanConfig = new List<ScanCondition>();

      if (dateFrom != null)
      {
        scanConfig = new List<ScanCondition>()
        {
          new ScanCondition("PurchaseDate", ScanOperator.GreaterThanOrEqual, dateFrom)
        };
      }

      var page = await this.DDBContext
        .ScanAsync<Expense>(scanConfig)
        .GetRemainingAsync();

      context.Logger.LogLine($"Found {page.Count} expenses");

      var response = new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonConvert.SerializeObject(page),
        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
      };

      return response;
    }

    /// <summary>
    /// A Lambda function that returns the expense identified by expenseId
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<APIGatewayProxyResponse> GetExpenseAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      string expenseId = null;
      if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
        expenseId = request.PathParameters[ID_QUERY_STRING_NAME];
      else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
        expenseId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

      if (string.IsNullOrEmpty(expenseId))
      {
        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.BadRequest,
          Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
        };
      }

      context.Logger.LogLine($"Getting expense {expenseId}");
      var expense = await DDBContext.LoadAsync<Expense>(expenseId);
      context.Logger.LogLine($"Found expense: {expense != null}");

      if (expense == null)
      {
        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.NotFound
        };
      }

      var response = new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Body = JsonConvert.SerializeObject(expense),
        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
      };
      return response;
    }

    /// <summary>
    /// A Lambda function that adds a expense.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<APIGatewayProxyResponse> AddExpenseAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      var expense = JsonConvert.DeserializeObject<Expense>(request?.Body);
      expense.Id = Guid.NewGuid().ToString();
      expense.CreatedTimestamp = DateTime.Now;

      context.Logger.LogLine($"Saving expense with id {expense.Id}");
      await DDBContext.SaveAsync<Expense>(expense);

      var response = new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK,
        Body = expense.Id.ToString(),
        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
      };
      return response;
    }

    /// <summary>
    /// A Lambda function that removes an expense from the DynamoDB table.
    /// </summary>
    /// <param name="request"></param>
    public async Task<APIGatewayProxyResponse> RemoveExpenseAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
      string expenseId = null;
      if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
        expenseId = request.PathParameters[ID_QUERY_STRING_NAME];
      else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
        expenseId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

      if (string.IsNullOrEmpty(expenseId))
      {
        return new APIGatewayProxyResponse
        {
          StatusCode = (int)HttpStatusCode.BadRequest,
          Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
        };
      }

      context.Logger.LogLine($"Deleting expense with id {expenseId}");
      await this.DDBContext.DeleteAsync<Expense>(expenseId);

      return new APIGatewayProxyResponse
      {
        StatusCode = (int)HttpStatusCode.OK
      };
    }

    private DateTime? GetDateParam(APIGatewayProxyRequest request, string queryString)
    {
      if (request.PathParameters != null && request.PathParameters.ContainsKey(queryString))
      {
        return DateTime.Parse(request.PathParameters[queryString]);
      }

      if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(queryString))
      {
        return DateTime.Parse(request.QueryStringParameters[queryString]);
      }

      return null;
    }
  }
}
