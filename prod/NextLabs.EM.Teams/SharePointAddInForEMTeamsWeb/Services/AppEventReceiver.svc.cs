using System;
using System.Collections.Generic;
using System.Web.Configuration;
using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.EventReceivers;

namespace SharePointAddInForEMTeamsWeb.Services
{
	public class AppEventReceiver : IRemoteEventService
	{
		#region Common Private Member Variables
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		private const string NXLRERNAME_LISTITEM = "NXLListItemEventReceiver";
		private static EventReceiverType[] arrayEventReceiverTypes = new EventReceiverType[]
		{
			EventReceiverType.ItemAdded,
			EventReceiverType.ItemUpdated
		};
		private static string listItemEventReceiverUrl = string.Format("https://{0}/Services/ListItemEventReceiver.svc", WebConfigurationManager.AppSettings.Get("HostedAppHostName"));
		//string remoteUrl1 = string.Format("https://{0}/Services/ListEventReceiver.svc", OperationContext.Current.Channel.LocalAddress.Uri.DnsSafeHost);
		#endregion

		#region Common Private Member Methods
		private static EventReceiverDefinitionCreationInformation BuildListItemEventReceiver(EventReceiverType eventReceiverType)
		{
			return new EventReceiverDefinitionCreationInformation()
			{
				EventType = eventReceiverType,
				ReceiverName = $"{NXLRERNAME_LISTITEM}{eventReceiverType}",
				ReceiverClass = NXLRERNAME_LISTITEM,
				ReceiverUrl = listItemEventReceiverUrl,
				SequenceNumber = 10000
			};
		}

		private static bool HasIncludeEventReceiver(EventReceiverType eventReceiverType, EventReceiverDefinitionCollection eventReceiverDefinitionCollection)
		{
			if (eventReceiverDefinitionCollection != null && eventReceiverDefinitionCollection.Count != 0)
			{
				foreach (EventReceiverDefinition erd in eventReceiverDefinitionCollection)
				{
					if (erd.ReceiverName.Equals($"{NXLRERNAME_LISTITEM}{eventReceiverType}")) return true;
				}
			}
			return false;
		}
		#endregion
		//Doing Event
		public SPRemoteEventResult ProcessEvent(SPRemoteEventProperties properties)
		{
			logger.Debug("Coming in AppEventReceiver - ProcessEvent");
			SPRemoteEventResult result = new SPRemoteEventResult();

			using (ClientContext clientContext = TokenHelper.CreateAppEventClientContext(properties, useAppWeb: false))
			{
				if (clientContext != null)
				{
					var documentsList = clientContext.Web.Lists.GetByTitle("Documents");
					clientContext.Load(documentsList, l=>l.EventReceivers);
					clientContext.ExecuteQuery();

					EventReceiverDefinitionCollection eventReceiverDefinitionCollection = documentsList.EventReceivers;
					logger.Debug($"AppEventReceiver - ProcessEvent - Before {properties.EventType}, Documents List EventReceivers Count: {eventReceiverDefinitionCollection.Count}");

					switch (properties.EventType)
					{
						case SPRemoteEventType.AppInstalled:
							//AppInstalled

							foreach (var eventReceiverType in arrayEventReceiverTypes)
							{
								try
								{
									if (!HasIncludeEventReceiver(eventReceiverType, eventReceiverDefinitionCollection))
									{
										EventReceiverDefinitionCreationInformation eventReceiver = BuildListItemEventReceiver(eventReceiverType);
										documentsList.EventReceivers.Add(eventReceiver);
										logger.Debug($"AppEventReceiver - ProcessEvent - Added {eventReceiverType}.");
									}
								}
								catch (Exception e) 
								{ 
									logger.Debug($"AppEventReceiver - Process AppInstalled Error: {e}");
								}
							}
							clientContext.ExecuteQuery();

							break;
						case SPRemoteEventType.AppUninstalling:
							//Uninstalling

							List<Guid> toDelete = new List<Guid>();
							foreach (var eventReceiverType in arrayEventReceiverTypes)
							{
								foreach (var rec in eventReceiverDefinitionCollection)
								{
									if (rec.ReceiverName == $"{NXLRERNAME_LISTITEM}{eventReceiverType}") toDelete.Add(rec.ReceiverId);
								}
							}
							foreach (Guid id in toDelete)
							{
								try
								{
									documentsList.EventReceivers.GetById(id).DeleteObject();
								}
								catch (Exception e) 
								{ 
									logger.Debug($"AppEventReceiver - Process AppUninstalling Error: {e}");
								}
							}
							clientContext.ExecuteQuery();

							//TO DO??
							//Add Default member and visitor permissions??

							break;
						default:
							break;
					}

					var tempList = clientContext.Web.Lists.GetByTitle("Documents");
					clientContext.Load(documentsList, l => l.EventReceivers);
					clientContext.ExecuteQuery();

					EventReceiverDefinitionCollection tempCollection = documentsList.EventReceivers;
					logger.Debug($"AppEventReceiver - ProcessEvent - After {properties.EventType}, Documents List EventReceivers Count: {tempCollection.Count}");
				}
			}
			logger.Debug("Leaving from AppEventReceiver - ProcessEvent");

			return result;
		}

		//Done Event
		public void ProcessOneWayEvent(SPRemoteEventProperties properties)
		{
			logger.Debug("Coming in AppEventReceiver - ProcessOneWayEvent");
			throw new NotImplementedException();
		}
	}
}
