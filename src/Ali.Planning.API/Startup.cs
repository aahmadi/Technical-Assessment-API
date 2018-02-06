using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Entities;
using AutoMapper;
using Entities.TestData;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Ali.Planning.API.Repositories;
using Ali.Planning.API.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Ali.Planning.API
{
    public class Startup
    {
        private IConfigurationRoot _config;
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsEnvironment("Development"))
            {
                // This will push telemetry data through Application Insights pipeline faster, allowing you to view results immediately.
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();
            _config = builder.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            // Add configuration service
            services.AddSingleton(_config);

            // Register the data context as a service to be able to inject it
            services.AddDbContext<PlanningDataContext>(ServiceLifetime.Scoped);

            services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<IPlanningRepository, PlanningRepository>();

            // Seed with sample data
            services.AddTransient<DatabaseInitializer>();

            // Register AutoMapper
            //services.AddAutoMapper();

            // Register Identity system for PlanningUser and IdentityRole
            services.AddIdentity<PlanningUser, IdentityRole>()
                .AddEntityFrameworkStores<PlanningDataContext>();

            // Add CORS policies
            services.AddCors(cfg =>
            {
                cfg.AddPolicy("Any", bldr =>
                {
                    bldr.AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithOrigins("http://localhost:4200");
                });
                
            });


            // Add framework services.
            services.AddApplicationInsightsTelemetry(_config);

            services.AddMvc( opt =>
            {
                opt.Filters.Add(new RequireHttpsAttribute());
            })
            //avoid circular referencing.
            .AddJsonOptions(opt => {
                opt.SerializerSettings.ReferenceLoopHandling =
                Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, 
            IHostingEnvironment env, 
            ILoggerFactory loggerFactory,
            DatabaseInitializer databaseInializer)
        {
            loggerFactory.AddConsole(_config.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseApplicationInsightsRequestTelemetry();

            app.UseApplicationInsightsExceptionTelemetry();

            // Seed the database
            databaseInializer.Seed().Wait();

            // Add Identity before Mvc middleware to protect the APIs
            app.UseIdentity();


            app.UseJwtBearerAuthentication(new JwtBearerOptions()
            {
                AutomaticAuthenticate = true,
                AutomaticChallenge = true,
                TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidIssuer = _config["Tokens:Issuer"],
                    ValidAudience = _config["Tokens:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Tokens:Key"])),
                    ValidateLifetime = true
                }
            });

            app.UseMvc();
        }
    }
}
