// Copyright (c) NextLabs Corporation. All rights reserved.


namespace Microsoft.Extensions.DependencyInjection
{
	using Microsoft.AspNetCore.Authentication;
	using Microsoft.Bot.Builder;
	using Microsoft.Bot.Builder.Integration.AspNet.Core;
	using QueryCloudAZSDK.CEModel;
	using NextLabs.GraphApp;
	using NextLabs.Teams.Bots;
	using System;
	using NextLabs.Common;
	using NextLabs.SharePoint;
    using Microsoft.Extensions.Configuration;
    using NextLabs.Service.HostedService;

    /// <summary>
    /// The bot builder extensions class.
    /// </summary>
    public static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Add bot feature.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <returns>The updated service collection.</returns>
		public static IServiceCollection AddEventBot(this IServiceCollection services)
		{
			//// Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.) 
			//services.AddSingleton<IStorage, MemoryStorage>();

			//// Create the Conversation state.
			//services.AddSingleton<ConversationState>();

			//// Create the User state.
			//services.AddSingleton<UserState>();

			// Create the Bot Framework Adapter with error handling enabled.
			services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

			//// Create a global hashset for our ConversationReferences 
			//services.AddSingleton<ConcurrentDictionary<string, ConversationReference>>();

			// Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
			return services.AddTransient<IBot, EventBot>();
		}

		public static IServiceCollection AddEventBot(this IServiceCollection services, IConfiguration configuration)
		{
			services.Configure<BotOptions>(configuration);
			services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
			return services.AddTransient<IBot, EventBot>();
		}

		public static IServiceCollection AddCloudAZQuery(this IServiceCollection services, IConfiguration configuration)
		{
			services.Configure<GeneralSettingOptions>(configuration);
			return services.AddSingleton<CloudAZQuery>();
		}

		public static IServiceCollection AddSharePointClient(this IServiceCollection services, IConfiguration configuration)
		{
			services.Configure<SharePointOptions>(configuration);
			return services.AddSingleton<NxlSharePointClient>();
		}

		public static IServiceCollection AddNxlGraphClient(this IServiceCollection services, IConfiguration configuration)
		{
			services.Configure<AzureAdOptions>(configuration);
			return services.AddSingleton<NxlGraphClient>();
		}

		public static IServiceCollection AddTeamEnforceHostedService(this IServiceCollection services, IConfiguration configuration) 
		{
			services.Configure<TeamEnforceOptions>(configuration);
			services.PostConfigure<TeamEnforceOptions>(options =>
			{
				options.TeamScanInterval *= 1000;
			});
			return services.AddHostedService<TeamEnforceHostedService>();
		}

		public static IServiceCollection AddFilesUserPermissionHostedService(this IServiceCollection services)
		{
			services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
			return services.AddHostedService<FilesUserPermissionHostedService>();
		}

		public static IServiceCollection AddCommonPermissionHostedService(this IServiceCollection services, IConfiguration configuration)
		{
			services.Configure<CommonPermissionOptions>(configuration);
			services.PostConfigure<CommonPermissionOptions>(options =>
			{
				options.CommonFilesScanInterval *= 1000 * 60;
			});
			return services.AddHostedService<CommonPermissionHostedService>();
		}

		public static IServiceCollection AddDataPersistenceHostedService(this IServiceCollection services, IConfiguration configuration)
		{
			services.Configure<DataSyncOptions>(configuration);
			services.PostConfigure<DataSyncOptions>(options =>
			{
				options.DatabaseSyncInterval *= 1000 * 60;
			});
			return services.AddHostedService<DataPersistenceHostedService>();
		}

		public static IServiceCollection AddTeamWrapper(this IServiceCollection services, IConfiguration configuration) 
		{
			services.Configure<TeamWrapperOptions>(configuration);
			return services.AddTransient<TeamWrapper>();
		}
	}
}
