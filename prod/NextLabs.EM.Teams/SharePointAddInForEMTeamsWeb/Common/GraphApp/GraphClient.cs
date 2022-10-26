extern alias GraphBeta;
using Beta = GraphBeta.Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Web.Configuration;
using System.Threading;
using System.Net;

namespace NextLabs.GraphApp
{
	public class NxlGraphClient
	{
		private static log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		private static string tenant;
		private static string appId;
		private static string appKey;
		private static Beta.GraphServiceClient graphClient;
		private static readonly int threshold = 1;
		private static readonly int decay = 3;
		public static bool Inited { get; private set; } = false;
		private static object syncRoot = new object();
		private const int timeoutWaitingTime = 200;//ms
		private static string aadUserAttributes;

		private NxlGraphClient()
		{
			Init();
		}

		public static void Init()
		{
			lock (syncRoot)
			{
				tenant = WebConfigurationManager.AppSettings.Get("Graph:Tenant") ?? throw new ArgumentNullException(nameof(tenant));
				appId = WebConfigurationManager.AppSettings.Get("Graph:AppId") ?? throw new ArgumentNullException(nameof(appId));
				appKey = WebConfigurationManager.AppSettings.Get("Graph:AppSecret") ?? throw new ArgumentNullException(nameof(appKey));
				aadUserAttributes = WebConfigurationManager.AppSettings.Get("AzureAD:UserAttributes").Trim().Trim(',') ?? throw new ArgumentNullException(nameof(appKey));
				graphClient = GetAppGraphServiceClient(tenant, appId, appKey) ?? throw new ArgumentNullException(nameof(graphClient));
				Inited = true;
			}
		}

		public static bool CheckConnection()
		{
			List<Beta.Group> teams = ListTeamsAsync().GetAwaiter().GetResult();
			return (teams != null && teams.Count > 0) ? true : false;
		}

