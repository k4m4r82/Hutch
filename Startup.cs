using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RawRabbit.Attributes;
using RawRabbit.Common;
using RawRabbit.Extensions.Client;
using RawRabbit.vNext;
using RawRabbit.vNext.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;
using Hutch.Controllers;
using System.Threading.Tasks;

namespace Hutch
{
	public class Startup
	{
		private readonly string _rootPath;

		public Startup(IHostingEnvironment env)
		{
			_rootPath = env.ContentRootPath;
			var builder = new ConfigurationBuilder()
				.SetBasePath(_rootPath)
				.AddJsonFile("appsettings.json")
				.AddEnvironmentVariables();
			Configuration = builder.Build();
		}

		public IConfigurationRoot Configuration { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			services
				.AddRawRabbit(
					Configuration.GetSection("RawRabbit"),
					ioc => ioc
						.AddSingleton(LoggingFactory.ApplicationLogger))
						.AddSingleton<IConfigurationEvaluator, AttributeConfigEvaluator>()
				.AddMvc();
		}

		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			loggerFactory
				.AddSerilog(GetConfiguredSerilogger())
				.AddConsole(Configuration.GetSection("Logging"));

            var busClient = BusClientFactory.CreateDefault();
            busClient.SubscribeAsync<EmailMessage>((message, context) =>
            {
                var logger = loggerFactory.CreateLogger<Startup>();

                logger.LogInformation($"Sending Email to: {message.To}, message: {message.Body}");

                /* do stuff */
                return Task.FromResult(true);
            }, c => {
                c.WithExchange(e => {
                    e.WithName("email");
                    e.WithAutoDelete(false);
                })
                .WithRoutingKey("email")
                .WithNoAck(false)
                .WithQueue(q => {
                    q.WithName("email_sender");
                    q.WithAutoDelete(false);
                    q.WithDurability(true);
                    q.WithExclusivity(true);
                });
            });

			app.UseMvc();
		}

		private ILogger GetConfiguredSerilogger()
		{
			return new LoggerConfiguration()
				.WriteTo.File($"{_rootPath}/Logs/serilog.log", LogEventLevel.Debug)
				.CreateLogger();
		}
	}
}
