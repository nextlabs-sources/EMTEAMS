namespace SharePointAddInForEMTeamsWeb
{
	extern alias GraphBeta;
	using Beta = GraphBeta.Microsoft.Graph;
	using IFilterTextReader;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using QueryCloudAZSDK.CEModel;
	using QueryCloudAZSDK;
	using NextLabs.GraphApp;
	using System.Text.RegularExpressions;
	using SharePointAddInForEMTeamsWeb.Models;
	using Microsoft.SharePoint.Client.EventReceivers;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Linq;

    public static class ItemHelper
	{
		private static log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		// undeleted file, start a thread to remove it
		private static readonly object delLock = new object();
		private static ConcurrentQueue<string> unDeletedFileUrls = new ConcurrentQueue<string>();
		private static bool bDelThread = false;

		public static void ProcessFileAsync(this ItemPackage package)
		{
			try
			{
				Beta.Group teamInfo = NxlGraphClient.GetGroupAsync(package.groupId).GetAwaiter().GetResult();

				//File Attribute
				Beta.DriveItem item = NxlGraphClient.GetItemByRelativePathAsync(package.groupId, package.relativePath).GetAwaiter().GetResult();
				if (item == null)
				{
					logger.Debug(string.Format("ProcessFileAsync item is empty"));
					return;
				}

				string itemId = item.Id;
				FileAttr  spItem = GetSPFilePropertiesAsync(package.groupId, itemId, item);

				if (spItem == null)
				{
					logger.Error("ProcessFileAsync - spItem is null");
					return;
				}

				//build srcFile attributes
				string srcFileName = spItem.WebUrl;
				CEAttres ceFileAttrs = new CEAttres();
				spItem.InjectAttributesTo(ref ceFileAttrs);
				TeamAttr teamAttrs = new TeamAttr(teamInfo);
				teamAttrs.InjectAttributesTo(ref ceFileAttrs);

				//Uploader Attribute
				Beta.User uploader = NxlGraphClient.GetUserAsync(package.userLoginName).GetAwaiter().GetResult();

				if (package.eventType == SPRemoteEventType.ItemAdded)
				{
					//build subject-Uploader attributes
					string subjectUploaderId = uploader.Id;
					string subjectUploaderName = uploader.DisplayName;
					CEAttres ceUploaderAttrs = new CEAttres();
					ceUploaderAttrs.InjectAttributesFrom(uploader.ToDictionary<string>());

					//query pc
					PolicyResult emPolicyResult = PolicyResult.DontCare;
					CERequest ceFileUploadReq = CloudAZQuery.CreateQueryReq(TeamAction.Channel_File_Upload, string.Empty, srcFileName, ceFileAttrs, subjectUploaderId, subjectUploaderName, ceUploaderAttrs);
					QueryStatus emQueryFileUploadRes = CloudAZQuery.QueryCloudAZPC(ceFileUploadReq, out List<CEObligation> obligations, out emPolicyResult);

					logger.Debug($"ProcessFileAsync - QueryCloudAZPC - Channel_File_Upload, FileUrl: {srcFileName}, UploaderName: {subjectUploaderName}, QueryFileUploadResult: {emQueryFileUploadRes}, PolicyResult: {emPolicyResult}");

					if (emQueryFileUploadRes != QueryStatus.S_OK) emPolicyResult = CloudAZQuery.DefaultPCResult;
					if (emPolicyResult == PolicyResult.Deny)
					{
						NxlGraphClient.DeleteItemAsync(package.groupId, itemId).GetAwaiter().GetResult();
						logger.Debug($"ProcessFileAsync - DeleteItemAsync({package.groupId}, {itemId})");
						return;
					}
				}

				List<Beta.DirectoryObject> groupUsers = NxlGraphClient.ListGroupUsersAsync(package.groupId).GetAwaiter().GetResult();
				if (groupUsers == null) return;
				List<Beta.DriveRecipient> allowRecipients = new List<Beta.DriveRecipient>();
				List<CERequest> ceRequests = new List<CERequest>();
				List<string> userMails = new List<string>();
				foreach (var member in groupUsers)
				{
					Beta.User memberDetail = NxlGraphClient.GetUserAsync(member.Id).GetAwaiter().GetResult();
					//build subject attributes
					string subjectUserName = memberDetail.DisplayName;
					string subjectUserId = memberDetail.Id;
					CEAttres ceSubjectAttrs = new CEAttres();
					ceSubjectAttrs.InjectAttributesFrom(memberDetail.ToDictionary<string>());

					//process query pc
					CERequest ceTeamReq = CloudAZQuery.CreateQueryReq(TeamAction.Channel_File_View, string.Empty, srcFileName, ceFileAttrs, subjectUserId, subjectUserName, ceSubjectAttrs);
					ceRequests.Add(ceTeamReq);
					userMails.Add(memberDetail.Mail);
				}

				QueryStatus emQueryRes = CloudAZQuery.MultipleQueryColuAZPC(ceRequests, out _, out List<PolicyResult> listPolicyResults);
				logger.Debug($"ProcessFileAsync - QueryCloudAZPC - Channel_File_View, FileUrl: {srcFileName}, QueryResult: {emQueryRes}");

				//process policy decisions
				if (emQueryRes != QueryStatus.S_OK)
				{
					int reqCount = ceRequests.Count;
					listPolicyResults.Clear();
					var defaultPCResult = CloudAZQuery.DefaultPCResult;
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

				logger.Debug($"ProcessFileAsync - allowRecipientsCount: {allowRecipients.Count}");
				//List all members permissions
				List<Beta.Permission> curPerms = NxlGraphClient.ListSharingPermissionsAsync(package.groupId, itemId).GetAwaiter().GetResult();
				if (curPerms != null)
				{
					//Delete non owner permissions
					if (ClearNonOwnerGroupPermissionsAsync(curPerms, package.groupId, itemId, teamInfo.DisplayName))
					{
						curPerms = NxlGraphClient.ListSharingPermissionsAsync(package.groupId, itemId).GetAwaiter().GetResult();
						//Update all members permissions
						if (allowRecipients.Count != 0)
						{
							List<string> Roles = new List<string>() { "write" };
							AllocNewSharingPermissionsAsync(allowRecipients, package.groupId, itemId, Roles);
						}
						curPerms = NxlGraphClient.ListSharingPermissionsAsync(package.groupId, itemId).GetAwaiter().GetResult();
					}
				}
			}
			catch (Exception e)
			{
				logger.Error($"ProcessFileAsync Error: {e}");
			}
		}

		private static FileAttr GetSPFilePropertiesAsync(string groupId, string itemId, Beta.DriveItem item)
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
					url = SaveToLocalAsync(groupId, itemId, item.Name);
					if (!string.IsNullOrEmpty(url) && File.Exists(url)) fileAttr.LocalAttrs = GetPropertiesByIFilter(url);
				}
			}
			catch (Exception e)
			{
				logger.Error(string.Format("GetSPFilePropertiesAsync Error: {0}", e));
			}
			finally 
			{
				if(!string.IsNullOrEmpty(url) && File.Exists(url)) SafelyDeleteFile(url);
			}
			return fileAttr;
		}

		private static Dictionary<string, string> GetPropertiesByIFilter(string url)
		{
			logger.Debug(string.Format("GetPropertiesByIFilter: {0}", url));
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

				foreach (string keyword in GlobalConfigs.Keywords)
				{
					Regex regex = new Regex(keyword, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
					MatchCollection matches = regex.Matches(content);
					properties[AttributePrefix.FileKeyword_ + keyword] = matches.Count.ToString();
				}
			}
			catch (Exception e)
			{
				logger.Error($"GetPropertiesByIFilter Error: {e}");
			}
			return properties;
		}

		public static string SaveToLocalAsync(string groupId, string itemId, string fileName)
		{
			Stream sm = null;
			string _url = string.Empty;
			try
			{
				sm = NxlGraphClient.GetItemStreamAsync(groupId, itemId).GetAwaiter().GetResult();
				long s_len = sm.Length;
				byte[] srcbuf = new byte[s_len];
				int writenLen = sm.ReadAsync(srcbuf, 0, (int)s_len).GetAwaiter().GetResult();
				logger.Debug(string.Format("SaveToLocalAsync - long s_len: {0}; (int)s_len: {1}; writenLen: {2}", s_len, (int)s_len, writenLen));
				_url = $"{GlobalConfigs.TempFolder}{Guid.NewGuid()}_{fileName}";
				File.WriteAllBytes(_url, srcbuf);
				logger.Debug(string.Format("SaveToLocalAsync - File.WriteAllBytes Finished, _url: {0}", _url));
			}
			catch (Exception e)
			{
				_url = string.Empty;    //if Exception, _url is invalid, so reset empty here
				logger.Error($"SaveToLocalAsync Error: {e}");
			}
			finally
			{
				if (sm != null) { sm.Close(); sm.Dispose(); }
			}
			return _url;
		}

		private static void RemoveTempFile(object state)
		{
			Thread.CurrentThread.Priority = ThreadPriority.Lowest;
			logger.Info($"Thread priority : {Thread.CurrentThread.Priority}.");
			while (true)
			{
				if (!unDeletedFileUrls.TryDequeue(out string curUrl)) break;
				logger.Debug(string.Format("Try to delete {0}.", curUrl));
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
							logger.Debug(string.Format("Re-Enqueue {0}.", curUrl));
							unDeletedFileUrls.Enqueue(curUrl);
						}
					}
				}
				Thread.Sleep(500);
			}
			lock (delLock) { bDelThread = false; }
		}

		private static void SafelyDeleteFile(string strfi)
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
				logger.Info(string.Format("[Ignore]SafelyDeleteFile - {0} the file will delete it later.", e.Message));
			}
		}

		private static bool ClearNonOwnerGroupPermissionsAsync(List<Beta.Permission> perms, string groupId, string fileId, string teamName)
		{
			bool clear = true;
			foreach (var perm in perms)
			{
				try
				{
					if (!(perm.GrantedTo != null && perm.GrantedTo.User != null && perm.GrantedTo.User.DisplayName != null && perm.GrantedTo.User.DisplayName.Equals($"{teamName} Owners", StringComparison.OrdinalIgnoreCase)))
						NxlGraphClient.DeleteSharingPermissionsAsync(groupId, fileId, perm.Id).GetAwaiter().GetResult();
				}
				catch (Exception e)
				{
					logger.Error($"ClearNonOwnerGroupPermissionsAsync Error: {e}");
					clear = false;
					break;
				}
			}
			return clear;
		}

		private static bool AllocNewSharingPermissionsAsync(List<Beta.DriveRecipient> recipients, string groupid, string fileId, List<string> roles)
		{
			bool renew = true;
			try
			{
				NxlGraphClient.SendSharingPermissionsAsync(groupid, fileId, new InviteDetails(recipients, roles)).GetAwaiter().GetResult();
			}
			catch (Exception e)
			{
				logger.Error(string.Format($"RenewSharingPermissionsAsync Error: {0}", e));
				renew = false;
			}
			return renew;
		}
	}
}