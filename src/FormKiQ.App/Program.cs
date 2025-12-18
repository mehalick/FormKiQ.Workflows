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

        builder.Services.AddHttpClient<ApiClient>(client => client.BaseAddress = new("https://ywr4pew5tl.execute-api.us-east-1.amazonaws.com"));

        builder.Services.AddBlazoredLocalStorage();

        await builder.Build().RunAsync();
    }
}
