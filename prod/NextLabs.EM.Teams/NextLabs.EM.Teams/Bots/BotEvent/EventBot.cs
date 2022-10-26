// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.Teams.Bots
{
	extern alias GraphBeta;
	using Beta = GraphBeta.Microsoft.Graph;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Bot.Builder;
	using Microsoft.Bot.Builder.Teams;
	using Microsoft.Bot.Schema;
	using Microsoft.Bot.Schema.Teams;
	using Microsoft.Extensions.Logging;
	using NextLabs.GraphApp;
	using QueryCloudAZSDK;
	using QueryCloudAZSDK.CEModel;
	using NextLabs.Teams.Models;
	using NextLabs.Common;
	using NextLabs.SharePoint;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.Options;
    using NextLabs.Service.HostedService;
	using Microsoft.Rest;
    using Microsoft.Rest.TransientFaultHandling;

    public class EventBot : TeamsActivityHandler
	{
		private readonly ILogger logger;
		private object changeLock = new object();
		private IOptionsSnapshot<BotOptions> botOptions;
		private string appCatalogId;
		private string microsoftAppId;
		private readonly NxlSharePointClient sharepointClient;
		private readonly NxlGraphClient nxlGraphClient;
		private readonly CloudAZQuery cloudazQuery;
		private TeamWrapper teamWrapper;
		private IBackgroundTaskQueue taskQueue;


		private RetryPolicy BotRetryPolicy 
		{
			get 
			{
				ExponentialBackoffRetryStrategy exponentialBackoffRetryStrategy = new ExponentialBackoffRetryStrategy(botOptions.Value.RetryCount, TimeSpan.FromSeconds(botOptions.Value.MinBackoff), TimeSpan.FromSeconds(botOptions.Value.MaxBackoff), TimeSpan.FromSeconds(botOptions.Value.DeltaBackoff));
				RetryPolicy retryPolicy = new RetryPolicy(new BotSdkTransientExceptionDetectionStrategy(), exponentialBackoffRetryStrategy) ?? throw new ArgumentNullException(nameof(retryPolicy));
				return retryPolicy;
			}
		}

		public EventBot(IOptionsSnapshot<BotOptions> botOptions, TeamWrapper teamWrapper, NxlSharePointClient sharepointClient, NxlGraphClient nxlGraphClient, CloudAZQuery cloudazQuery, ILogger<EventBot> logger, IBackgroundTaskQueue taskQueue)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(this.logger));
			this.botOptions = botOptions ?? throw new ArgumentNullException(nameof(this.botOptions));
			this.teamWrapper = teamWrapper ?? throw new ArgumentNullException(nameof(this.teamWrapper));
			this.sharepointClient = sharepointClient ?? throw new ArgumentNullException(nameof(this.sharepointClient));
			this.nxlGraphClient = nxlGraphClient ?? throw new ArgumentNullException(nameof(this.nxlGraphClient));
			this.cloudazQuery = cloudazQuery ?? throw new ArgumentNullException(nameof(this.cloudazQuery));
			this.microsoftAppId = botOptions.Value.MicrosoftAppId ?? throw new ArgumentNullException(nameof(this.microsoftAppId));
			this.appCatalogId = botOptions.Value.AppCatalogId ?? throw new ArgumentNullException(nameof(this.appCatalogId));
			this.taskQueue = taskQueue ?? throw new ArgumentNullException(nameof(this.taskQueue));
		}

		public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
		{
			await base.OnTurnAsync(turnContext, cancellationToken);
		}

		protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
		{
			try
			{
				await BotRetryPolicy.ExecuteAsync(() => turnContext.SendActivityAsync(MessageFactory.Text($"Echo: {turnContext.Activity.Text.Trim()}"), cancellationToken)).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				logger.LogError("OnMessageActivityAsync - SendActivityAsync Error: {e}", e);
			}
			
		}

		protected override async Task OnTeamsMembersAddedAsync(IList<TeamsChannelAccount> teamsMembersAdded, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
            string teamName = teamInfo.Name;
			string teamId = GetTeamId(turnContext, teamInfo);
			logger.LogDebug("OnTeamsMembersAddedAsync: Get team Name: {teamName} team Id: {teamId}", teamName, teamId);

			foreach (var member in teamsMembersAdded)
			{
				try
				{
					//Team Attributes
					TeamAttr teamAttrs = new TeamAttr(teamId, teamName);
					CEAttres ceTeamAttrs = new CEAttres();
					teamAttrs.InjectAttributesTo(ref ceTeamAttrs);
					logger.LogDebug("OnTeamsMembersAddedAsync - Initial Resource - teamName: {teamName}, teamId: {teamId}.", teamName, teamId);

					if(member.Id.EndsWith(microsoftAppId))
					{
						if (TeamCache.TryGet(teamId, out CacheDetail detail) && detail.Enforce == TeamEnforce.Do)
						{
							if (detail.Tags.NotNull() && detail.Tags.Count != 0)
							{
								try
								{
									await BotRetryPolicy.ExecuteAsync(() => turnContext.SendActivityAsync(MessageFactory.Text($"Classify {teamName} to {detail.Tags.ToDisplayString()}"), cancellationToken)).ConfigureAwait(false);
								}
								catch (Exception e) 
								{ 
									logger.LogInformation("OnTeamsMembersAddedAsync - Classify {teamName} to {Tags}.", teamName, detail.Tags.ToDisplayString());
									logger.LogWarning("OnTeamsMembersAddedAsync - SendActivityAsync Error: {e}", e);
								}
							}
						}
						else
							//Handle manually add bot into team
							await HandleManuallyAddBotAsync(teamAttrs, turnContext, cancellationToken);

						taskQueue.QueueBackgroundWorkItem(async token =>
						{
							var guid = Guid.NewGuid().ToString();
							logger.LogDebug("Queued Background Task {Guid} is starting.", guid);
							try
							{
								await teamWrapper.Bind(teamId, teamName, null).ProcessChannelDriveAsync();
							}
							catch (OperationCanceledException)
							{
								// Prevent throwing if cancelled
								logger.LogDebug("QueueBackgroundWorkItem - teamWrapper is canceled by OperationCanceledException");
							}
							logger.LogDebug("Queued Background Task {Guid} is ending. ", guid);
						});

						//Add SharePoint AddIn
						string siteUrl = await nxlGraphClient.GetSharePointSiteAsync(teamId);
						if (!string.IsNullOrEmpty(siteUrl)) sharepointClient.AutoInstallApp(siteUrl, true);
						else logger.LogError("OnTeamsMembersAddedAsync - get SharePoint Site url Error!");

						continue;
					}

					// Subject Attributes
					string userId = member.AadObjectId;
					Beta.User curUser = await nxlGraphClient.GetUserAsync(userId);
					if (curUser.IsNull()) continue;
					string userName = curUser.DisplayName;
					IDictionary<string, string> userAttributes = curUser.ToDictionary<string>();
					CEAttres ceSubjectAttrs = new CEAttres();
					ceSubjectAttrs.InjectAttributesFrom(userAttributes);
					logger.LogDebug("OnTeamsMembersAddedAsync - Initial Subject - User Name: {userName}, User Id: {userId}.", userName, userId);

					//query pc
					CERequest ceTeamReq = cloudazQuery.CreateQueryReq(TeamAction.Team_Join, "", teamName, ceTeamAttrs, userId, userName, ceSubjectAttrs);
					QueryStatus emQueryRes = cloudazQuery.QueryCloudAZPC(ceTeamReq, out List<CEObligation> obligations, out PolicyResult emPolicyResult);
					logger.LogDebug("OnTeamsMembersAddedAsync - {userName} join {teamName} PCResult - QueryStatus: {emQueryRes}; PolicyResult: {emPolicyResult}; Obligations count: {obligations.Count}.",
						userName, teamName, emQueryRes, emPolicyResult, obligations.Count);

					if (emQueryRes != QueryStatus.S_OK) emPolicyResult = cloudazQuery.DefaultPCResult;

					//query result
					if (emPolicyResult == PolicyResult.Deny)
					{
						//wait to find user
						if (await WaitingUserDetected(turnContext, cancellationToken, teamId, userId))
						{
							string textCarrier = $"{userName} has been denied to join";
							//deny user
							if (!await nxlGraphClient.DeleteGroupUserAsync(teamId, userId))
								textCarrier += ", but remove failed, because Delete User API of Microsoft Graph occurs error, please remove manually";
							try
							{
								await BotRetryPolicy.ExecuteAsync(() => turnContext.SendActivityAsync(MessageFactory.Text($"{textCarrier}."), cancellationToken)).ConfigureAwait(false);
							}
							catch (Exception e)
							{
								logger.LogInformation("OnTeamsMembersAddedAsync - {textCarrier}.", textCarrier);
								logger.LogWarning("OnTeamsMembersAddedAsync - SendActivityAsync Error: {e}", e);
							}
						}
					}
					else //Now, we needn't to check any obligation or other conditions, show alert welcome directly. if need modify here
					{
						try
						{
							await BotRetryPolicy.ExecuteAsync(() => turnContext.SendActivityAsync(MessageFactory.Text($"Welcome to the team, {member.GivenName} {member.Surname}."), cancellationToken)).ConfigureAwait(false);
						}
						catch (Exception e)
						{
							logger.LogInformation("OnTeamsMembersAddedAsync - {member.GivenName} {member.Surname} Join into the team.", member.GivenName, member.Surname);
							logger.LogWarning("OnTeamsMembersAddedAsync - SendActivityAsync Error: {e}", e);
						}
					}

					Dictionary<string, List<string>> newTags = new Dictionary<string, List<string>>();
					//process obligations
					foreach (var ob in obligations)
					{
						//obligation: Team_Auto_Classify, only Allow able
						if (emPolicyResult == PolicyResult.Allow) ob.ExtractTeamAutoClassify(ref newTags);

						//obligation: Team_Notify, both Allow and Deny able
						if (ob.ExtractTeamNotify(out string strNotify)) 
						{
							try
							{
								await BotRetryPolicy.ExecuteAsync(() => turnContext.SendActivityAsync(MessageFactory.Text($"Notification: {strNotify}"), cancellationToken)).ConfigureAwait(false);
							}
							catch (Exception e)
							{
								logger.LogInformation("OnTeamsMembersAddedAsync - Notification: {strNotify}", strNotify);
								logger.LogWarning("OnTeamsMembersAddedAsync - SendActivityAsync Error: {e}", e);
							}
						}
					}

					//Add or update database and cache, after process classification obligation, when User added into team
					if (emPolicyResult == PolicyResult.Allow && newTags.Count != 0)
					{
						TeamCache.SetAddOrUpdate(teamId, newTags, teamName, TeamEnforce.Do, out Dictionary<string, List<string>> totalTags);
						try
						{
							await BotRetryPolicy.ExecuteAsync(() => turnContext.SendActivityAsync(MessageFactory.Text($"Notification: Added classification(s): {newTags.ToDisplayString()}, now total classification(s): {totalTags.ToDisplayString()}."), cancellationToken)).ConfigureAwait(false);
						}
						catch (Exception e)
						{
							logger.LogInformation("OnTeamsMembersAddedAsync - Notification: Added classification(s): {newTags}, now total classification(s): {totalTags}.", newTags.ToDisplayString(), totalTags.ToDisplayString());
							logger.LogWarning("OnTeamsMembersAddedAsync - SendActivityAsync Error: {e}", e);
						}
					}

					//process all files for new member
					if (emPolicyResult != PolicyResult.Deny)
					{
						taskQueue.QueueBackgroundWorkItem(async token =>
						{
							var guid = Guid.NewGuid().ToString();

							logger.LogDebug("Queued Background Task {Guid} is starting.", guid);

							try
							{
								await teamWrapper.Bind(teamId, teamName, curUser).ProcessChannelDriveAsync();
							}
							catch (OperationCanceledException)
							{
								// Prevent throwing if cancelled
								logger.LogDebug("QueueBackgroundWorkItem - teamWrapper for {user} is canceled by OperationCanceledException", curUser.DisplayName);
							}

							logger.LogDebug("Queued Background Task {Guid} is ending. ", guid);
						});
					}
				}
				catch (Exception ex)
				{
					logger.LogError("OnTeamsMembersAddedAsync Error: {ex}.", ex);
				}
			}
		}

		protected override async Task OnTeamsMembersRemovedAsync(IList<TeamsChannelAccount> teamsMembersRemoved, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			string teamId = turnContext.Activity.ChannelData.team.aadGroupId;
			string teamName = turnContext.Activity.ChannelData.team.name;
			foreach (var member in teamsMembersRemoved)
			{
				if (member.Id.EndsWith(microsoftAppId))
				{
					logger.LogDebug("Delete bot for {teamName}.", teamName);
					try
					{
						TeamCache.SetAddOrUpdate(teamId, new Dictionary<string, List<string>>(), teamName, TeamEnforce.Dont, true);

						//Remove SharePoint Addin
						string siteUrl = await nxlGraphClient.GetSharePointSiteAsync(teamId);
						if (!string.IsNullOrEmpty(siteUrl)) sharepointClient.AutoUninstallApp(siteUrl);
						else logger.LogError("OnTeamsMembersRemoveddAsync - get SharePoint Site url Error!");
					}
					catch (Exception e)
					{
						logger.LogError("OnTeamsMembersRemovedAsync error: {e}", teamName, e);
					}
				}
			}
		}

		#region useless bot events
		protected override Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			return base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
		}

		protected override async Task OnTeamsMembersAddedDispatchAsync(IList<ChannelAccount> membersAdded, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			await base.OnTeamsMembersAddedDispatchAsync(membersAdded, teamInfo, turnContext, cancellationToken);
		}

		protected override async Task OnTeamsMembersRemovedDispatchAsync(IList<ChannelAccount> membersRemoved, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			await base.OnTeamsMembersRemovedDispatchAsync(membersRemoved, teamInfo, turnContext, cancellationToken);
		}

		protected override async Task OnTeamsChannelCreatedAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			await base.OnTeamsChannelCreatedAsync(channelInfo, teamInfo, turnContext, cancellationToken);
		}

		protected override async Task OnTeamsChannelDeletedAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			await base.OnTeamsChannelDeletedAsync(channelInfo, teamInfo, turnContext, cancellationToken);
		}

		protected override async Task OnTeamsChannelRenamedAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			await base.OnTeamsChannelRenamedAsync(channelInfo, teamInfo, turnContext, cancellationToken);
		}

		protected override async Task OnTeamsTeamRenamedAsync(TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			await base.OnTeamsTeamRenamedAsync(teamInfo, turnContext, cancellationToken);
		}

		protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			await base.OnMembersAddedAsync(membersAdded, turnContext, cancellationToken);
		}

		protected override async Task OnMembersRemovedAsync(IList<ChannelAccount> membersRemoved, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			await base.OnMembersRemovedAsync(membersRemoved, turnContext, cancellationToken);
		}
		#endregion

		private string GetTeamId(ITurnContext turnContext, TeamInfo teamInfo)
		{
			string result = null;
			while(result == null) 
			{
				try
				{
					result = TeamsInfo.GetTeamDetailsAsync(turnContext, teamInfo.Id).GetAwaiter().GetResult().AadGroupId;
				}
				catch (HttpOperationException hoe)
				{
					if (Util.CheckResponseStatusCodeFailed(hoe))
						logger.LogWarning("OnTeamsMembersRemovedAsync Http Failed: {Method}: {Status}", hoe.Request.Method, hoe.Response.StatusCode);
					else 
					{ 
						logger.LogError("OnTeamsMembersRemovedAsync HttpOperationException error: {hoe}", hoe);
						break;
					}
				}
				catch (Exception e)
				{
					logger.LogError("OnTeamsMembersRemovedAsync error: {e}", e);
					break;
				}
			}
			return result;
		}

		private async Task<bool> WaitingUserDetected(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken, string teamId, string userId)
		{
			bool found = false;
			while (!found)
			{
				try
				{
					if (await CheckUserInTeamAsync(turnContext, cancellationToken, teamId, userId))
					{
						found = true;
						break;
					}
					Thread.Sleep(100);
				}
				catch (Exception e)
				{
					logger.LogError("WaitingUserDetected, Error: {e}", e);
				}
			}
			return found;
		}

		private async Task<bool> CheckUserInTeamAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken, string teamId, string userId)
		{
			bool bFound = false;
			var UserCollection = await nxlGraphClient.ListGroupUsersAsync(teamId);
			if (UserCollection != null)
			{
				foreach (var doMember in UserCollection)
				{
					try
					{
						var theUser = doMember as Beta.User;
						if (theUser.Id == userId)
						{
							bFound = true;
							logger.LogDebug("CheckUserInTeamAsync - Found User-{UserName}:{UserId} at team-{teamId}", theUser.DisplayName, theUser.Id, teamId);
							break;
						}
					}
					catch (Exception e)
					{
						logger.LogError(e.ToString());
					}
				}
			}
			else
			{
				logger.LogError("CheckUserInTeamAsync - ListGroupUsersAsync({teamId}) Error!", teamId);
			}
			return bFound;
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

		private async Task HandleManuallyAddBotAsync(TeamAttr teamAttr, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
		{
			string teamId = teamAttr.Id;
			string teamName = teamAttr.Name;
			CEAttres ceTeamAttrs = new CEAttres();
			teamAttr.InjectAttributesTo(ref ceTeamAttrs);

			//get team creater attributes
			List<Beta.DirectoryObject> owners = await this.nxlGraphClient.ListGroupOwnersAsync(teamId);
			logger.LogDebug("HandleManuallyAddBotAsync - Get owners, count = {ownersCount}", owners.Count);
			Beta.User creater = GetFirstUserAdminOfTeam(owners);
			string createrName = creater.DisplayName;
			string createrId = creater.Id;
			IDictionary<string, string> userAttributes = creater.ToDictionary<string>();
			CEAttres ceCreaterAttrs = new CEAttres();
			ceCreaterAttrs.InjectAttributesFrom(userAttributes);
			logger.LogDebug("HandleManuallyAddBotAsync - Creater - Name: {createrName}, Id: {createrId}.", createrName, createrId);

			//query PC
			CERequest ceTeamReq = cloudazQuery.CreateQueryReq(TeamAction.Team_Create, string.Empty, teamName, ceTeamAttrs, createrId, createrName, ceCreaterAttrs);
			QueryStatus emQueryRes = cloudazQuery.QueryCloudAZPC(ceTeamReq, out List<CEObligation> obligations, out PolicyResult emPolicyResult);

			//Handle-added Bot need ignore PC Result 
			if (emQueryRes != QueryStatus.S_OK)
			{
				logger.LogError("HandleManuallyAddBot - Team-{teamName}, QueryStatus: {QueryRes}, so team calssification stops!", teamName, emQueryRes);
				return;
			}
			else
				logger.LogDebug("HandleManuallyAddBot - Team-{teamName}, QueryStatus: {QueryRes}, PolicyResult {PolicyResult}", teamName, emQueryRes, emPolicyResult);

			Dictionary<string, List<string>> newTags = new Dictionary<string, List<string>>();
			foreach (var ob in obligations)
			{
				//obligation: Team_Auto_Classify
				ob.ExtractTeamAutoClassify(ref newTags);
			}

			TeamCache.SetAddOrUpdate(teamId, newTags, teamName, TeamEnforce.Do, out Dictionary<string, List<string>> totalTags);
			if (newTags.Count > 0)
			{
				try
				{
					await BotRetryPolicy.ExecuteAsync(() => turnContext.SendActivityAsync(MessageFactory.Text($"Notification: Added classification(s): {newTags.ToDisplayString()}, now total classification(s): {totalTags.ToDisplayString()}."), cancellationToken)).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					logger.LogInformation("HandleManuallyAddBotAsync - Notification: Added classification(s): {newTags}, now total classification(s): {totalTags}.", newTags.ToDisplayString(), totalTags.ToDisplayString());
					logger.LogWarning("HandleManuallyAddBotAsync - SendActivityAsync Error: {e}", e);
				}
			}
		}
	}
}
