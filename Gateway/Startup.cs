using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Microsoft.Extensions.Primitives;
using System.Net.Http;
using Ocelot.Request.Middleware;
using Gateway.Core.Errors;
using System.Collections.Generic;

namespace Gateway
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
            services.AddCors();

            #region JWT
            var authenticationSettings = new AuthenticationSettings();
            Configuration.GetSection("Authentication").Bind(authenticationSettings);

            services.AddAuthentication(option =>
            {
                option.DefaultAuthenticateScheme = "Bearer";
                option.DefaultScheme = "Bearer";
                option.DefaultChallengeScheme = "Bearer";
            }).AddJwtBearer("IndfKey", cfg =>
            {
                cfg.RequireHttpsMetadata = false;
                cfg.SaveToken = true;
                cfg.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = authenticationSettings.JwtIssuer,
                    ValidAudience = authenticationSettings.JwtIssuer,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authenticationSettings.JwtKey)),
                };
                cfg.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies["SESSIONID"];
                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                };
            });
            #endregion

            services.AddMvcCore().AddApiExplorer();
            services.AddOcelot(Configuration);
            services.AddSwaggerForOcelot(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwaggerForOcelotUI(c => {
                    c.RoutePrefix = string.Empty;
                });
            }

            app.UseHttpsRedirection();

            app.UseCors(x => x
                .SetIsOriginAllowed(origin => true)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());

            //app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            var configuration = new OcelotPipelineConfiguration
            {
                PreQueryStringBuilderMiddleware = async (ctx, next) =>
                {
                    #region ESP_AUTH
                    var downstreamRoute = ctx.Items.DownstreamRoute();
                    string key = downstreamRoute.Key;

                    if(key is null)
                    {
                        ctx.Items.SetError(new OcelotKeyError());
                        return;
                    }

                    if (key is not null && key != "AuthMsvKey")
                    {
                        string ClaimId = ctx.User.FindFirst(c => c.Type == "claim_id").Value;

                        if (string.IsNullOrEmpty(ClaimId))
                        {
                            ctx.Items.SetError(new ClaimIdError());
                            return;
                        }

                        StringValues espIdSv;
                        bool operationStatus;
                        operationStatus = ctx.Request.Query.TryGetValue("espId", out espIdSv);

                        if(operationStatus)
                        {
                            var client = new HttpClient();
                            client.DefaultRequestHeaders.Add("claim_id", ClaimId);
                            var authMsvGatewayUrl = Configuration.GetValue<string>("AuthGatewayApiUrl");
                            var response = await client.GetAsync($"{authMsvGatewayUrl}/{espIdSv}");

                            if (response.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                var respContentEudId = await response.Content.ReadAsStringAsync();
                                var downstreamRequest = (DownstreamRequest) ctx.Items["DownstreamRequest"];
                                downstreamRequest.Headers.Add("param_esp_id", espIdSv.ToString());
                                downstreamRequest.Headers.Add("param_eud_id", respContentEudId);
                                ctx.Items["DownstreamRequest"] = downstreamRequest;
                            } else
                            {
                                ctx.Items.SetError(new AuthoriseEspError(espIdSv));
                                return;
                            }
                        }
                    }
                    #endregion
                    await next.Invoke();
                }
            };

            //app.UseEndpoints(endpoints =>
            //{
            //    endpoints.MapControllers();
            //});

            app.UseOcelot(configuration).Wait();
        }
    }
}
