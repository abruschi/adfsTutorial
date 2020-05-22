﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders.InMemory;

using TodoListClient.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using System;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Identity.Client;

namespace WebApp_OpenIDConnect_DotNet
{
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
            services.AddDistributedMemoryCache();

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
                // Handling SameSite cookie according to https://docs.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-3.1
                options.HandleSameSiteCookieCompatibility();
            });

            services.AddOptions();

            services.AddSignIn(options =>
            {
                Configuration.Bind("AzureAd", options);
                options.MetadataAddress = "https://sts.cxpaadtenant.com/adfs/.well-known/openid-configuration";
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                options.TokenValidationParameters.ValidIssuer = "https://sts.cxpaadtenant.com/adfs";
                options.TokenValidationParameters.IssuerValidator = ValidateAFDSIssuer;
                options.TokenValidationParameters.NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";


            }, options =>
            {
                Configuration.Bind("AzureAd", options);
            });

            services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.ResponseType = OpenIdConnectResponseType.CodeIdToken;

                var codeReceivedHandler = options.Events.OnAuthorizationCodeReceived;
                options.Events.OnAuthorizationCodeReceived = async context =>
                {
                    var builder = ConfidentialClientApplicationBuilder
                        .Create("71426af8-76ba-4bfa-a060-b9cd3975f18f")
                        .WithRedirectUri("https://localhost:44321/signin-oidc")
                        .WithAdfsAuthority("https://sts.cxpaadtenant.com/adfs")
                        .WithClientSecret("YJcQffupUbiKs8rnMV7uPPMj53xR6DbLSjTZHys4");

                    var app = builder.Build();
                    var token = await app.AcquireTokenByAuthorizationCode(new string[] { "openid" }, context.ProtocolMessage.Code)
                    .ExecuteAsync().ConfigureAwait(false);
                    await codeReceivedHandler(context).ConfigureAwait(false);
                };
            });

                services.AddTodoListService(Configuration);

            services.AddControllersWithViews(options =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
            }).AddMicrosoftIdentityUI();

            services.AddRazorPages();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }

        private string ValidateAFDSIssuer(string actualIssuer, SecurityToken securityToken, TokenValidationParameters validationParameters)
        {
            if (string.IsNullOrEmpty(actualIssuer))
                throw new ArgumentNullException(nameof(actualIssuer));

            if (securityToken == null)
                throw new ArgumentNullException(nameof(securityToken));

            if (validationParameters == null)
                throw new ArgumentNullException(nameof(validationParameters));

            if (validationParameters.ValidIssuer == actualIssuer)
                return actualIssuer;

            // If a valid issuer is not found, throw
            // brentsch - todo, create a list of all the possible valid issuers in TokenValidationParameters
            throw new SecurityTokenInvalidIssuerException($"Issuer: '{actualIssuer}', does not match any of the valid issuers provided for this application.");
        }
    }
}