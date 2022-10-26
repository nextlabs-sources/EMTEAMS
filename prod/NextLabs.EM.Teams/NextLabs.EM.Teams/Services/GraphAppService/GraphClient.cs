extern alias GraphBeta;
using Beta = GraphBeta.Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using System;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Options;
using NextLabs.Common;

namespace NextLabs.GraphApp
{
	public class NxlGraphClient
	{
		private readonly ILogger logger;
		private IOptionsMonitor<AzureAdOptions> options;
		private string tenant;
		private string appId;
		private string appKey;
		private string adUserAttributes;
		private object changeLock = new object();
		private Beta.GraphServiceClient graphClient;
		private const int threshold = 1;
		private const int decay = 3;
		private const int timeoutWaitingTime = 200;//ms

		public NxlGraphClient(IOptionsMonitor<AzureAdOptions> options, ILoggerFactory loggerFactory)
		{
			logger = loggerFactory.CreateLogger<NxlGraphClient>() ?? throw new ArgumentNullException(nameof(loggerFactory));
			this.options = options ?? throw new ArgumentNullException(nameof(this.options));
			this.options.OnChange(options => {
				lock (changeLock)
				{
					this.tenant = options.TenantId;
					this.appId = options.AppId;
					this.appKey = options.AppSecret;
					this.adUserAttributes = options.ADUserAttributes.ToLower();
					this.graphClient = GetAppGraphServiceClient(this.tenant, this.appId, this.appKey) ?? throw new ArgumentNullException(nameof(graphClient));
				}
				this.logger.LogInformation("AzureAdOptions of NxlGraphClient Changed, Connection: {bConnected}", CheckGraphConnection());
			});
			this.tenant = options.CurrentValue.TenantId;
			this.appId = options.CurrentValue.AppId;
			this.appKey = options.CurrentValue.AppSecret;
			this.adUserAttributes = options.CurrentValue.ADUserAttributes.ToLower();
			this.graphClient = GetAppGraphServiceClient(this.tenant, this.appId, this.appKey) ?? throw new ArgumentNullException(nameof(graphClient));
		}

		public bool CheckGraphConnection()
		{
			List<Beta.Group> teams = ListTeamsAsync().GetAwaiter().GetResult();
			return (teams != null && teams.Count > 0) ? true : false;
		}

		private Beta.GraphServiceClient GetAppGraphServiceClient(string strTenantID, string strClientId, string strlientSecret)
		{
			// Build a client application.
			IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
					.Create(strClientId)
					.WithTenantId(strTenantID)
					.WithClientSecret(strlientSecret)
					.Build();

			// Create an authentication provider by passing in a client application and graph scopes.
			ClientCredentialProvider authProvider
					= new ClientCredentialProvider(confidentialClientApplication) ?? throw new ArgumentNullException(nameof(confidentialClientApplication));
			// Create a new instance of GraphServiceClient with the authentication provider.
			Beta.GraphServiceClient graphClient
					= new Beta.GraphServiceClient(authProvider) ?? throw new ArgumentNullException(nameof(authProvider));

			return graphClient;
		}

		// List groups
		// https://docs.microsoft.com/zh-cn/graph/api/group-list?view=graph-rest-beta&tabs=http
		// Application: Group.Read.All, Group.ReadWrite.All, Directory.Read.All, Directory.ReadWrite.All(from least to most privileged)
		public async Task<List<Beta.Group>> ListGroupsAsync()
		{
			List<Beta.Group> groupList = new List<Beta.Group>();
			Beta.IGraphServiceGroupsCollectionPage groups = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					groups = await graphClient.Groups.Request().GetAsync();
					groupList.AddRange(groups);
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("ListGroupsAsync() - Resouce NotFound.");
						break;
					}
					else
					{
						logger.LogError("ListGroupsAsync() - Failed but continue. Exception: {se}", se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("ListGroupsAsync() - Failed but continue. Exception: {e}", e);
				}
				interval /= decay;
			}

			while (groups != null && groups.NextPageRequest != null)
			{
				interval = 1_000;
				while (interval > threshold)
				{
					try
					{
						groups = groups.NextPageRequest.GetAsync().Result;
						groupList.AddRange(groups);
						break;
					}
					catch (Microsoft.Graph.ServiceException se)
					{
						if (TimeoutExceptionFilter(se))
						{
							Thread.Sleep(timeoutWaitingTime);
						}
						else if (NotFoundExceptionFilter(se))
						{
							logger.LogError("ListGroupsAsync().NextPageRequest - Resouce NotFound.");
							break;
						}
						else
						{
							logger.LogError("ListGroupsAsync().NextPageRequest - Failed but continue. Exception: {se}", se);
						}
					}
					catch (Exception e)
					{
						logger.LogError("ListGroupsAsync().NextPageRequest - Failed but continue. Exception: {e}", e);
					}
					interval /= decay;
				}
			}

