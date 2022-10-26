// Copyright (c) NextLabs Corporation. All rights reserved.


namespace Microsoft.Extensions.DependencyInjection
{
	using Microsoft.Extensions.Logging;
	using NextLabs.Common;
	using QueryCloudAZSDK.CEModel;
	using NextLabs.GraphApp;
	using QueryCloudAZSDK;
	using System;
	using System.Collections.Generic;
    using NextLabs.SharePoint;
    using NextLabs.Teams;
    using NextLabs.Teams.Models;
    using System.Linq;

    public class NxlServicesInspector
	{
		private ILogger logger;
		private CloudAZQuery cloudAZQuery;
		private NxlGraphClient nxlGraphClient;
		private NxlSharePointClient nxlSharePointClient;
		private NxlDBContext nxlDBContext;

		public NxlServicesInspector(CloudAZQuery cloudAZQuery, NxlGraphClient nxlGraphClient, NxlSharePointClient nxlSharePointClient, NxlDBContext nxlDBContext, ILogger<NxlServicesInspector> logger)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(this.logger));
			this.cloudAZQuery = cloudAZQuery ?? throw new ArgumentNullException(nameof(this.cloudAZQuery));
			this.nxlGraphClient = nxlGraphClient ?? throw new ArgumentNullException(nameof(this.nxlGraphClient));
			this.nxlSharePointClient = nxlSharePointClient ?? throw new ArgumentNullException(nameof(this.nxlSharePointClient));
			this.nxlDBContext = nxlDBContext ?? throw new ArgumentNullException(nameof(this.nxlDBContext));
		}

		public bool CheckAll()
		{
			QueryStatus queryStatus = cloudAZQuery.CheckConnection();
			if (queryStatus != QueryStatus.S_OK)
			{
				queryStatus = cloudAZQuery.CheckConnection();
				if (queryStatus != QueryStatus.S_OK)
				{
					logger.LogError("CloudAz Connection Failed!");
					return false;
				}
			}
			logger.LogInformation("CloudAz Connected.");

			bool bConnected = nxlGraphClient.CheckGraphConnection();
			if (!bConnected)
			{
				bConnected = nxlGraphClient.CheckGraphConnection();
				if (!bConnected)
				{
					logger.LogError("Graph App Connection Failed!");
					return false;
				}
			}
			logger.LogInformation("Graph Client Connected.");

			List<TeamAttr> teamAttrs = nxlDBContext.TeamAttrs.ToList();
			if (teamAttrs == null)
			{
				teamAttrs = nxlDBContext.TeamAttrs.ToList();
				if (teamAttrs == null)
				{
					logger.LogError("Failed connect to Database!");
					return false;
				}
			}
			TeamCache.Init(teamAttrs);
			logger.LogInformation("Database Connected and Cache Initialized.");

			if (!nxlSharePointClient.Check()) 
			{
				if (!nxlSharePointClient.Check())
				{
					logger.LogInformation("SharePoint Connected Failed.");
					return false;
				}
			}
			logger.LogInformation("SharePoint Connected.");

			return true;
		}
	}
}
