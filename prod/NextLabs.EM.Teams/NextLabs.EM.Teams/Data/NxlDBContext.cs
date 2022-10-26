// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.Teams
{
    using Microsoft.EntityFrameworkCore;
    using NextLabs.Teams.Models;

    public class NxlDBContext : DbContext
    {
        public NxlDBContext(DbContextOptions<NxlDBContext> options) : base(options)
        {
        }

        public DbSet<TeamAttr> TeamAttrs { get; set; }
    }
}