			return groupList;
		}
		public async Task<List<Beta.Group>> ListGroupsAsync(string filter)
		{
			List<Beta.Group> groupList = new List<Beta.Group>();
			Beta.IGraphServiceGroupsCollectionPage groups = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					groups = await graphClient.Groups.Request().Filter(filter).GetAsync();
					groupList.AddRange(groups);
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("ListGroupsAsync({filter}) - Resouce NotFound.", filter);
						break;
					}
					else
					{
						logger.LogError("ListGroupsAsync({filter}) - Failed but continue. Exception: {se}", filter, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("ListGroupsAsync({filter}) - Failed but continue. Exception: {e}", filter, e);
				}
				interval /= decay;
			}

			while (groups != null && groups.NextPageRequest != null) 
			{
				interval = 1_000;
				while (interval > threshold)
				{
					try
					{
						groups = groups.NextPageRequest.GetAsync().Result;
						groupList.AddRange(groups);
						break;
					}
					catch (Microsoft.Graph.ServiceException se)
					{
						if (TimeoutExceptionFilter(se))
						{
							Thread.Sleep(timeoutWaitingTime);
						}
						else if (NotFoundExceptionFilter(se))
						{
							logger.LogError("ListGroupsAsync({filter}).NextPageRequest - Resouce NotFound.", filter);
							break;
						}
						else
						{
							logger.LogError("ListGroupsAsync({filter}).NextPageRequest - Failed but continue. Exception: {se}", filter, se);
						}
					}
					catch (Exception e)
					{
						logger.LogError("ListGroupsAsync({filter}).NextPageRequest - Failed but continue. Exception: {e}", filter, e);
					}
					interval /= decay;
				}
			}

