using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OfficeDevPnP.Core.Framework.Provisioning.Model
{
    /// <summary>
    /// Collection of RoleDefinition objects
    /// </summary>
    public partial class RoleDefinitionCollection : BaseProvisioningTemplateObjectCollection<RoleDefinition>
    {
        /// <summary>
        /// Constructor for RoleDefibitionCollection class
        /// </summary>
        /// <param name="parentTemplate">Parent provisioning template</param>
        public RoleDefinitionCollection(ProvisioningTemplate parentTemplate) : base(parentTemplate)
        {

        }
    }
}
