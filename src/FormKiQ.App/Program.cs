using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace FormKiQ.App;

public abstract class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
        });

        var x = builder.Configuration;

        builder.Services.AddHttpClient<ApiClient>(client => client.BaseAddress = new(builder.Configuration["FormKiQApiHost"]!));

        builder.Services.AddBlazoredLocalStorage();

        await builder.Build().RunAsync();
    }
}
