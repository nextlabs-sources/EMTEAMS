// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.Common
{
	extern alias GraphBeta;
	using Beta = GraphBeta.Microsoft.Graph;
	using IFilterTextReader;
	using Microsoft.Extensions.Logging;
	using NextLabs.Teams.Models;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using QueryCloudAZSDK.CEModel;
	using QueryCloudAZSDK;
	using NextLabs.GraphApp;
	using System.Text.RegularExpressions;
    using Microsoft.Extensions.Options;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Linq;

    public sealed class TeamWrapper
	{
		private static readonly string[] SupportFileExtension = { ".txt", ".pdf", ".docx", ".pptx", ".xlsx",
														".docm", ".dotx", ".xlam", ".xlsb", ".xlsm", ".pptm", ".ppam" };

		private readonly ILogger logger;
		private IOptionsSnapshot<TeamWrapperOptions> teamWrapperOptions;
		private string[] Keywords { get; set; }
		private readonly NxlGraphClient nxlGraphClient;
		private readonly CloudAZQuery cloudazQuery;

		private string id;
		private string name;
		private Beta.User user;

		// undeleted file, start a thread to remove it
		private static readonly object delLock = new object();
		private static ConcurrentQueue<string> unDeletedFileUrls = new ConcurrentQueue<string>();
		private static bool bDelThread = false;

		public TeamWrapper(IOptionsSnapshot<TeamWrapperOptions> teamWrapperOptions, NxlGraphClient nxlGraphClient, CloudAZQuery cloudazQuery, ILogger<TeamWrapper> logger)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(this.logger));
			this.teamWrapperOptions = teamWrapperOptions ?? throw new ArgumentNullException(nameof(this.teamWrapperOptions));
			this.nxlGraphClient = nxlGraphClient ?? throw new ArgumentNullException(nameof(this.nxlGraphClient));
			this.cloudazQuery = cloudazQuery ?? throw new ArgumentNullException(nameof(this.cloudazQuery));

			this.Keywords = this.teamWrapperOptions.Value.ContentKeywords.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? throw new ArgumentNullException(nameof(this.Keywords));
		}

		public TeamWrapper Bind(string id, string name, Beta.User user = null)
		{
			this.id = id;
			this.name = name;
			this.user = user;
			return this;
		}

		public TeamWrapper Bind(KeyValuePair<string, string> pair, Beta.User user = null)
		{
			this.id = pair.Key;
			this.name = pair.Value;
			this.user = user;
			return this;
		}

		private static bool CheckSupportExtension(string fileName)
		{
			foreach (string strExtension in SupportFileExtension) { if (fileName.EndsWith(strExtension, StringComparison.OrdinalIgnoreCase)) return true; }
			return false;
		}

		public async Task ProcessChannelDriveAsync()
		{
			var channels = await nxlGraphClient.ListChannelsAsync(this.id);
			if (channels != null)
			{
				foreach (var channel in channels)
				{
					try
					{
						Beta.DriveItem tabItem = await nxlGraphClient.GetChannelFilesFolderAsync(this.id, channel.Id);
						logger.LogDebug("ProcessChannelDriveAsync: Channel={channelId}; Tab={tabItemId}; User={userId}", channel.Id, tabItem.Id, this.user != null ? this.user.Id : "null");
						if(tabItem != null && tabItem.Id != null) await ProcessFolderAsync(channel.Id, tabItem.Id, this.user);
					}
					catch (Exception e)
					{
						logger.LogError("ProcessChannelDriveAsync Error: {channelId}: {e}", channel.Id, e);
					}
				}
			}
		}

		private async Task ProcessFileAsync(string channelId, string fileId, Beta.User user = null)
		{
			try
			{
				Beta.DriveItem item = await nxlGraphClient.GetItemAsync(this.id, fileId);
				if (item != null)
				{
					if (!CheckSupportExtension(item.Name)) return;
				}
				else
				{
					logger.LogError("ProcessFileAsync - GetItemAsync({teamId}, {fileId}) Error!", this.id, fileId);
					return;
				}

				FileAttr spItem = await GetSPFilePropertiesAsync(fileId, item);
				if (spItem != null)
				{
					//build srcFile attributes
					string srcFileName = spItem.WebUrl;
					CEAttres ceFileAttrs = new CEAttres();
					spItem.InjectAttributesTo(ref ceFileAttrs);
					Beta.Group teamInfo = await nxlGraphClient.GetGroupAsync(this.id);
					if (teamInfo == null) return;
					TeamAttr teamAttrs = new TeamAttr(teamInfo);
					teamAttrs.InjectAttributesTo(ref ceFileAttrs);

					List<Beta.DriveRecipient> allowRecipients = new List<Beta.DriveRecipient>();
					if (user == null) //all group users
					{
						GetGroupAllowRecipients(srcFileName, ceFileAttrs, ref allowRecipients);
					}
					else //single user
					{
						GetAllowRecipient(srcFileName, ceFileAttrs, user, ref allowRecipients);
					}
					logger.LogDebug("ProcessFileAsync - allowRecipientsCount: {allowRecipientsCount}.", allowRecipients.Count);

					//Get all permissions
					SetNXLPermissions(fileId, allowRecipients);
				}
				else
				{
					logger.LogError("ProcessFileAsync - GetSPFilePropertiesAsync({fileId}, {itemId}) Error!", fileId, item.Id);
				}
			}
			catch (Exception e)
			{
				logger.LogError("ProcessFileAsync Error: {channelId}: {fileId}, {e}", channelId, fileId, e);
			}
		}

		private async Task ProcessFolderAsync(string channelId, string folderId, Beta.User user = null)
		{
			try
			{
				foreach (var item in await nxlGraphClient.GetDriveItemChildrensPageAsync(this.id, folderId))
				{
					try
					{
						if (item.Folder != null) await ProcessFolderAsync(channelId, item.Id, user);
						if (item.File != null) await ProcessFileAsync(channelId, item.Id, user);
					}
					catch (Exception e)
					{
						logger.LogError("ProcessFolderAsync Error: item.Id: {item.Id}, {e}.", item.Id, e);
					}
				}
			}
			catch (Exception e)
			{
				logger.LogError("ProcessFolderAsync Error: {folderId}@{channelId}, {e}", folderId, channelId, e);
			}
		}

		private void GetGroupAllowRecipients(string srcFileName, CEAttres ceFileAttrs, ref List<Beta.DriveRecipient> allowRecipients)
		{
			var groupUsers = nxlGraphClient.ListGroupUsersAsync(this.id).GetAwaiter().GetResult();
			if (groupUsers == null)
			{
				logger.LogError("GetGroupAllowRecipients - ListGroupUsersAsync({teamId}) Error!", this.id);
				return;
			}
			List<CERequest> ceRequests = new List<CERequest>();
			List<string> userMails = new List<string>();
			foreach (var dObj in groupUsers)
			{
				try
				{
					Beta.User memberDetail = nxlGraphClient.GetUserAsync(dObj.Id).GetAwaiter().GetResult();
					//build subject attributes
					string subjectUserName = memberDetail.DisplayName;
					string subjectUserId = memberDetail.Id;
					IDictionary<string, string> userAttributes = memberDetail.ToDictionary<string>();
					CEAttres ceSubjectAttrs = new CEAttres();
					ceSubjectAttrs.InjectAttributesFrom(userAttributes);

					//process query pc
					CERequest ceTeamReq = cloudazQuery.CreateQueryReq(TeamAction.Channel_File_View, string.Empty, srcFileName, ceFileAttrs, subjectUserId, subjectUserName, ceSubjectAttrs);
					ceRequests.Add(ceTeamReq);
					userMails.Add(memberDetail.Mail);
				}
				catch (Exception e) 
				{ 
					logger.LogError("GetGroupAllowRecipients - ListGroupUsersAsync({e}) Error!", e.ToString());
				}
			}

			QueryStatus emQueryRes = cloudazQuery.MultipleQueryColuAZPC(ceRequests, out List<PolicyResult> listPolicyResults, out _);

			//process policy decisions
			if (emQueryRes != QueryStatus.S_OK)
			{
				int reqCount = ceRequests.Count;
				listPolicyResults.Clear();
				var defaultPCResult = cloudazQuery.DefaultPCResult;
				for (int i = 0; i < reqCount; ++i)
				{
					listPolicyResults.Add(defaultPCResult);
				}
			}

			int count = listPolicyResults.Count;
			for (int i = 0; i < count; ++i)
			{
				if (listPolicyResults[i] != PolicyResult.Deny)
				{
					Beta.DriveRecipient curRecipient = new Beta.DriveRecipient
					{
						Email = userMails[i]
					};
					allowRecipients.Add(curRecipient);
				}
			}

		}

		private void GetAllowRecipient(string srcFileName, CEAttres ceFileAttrs, Beta.User user, ref List<Beta.DriveRecipient> allowRecipients)
		{
			//build subject attributes
			string subjectUserName = user.DisplayName;
			string subjectUserId = user.Id;
			IDictionary<string, string> userAttributes = user.ToDictionary<string>();
			CEAttres ceSubjectAttrs = new CEAttres();
			ceSubjectAttrs.InjectAttributesFrom(userAttributes);

			//process query pc
			CERequest ceTeamReq = cloudazQuery.CreateQueryReq(TeamAction.Channel_File_View, string.Empty, srcFileName, ceFileAttrs, subjectUserId, subjectUserName, ceSubjectAttrs);
			QueryStatus emQueryRes = cloudazQuery.QueryCloudAZPC(ceTeamReq, out _, out PolicyResult emPolicyResult);

			logger.LogDebug("GetAllowRecipient - for UserName: {UserName}; FileName: {FileName}; QueryRes: {QueryRes}; PolicyResult: {PolicyResult}.",
				subjectUserName, srcFileName, emQueryRes, emPolicyResult);

			//process policy decision
			if (emQueryRes != QueryStatus.S_OK) emPolicyResult = cloudazQuery.DefaultPCResult;
			if (emPolicyResult != PolicyResult.Deny)
			{
				Beta.DriveRecipient curRecipient = new Beta.DriveRecipient
				{
					Email = user.Mail
				};
				allowRecipients.Add(curRecipient);
			}
		}

		private void SetNXLPermissions(string fileId, List<Beta.DriveRecipient> allowRecipients)
		{
			//Get all permissions
			List<Beta.Permission> perms = nxlGraphClient.ListSharingPermissionsAsync(this.id, fileId).GetAwaiter().GetResult();
			if (perms != null)
			{
				//Delete all members permissions
				if (ClearNonOwnerGroupPermissionsAsync(perms, fileId, name).GetAwaiter().GetResult())
				{
					logger.LogDebug("SetNXLPermissions - Deleted Permissions finished and Start to Update Permissions for {fileId}", fileId);
					//Update all members permissions
					if (allowRecipients.Count != 0)
					{
						List<string> Roles = new List<string>() { "write" };
						AllocNewSharingPermissionsAsync(allowRecipients, fileId, Roles).GetAwaiter().GetResult();
					}
				}
			}
			else
			{
				logger.LogError("SetNXLPermissions - ListSharingPermissionsAsync({teamId}, {fileId}) Error!", this.id, fileId);
			}
		}

		private async Task<FileAttr> GetSPFilePropertiesAsync(string itemId, Beta.DriveItem item)
		{
			FileAttr fileAttr = null;
			string url = null;
			try
			{
				//put all sp properties with local properties
				fileAttr = new FileAttr(item);
				if (item.Size != null && item.Size != 0)
				{
					//put all local properties
					url = await SaveToLocalAsync(this.id, itemId, item.Name);
					if (!string.IsNullOrEmpty(url) && File.Exists(url)) fileAttr.LocalAttrs = GetPropertiesByIFilter(url);
				}
			}
			catch (Exception e)
			{
				logger.LogError("GetSPFilePropertiesAsync({itemId}) Error: {e}", itemId, e);
			}
			finally
			{
				if (!string.IsNullOrEmpty(url) && File.Exists(url)) SafelyDeleteFile(url);
			}
			return fileAttr;
		}

		private Dictionary<string, string> GetPropertiesByIFilter(string url)
		{
			logger.LogDebug("GetPropertiesByIFilter: {url}", url);
			Dictionary<string, string> properties = new Dictionary<string, string>();
			try
			{
				string content = string.Empty;
				using (var reader = new FilterReader(url))
				{
					string line = string.Empty;
					while ((line = reader.ReadLine()) != null) content += line + Environment.NewLine;
					Dictionary<string, List<object>> MetaProps = reader.MetaDataProperties;
					foreach (KeyValuePair<string, List<object>> prop in MetaProps)
					{
						if (string.IsNullOrEmpty(prop.Key) || prop.Value.Count == 0) continue;
						string attrKey = prop.Key.ToLower().Replace(' ', '_');
						if (attrKey.StartsWith("document_"))
						{
							attrKey = attrKey.TrimStart("document_".ToCharArray());
						}
						properties[AttributePrefix.File_ + attrKey] = string.Join(",", prop.Value);
					}
				};

				foreach (string keyword in this.Keywords)
				{
					Regex regex = new Regex(keyword, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
					MatchCollection matches = regex.Matches(content);
					properties[AttributePrefix.FileKeyword_ + keyword] = matches.Count.ToString();
				}
			}
			catch (Exception e)
			{
				logger.LogError("GetPropertiesByIFilter Error: {e}", e);
			}
			return properties;
		}

		public async Task<string> SaveToLocalAsync(string groupId, string itemId, string fileName)
		{
			Stream sm = null;
			string _url = string.Empty;
			try
			{
				sm = await nxlGraphClient.GetItemStreamAsync(groupId, itemId);
				long s_len = sm.Length;
				byte[] srcbuf = new byte[s_len];
				int writenLen = await sm.ReadAsync(srcbuf, 0, (int)s_len);
				logger.LogDebug("SaveToLocalAsync - long s_len: {s_len}; (int)s_len: {int_len}; writenLen: {writenLen}", s_len, (int)s_len, writenLen);
				_url = $"{Global.TempFolder}{Guid.NewGuid()}_{fileName}";
				File.WriteAllBytes(_url, srcbuf);
				logger.LogDebug("SaveToLocalAsync - File.WriteAllBytes Finished, _url: {_url}", _url);
			}
			catch (Exception ex)
			{
				_url = string.Empty;    //if Exception, _url is invalid, so reset empty here
				logger.LogError("SaveToLocalAsync - {ex}", ex);
			}
			finally
			{
				if (sm != null) { sm.Close(); sm.Dispose(); }
			}
			return _url;
		}


		private void RemoveTempFile(object state)
		{
			Thread.CurrentThread.Priority = ThreadPriority.Lowest;
			logger.LogInformation($"Thread priority : {Thread.CurrentThread.Priority}.");
			while (true)
			{
                if (!unDeletedFileUrls.TryDequeue(out string curUrl)) break;
                logger.LogDebug(string.Format("Try to delete {0}.", curUrl));
				if (!string.IsNullOrEmpty(curUrl) && File.Exists(curUrl))
				{
					try
					{
						File.SetAttributes(curUrl, FileAttributes.Normal);
						File.Delete(curUrl);
					}
					catch
					{
						if (!unDeletedFileUrls.Contains(curUrl))
						{
							logger.LogDebug(string.Format("Re-Enqueue {0}.", curUrl));
							unDeletedFileUrls.Enqueue(curUrl);
						}
					}
				}
				Thread.Sleep(500);
			}
			lock (delLock) { bDelThread = false; }
		}

		private void SafelyDeleteFile(string strfi)
		{
			try
			{
				if (!string.IsNullOrEmpty(strfi) && File.Exists(strfi))
				{
					//https://stackoverflow.com/questions/8821410/why-is-access-to-the-path-denied
					File.SetAttributes(strfi, FileAttributes.Normal);
					File.Delete(strfi);
				}
			}
			catch (Exception e)
			{
				lock (delLock)
				{
					if (!unDeletedFileUrls.Contains(strfi)) unDeletedFileUrls.Enqueue(strfi);
					if (!bDelThread)
					{
						bDelThread = true;
						ThreadPool.QueueUserWorkItem(RemoveTempFile);
					}
				}
				logger.LogInformation("[Ignore]SafelyDeleteFile - {0} the file will delete it later.", e.Message);
			}
		}

		private async Task<bool> ClearNonOwnerGroupPermissionsAsync(List<Beta.Permission> perms, string fileId, string teamName)
		{
			bool clear = true;
			foreach (var perm in perms)
			{
				try
				{
					if (!(perm.GrantedTo != null && perm.GrantedTo.User != null && perm.GrantedTo.User.DisplayName != null && perm.GrantedTo.User.DisplayName.Equals($"{teamName} Owners", StringComparison.OrdinalIgnoreCase)))
						await nxlGraphClient.DeleteSharingPermissionsAsync(this.id, fileId, perm.Id);
				}
				catch (Exception e)
				{
					logger.LogError("ClearSharingPermissionsAsync Error: {e}", e);
					clear = false;
				}
			}
			return clear;
		}

		private async Task<bool> AllocNewSharingPermissionsAsync(List<Beta.DriveRecipient> recipients, string fileId, List<string> roles)
		{
			bool renew = true;
			try
			{
				await nxlGraphClient.SendSharingPermissionsAsync(this.id, fileId, new InviteDetails(recipients, roles));
			}
			catch (Exception e)
			{
				logger.LogError("RenewSharingPermissionsAsync Error: {e}", e);
				renew = false;
			}
			return renew;
		}
	}
}