using FlailTools.Core.Data;
using FlailTools.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Structed.Inkwell.Data;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

HttpClient http = new() { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };

builder.Services.AddScoped(_ => http);
builder.Services.AddScoped<IDataFileReader>(_ => new HttpDataFileReader(http));
builder.Services.AddScoped<GameDataSource>();

await builder.Build().RunAsync();
