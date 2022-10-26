// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.Data
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Linq;
    using System.Threading.Tasks;
    using SharePointAddInForEMTeamsWeb.Models;

    public class NxlDBContext : DbContext
    {
        public NxlDBContext() : base("DefaultConnection")
        {

        }

        public DbSet<TeamAttr> TeamAttrs { get; set; }
    }
}
