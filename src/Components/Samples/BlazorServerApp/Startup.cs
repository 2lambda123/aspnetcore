// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using BlazorServerApp.Data;

namespace BlazorServerApp;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddSingleton<WeatherForecastService>();
        services.AddTransient<AuthMessageHandler>();
        services.AddHttpClient("Auth")
            .AddHttpMessageHandler<AuthMessageHandler>();
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
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapBlazorHub();
            endpoints.MapFallbackToPage("/_Host");
        });
    }

    public class AuthMessageHandler : DelegatingHandler
    {
        private readonly ILogger<AuthMessageHandler> logger;
        private readonly IAuthenticationStateProviderAccessor accessor;

        public AuthMessageHandler(IAuthenticationStateProviderAccessor accessor, ILogger<AuthMessageHandler> handler)
        {
            this.logger = handler;
            this.accessor = accessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var state = await accessor.AuthenticationStateProvider.GetAuthenticationStateAsync();
            logger.LogInformation($"Provider id : {accessor.AuthenticationStateProvider.Id}");
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
