using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OfficeDevPnP.Core.Framework.Provisioning.Model
{
    /// <summary>
    /// Collection of Localization objects
    /// </summary>
    public partial class LocalizationCollection: BaseProvisioningTemplateObjectCollection<Localization>
    {
        /// <summary>
        /// Constructor for LocalizationCollection class
        /// </summary>
        /// <param name="parentTemplate">Parent provisioning template</param>
        public LocalizationCollection(ProvisioningTemplate parentTemplate): 
            base(parentTemplate)
        {
        }
    }
}
