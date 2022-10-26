// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.Service.HostedService
{
	using Microsoft.Extensions.Hosting;
	using Microsoft.Extensions.Logging;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	public class FilesUserPermissionHostedService : BackgroundService
	{
		private readonly ILogger logger;
		public IBackgroundTaskQueue TaskQueue { get; }

		public FilesUserPermissionHostedService(IBackgroundTaskQueue taskQueue, ILogger<TeamEnforceHostedService> logger)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(this.logger));
			TaskQueue = taskQueue ?? throw new ArgumentNullException(nameof(this.TaskQueue));
		}

		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			logger.LogInformation("FilesUserPermissionHostedService starting at: {time}", DateTimeOffset.Now);

			await base.StartAsync(cancellationToken);
		}


		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			logger.LogInformation("FilesUserPermissionHostedService stopping at: {time}", DateTimeOffset.Now);

			await base.StopAsync(cancellationToken);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			logger.LogInformation("Files User Permission Hosted Service is executing.");
			await BackgroundProcessing(stoppingToken);
		}

		private async Task BackgroundProcessing(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				//var workItem = null;
				try
				{
					var workItem = await TaskQueue.DequeueAsync(stoppingToken);
					await workItem(stoppingToken);
				}
				catch (OperationCanceledException)
				{
					// Prevent throwing if cancelled
					logger.LogDebug("BackgroundProcessing - Service is canceled by OperationCanceledException");
				}
				catch (Exception ex)
				{
					logger.LogError("BackgroundProcessing - Error occurred: {ex}", ex);
				}
			}
		}
	}
}
