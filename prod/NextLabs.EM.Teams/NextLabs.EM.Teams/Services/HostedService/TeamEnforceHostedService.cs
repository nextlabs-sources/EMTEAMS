// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.Service.HostedService
{
	extern alias GraphBeta;
	using Beta = GraphBeta.Microsoft.Graph;
	using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Hosting;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Options;
	using NextLabs.Common;
	using NextLabs.GraphApp;
    using NextLabs.Teams.Models;
	using QueryCloudAZSDK;
	using QueryCloudAZSDK.CEModel;
	using System;
	using System.Collections.Generic;
    using System.Net;
	using System.Threading;
	using System.Threading.Tasks;
    using NextLabs.SharePoint;
	using NextLabs.Teams;

	public class TeamEnforceHostedService : BackgroundService
	{
		private readonly ILogger logger;
		private object changeLock = new object();
		private IOptionsMonitor<TeamEnforceOptions> teamEnforceOptions;
		private string appCatalogId;
		private int teamScanInterval;
		private readonly NxlGraphClient nxlGraphClient;
		private readonly IServiceScopeFactory scopeFactory;
		private readonly CloudAZQuery cloudazQuery;
		private readonly NxlSharePointClient sharepointClient;

		public TeamEnforceHostedService(IOptionsMonitor<TeamEnforceOptions> teamEnforceOptions, IServiceScopeFactory scopeFactory, ILogger<TeamEnforceHostedService> logger, NxlGraphClient nxlGraphClient, CloudAZQuery cloudazQuery, NxlSharePointClient sharepointClient)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(this.logger));
			this.teamEnforceOptions = teamEnforceOptions ?? throw new ArgumentNullException(nameof(this.teamEnforceOptions));
			//Twice Triggered, when changed, ASP.NET Core bug
			//https://github.com/dotnet/aspnetcore/issues/2542
			//https://github.com/aspnet/Logging/issues/874
			this.teamEnforceOptions.OnChange(teamEnforceOptions => {
				lock (changeLock) 
				{
					this.appCatalogId = teamEnforceOptions.AppCatalogId ?? throw new ArgumentNullException(nameof(this.appCatalogId));
					this.teamScanInterval = teamEnforceOptions.TeamScanInterval;
				}
				this.logger.LogInformation("TeamEnforceOptions of TeamEnforceHostedService Changed, now TeamScanInterval: {teamScanInterval}ms", this.teamScanInterval);
			});
			this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(this.scopeFactory));
			this.nxlGraphClient = nxlGraphClient ?? throw new ArgumentNullException(nameof(this.nxlGraphClient));
			this.cloudazQuery = cloudazQuery ?? throw new ArgumentNullException(nameof(this.cloudazQuery));
			this.sharepointClient = sharepointClient ?? throw new ArgumentNullException(nameof(this.sharepointClient));
			this.appCatalogId = this.teamEnforceOptions.CurrentValue.AppCatalogId ?? throw new ArgumentNullException(nameof(this.appCatalogId));
			this.teamScanInterval = this.teamEnforceOptions.CurrentValue.TeamScanInterval;
		}

		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			logger.LogInformation("TeamEnforceHostedService starting at: {time}", DateTimeOffset.Now);

			await base.StartAsync(cancellationToken);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			logger.LogInformation("TeamEnforceHostedService stopping at: {time}", DateTimeOffset.Now);

			await base.StopAsync(cancellationToken);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			logger.LogInformation("Team Enforce Hosted Service is executing.");
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await ProcessTeamCreatingAsync();
					await Task.Delay(teamScanInterval, stoppingToken);
				}
				catch (OperationCanceledException)
				{
					// Prevent throwing if cancelled
					logger.LogDebug("TeamEnforceHostedService - Service is canceled by OperationCanceledException");
				}
				catch (Exception e)
				{
					logger.LogError("TeamEnforceHostedService - ExecuteAsync Error: {e}", e);
				}
			}
		}

		//Sometime, like create team under api by Jmeter, the first owner is ServicePrincipal type not User type
		private Beta.User GetFirstUserAdminOfTeam(List<Beta.DirectoryObject> owners) 
		{
			Beta.User creater = null;
			foreach (var user in owners) 
			{
				if (user != null && user is Beta.User)
				{
					creater = nxlGraphClient.GetUserAsync(user.Id).GetAwaiter().GetResult();
					logger.LogDebug("GetFirstUserAdminOfTeam - Creater: {0}", creater.DisplayName);
					break;
				}
			}
			return creater;
		}

		private async Task ProcessTeamCreatingAsync()
		{
			var teams = await this.nxlGraphClient.ListTeamsAsync();
			if (teams != null)
			{
				foreach (Beta.Group group in teams)
				{
					try
					{
						string teamId = group.Id;
						if (TeamCache.ContainKey(teamId)) continue;

						//get team attributes
						string teamName = group.DisplayName;
						TeamAttr teamAttrs = new TeamAttr(teamId, teamName, false);
						if (teamAttrs.IsNull()) continue;
						CEAttres ceTeamAttrs = new CEAttres();
						teamAttrs.InjectAttributesTo(ref ceTeamAttrs);
						logger.LogDebug("ProcessTeamCreatingAsync - team-{teamName}:{teamId} attributes has builded!", teamName, teamAttrs.Id);

						//get team creater attributes
						List<Beta.DirectoryObject> owners = await this.nxlGraphClient.ListGroupOwnersAsync(teamId);
						logger.LogDebug("ProcessTeamCreatingAsync - Get owners, count = {ownersCount}", owners.Count);
						Beta.User creater = GetFirstUserAdminOfTeam(owners);
						string createrName = creater.DisplayName;
						string createrId = creater.Id;
						IDictionary<string, string> userAttributes = creater.ToDictionary<string>();
						CEAttres ceCreaterAttrs = new CEAttres();
						ceCreaterAttrs.InjectAttributesFrom(userAttributes);
						logger.LogDebug("ProcessTeamCreatingAsync - Creater - Name: {createrName}, Id: {createrId}.", createrName, createrId);

						//query PC
						CERequest ceTeamReq = cloudazQuery.CreateQueryReq(TeamAction.Team_Create, string.Empty, teamName, ceTeamAttrs, createrId, createrName, ceCreaterAttrs);
						QueryStatus emQueryRes = cloudazQuery.QueryCloudAZPC(ceTeamReq, out List<CEObligation> obligations, out PolicyResult emPolicyResult);
						logger.LogDebug("ProcessTeamCreatingAsync - Process Team-{teamName} Creating, QueryStatus: {QueryRes}, PolicyResult {PolicyResult}", teamName, emQueryRes, emPolicyResult);

						if (emQueryRes != QueryStatus.S_OK) emPolicyResult = cloudazQuery.DefaultPCResult;
						Dictionary<string, List<string>> newTags = new Dictionary<string, List<string>>();
						//process policy desicion
						if (emPolicyResult == PolicyResult.Allow)
						{
							//get auto classification tags from pc
							foreach (var ob in obligations)
							{
								//obligation: Team_Auto_Classify, only Allow able
								ob.ExtractTeamAutoClassify(ref newTags);
							}

                            //Join bot into team and add/update cache
                            try
                            {
                                HttpStatusCode addedStatus = this.nxlGraphClient.AddAppToTeamAsync(teamId, appCatalogId).GetAwaiter().GetResult();
                                //by Graph API, if app bot does not exist in team any more, it should be deleted and return HttpStatusCode.Conflict ServiceException
                                if (addedStatus == HttpStatusCode.OK || addedStatus == HttpStatusCode.Conflict)
                                {
									logger.LogDebug("ProcessTeamCreatingAsync - {done} team-{teamName}.", (addedStatus == HttpStatusCode.OK ? "Add bot into" : "Bot has exsited in"), teamName);
									TeamCache.SetAddOrUpdate(teamId, newTags, teamName, TeamEnforce.Do, out Dictionary<string, List<string>> totalTags);
									logger.LogDebug("ProcessTeamCreatingAsync - team-{teamName}, added/updated classification(s): {new}, now total classification(s): {total}.", teamName, newTags.ToDisplayString(), totalTags.ToDisplayString());
                                }
                                else 
									logger.LogError("ProcessTeamCreatingAsync - Added Bot Exception: status code: {addedStatus}", addedStatus);
                            }
                            catch (Exception e)
                            {
                                logger.LogError("ProcessTeamCreatingAsync - Join bot into team-{teamName} failed, {e}", teamName, e);
                            }
                        }
                        else //if team donot need enforcer, cache team info for Dont
						{
							TeamCache.SetAddOrUpdate(teamId, newTags, teamName, TeamEnforce.Dont);
							logger.LogInformation("ProcessTeamCreatingAsync - As NextLabs Policy strategy, {teamName}:{teamId} isn't required to enforce.", teamAttrs.Name, teamId);
						}
					}
					catch (Exception e)
					{
						logger.LogError("ProcessTeamCreatingAsync Error: {e}", e);
					}
				}
			}
		}
	}
}
