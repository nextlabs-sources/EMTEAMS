extern alias GraphBeta;
using Beta = GraphBeta.Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NextLabs.GraphApp;
using NextLabs.Common;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using System.ComponentModel.DataAnnotations;
using NextLabs.Teams;
using NextLabs.Teams.Models;

namespace NextLabs.Service.HostedService
{
	internal class CommonPermissionHostedService : BackgroundService
	{
		private readonly ILogger logger;
		private readonly IServiceScopeFactory scopeFactory;
		private readonly NxlGraphClient nxlGraphClient;
		private IOptionsMonitor<CommonPermissionOptions> commonPermissionOptions;
		[Range(0.001, 10000000)]
		private int commonFilesScanInterval;
		private object changeLock = new object();

		private ConcurrentQueue<KeyValuePair<string, string>> groupIdNameQueue = new ConcurrentQueue<KeyValuePair<string, string>>();

		public CommonPermissionHostedService(IOptionsMonitor<CommonPermissionOptions> commonPermissionOptions, IServiceScopeFactory scopeFactory, ILogger<CommonPermissionHostedService> logger, NxlGraphClient nxlGraphClient)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(this.logger));
			this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(this.scopeFactory));
			this.nxlGraphClient = nxlGraphClient ?? throw new ArgumentNullException(nameof(this.nxlGraphClient));
			this.commonPermissionOptions = commonPermissionOptions ?? throw new ArgumentNullException(nameof(this.commonPermissionOptions));
			this.commonFilesScanInterval = this.commonPermissionOptions.CurrentValue.CommonFilesScanInterval;
			this.commonPermissionOptions.OnChange(options => {
				lock (changeLock)
				{
					this.commonFilesScanInterval = options.CommonFilesScanInterval;
				}
				this.logger.LogInformation("commonPermissionOptions of CommonPermissionHostedService Changed, now CommonFilesScanInterval: {intervals}ms.", this.commonFilesScanInterval);
			});
		}

		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			logger.LogInformation("CommonPermissionHostedService starting at: {time}", DateTimeOffset.Now);

			await base.StartAsync(cancellationToken);
		}


		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			logger.LogInformation("CommonPermissionHostedService stopping at: {time}", DateTimeOffset.Now);

			await base.StopAsync(cancellationToken);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			logger.LogInformation("CommonPermissionHostedService is executing.");
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await MultiProcessFilePermisssionAsync(stoppingToken);
					await Task.Delay(commonFilesScanInterval, stoppingToken);
				}
				catch (Exception e)
				{
					logger.LogError($"CommonPermissionHostedService - ExecuteAsync Error: {e}");
				}
			}
		}

		private async Task MultiProcessFilePermisssionAsync(CancellationToken stoppingToken)
		{
			DateTime start = DateTime.Now;
			logger.LogDebug($"MultiProcessFilePermisssionAsync start at {start}");
			await GetAllTeamsIDNameAsync();
			if (groupIdNameQueue.Count != 0)
			{
				try
				{
					const int MAXTASKNUM = 7;
					Task[] arrTask = new Task[MAXTASKNUM];
					for (int i = 0; i < MAXTASKNUM; ++i)
					{
						arrTask[i] = Task.Factory.StartNew(() =>
						{
							while (!groupIdNameQueue.IsEmpty)
							{
								try
								{
									if (groupIdNameQueue.TryDequeue(out var teamIdName))
									{
										if (TeamCache.TryGet(teamIdName.Key, out CacheDetail detail) && detail.Enforce == TeamEnforce.Do)
										{
											TeamWrapper teamWrapper = scopeFactory.CreateScope().ServiceProvider.GetRequiredService<TeamWrapper>().Bind(teamIdName);
											teamWrapper.ProcessChannelDriveAsync().GetAwaiter().GetResult();
										}
									}
								}
								catch (Exception e)
								{
									logger.LogDebug($"MultiProcessFilePermisssionAsync-{Task.CurrentId} Error: {e}");
								}
							}
						});
					}
					Task.WaitAll(arrTask, stoppingToken);
				}
				catch (OperationCanceledException)
				{
					// Prevent throwing if cancelled
					logger.LogDebug("CommonPermissionHostedService - service is canceled by OperationCanceledException");
				}
				catch (Exception e) 
				{ 
					logger.LogError($"MultiProcessFilePermisssionAsync - Error: {e}");
				}
			}
			DateTime end = DateTime.Now;
			logger.LogDebug($"MultiProcessFilePermisssionAsync end at {end}, cost: {end - start}");
		}

		private async Task GetAllTeamsIDNameAsync()
		{
			var teams = await nxlGraphClient.ListTeamsAsync();
			if(!groupIdNameQueue.IsEmpty)  logger.LogWarning($"GetAllTeamsIDNameAsync - groupIdNameQueue isn't empty, count: {groupIdNameQueue.Count}");
			if (teams != null)
			{
				//Newer teams is more important to process
				teams.Sort((g1, g2) => -g1.CreatedDateTime.GetValueOrDefault().CompareTo(g2.CreatedDateTime.GetValueOrDefault()));
				//teams.Sort(delegate (Beta.Group g1, Beta.Group g2)
				//{
				//	return -g1.CreatedDateTime.GetValueOrDefault().CompareTo(g2.CreatedDateTime.GetValueOrDefault());
				//});
				foreach (Beta.Group group in teams)
				{
					groupIdNameQueue.Enqueue(new KeyValuePair<string, string>(group.Id, group.DisplayName));
				}
			}
		}
	}
}
