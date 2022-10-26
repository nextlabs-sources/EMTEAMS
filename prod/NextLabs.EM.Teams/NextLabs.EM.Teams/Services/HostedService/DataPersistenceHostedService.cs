// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.Service.HostedService
{
	extern alias GraphBeta;
	using Microsoft.Extensions.Hosting;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Options;
	using NextLabs.Common;
    using NextLabs.Teams.Models;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
    using NextLabs.Teams;
	using System.Linq;
    using Microsoft.Extensions.DependencyInjection;

    public class DataPersistenceHostedService : BackgroundService
	{
		private readonly ILogger logger;
		private readonly object changeLock = new object();
		private IOptionsMonitor<DataSyncOptions> dataSyncOptions;
		private int syncInterval;
		private readonly IServiceScopeFactory scopeFactory;
		
		public DataPersistenceHostedService(IOptionsMonitor<DataSyncOptions> dataSyncOptions, ILogger<DataPersistenceHostedService> logger, IServiceScopeFactory scopeFactory)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(this.logger));
			this.dataSyncOptions = dataSyncOptions ?? throw new ArgumentNullException(nameof(this.dataSyncOptions));
			this.dataSyncOptions.OnChange(options => {
				lock (changeLock) 
				{
					this.syncInterval = options.DatabaseSyncInterval;
				}
				this.logger.LogInformation("DataSyncOptions of DataPersistenceHostedService Changed, now DatabaseSyncInterval: {DatabaseSyncInterval}m", this.syncInterval);
			});
			this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(this.scopeFactory));
			this.syncInterval = this.dataSyncOptions.CurrentValue.DatabaseSyncInterval;
		}

		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			logger.LogInformation("TeamEnforceHostedService starting at: {time}", DateTimeOffset.Now);

			await base.StartAsync(cancellationToken);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			logger.LogInformation("TeamEnforceHostedService stopping at: {time}", DateTimeOffset.Now);

			//start sync before shutdown
			await PersistCacheAsync();
			logger.LogInformation("TeamEnforceHostedService persisted cache to database at: {time}", DateTimeOffset.Now);

			await base.StopAsync(cancellationToken);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			logger.LogInformation("DataPersistenceHostedService is executing.");
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await PersistCacheAsync();
					await Task.Delay(this.syncInterval, stoppingToken);
				}
				catch (OperationCanceledException)
				{
					// Prevent throwing if cancelled
					logger.LogDebug("DataPersistenceHostedService - service is canceled by OperationCanceledException");
				}
				catch (Exception e)
				{
					logger.LogError("DataPersistenceHostedService - ExecuteAsync Error: {e}", e);
				}
			}
		}

		private async Task PersistCacheAsync() 
		{
			logger.LogInformation("DataPersistenceHostedService - Persist cache starting at: {time}", DateTimeOffset.Now);

			TeamCache.CopyAllAndSetSYNC(out Dictionary<string, CacheDetail> WaitToPersistCaches);

			//DELETE
			var NeedDeletedCache = WaitToPersistCaches.Where(item => item.Value.Status == CacheStatus.DELETED).ToList();
			List<TeamAttr> DeletedRecords = new List<TeamAttr>();
			foreach (var item in NeedDeletedCache)
			{
				DeletedRecords.Add(new TeamAttr(item.Key));
			}
			logger.LogDebug("DataPersistenceHostedService - Deleted Records Count: {n}", DeletedRecords.Count);

			//ADD
			var NeedAddedCache = WaitToPersistCaches.Where(item => item.Value.Status == CacheStatus.ADDED).ToList();
			List<TeamAttr> AddedRecords = new List<TeamAttr>();
			foreach (var item in NeedAddedCache)
			{
				AddedRecords.Add(new TeamAttr(item.Key, item.Value.Name, item.Value.Tags, item.Value.Enforce));
			}
			logger.LogDebug("DataPersistenceHostedService - Added Records Count: {n}", AddedRecords.Count);

			//UPDATE
			var NeedUpdatedCache = WaitToPersistCaches.Where(item => item.Value.Status == CacheStatus.UPDATED).ToList();
			List<TeamAttr> UpdatedRecords = new List<TeamAttr>();
			foreach (var item in NeedUpdatedCache)
			{
				UpdatedRecords.Add(new TeamAttr(item.Key, item.Value.Name, item.Value.Tags, item.Value.Enforce));
			}
			logger.LogDebug("DataPersistenceHostedService - Updated Records Count: {n}", UpdatedRecords.Count);

			using (var dbContext = scopeFactory.CreateScope().ServiceProvider.GetRequiredService<NxlDBContext>())
			{
				if (DeletedRecords.Count != 0) dbContext.TeamAttrs.RemoveRange(DeletedRecords);
				if (UpdatedRecords.Count != 0) dbContext.TeamAttrs.UpdateRange(UpdatedRecords);
				if (AddedRecords.Count != 0) dbContext.TeamAttrs.AddRange(AddedRecords);
				if (AddedRecords.Count != 0 || UpdatedRecords.Count != 0 || DeletedRecords.Count != 0) await dbContext.SaveChangesAsync();
			}

			logger.LogInformation("DataPersistenceHostedService - Persist cache ending at: {time}", DateTimeOffset.Now);
		}
	}
}