		private static Beta.GraphServiceClient GetAppGraphServiceClient(string strTenantID, string strClientId, string strlientSecret)
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
		public static async Task<List<Beta.Group>> ListGroupsAsync()
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
						logger.Error(string.Format("ListGroupsAsync() - Resouce NotFound."));
						break;
					}
					else
					{
						logger.Error(string.Format("ListGroupsAsync() - Failed but continue. Exception: {0}", se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("ListGroupsAsync() - Failed but continue. Exception: {0}", e));
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
							logger.Error(string.Format("ListGroupsAsync().NextPageRequest - Resouce NotFound."));
							break;
						}
						else
						{
							logger.Error(string.Format("ListGroupsAsync().NextPageRequest - Failed but continue. Exception: {0}", se));
						}
					}
					catch (Exception e)
					{
						logger.Error(string.Format("ListGroupsAsync().NextPageRequest - Failed but continue. Exception: {0}", e));
					}
					interval /= decay;
				}
			}

			return groupList;
		}
		public static async Task<List<Beta.Group>> ListGroupsAsync(string filter)
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
						logger.Error(string.Format("ListGroupsAsync({0}) - Resouce NotFound.", filter));
						break;
					}
					else
					{
						logger.Error(string.Format("ListGroupsAsync({0}) - Failed but continue. Exception: {1}", filter, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("ListGroupsAsync({0}) - Failed but continue. Exception: {1}", filter, e));
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
							logger.Error(string.Format("ListGroupsAsync({0}).NextPageRequest - Resouce NotFound.", filter));
							break;
						}
						else
						{
							logger.Error(string.Format("ListGroupsAsync({0}).NextPageRequest - Failed but continue. Exception: {1}", filter, se));
						}
					}
					catch (Exception e)
					{
						logger.Error(string.Format("ListGroupsAsync({0}).NextPageRequest - Failed but continue. Exception: {1}", filter, e));
					}
					interval /= decay;
				}
			}

			return groupList;
		}
		public static async Task<List<Beta.Group>> ListTeamsAsync()
		{
			return await ListGroupsAsync(@"resourceProvisioningOptions/Any(x:x eq 'Team')");
		}

		// List channels
		// https://docs.microsoft.com/zh-cn/graph/api/channel-list?view=graph-rest-beta&tabs=http
		// Application: Group.Read.All, Group.ReadWrite.All(from least to most privileged)
		public static async Task<List<Beta.Channel>> ListChannelsAsync(string teamId)
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
						logger.Error(string.Format("ListChannelsAsync({0}}) - Resouce NotFound.", teamId));
						break;
					}
					else
					{
						logger.Error(string.Format("ListChannelsAsync({0}}) - Failed but continue. Exception: {1}", teamId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("ListChannelsAsync({0}}) - Failed but continue. Exception: {1}", teamId, e));
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
							logger.Error(string.Format("ListChannelsAsync({0}}).NextPageRequest - Resouce NotFound.", teamId));
							break;
						}
						else
						{
							logger.Error(string.Format("ListChannelsAsync({0}}).NextPageRequest - Failed but continue. Exception: {1}", teamId, se));
						}
					}
					catch (Exception e)
					{
						logger.Error(string.Format("ListChannelsAsync({0}}).NextPageRequest - Failed but continue. Exception: {1}", teamId, e));
					}
					interval /= decay;
				}
			}

			return channelList;
		}

		// Get a channel
		// https://docs.microsoft.com/zh-cn/graph/api/channel-get?view=graph-rest-beta&tabs=http
		// Application: Group.Read.All, Group.ReadWrite.All(from least to most privileged)
		public static async Task<Beta.Channel> GetChannelAsync(string teamId, string channelId)
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
						logger.Error(string.Format("GetChannelAsync({0}, {1}) - Resouce NotFound.", teamId, channelId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetChannelAsync({0}, {1}) - Failed but continue. Exception: {2}", teamId, channelId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetChannelAsync({0}, {1}) - Failed but continue. Exception: {2}", teamId, channelId, e));
				}
				interval /= decay;
			}
			return channel;
		}

		// Get a group
		// https://docs.microsoft.com/zh-cn/graph/api/group-get?view=graph-rest-beta&tabs=csharp
		// Application: Group.Read.All、Directory.Read.All、Group.ReadWrite.All、Directory.ReadWrite.All(from least to most privileged)
		public static async Task<Beta.Group> GetGroupAsync(string groupId)
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
						logger.Error(string.Format("GetGroupAsync({0}) - Resouce NotFound.", groupId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetGroupAsync({0}) - Failed but continue. Exception: {1}", groupId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetGroupAsync({0}) - Failed but continue. Exception: {1}", groupId, e));
				}
				interval /= decay;
			}
			return group;
		}

		// List users
		// https://docs.microsoft.com/zh-cn/graph/api/user-list?view=graph-rest-beta&tabs=http
		// Application: User.Read.All, User.ReadWrite.All, Directory.Read.All, Directory.ReadWrite.All(from least to most privileged)
		public static async Task<List<Beta.User>> ListUsersAsync()
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
						logger.Error("ListUsersAsync() - Resouce NotFound.");
						break;
					}
					else
					{
						logger.Error(string.Format("ListUsersAsync() - Failed but continue. Exception: {0}", se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("ListUsersAsync() - Failed but continue. Exception: {0}", e));
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
							logger.Error("ListUsersAsync().NextPageRequest - Resouce NotFound.");
							break;
						}
						else
						{
							logger.Error(string.Format("ListUsersAsync().NextPageRequest - Failed but continue. Exception: {0}", se));
						}
					}
					catch (Exception e)
					{
						logger.Error(string.Format("ListGroupsAsync() - Failed but continue. Exception: {0}", e));
					}
					interval /= decay;
				}
			}

			return userList;
		}

		// Get a user
		// https://docs.microsoft.com/zh-cn/graph/api/user-get?view=graph-rest-beta&tabs=http
		// Application: User.Read.All, User.ReadWrite.All, Directory.Read.All, Directory.ReadWrite.All(from least to most privileged)
		public static async Task<Beta.User> GetUserAsync(string userIdOrPrincipalName)
		{
			Beta.User user = null;
			int interval = 1_000;
			while (interval > threshold)
			{
				try
				{
					if (aadUserAttributes.Equals("default"))
						user = await graphClient.Users[userIdOrPrincipalName].Request().GetAsync();
					else
						user = await graphClient.Users[userIdOrPrincipalName].Request().Select(aadUserAttributes).GetAsync();
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
						logger.Error(string.Format("GetUserAsync({0}) - Resouce NotFound.", userIdOrPrincipalName));
						break;
					}
					else
					{
						logger.Error(string.Format("GetUserAsync({0}) - Failed but continue. Exception: {1}", userIdOrPrincipalName, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetUserAsync({0}) - Failed but continue. Exception: {1}", userIdOrPrincipalName, e));
				}
				interval /= decay;
			}
			return user;
		}

		// List group members
		// https://docs.microsoft.com/zh-cn/graph/api/group-list-members?view=graph-rest-beta&tabs=csharp
		// Application: Group.Read.All, Directory.Read.All(from least to most privileged)
		public static async Task<List<Beta.DirectoryObject>> ListGroupUsersAsync(string groupId)
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
						logger.Error(string.Format("ListGroupUsersAsync({0}) - Resouce NotFound.", groupId));
						break;
					}
					else
					{
						logger.Error(string.Format("ListGroupUsersAsync({0}) - Failed but continue. Exception: {1}", groupId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("ListGroupUsersAsync({0}) - Failed but continue. Exception: {1}", groupId, e));
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
							logger.Error(string.Format("ListGroupUsersAsync({0}).NextPageRequest - Resouce NotFound.", groupId));
							break;
						}
						else
						{
							logger.Error(string.Format("ListGroupUsersAsync({0}).NextPageRequest - Failed but continue. Exception: {1}", groupId, se));
						}
					}
					catch (Exception e)
					{
						logger.Error(string.Format("ListGroupUsersAsync({0}).NextPageRequest - Failed but continue. Exception: {1}", groupId, e));
					}
					interval /= decay;
				}
			}

			return DOList;
		}

		// Delete group member
		// https://docs.microsoft.com/zh-cn/graph/api/group-delete-members?view=graph-rest-beta&tabs=csharp
		// Application: GroupMember.ReadWrite.All, Group.ReadWrite.All, Directory.ReadWrite.All(from least to most privileged)
		public static async Task<bool> DeleteGroupUserAsync(string groupId, string userId)
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
						logger.Error(string.Format("DeleteGroupUsersAsync({0}, {1}) - Resouce NotFound.", groupId, userId));
						break;
					}
					else
					{
						logger.Error(string.Format("DeleteGroupUsersAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, userId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("DeleteGroupUsersAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, userId, e));
				}
				interval /= decay;
			}
			return interval > threshold;
		}

		// List group owners
		// https://docs.microsoft.com/zh-cn/graph/api/group-list-owners?view=graph-rest-beta&tabs=http
		// Application: Group.Read.All and User.Read.All, Group.Read.All and User.ReadWrite.All(from least to most privileged)
		public static async Task<List<Beta.DirectoryObject>> ListGroupOwnersAsync(string groupId)
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
						logger.Error(string.Format("ListGroupOwnersAsync({0}) - Resouce NotFound.", groupId));
						break;
					}
					else
					{
						logger.Error(string.Format("ListGroupOwnersAsync({0}) - Failed but continue. Exception: {1}", groupId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("ListGroupOwnersAsync({0}) - Failed but continue. Exception: {1}", groupId, e));
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
							logger.Error(string.Format("ListGroupOwnersAsync({0}).NextPageRequest - Resouce NotFound.", groupId));
							break;
						}
						else
						{
							logger.Error(string.Format("ListGroupOwnersAsync({0}).NextPageRequest - Failed but continue. Exception: {1}", groupId, se));
						}
					}
					catch (Exception e)
					{
						logger.Error(string.Format("ListGroupOwnersAsync({0}).NextPageRequest - Failed but continue. Exception: {1}", groupId, e));
					}
					interval /= decay;
				}
			}

			return DOList;
		}

		// Get Drive
		// https://docs.microsoft.com/zh-cn/graph/api/drive-get?view=graph-rest-beta&tabs=http
		// Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public static async Task<List<Beta.DriveItem>> GetDefaultDriveItemsAsync(string groupId)
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
						logger.Error(string.Format("GetDefaultDriveItemsAsync({0}) - Resouce NotFound.", groupId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetDefaultDriveItemsAsync({0}) - Failed but continue. Exception: {1}", groupId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetDefaultDriveItemsAsync({0}) - Failed but continue. Exception: {1}", groupId, e));
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
							logger.Error(string.Format("GetDefaultDriveItemsAsync({0}).NextPageRequest - Resouce NotFound.", groupId));
							break;
						}
						else
						{
							logger.Error(string.Format("GetDefaultDriveItemsAsync({0}).NextPageRequest - Failed but continue. Exception: {1}", groupId, se));
						}
					}
					catch (Exception e)
					{
						logger.Error(string.Format("GetDefaultDriveItemsAsync({0}).NextPageRequest - Failed but continue. Exception: {1}", groupId, e));
					}
					interval /= decay;
				}
			}

			return itemList;
		}

		//Get Drive
		//https://docs.microsoft.com/en-us/graph/api/drive-get?view=graph-rest-1.0&tabs=http
		//Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All
		public static async Task<Beta.Drive> GetGroupDefaultDriveAsync(string groupId)
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
						logger.Error(string.Format("GetChannelFilesFolderAsync({0}) - Resouce NotFound.", groupId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetChannelFilesFolderAsync({0}) - Failed but continue. Exception: {1}", groupId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetChannelFilesFolderAsync({0}) - Failed but continue. Exception: {1}", groupId, e));
				}
				interval /= decay;
			}
			return drv;
		}

		// Get a DriveItem resource(Channel FilesFolder)
		// https://docs.microsoft.com/zh-cn/graph/api/driveitem-get?view=graph-rest-beta&tabs=http
		//Application: Files.Read.All, Files.ReadWrite.All, Group.Read.All, Group.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public static async Task<Beta.DriveItem> GetChannelFilesFolderAsync(string teamId, string channelId)
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
						logger.Error(string.Format("GetChannelFilesFolderAsync({0}, {1}) - Resouce NotFound.", teamId, channelId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetChannelFilesFolderAsync({0}, {1}) - Failed but continue. Exception: {2}", teamId, channelId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetChannelFilesFolderAsync({0}, {1}) - Failed but continue. Exception: {2}", teamId, channelId, e));
				}
				interval /= decay;
			}
			return item;
		}

		public static async Task<List<Beta.ConversationMember>> GetChannelMembersAsync(string teamId, string channelId)
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
						logger.Error(string.Format("GetChannelMembersAsync({0}, {1}) - Resouce NotFound.", teamId, channelId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetChannelMembersAsync({0}, {1}) - Failed but continue. Exception: {2}", teamId, channelId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetChannelMembersAsync({0}, {1}) - Failed but continue. Exception: {2}", teamId, channelId, e));
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
							logger.Error(string.Format("GetChannelMembersAsync({0}, {1}).NextPageRequest - Resouce NotFound.", teamId, channelId));
							break;
						}
						else
						{
							logger.Error(string.Format("GetChannelMembersAsync({0}, {1}).NextPageRequest - Failed but continue. Exception: {2}", teamId, channelId, se));
						}
					}
					catch (Exception e)
					{
						logger.Error(string.Format("GetChannelMembersAsync({0}, {1}).NextPageRequest - Failed but continue. Exception: {2}", teamId, channelId, e));
					}
					interval /= decay;
				}
			}

			return memberList;
		}

	public static async Task<Beta.ConversationMember> GetChannelMemberAsync(string teamId, string channelId, string id)
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
						logger.Error(string.Format("GetChannelMemberAsync({0}, {1}) - Resouce NotFound.", teamId, channelId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetChannelMemberAsync({0}, {1}) - Failed but continue. Exception: {2}", teamId, channelId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetChannelMemberAsync({0}, {1}) - Failed but continue. Exception: {2}", teamId, channelId, e));
				}
				interval /= decay;
			}
			return member;
		}

		// List children of a driveItem
		// https://docs.microsoft.com/en-us/graph/api/driveitem-list-children?view=graph-rest-beta&tabs=csharp
		// Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public static async Task<List<Beta.DriveItem>> GetDriveItemChildrensPageAsync(string groupId, string itemId)
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
						logger.Error(string.Format("GetDriveItemChildrensPageAsync({0}, {1}) - Resouce NotFound.", groupId, itemId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetDriveItemChildrensPageAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, itemId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetDriveItemChildrensPageAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, itemId, e));
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
							logger.Error(string.Format("GetDriveItemChildrensPageAsync({0}, {1}).NextPageRequest - Resouce NotFound.", groupId, itemId));
							break;
						}
						else
						{
							logger.Error(string.Format("GetDriveItemChildrensPageAsync({0}, {1}).NextPageRequest - Failed but continue. Exception: {2}", groupId, itemId, se));
						}
					}
					catch (Exception e)
					{
						logger.Error(string.Format("GetDriveItemChildrensPageAsync({0}, {1}).NextPageRequest - Failed but continue. Exception: {2}", groupId, itemId, e));
					}
					interval /= decay;
				}
			}

			return itemList;
		}

		// Get a DriveItem resource
		// https://docs.microsoft.com/zh-cn/graph/api/driveitem-get?view=graph-rest-beta&tabs=http
		// Application: Files.Read.All, Files.ReadWrite.All, Group.Read.All, Group.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
				public static async Task<Beta.DriveItem> GetItemAsync(string groupId, string itemId)
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
						logger.Error(string.Format("GetItemAsync({0}, {1}) - Resouce NotFound.", groupId, itemId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetItemAsync({0}, {1}) - Failed but continue. Exception: {1}", groupId, itemId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetItemAsync({0}, {1}) - Failed but continue. Exception: {1}", groupId, itemId, e));
				}
				interval /= decay;
			}
			return item;
		}
		
		public static async Task<Beta.DriveItem> GetItemByRelativePathAsync(string groupId, string relativePath)
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
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.Error(string.Format("GetItemAsync({0}, {1}) - Resouce NotFound.", groupId, relativePath));
						break;
					}
					else
					{
						logger.Error(string.Format("GetItemAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, relativePath, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetItemAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, relativePath, e));
				}
				interval /= decay;
			}
			return item;
		}


		// Download the contents of a DriveItem
		// https://docs.microsoft.com/zh-cn/graph/api/driveitem-get-content?view=graph-rest-beta&tabs=http
		// Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public static async Task<Stream> GetItemStreamAsync(string groupId, string itemId)
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
						logger.Error(string.Format("GetItemStreamAsync({0}, {1}) - Resouce NotFound.", groupId, itemId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetItemStreamAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, itemId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetItemStreamAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, itemId, e));
				}
				interval /= decay;
			}
			return strm;
		}

		public static async Task<Stream> GetItemStreamByRelativePathAsync(string groupId, string relativePath)
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
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.Error(string.Format("GetItemStreamAsync({0}, {1}) - Resouce NotFound.", groupId, relativePath));
						break;
					}
					else
					{
						logger.Error(string.Format("GetItemStreamAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, relativePath, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetItemStreamAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, relativePath, e));
				}
				interval /= decay;
			}
			return strm;
		}

		//Delete a DriveItem
		//https://docs.microsoft.com/en-us/graph/api/driveitem-delete?view=graph-rest-1.0&tabs=http
		//Application:	Files.ReadWrite.All, Sites.ReadWrite.All
		public static async Task<bool> DeleteItemAsync(string groupId, string itemId)
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
				catch (Microsoft.Graph.ServiceException se)
				{
					if (TimeoutExceptionFilter(se))
					{
						Thread.Sleep(timeoutWaitingTime);
					}
					else if (NotFoundExceptionFilter(se))
					{
						logger.Error(string.Format("GetItemAsync({0}, {1}) - Resouce NotFound.", groupId, itemId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetItemAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, itemId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetItemAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, itemId, e));
				}
				interval /= decay;
			}
			return result;
		}

		// List sharing permissions on a DriveItem
		// https://docs.microsoft.com/zh-cn/graph/api/driveitem-list-permissions?view=graph-rest-beta&tabs=csharp
		// Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public static async Task<List<Beta.Permission>> ListSharingPermissionsAsync(string groupId, string itemId)
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
						logger.Error(string.Format("ListSharingPermissionsAsync({0}, {1}) - Resouce NotFound.", groupId, itemId));
						break;
					}
					else
					{
						logger.Error(string.Format("ListSharingPermissionsAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, itemId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("ListSharingPermissionsAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, itemId, e));
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
							logger.Error(string.Format("ListSharingPermissionsAsync({0}, {1}).NextPageRequest - Resouce NotFound.", groupId, itemId));
							break;
						}
						else
						{
							logger.Error(string.Format("ListSharingPermissionsAsync({0}, {1}).NextPageRequest - Failed but continue. Exception: {2}", groupId, itemId, se));
						}
					}
					catch (Exception e)
					{
						logger.Error(string.Format("ListSharingPermissionsAsync({0}, {1}).NextPageRequest - Failed but continue. Exception: {2}", groupId, itemId, e));
					}
					interval /= decay;
				}
			}

			return permissionList;
		}

		// Send a sharing invitation
		// https://docs.microsoft.com/zh-cn/graph/api/driveitem-invite?view=graph-rest-beta&tabs=csharp
		// Application: Files.ReadWrite.All, Sites.ReadWrite.All(from least to most privileged)
		public static async Task SendSharingPermissionsAsync(string groupId, string itemId, InviteDetails inviteFormat)
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
						logger.Error(string.Format("SendSharingPermissionsAsync({0}, {1}) - Resouce NotFound.", groupId, itemId));
						break;
					}
					else
					{
						logger.Error(string.Format("SendSharingPermissionsAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, itemId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("SendSharingPermissionsAsync({0}, {1}) - Failed but continue. Exception: {2}", groupId, itemId, e));
				}
				interval /= decay;
			}
		}

		// Get sharing permission for a file or folder
		// https://docs.microsoft.com/zh-cn/graph/api/permission-get?view=graph-rest-beta&tabs=http
		// Application: Files.Read.All, Files.ReadWrite.All, Sites.Read.All, Sites.ReadWrite.All(from least to most privileged)
		public static async Task<Beta.Permission> GetSharingPermissionAsync(string groupId, string itemId, string permId)
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
						logger.Error(string.Format("GetSharingPermissionAsync({0}, {1}, {2}) - Resouce NotFound.", groupId, itemId, permId));
						break;
					}
					else
					{
						logger.Error(string.Format("GetSharingPermissionAsync({0}, {1}, {2}) - Failed but continue. Exception: {3}", groupId, itemId, permId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("GetSharingPermissionAsync({0}, {1}, {2}) - Failed but continue. Exception: {3}", groupId, itemId, permId, e));
				}
				interval /= decay;
			}
			return permission;
		}

		// Update sharing permission
		// https://docs.microsoft.com/zh-cn/graph/api/permission-update?view=graph-rest-beta&tabs=http
		// Application: Files.ReadWrite.All, Sites.ReadWrite.All(from least to most privileged)
		public static async Task UpdateSharingPermissionsAsync(string groupId, string itemId, string permId, List<string> updatedPermissions)
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
					logger.Error(string.Format("UpdateSharingPermissionsAsync({0}, {1}, {2}, {3}  - Succeed", groupId, itemId, permId, string.Join(",", updatedPermissions.ToArray())));
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
						logger.Error(string.Format("UpdateSharingPermissionsAsync({0}, {1}, {2}, {3}) - Resouce NotFound.", groupId, itemId, permId, string.Join(",", updatedPermissions.ToArray())));
						break;
					}
					else
					{
						logger.Error(string.Format("UpdateSharingPermissionsAsync({0}, {1}, {2}, {3}) - Failed but continue. Exception: {4}", groupId, itemId, permId, string.Join(",", updatedPermissions.ToArray()), se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("UpdateSharingPermissionsAsync({0}, {1}, {2}, {3}) - Failed but continue. Exception: {4}", groupId, itemId, permId, string.Join(",", updatedPermissions.ToArray()), e));
				}
				interval /= decay;
			}
		}

		// Delete a sharing permission from a file or folder
		// https://docs.microsoft.com/zh-cn/graph/api/permission-delete?view=graph-rest-beta&tabs=http
		// Application: Files.ReadWrite.All, Sites.ReadWrite.All(from least to most privileged)
		public static async Task DeleteSharingPermissionsAsync(string groupId, string itemId, string permId)
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
						logger.Error(string.Format("DeleteSharingPermissionsAsync({0}, {1}, {2}) - Resouce NotFound.", groupId, itemId, permId));
						break;
					}
					else
					{
						logger.Error(string.Format("DeleteSharingPermissionsAsync({0}, {1}, {2}) - Failed but continue. Exception: {3}", groupId, itemId, permId, se));
					}
				}
				catch (Exception e)
				{
					logger.Error(string.Format("DeleteSharingPermissionsAsync({0}, {1}, {2}) - Failed but continue. Exception: {3}", groupId, itemId, permId, e));
				}
				interval /= decay;
			}
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
		
	}
}
