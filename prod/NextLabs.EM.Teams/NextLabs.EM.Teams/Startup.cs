// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NexLabs.Teams
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NextLabs.Teams;

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson();

            services.AddDbContext<NxlDBContext>(options => options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")), ServiceLifetime.Transient);

            services.AddTeamWrapper(Configuration);

            services.AddCloudAZQuery(this.Configuration.GetSection("NextLabs"));

            services.AddNxlGraphClient(this.Configuration.GetSection("AzureAd"));

            services.AddSharePointClient(this.Configuration.GetSection("SharePoint"));

            services.AddTransient<NxlServicesInspector>();

            services.AddEventBot(Configuration);

            services.AddTeamEnforceHostedService(Configuration);

            services.AddFilesUserPermissionHostedService();

            services.AddCommonPermissionHostedService(Configuration);

            services.AddDataPersistenceHostedService(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseRouting()
                .UseAuthorization()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });

            // app.UseHttpsRedirection();
        }
    }
}
