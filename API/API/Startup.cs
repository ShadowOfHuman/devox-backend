﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
//using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using API.DAL.Context;

using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using API.BLL.Services.Users;
using API.BLL.Services.AccessControl;
using System.Reflection;
using System.IO;
using Swashbuckle.AspNetCore.Swagger;
using API.Hubs;
using API.Extentions;
using API.BLL.Helpers;
using API.BLL.Services.Emails;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using API.BLL.Services.Games;
using API.BLL.Services.GameMoves;

namespace API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        readonly string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var appSettingsSection = Configuration.GetSection("AppSettings");
            services.Configure<AppSettings>(appSettingsSection);
            var appSettings = appSettingsSection.Get<AppSettings>();

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            services.AddScoped<IDbContext, ApplicationDBContext>();
            services.AddDbContext<ApplicationDBContext>(options => options.UseMySql(appSettings.ConnectionString));
            services.AddScoped<IAccessControlService, AccessControlService>();
            services.AddScoped<IUserServices, UserService>();
            services.AddScoped<IGameServices, GameServices>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IGameMoveService, GameMoveService>();

            var key = Encoding.ASCII.GetBytes(appSettings.Secret);

            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                 .AddJwtBearer(x =>
                 {
                     x.Events = new JwtBearerEvents
                     {
                         OnTokenValidated = context =>
                         {
                             var userService = context.HttpContext.RequestServices.GetRequiredService<IUserServices>();
                             var userId = int.Parse(context.Principal.Identity.Name);
                             var user = userService.Get(userId);
                             if (user == null)
                                 // return unauthorized if user no longer exists
                                 context.Fail("Unauthorized");
                             return Task.CompletedTask;
                         }
                     };
                     x.RequireHttpsMetadata = false;
                     x.SaveToken = true;
                     x.TokenValidationParameters = new TokenValidationParameters
                     {
                         ValidateIssuerSigningKey = true,
                         IssuerSigningKey = new SymmetricSecurityKey(key),
                         ValidateIssuer = false,
                         ValidateAudience = false
                     };
                 }
                 );
            services.AddCors();
            services.AddSwaggerGen(c =>
            {
                c.CustomSchemaIds(x => x.FullName);
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "DevOX API", Version = "v1" });
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });
            services.AddSignalR();
        }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env, IDbContext context)
        {
            context.EnsureCreated();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "DevOX API V1");
            });
            
            app.ConfigureCustomExceptionMiddleware();
            app.UseAuthentication();

            app.UseCors(builder =>
            builder.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
                );
            app.UseSignalR(endpoints =>
            endpoints.MapHub<GameHub>("/game")
            );
            
            //app.UseHttpsRedirection();
            app.UseMvc();
            
        }
    }
}
