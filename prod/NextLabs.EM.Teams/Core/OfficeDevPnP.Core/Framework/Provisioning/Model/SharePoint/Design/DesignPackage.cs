using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OfficeDevPnP.Core.Framework.Provisioning.Model
{
    /// <summary>
    /// Defines a Design Package to import into the current Publishing site
    /// </summary>
    public partial class DesignPackage : BaseModel, IEquatable<DesignPackage>
    {
        #region Public Members

        /// <summary>
        /// Defines the path of the Design Package to import into the current Publishing site
        /// </summary>
        public String DesignPackagePath { get; set; }

        /// <summary>
        /// The Major Version of the Design Package to import into the current Publishing site
        /// </summary>
        public Int32 MajorVersion { get; set; }

        /// <summary>
        /// The Minor Version of the Design Package to import into the current Publishing site
        /// </summary>
        public Int32 MinorVersion { get; set; }

        /// <summary>
        /// The ID of the Design Package to import into the current Publishing site
        /// </summary>
        public Guid PackageGuid { get; set; }

        /// <summary>
        /// The Name of the Design Package to import into the current Publishing site
        /// </summary>
        public String PackageName { get; set; }

        #endregion

        #region Comparison code

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns>Returns HashCode</returns>
        public override int GetHashCode()
        {
            return (String.Format("{0}|{1}|{2}|{3}|{4}|",
                (this.DesignPackagePath != null ? this.DesignPackagePath.GetHashCode() : 0),
                this.MajorVersion.GetHashCode(),
                this.MinorVersion.GetHashCode(),
                (this.PackageGuid != null ? this.PackageGuid.GetHashCode() : 0),
                (this.PackageName != null ? this.PackageName.GetHashCode() : 0)
            ).GetHashCode());
        }

        /// <summary>
        /// Compares object with DesignPackage
        /// </summary>
        /// <param name="obj">Object that represents DesignPackage</param>
        /// <returns>true if the current object is equal to the DesignPackage</returns>
        public override bool Equals(object obj)
        {
            if (!(obj is DesignPackage))
            {
                return (false);
            }
            return (Equals((DesignPackage)obj));
        }

        /// <summary>
        /// Compares DesignPackage object based on DesignPackagePath, MajorVersion, MinorVersion, PackageGuid and PackageName.
        /// </summary>
        /// <param name="other">DesignPackage object</param>
        /// <returns>true if the DesignPackage object is equal to the current object; otherwise, false.</returns>
        public bool Equals(DesignPackage other)
        {
            if (other == null)
            {
                return (false);
            }

            return (
                this.DesignPackagePath == other.DesignPackagePath &&
                this.MajorVersion == other.MajorVersion &&
                this.MinorVersion == other.MinorVersion &&
                this.PackageGuid == other.PackageGuid &&
                this.PackageName == other.PackageName
                );
        }

        #endregion
    }
}
