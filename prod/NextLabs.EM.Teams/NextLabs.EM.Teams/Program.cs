// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NexLabs.Teams
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NextLabs.Logging;
    using System.Threading;

    public class Program
    {
        public static void Main(string[] args)
        {
            //CreateHostBuilder(args).Build().Run();
            var host = CreateHostBuilder(args).Build();

			using (var scope = host.Services.CreateScope())
			{
				var services = scope.ServiceProvider;

				var loggerFactory = services.GetRequiredService<ILoggerFactory>();
				loggerFactory.AddLog4Net();
				var logger = loggerFactory.CreateLogger<Program>();
                logger.LogInformation("Logger configed, start to check NextLabs Services...");

                var nxlServicesinitializer = services.GetRequiredService<NxlServicesInspector>();
                if (!nxlServicesinitializer.CheckAll())
                {
                    logger.LogInformation("Check NextLabs Services failed, closing...");
                    Thread.Sleep(1000);
                    return;
                }
				logger.LogInformation("NextLabs Services all passed, host starting...");
			}

			host.Run();
		}

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureLogging((logging) =>
                    {
                        logging.AddDebug();
                        logging.AddConsole();
                    });
                    webBuilder.UseStartup<Startup>();
                });
    }
}
