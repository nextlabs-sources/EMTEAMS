using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.EventReceivers;

namespace SharePointAddInForEMTeamsWeb.Services
{
	public class ListItemEventReceiver : IRemoteEventService
	{
		#region Common Private Member Variables
		private static readonly string[] SupportFileExtension = { ".txt", ".pdf", ".docx", ".pptx", ".xlsx",
														".docm", ".dotx", ".xlam", ".xlsb", ".xlsm", ".pptm", ".ppam" };

		private static readonly char[] documentsPrefix = "Shared Documents/".ToCharArray();
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		#endregion

		#region Common Private Member Methods
		private static bool IsSharePointAppUser(string userLoginName)
		{
			return userLoginName.Equals("app@sharepoint", StringComparison.OrdinalIgnoreCase) || userLoginName.Equals("sharepointapp", StringComparison.OrdinalIgnoreCase) ? true : false;
		}

		private static bool IsSupportedExtension(string fileName)
		{
			foreach (string strExtension in SupportFileExtension) { if (fileName.EndsWith(strExtension, StringComparison.OrdinalIgnoreCase)) return true; }
			return false;
		}
		#endregion
		
		/// <summary>
		/// Handles events that occur before an action occurs, such as when a user adds or deletes a list item.
		/// </summary>
		/// <param name="properties">Holds information about the remote event.</param>
		/// <returns>Holds information returned from the remote event.</returns>
		public SPRemoteEventResult ProcessEvent(SPRemoteEventProperties properties)
		{
			SPRemoteEventResult result = new SPRemoteEventResult();

			return result;
		}

		#region Before Event Private Member Variables
		private static readonly List<SPRemoteEventType> supportedBeforeEventTypes =
			new List<SPRemoteEventType>()
			{
				//SPRemoteEventType.ItemDeleting,
			};
		#endregion

		#region Before Event Private Member Methods
		private static bool IsSupportedBeforeEventType(SPRemoteEventType spRemoteEventType)
		{
			return supportedBeforeEventTypes.Contains(spRemoteEventType);
		}

		private static bool SupportedBeforeEventFilter(SPRemoteEventType eventType, string fileUrl, string userLoginName)
		{
			if (!IsSharePointAppUser(userLoginName) && IsSupportedBeforeEventType(eventType))
			{
				if (IsSupportedExtension(fileUrl)) return false;
			}
			return true;
		}
		#endregion

		/// <summary>
		/// Handles events that occur after an action occurs, such as after a user adds an item to a list or deletes an item from a list.
		/// </summary>
		/// <param name="properties">Holds information about the remote event.</param>
		public void ProcessOneWayEvent(SPRemoteEventProperties properties)
		{
			logger.Debug("Coming in ListItemEventReceiver - ProcessOneWayEvent");

			string userLoginName = properties.ItemEventProperties.UserLoginName.Split('|').Last();
			string fileUrl = string.IsNullOrEmpty(properties.ItemEventProperties.AfterUrl) ? properties.ItemEventProperties.BeforeUrl : properties.ItemEventProperties.AfterUrl;
			if (NotSupportedAfterEventFilter(properties, fileUrl, userLoginName)) return; //Non App, Non Empty File, Event type and File extension filter
			logger.Debug("ListItemEventReceiver - ProcessOneWayEvent - Pass filter.");
			using (ClientContext clientContext = TokenHelper.CreateRemoteEventReceiverClientContext(properties))
			{
				if (clientContext != null)
				{
					try
					{
						Site theSite = clientContext.Site;
						clientContext.Load(theSite, s => s.GroupId);
						clientContext.ExecuteQuery();
						string groupId = theSite.GroupId.ToString();
						//TO DO: check is enforced team or not
						string relativePath = fileUrl.TrimStart(documentsPrefix);
						logger.Debug($"Build ItemPackage details: properties.EventType: {properties.EventType}, userLoginName: {userLoginName}, groupId: {groupId}, relativePath: {relativePath}.");
						ItemPackage itemPackage = new ItemPackage(properties.EventType, userLoginName, groupId, relativePath);
						ItemHandler.PushAndRun(itemPackage);
					}
					catch (Exception e)
					{
						logger.Error($"ListItemEventReceiver - ProcessOneWayEvent Error: {e}");
					}
				}
			}
			logger.Debug("Leaving from ListItemEventReceiver - ProcessOneWayEvent");
		}

		#region After Event Private Member Variables
		private static readonly List<SPRemoteEventType> supportedAfterEventTypes =
			new List<SPRemoteEventType>()
			{
				SPRemoteEventType.ItemAdded,
				SPRemoteEventType.ItemUpdated
			};
		#endregion

		#region After Event Private Member Methods
		private static bool IsSupportAfterEventType(SPRemoteEventType spRemoteEventType)
		{
			return supportedAfterEventTypes.Contains(spRemoteEventType);
		}

		private static bool IsEmptyOrNotExistedItemInAfterEvent(SPRemoteItemEventProperties properties)
		{
			//properties.AfterProperties != null >>> File Deleted(NotExisted)
			//properties.AfterProperties["vti_filesize"] is int? && (properties.AfterProperties["vti_filesize"] as int?).Equals(0) >>> File Empty
			return properties.AfterProperties != null && properties.AfterProperties["vti_filesize"] is int? && (properties.AfterProperties["vti_filesize"] as int?) == 0 ? true : false;
		}

		private bool NotSupportedAfterEventFilter(SPRemoteEventProperties properties, string fileUrl, string userLoginName)
		{
			//query pc for file name, so can not pass empty file directly.
			return (IsSharePointAppUser(userLoginName) /*|| IsEmptyOrNotExistedItemInAfterEvent(properties.ItemEventProperties)*/ || !IsSupportAfterEventType(properties.EventType) || !IsSupportedExtension(fileUrl)) ? true : false;
		}
		#endregion
	}
}