			return groupList;
		}
		public async Task<List<Beta.Group>> ListGroupsByOrderAsync(string filter, string orderBy)
		{
			List<Beta.Group> groupList = new List<Beta.Group>();
			Beta.IGraphServiceGroupsCollectionPage groups = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					groups = await graphClient.Groups.Request().OrderBy(orderBy).Filter(filter).GetAsync();
					groupList.AddRange(groups);
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("ListGroupsAsync({filter}) - Resouce NotFound.", filter);
						break;
					}
					else
					{
						logger.LogError("ListGroupsAsync({filter}) - Failed but continue. Exception: {se}", filter, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("ListGroupsAsync({filter}) - Failed but continue. Exception: {e}", filter, e);
				}
				interval /= decay;
			}

			while (groups != null && groups.NextPageRequest != null)
			{
				interval = 1_000;
				while (interval > threshold)
				{
					try
					{
						groups = groups.NextPageRequest.GetAsync().Result;
						groupList.AddRange(groups);
						break;
					}
					catch (Microsoft.Graph.ServiceException se)
					{
						if (TimeoutExceptionFilter(se))
						{
							Thread.Sleep(timeoutWaitingTime);
						}
						else if (NotFoundExceptionFilter(se))
						{
							logger.LogError("ListGroupsAsync({filter}).NextPageRequest - Resouce NotFound.", filter);
							break;
						}
						else
						{
							logger.LogError("ListGroupsAsync({filter}).NextPageRequest - Failed but continue. Exception: {se}", filter, se);
						}
					}
					catch (Exception e)
					{
						logger.LogError("ListGroupsAsync({filter}).NextPageRequest - Failed but continue. Exception: {e}", filter, e);
					}
					interval /= decay;
				}
			}

			return groupList;
		}
		public async Task<List<Beta.Group>> ListTeamsAsync()
		{
			return await ListGroupsAsync(@"resourceProvisioningOptions/Any(x:x eq 'Team')");
		}
		public async Task<List<Beta.Group>> ListTeamsByOrderAsync(string orderBy)
		{
			return await ListGroupsByOrderAsync(@"resourceProvisioningOptions/Any(x:x eq 'Team')", orderBy);
		}

		// List channels
		// https://docs.microsoft.com/zh-cn/graph/api/channel-list?view=graph-rest-beta&tabs=http
		// Application: Group.Read.All, Group.ReadWrite.All(from least to most privileged)
		public async Task<List<Beta.Channel>> ListChannelsAsync(string teamId)
		{
			List<Beta.Channel> channelList = new List<Beta.Channel>();
			Beta.ITeamChannelsCollectionPage channels = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					channels = await graphClient.Teams[teamId].Channels.Request().GetAsync();
					channelList.AddRange(channels.CurrentPage);
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("ListChannelsAsync({teamId}}) - Resouce NotFound.", teamId);
						break;
					}
					else
					{
						logger.LogError("ListChannelsAsync({teamId}}) - Failed but continue. Exception: {se}", teamId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("ListChannelsAsync({teamId}}) - Failed but continue. Exception: {se}", teamId, e);
				}
				interval /= decay;
			}

			while (channels != null && channels.NextPageRequest != null)
			{
				interval = 1_000;
				while (interval > threshold)
				{
					try
					{
						channels = channels.NextPageRequest.GetAsync().Result;
						channelList.AddRange(channels);
						break;
					}
					catch (Microsoft.Graph.ServiceException se)
					{
						if (TimeoutExceptionFilter(se))
						{
							Thread.Sleep(timeoutWaitingTime);
						}
						else if (NotFoundExceptionFilter(se))
						{
							logger.LogError("ListChannelsAsync({teamId}}).NextPageRequest - Resouce NotFound.", teamId);
							break;
						}
						else
						{
							logger.LogError("ListChannelsAsync({teamId}}).NextPageRequest - Failed but continue. Exception: {se}", teamId, se);
						}
					}
					catch (Exception e)
					{
						logger.LogError("ListChannelsAsync({teamId}}).NextPageRequest - Failed but continue. Exception: {se}", teamId, e);
					}
					interval /= decay;
				}
			}

			return channelList;
		}

		// Get a channel
		// https://docs.microsoft.com/zh-cn/graph/api/channel-get?view=graph-rest-beta&tabs=http
		// Application: Group.Read.All, Group.ReadWrite.All(from least to most privileged)
		public async Task<Beta.Channel> GetChannelAsync(string teamId, string channelId)
		{
			Beta.Channel channel = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					channel = await graphClient.Teams[teamId].Channels[channelId].Request().GetAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetChannelAsync({teamId}, {channelId}) - Resouce NotFound.", teamId, channelId);
						break;
					}
					else
					{
						logger.LogError("GetChannelAsync({teamId}, {channelId}) - Failed but continue. Exception: {se}", teamId, channelId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetChannelAsync({teamId}, {channelId}) - Failed but continue. Exception: {e}", teamId, channelId, e);
				}
				interval /= decay;
			}
			return channel;
		}

		// Get a group
		// https://docs.microsoft.com/zh-cn/graph/api/group-get?view=graph-rest-beta&tabs=csharp
		// Application: Group.Read.All、Directory.Read.All、Group.ReadWrite.All、Directory.ReadWrite.All(from least to most privileged)
		public async Task<Beta.Group> GetGroupAsync(string groupId)
		{
			Beta.Group group = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					group = await graphClient.Groups[groupId].Request().GetAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetGroupAsync({groupId}) - Resouce NotFound.", groupId);
						break;
					}
					else
					{
						logger.LogError("GetGroupAsync({groupId}) - Failed but continue. Exception: {se}", groupId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetGroupAsync({groupId}) - Failed but continue. Exception: {e}", groupId, e);
				}
				interval /= decay;
			}
			return group;
		}

		// List users
		// https://docs.microsoft.com/zh-cn/graph/api/user-list?view=graph-rest-beta&tabs=http
		// Application: User.Read.All, User.ReadWrite.All, Directory.Read.All, Directory.ReadWrite.All(from least to most privileged)
		public async Task<List<Beta.User>> ListUsersAsync()
		{
			List<Beta.User> userList = new List<Beta.User>();
			Beta.IGraphServiceUsersCollectionPage users = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					users = await graphClient.Users.Request().GetAsync();
					userList.AddRange(users);
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("ListUsersAsync() - Resouce NotFound.");
						break;
					}
					else
					{
						logger.LogError("ListUsersAsync() - Failed but continue. Exception: {se}", se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("ListUsersAsync() - Failed but continue. Exception: {e}", e);
				}
				interval /= decay;
			}

			while (users != null && users.NextPageRequest != null)
			{
				interval = 1_000;
				while (interval > threshold)
				{
					try
					{
						users = users.NextPageRequest.GetAsync().Result;
						userList.AddRange(users);
						break;
					}
					catch (Microsoft.Graph.ServiceException se)
					{
						if (TimeoutExceptionFilter(se))
						{
							Thread.Sleep(timeoutWaitingTime);
						}
						else if (NotFoundExceptionFilter(se))
						{
							logger.LogError("ListUsersAsync().NextPageRequest - Resouce NotFound.");
							break;
						}
						else
						{
							logger.LogError("ListUsersAsync().NextPageRequest - Failed but continue. Exception: {se}", se);
						}
					}
					catch (Exception e)
					{
						logger.LogError("ListGroupsAsync() - Failed but continue. Exception: {e}", e);
					}
					interval /= decay;
				}
			}

			return userList;
		}

		// Get a user
		// https://docs.microsoft.com/zh-cn/graph/api/user-get?view=graph-rest-beta&tabs=http
		// Application: User.Read.All, User.ReadWrite.All, Directory.Read.All, Directory.ReadWrite.All(from least to most privileged)
		public async Task<Beta.User> GetUserAsync(string userIdOrPrincipalName)
		{
			Beta.User user = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					if (adUserAttributes.Equals("default"))
						user = await graphClient.Users[userIdOrPrincipalName].Request().GetAsync();
					else
						user = await graphClient.Users[userIdOrPrincipalName].Request().Select(adUserAttributes).GetAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetUserAsync({userId}) - Resouce NotFound.", userIdOrPrincipalName);
						break;
					}
					else
					{
						logger.LogError("GetUserAsync({userId}) - Failed but continue. Exception: {se}", userIdOrPrincipalName, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetUserAsync({userId}) - Failed but continue. Exception: {e}", userIdOrPrincipalName, e);
				}
				interval /= decay;
			}
			return user;
		}

		// List group members
		// https://docs.microsoft.com/zh-cn/graph/api/group-list-members?view=graph-rest-beta&tabs=csharp
		// Application: Group.Read.All, Directory.Read.All(from least to most privileged)
		public async Task<List<Beta.DirectoryObject>> ListGroupUsersAsync(string groupId)
		{
			List<Beta.DirectoryObject> DOList = new List<Beta.DirectoryObject>();
			Beta.IGroupMembersCollectionWithReferencesPage members = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					members = await graphClient.Groups[groupId].Members.Request().Select("id").GetAsync();
					DOList.AddRange(members);
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("ListGroupUsersAsync({groupId}) - Resouce NotFound.", groupId);
						break;
					}
					else
					{
						logger.LogError("ListGroupUsersAsync({groupId}) - Failed but continue. Exception: {e}", groupId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("ListGroupUsersAsync({groupId}) - Failed but continue. Exception: {e}", groupId, e);
				}
				interval /= decay;
			}

			while (members != null && members.NextPageRequest != null)
			{
				interval = 1_000;
				while (interval > threshold)
				{
					try
					{
						members = members.NextPageRequest.GetAsync().Result;
						DOList.AddRange(members);
						break;
					}
					catch (Microsoft.Graph.ServiceException se)
					{
						if (TimeoutExceptionFilter(se))
						{
							Thread.Sleep(timeoutWaitingTime);
						}
						else if (NotFoundExceptionFilter(se))
						{
							logger.LogError("ListGroupUsersAsync({groupId}).NextPageRequest - Resouce NotFound.", groupId);
							break;
						}
						else
						{
							logger.LogError("ListGroupUsersAsync({groupId}).NextPageRequest - Failed but continue. Exception: {e}", groupId, se);
						}
					}
					catch (Exception e)
					{
						logger.LogError("ListGroupUsersAsync({groupId}).NextPageRequest - Failed but continue. Exception: {e}", groupId, e);
					}
					interval /= decay;
				}
			}

			return DOList;
		}

		// Delete group member
		// https://docs.microsoft.com/zh-cn/graph/api/group-delete-members?view=graph-rest-beta&tabs=csharp
		// Application: GroupMember.ReadWrite.All, Group.ReadWrite.All, Directory.ReadWrite.All(from least to most privileged)
		public async Task<bool> DeleteGroupUserAsync(string groupId, string userId)
		{
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					await graphClient.Groups[groupId].Members[userId].Reference.Request().DeleteAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("DeleteGroupUsersAsync({groupId}, {userId}) - Resouce NotFound.", groupId, userId);
						break;
					}
					else
					{
						logger.LogError("DeleteGroupUsersAsync({groupId}, {userId}) - Failed but continue. Exception: {e}", groupId, userId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("DeleteGroupUsersAsync({groupId}, {userId}) - Failed but continue. Exception: {e}", groupId, userId, e);
				}
				interval /= decay;
			}
			return interval > threshold;
		}

		// List group owners
		// https://docs.microsoft.com/zh-cn/graph/api/group-list-owners?view=graph-rest-beta&tabs=http
		// Application: Group.Read.All and User.Read.All, Group.Read.All and User.ReadWrite.All(from least to most privileged)
		public async Task<List<Beta.DirectoryObject>> ListGroupOwnersAsync(string groupId)
		{
			List<Beta.DirectoryObject> DOList = new List<Beta.DirectoryObject>();
			Beta.IGroupOwnersCollectionWithReferencesPage owners = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					owners = await graphClient.Groups[groupId].Owners.Request().Select("id").GetAsync();
					DOList.AddRange(owners);
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("ListGroupOwnersAsync({groupId}) - Resouce NotFound.", groupId);
						break;
					}
					else
					{
						logger.LogError("ListGroupOwnersAsync({groupId}) - Failed but continue. Exception: {se}", groupId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("ListGroupOwnersAsync({groupId}) - Failed but continue. Exception: {e}", groupId, e);
				}
				interval /= decay;
			}

			while (owners != null && owners.NextPageRequest != null)
			{
				interval = 1_000;
				while (interval > threshold)
				{
					try
					{
						owners = owners.NextPageRequest.GetAsync().Result;
						DOList.AddRange(owners);
						break;
					}
					catch (Microsoft.Graph.ServiceException se)
					{
						if (TimeoutExceptionFilter(se))
						{
							Thread.Sleep(timeoutWaitingTime);
						}
						else if (NotFoundExceptionFilter(se))
						{
							logger.LogError("ListGroupOwnersAsync({groupId}).NextPageRequest - Resouce NotFound.", groupId);
							break;
						}
						else
						{
							logger.LogError("ListGroupOwnersAsync({groupId}).NextPageRequest - Failed but continue. Exception: {se}", groupId, se);
						}
					}
					catch (Exception e)
					{
						logger.LogError("ListGroupsAsync() - Failed but continue. Exception: {e}", e);
					}
					interval /= decay;
				}
			}

			return DOList;
		}

		// Get Drive
		// https://docs.microsoft.com/zh-cn/graph/api/drive-get?view=graph-rest-beta&tabs=http
		// Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public async Task<List<Beta.DriveItem>> GetDefaultDriveItemsAsync(string groupId)
		{
			List<Beta.DriveItem> itemList = new List<Beta.DriveItem>();
			Beta.IDriveItemChildrenCollectionPage items = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					items = await graphClient.Groups[groupId].Drive.Root.Children.Request().GetAsync();
					itemList.AddRange(items);
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetDefaultDriveItemsAsync({groupId}) - Resouce NotFound.", groupId);
						break;
					}
					else
					{
						logger.LogError("GetDefaultDriveItemsAsync({groupId}) - Failed but continue. Exception: {se}", groupId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetDefaultDriveItemsAsync({groupId}) - Failed but continue. Exception: {e}", groupId, e);
				}
				interval /= decay;
			}

			while (items != null && items.NextPageRequest != null)
			{
				interval = 1_000;
				while (interval > threshold)
				{
					try
					{
						items = items.NextPageRequest.GetAsync().Result;
						itemList.AddRange(items);
						break;
					}
					catch (Microsoft.Graph.ServiceException se)
					{
						if (TimeoutExceptionFilter(se))
						{
							Thread.Sleep(timeoutWaitingTime);
						}
						else if (NotFoundExceptionFilter(se))
						{
							logger.LogError("GetDefaultDriveItemsAsync({groupId}).NextPageRequest - Resouce NotFound.", groupId);
							break;
						}
						else
						{
							logger.LogError("GetDefaultDriveItemsAsync({groupId}).NextPageRequest - Failed but continue. Exception: {se}", groupId, se);
						}
					}
					catch (Exception e)
					{
						logger.LogError("ListGroupsAsync() - Failed but continue. Exception: {e}", e);
					}
					interval /= decay;
				}
			}

			return itemList;
		}

		//Get Drive
		//https://docs.microsoft.com/en-us/graph/api/drive-get?view=graph-rest-1.0&tabs=http
		//Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All
		public async Task<Beta.Drive> GetGroupDefaultDriveAsync(string groupId)
		{
			Beta.Drive drv = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					drv = await graphClient.Groups[groupId].Drive.Request().GetAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetChannelFilesFolderAsync({groupId}) - Resouce NotFound.", groupId);
						break;
					}
					else
					{
						logger.LogError("GetChannelFilesFolderAsync({groupId}) - Failed but continue. Exception: {se}", groupId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetChannelFilesFolderAsync({groupId}) - Failed but continue. Exception: {e}", groupId, e);
				}
				interval /= decay;
			}
			return drv;
		}

		// Get a DriveItem resource(Channel FilesFolder)
		// https://docs.microsoft.com/zh-cn/graph/api/driveitem-get?view=graph-rest-beta&tabs=http
		//Application: Files.Read.All, Files.ReadWrite.All, Group.Read.All, Group.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public async Task<Beta.DriveItem> GetChannelFilesFolderAsync(string teamId, string channelId)
		{
			Beta.DriveItem item = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					item = await graphClient.Teams[teamId].Channels[channelId].FilesFolder.Request().GetAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetChannelFilesFolderAsync({teamId}, {channelId}) - Resouce NotFound.", teamId, channelId);
						break;
					}
					else
					{
						logger.LogError("GetChannelFilesFolderAsync({teamId}, {channelId}) - Failed but continue. Exception: {se}", teamId, channelId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetChannelFilesFolderAsync({teamId}, {channelId}) - Failed but continue. Exception: {e}", teamId, channelId, e);
				}
				interval /= decay;
			}
			return item;
		}

		public async Task<List<Beta.ConversationMember>> GetChannelMembersAsync(string teamId, string channelId)
		{
			List<Beta.ConversationMember> memberList = new List<Beta.ConversationMember>();
			Beta.IChannelMembersCollectionPage members = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					members = await graphClient.Teams[teamId].Channels[channelId].Members.Request().GetAsync();
					memberList.AddRange(members);
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetChannelMembersAsync({teamId}, {channelId}) - Resouce NotFound.", teamId, channelId);
						break;
					}
					else
					{
						logger.LogError("GetChannelMembersAsync({teamId}, {channelId}) - Failed but continue. Exception: {se}", teamId, channelId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetChannelMembersAsync({teamId}, {channelId}) - Failed but continue. Exception: {e}", teamId, channelId, e);
				}
				interval /= decay;
			}

			while (members != null && members.NextPageRequest != null)
			{
				interval = 1_000;
				while (interval > threshold)
				{
					try
					{
						members = members.NextPageRequest.GetAsync().Result;
						memberList.AddRange(members);
						break;
					}
					catch (Microsoft.Graph.ServiceException se)
					{
						if (TimeoutExceptionFilter(se))
						{
							Thread.Sleep(timeoutWaitingTime);
						}
						else if (NotFoundExceptionFilter(se))
						{
							logger.LogError("GetChannelMembersAsync({teamId}, {channelId}).NextPageRequest - Resouce NotFound.", teamId, channelId);
							break;
						}
						else
						{
							logger.LogError("GetChannelMembersAsync({teamId}, {channelId}).NextPageRequest - Failed but continue. Exception: {se}", teamId, channelId, se);
						}
					}
					catch (Exception e)
					{
						logger.LogError("ListGroupsAsync() - Failed but continue. Exception: {e}", e);
					}
					interval /= decay;
				}
			}

			return memberList;
		}
		public async Task<Beta.ConversationMember> GetChannelMemberAsync(string teamId, string channelId, string id)
		{
			Beta.ConversationMember member = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					member = await graphClient.Teams[teamId].Channels[channelId].Members[id].Request().GetAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetChannelMemberAsync({teamId}, {channelId}) - Resouce NotFound.", teamId, channelId);
						break;
					}
					else
					{
						logger.LogError("GetChannelMemberAsync({teamId}, {channelId}) - Failed but continue. Exception: {se}", teamId, channelId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetChannelMemberAsync({teamId}, {channelId}) - Failed but continue. Exception: {e}", teamId, channelId, e);
				}
				interval /= decay;
			}
			return member;
		}

		// List children of a driveItem
		// https://docs.microsoft.com/en-us/graph/api/driveitem-list-children?view=graph-rest-beta&tabs=csharp
		// Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public async Task<List<Beta.DriveItem>> GetDriveItemChildrensPageAsync(string groupId, string itemId)
		{
			List<Beta.DriveItem> itemList = new List<Beta.DriveItem>();
			Beta.IDriveItemChildrenCollectionPage driveItems = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					driveItems = await graphClient.Groups[groupId].Drive.Items[itemId].Children.Request().GetAsync();
					itemList.AddRange(driveItems);
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetDriveItemChildrensPageAsync({groupId}, {itemId}) - Resouce NotFound.", groupId, itemId);
						break;
					}
					else
					{
						logger.LogError("GetDriveItemChildrensPageAsync({groupId}, {itemId}) - Failed but continue. Exception: {se}", groupId, itemId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetDriveItemChildrensPageAsync({groupId}, {itemId}) - Failed but continue. Exception: {e}", groupId, itemId, e);
				}
				interval /= decay;
			}

			while (driveItems != null && driveItems.NextPageRequest != null)
			{
				interval = 1_000;
				while (interval > threshold)
				{
					try
					{
						driveItems = driveItems.NextPageRequest.GetAsync().Result;
						itemList.AddRange(driveItems);
						break;
					}
					catch (Microsoft.Graph.ServiceException se)
					{
						if (TimeoutExceptionFilter(se))
						{
							Thread.Sleep(timeoutWaitingTime);
						}
						else if (NotFoundExceptionFilter(se))
						{
							logger.LogError("GetDriveItemChildrensPageAsync({groupId}, {itemId}).NextPageRequest - Resouce NotFound.", groupId, itemId);
							break;
						}
						else
						{
							logger.LogError("GetDriveItemChildrensPageAsync({groupId}, {itemId}).NextPageRequest - Failed but continue. Exception: {se}", groupId, itemId, se);
						}
					}
					catch (Exception e)
					{
						logger.LogError("GetDriveItemChildrensPageAsync({groupId}, {itemId}).NextPageRequest - Failed but continue. Exception: {e}", groupId, itemId, e);
					}
					interval /= decay;
				}
			}

			return itemList;
		}

		// Get a DriveItem resource
		// https://docs.microsoft.com/zh-cn/graph/api/driveitem-get?view=graph-rest-beta&tabs=http
		// Application: Files.Read.All, Files.ReadWrite.All, Group.Read.All, Group.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public async Task<Beta.DriveItem> GetItemAsync(string groupId, string itemId)
		{
			Beta.DriveItem item = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					item = await graphClient.Groups[groupId].Drive.Items[itemId].Request().GetAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetItemAsync({groupId}, {itemId}) - Resouce NotFound.");
						break;
					}
					else
					{
						logger.LogError("GetItemAsync({groupId}, {itemId}) - Failed but continue. Exception: {se}", groupId, itemId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetItemAsync({groupId}, {itemId}) - Failed but continue. Exception: {e}", groupId, itemId, e);
				}
				interval /= decay;
			}
			return item;
		}
		
				public async Task<Beta.DriveItem> GetItemByRelativePathAsync(string groupId, string relativePath)
		{
			Beta.DriveItem item = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					item = await graphClient.Groups[groupId].Drive.Root.ItemWithPath(relativePath).Request().GetAsync();
					break;
				}
				catch (Exception e)
				{
					logger.LogError($"GetItemAsync({groupId}, {relativePath}) - Failed but continue. Exception: {e}");
				}
				interval /= decay;
			}
			return item;
		}

		// Download the contents of a DriveItem
		// https://docs.microsoft.com/zh-cn/graph/api/driveitem-get-content?view=graph-rest-beta&tabs=http
		// Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public async Task<Stream> GetItemStreamAsync(string groupId, string itemId)
		{
			Stream strm = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					strm = await graphClient.Groups[groupId].Drive.Items[itemId].Content.Request().GetAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetItemStreamAsync({groupId}, {itemId}) - Resouce NotFound.");
						break;
					}
					else
					{
						logger.LogError("GetItemStreamAsync({groupId}, {itemId}) - Failed but continue. Exception: {se}", groupId, itemId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetItemStreamAsync({groupId}, {itemId}) - Failed but continue. Exception: {e}", groupId, itemId, e);
				}
				interval /= decay;
			}
			return strm;
		}
		
				public async Task<Stream> GetItemStreamByRelativePathAsync(string groupId, string relativePath)
		{
			Stream strm = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					strm = await graphClient.Groups[groupId].Drive.Root.ItemWithPath(relativePath).Content.Request().GetAsync();
					break;
				}
				catch (Exception e)
				{
					logger.LogError($"GetItemStreamAsync({groupId}, {relativePath}) - Failed but continue. Exception: {e}");
				}
				interval /= decay;
			}
			return strm;
		}

		//Delete a DriveItem
		//https://docs.microsoft.com/en-us/graph/api/driveitem-delete?view=graph-rest-1.0&tabs=http
		//Application:	Files.ReadWrite.All, Sites.ReadWrite.All
		public async Task<bool> DeleteItemAsync(string groupId, string itemId)
		{
			bool result = false;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					await graphClient.Groups[groupId].Drive.Items[itemId].Request().DeleteAsync();
					result = true;
					break;
				}
				catch (Exception e)
				{
					logger.LogError($"GetItemAsync({groupId}, {itemId}) - Failed but continue. Exception: {e}");
				}
				interval /= decay;
			}
			return result;
		}

		// List sharing permissions on a DriveItem
		// https://docs.microsoft.com/zh-cn/graph/api/driveitem-list-permissions?view=graph-rest-beta&tabs=csharp
		// Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public async Task<List<Beta.Permission>> ListSharingPermissionsAsync(string groupId, string itemId)
		{
			List<Beta.Permission> permissionList = new List<Beta.Permission>();
			Beta.IDriveItemPermissionsCollectionPage permissions = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					permissions = await graphClient.Groups[groupId].Drive.Items[itemId].Permissions.Request().GetAsync();
					permissionList.AddRange(permissions);
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("ListSharingPermissionsAsync({groupId}, {itemId}) - Resouce NotFound.");
						break;
					}
					else
					{
						logger.LogError("ListSharingPermissionsAsync({groupId}, {itemId}) - Failed but continue. Exception: {se}", groupId, itemId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("ListSharingPermissionsAsync({groupId}, {itemId}) - Failed but continue. Exception: {e}", groupId, itemId, e);
				}
				interval /= decay;
			}

			while (permissions != null && permissions.NextPageRequest != null)
			{
				interval = 1_000;
				while (interval > threshold)
				{
					try
					{
						permissions = permissions.NextPageRequest.GetAsync().Result;
						permissionList.AddRange(permissions);
						break;
					}
					catch (Microsoft.Graph.ServiceException se)
					{
						if (TimeoutExceptionFilter(se))
						{
							Thread.Sleep(timeoutWaitingTime);
						}
						else if (NotFoundExceptionFilter(se))
						{
							logger.LogError("ListSharingPermissionsAsync({groupId}, {itemId}).NextPageRequest - Resouce NotFound.");
							break;
						}
						else
						{
							logger.LogError("ListSharingPermissionsAsync({groupId}, {itemId}).NextPageRequest - Failed but continue. Exception: {se}", groupId, itemId, se);
						}
					}
					catch (Exception e)
					{
						logger.LogError("ListSharingPermissionsAsync({groupId}, {itemId}).NextPageRequest - Failed but continue. Exception: {e}", groupId, itemId, e);
					}
					interval /= decay;
				}
			}

			return permissionList;
		}

		// Send a sharing invitation
		// https://docs.microsoft.com/zh-cn/graph/api/driveitem-invite?view=graph-rest-beta&tabs=csharp
		// Application: Files.ReadWrite.All, Sites.ReadWrite.All(from least to most privileged)
		public async Task SendSharingPermissionsAsync(string groupId, string itemId, InviteDetails inviteFormat)
		{
			var recipients = inviteFormat.Recipients;
			var message = inviteFormat.Message;
			var requireSignIn = inviteFormat.RequireSignIn;
			var sendInvitation = inviteFormat.SendInvitation;
			var roles = inviteFormat.Roles;
			var password = inviteFormat.Password;
			var expirationDateTime = inviteFormat.ExpirationDateTime;

			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					await graphClient.Groups[groupId].Drive.Items[itemId].Invite(recipients, requireSignIn, roles, sendInvitation, message, expirationDateTime, password).Request().PostAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("SendSharingPermissionsAsync({groupId}, {itemId}) - Resouce NotFound.", groupId, itemId);
						break;
					}
					else
					{
						logger.LogError("SendSharingPermissionsAsync({groupId}, {itemId}) - Failed but continue. Exception: {se}", groupId, itemId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("SendSharingPermissionsAsync({groupId}, {itemId}) - Failed but continue. Exception: {e}", groupId, itemId, e);
				}
				interval /= decay;
			}
		}

		// Get sharing permission for a file or folder
		// https://docs.microsoft.com/zh-cn/graph/api/permission-get?view=graph-rest-beta&tabs=http
		// Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public async Task<Beta.Permission> GetSharingPermissionAsync(string groupId, string itemId, string permId)
		{
			Beta.Permission permission = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					permission = await graphClient.Groups[groupId].Drive.Items[itemId].Permissions[permId].Request().GetAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("GetSharingPermissionAsync({groupId}, {itemId}, {permId}) - Resouce NotFound.", groupId, itemId, permId);
						break;
					}
					else
					{
						logger.LogError("GetSharingPermissionAsync({groupId}, {itemId}, {permId}) - Failed but continue. Exception: {se}", groupId, itemId, permId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("GetSharingPermissionAsync({groupId}, {itemId}, {permId}) - Failed but continue. Exception: {e}", groupId, itemId, permId, e);
				}
				interval /= decay;
			}
			return permission;
		}

		// Update sharing permission
		// https://docs.microsoft.com/zh-cn/graph/api/permission-update?view=graph-rest-beta&tabs=http
		// Application: Files.ReadWrite.All, Sites.ReadWrite.All(from least to most privileged)
		public async Task UpdateSharingPermissionsAsync(string groupId, string itemId, string permId, List<string> updatedPermissions)
		{
			var permission = new Beta.Permission
			{
				Roles = new List<string>(updatedPermissions)
			};
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					await graphClient.Groups[groupId].Drive.Items[itemId].Permissions[permId].Request().UpdateAsync(permission);
					logger.LogError("UpdateSharingPermissionsAsync({groupId}, {itemId}, {permId}, {updatedPermissions}  - Succeed", groupId, itemId, permId, string.Join(",", updatedPermissions.ToArray()));
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.LogError("UpdateSharingPermissionsAsync({groupId}, {itemId}, {permId}, {updatedPermissions}) - Resouce NotFound.", groupId, itemId, permId, string.Join(",", updatedPermissions.ToArray()));
						break;
					}
					else
					{
						logger.LogError("UpdateSharingPermissionsAsync({groupId}, {itemId}, {permId}, {updatedPermissions}) - Failed but continue. Exception: {se}", groupId, itemId, permId, string.Join(",", updatedPermissions.ToArray()), se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("UpdateSharingPermissionsAsync({groupId}, {itemId}, {permId}, {updatedPermissions}) - Failed but continue. Exception: {e}", groupId, itemId, permId, string.Join(",", updatedPermissions.ToArray()), e);
				}
				interval /= decay;
			}
		}

		// Delete a sharing permission from a file or folder
		// https://docs.microsoft.com/zh-cn/graph/api/permission-delete?view=graph-rest-beta&tabs=http
		// Application: Files.ReadWrite.All, Sites.ReadWrite.All(from least to most privileged)
		public async Task DeleteSharingPermissionsAsync(string groupId, string itemId, string permId)
		{
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					await graphClient.Groups[groupId].Drive.Items[itemId].Permissions[permId].Request().DeleteAsync();
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					} 
					else if (NotFoundExceptionFilter(se)) 
					{
						logger.LogError("DeleteSharingPermissionsAsync({groupId}, {itemId}, {permId}) - Resouce NotFound.", groupId, itemId, permId);
						break;
					}
					else
					{
						logger.LogError("DeleteSharingPermissionsAsync({groupId}, {itemId}, {permId}) - Failed but continue. Exception: {se}", groupId, itemId, permId, se);
					}
				}
				catch (Exception e)
				{
					logger.LogError("DeleteSharingPermissionsAsync({groupId}, {itemId}, {permId}) - Failed but continue. Exception: {e}", groupId, itemId, permId, e);
				}
				interval /= decay;
			}
		}

		// Add app to team
		// https://docs.microsoft.com/en-us/graph/api/teamsappinstallation-add?view=graph-rest-beta&tabs=csharp
		// Application: Group.ReadWrite.All
		public async Task<HttpStatusCode> AddAppToTeamAsync(string teamId, string appCatalogId)
		{
			HttpStatusCode result = HttpStatusCode.BadRequest;
			Beta.TeamsAppInstallation teamsAppInstallation = new Beta.TeamsAppInstallation
			{
				AdditionalData = new Dictionary<string, object>()
					{
						{
							"teamsApp@odata.bind", $"https://graph.microsoft.com/beta/appCatalogs/teamsApps/{appCatalogId}"
						}
					}
			};
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					await graphClient.Teams[teamId].InstalledApps.Request().AddAsync(teamsAppInstallation);
					result = HttpStatusCode.OK;
					break;
				}
				catch (Microsoft.Graph.ServiceException se)
				{
					if (!TimeoutExceptionFilter(se))
					{
						result = se.StatusCode;
						break;
					}
					else
					{
						Thread.Sleep(timeoutWaitingTime);
					}
				}
				catch (Exception e)
				{
					logger.LogError("AddAppToTeamAsync({teamId}, {appCatalogId}) - Failed but continue. Exception: {e}", teamId, appCatalogId, e);
				}
				interval /= decay;
			}
			return result;
		}

		private static bool TimeoutExceptionFilter(Microsoft.Graph.ServiceException se) 
		{
			return (se.Error.Code.Equals("Timeout", StringComparison.OrdinalIgnoreCase)) ? true : false;
		}

		private static bool NotFoundExceptionFilter(Microsoft.Graph.ServiceException se)
		{
			return (se.StatusCode == HttpStatusCode.NotFound
				|| se.Error.Code.Equals("itemNotFound", StringComparison.OrdinalIgnoreCase) 
				|| se.Error.Code.Equals("NotFound", StringComparison.OrdinalIgnoreCase)) ? true : false;
		}

		public async Task<string> GetSharePointSiteAsync(string teamId)
		{
			string result = null;
			Beta.Drive drive  = await GetGroupDefaultDriveAsync(teamId);
			if (drive.NotNull())
			{
				//Don't use TrimEnd. In Net Core 3.1, TrimEnd not surport blank space and '%20'
				result = drive.WebUrl.Replace(Global.Shared_Documents, null, StringComparison.OrdinalIgnoreCase);
			}
			return result;
		}
	}
}
