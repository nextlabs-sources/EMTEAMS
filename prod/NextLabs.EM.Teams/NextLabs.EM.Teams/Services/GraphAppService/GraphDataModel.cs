extern alias GraphBeta;
using Beta = GraphBeta.Microsoft.Graph;
using System;

using System.Collections.Generic;

namespace NextLabs.GraphApp
{
		public class InviteDetails
    {
        public InviteDetails()
        {
            throw new NotImplementedException();
        }

        public InviteDetails(List<Beta.DriveRecipient> recipients, List<string> roles, bool requireSignIn = true, bool sendInvitation = false, string message = null, string password = null, string expirationDateTime = null)
        {
            Recipients = recipients;
            Roles = roles;
            Password = password;
            RequireSignIn = requireSignIn;
            SendInvitation = sendInvitation;
            Message = message;
            ExpirationDateTime = expirationDateTime;
        }
        public List<Beta.DriveRecipient> Recipients { get; set; }
        public string Message { get; set; }
        public bool RequireSignIn { get; set; }
        public bool SendInvitation { get; set; }
        public List<string> Roles { get; set; }
        public string Password { get; set; }
        public string ExpirationDateTime { get; set; }  //"2018-07-15T14:00:00Z"
    }
}
