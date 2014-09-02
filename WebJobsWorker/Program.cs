using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using SendGrid;

namespace WebJobsWorker
{
	// To learn more about Microsoft Azure WebJobs, please see http://go.microsoft.com/fwlink/?LinkID=401557
	class Program
	{
		private static readonly Random Random = new Random();
		private static readonly TimeSpan Period = new TimeSpan(0, 1, 0, 0);
		private const int Delay = 1000 * 60 * 5;

		static void Main()
		{
			var lastSend = DateTime.MinValue;

			var connectionStirng = ConfigurationManager.ConnectionStrings["StorageConnectionString"].ConnectionString;
			var storageAccount = CloudStorageAccount.Parse(connectionStirng);
			var table = CreateTable(storageAccount);
			while (true)
			{
				try
				{

					WriteRate(table);
					if (ShouldSendMail(lastSend, Period))
					{
						lastSend = DateTime.Now;
						var items = GetItemsFromLastHour(table);
						SendMail(items);
					}

					var wait = Task.Delay(Delay);
					wait.Wait();
				}
				catch (Exception)
				{

				}
			}
		}

		private static IList<ExchangeRate> GetItemsFromLastHour(CloudTable table)
		{
			var query = new TableQuery<ExchangeRate>()
				.Take(12);
			return table.ExecuteQuery(query).ToList();
		}

		private static bool ShouldSendMail(DateTime lastSend, TimeSpan period)
		{
			return (DateTime.Now - lastSend) > period;
		}

		private static void SendMail(IList<ExchangeRate> rates)
		{
			var username = ConfigurationManager.AppSettings["emailUserName"];
			var pswd = ConfigurationManager.AppSettings["emailPassword"];
			var message = CreateMessage(rates);
			var credentials = new NetworkCredential(username, pswd);
			var transportWeb = new Web(credentials);
			transportWeb.DeliverAsync(message);
		}

		private static SendGridMessage CreateMessage(IList<ExchangeRate> rates)
		{
			var myMessage = new SendGridMessage { From = new MailAddress("noreply@example.com") };

			var recipients = new List<String>
			{
				@"Piotr Kościelniak <piotrek.koscielniak@gmail.com>",
				@"Marcin <dismed14@gmail.com>"
			};

			myMessage.AddTo(recipients);

			myMessage.Subject = "Testing web jobs";

			myMessage.Html = GetHtmlContent(rates);
			myMessage.Text = GetPlainText(rates);
			return myMessage;
		}

		private const string Header = "Kurs z ostatniej godziny";

		private static string GetPlainText(IEnumerable<ExchangeRate> rates)
		{
			var builder = new StringBuilder();
			builder.Append(Header);

			foreach (var rate in rates)
			{
				builder.Append(string.Format("{0}: {1}", rate.Timestamp, rate.Rate));
			}
			return builder.ToString();
		}
		private static string GetHtmlContent(IEnumerable<ExchangeRate> rates)
		{
			var builder = new StringBuilder();
			builder.Append("<h3>" + Header + "</h3>");

			foreach (var rate in rates)
			{
				builder.Append(string.Format("<p>{0}: {1}</p>", rate.Timestamp, rate.Rate));
			}
			return builder.ToString();
		}

		private static void WriteRate(CloudTable table)
		{
			var rate = GetRate();
			var insertOperation = TableOperation.Insert(rate);
			table.Execute(insertOperation);
		}

		private static ExchangeRate GetRate()
		{
			return new ExchangeRate
			{
				PartitionKey = "BitCoin",
				RowKey = GetRowKey(),
				Rate = GetValue()
			};
		}

		private static string GetRowKey()
		{
			return string.Format("{0:d19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks);
		}

		private static double GetValue()
		{
			return Random.NextDouble() * 1000;
		}


		private static CloudTable CreateTable(CloudStorageAccount storageAccount)
		{
			var tableClient = storageAccount.CreateCloudTableClient();
			var table = tableClient.GetTableReference("currencyRates");
			table.CreateIfNotExists();
			return table;
		}
	}


	public class ExchangeRate : TableEntity
	{
		public double Rate { get; set; }
	}
}
