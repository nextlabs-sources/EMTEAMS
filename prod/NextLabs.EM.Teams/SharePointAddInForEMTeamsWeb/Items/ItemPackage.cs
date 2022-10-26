using Microsoft.SharePoint.Client.EventReceivers;

namespace SharePointAddInForEMTeamsWeb
{
	public class ItemPackage
	{
		public string userLoginName;
		public string relativePath;
		public string groupId;
		public SPRemoteEventType eventType;

		public ItemPackage(SPRemoteEventType eventType, string userLoginName, string groupId, string relativePath)
		{
			this.userLoginName = userLoginName;
			this.relativePath = relativePath;
			this.groupId = groupId;
			this.eventType = eventType;
		}
	}
}