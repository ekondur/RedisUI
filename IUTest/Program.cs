using IUTest;
using RedisUI;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
//builder.Services.AddSingleton(ConnectionMultiplexer.Connect("localhost"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

ConfigurationOptions options = new ConfigurationOptions
{
    EndPoints = { { "localhost", 6379 } },
};

app.UseRedisUI(new RedisUISettings
{
    AuthorizationFilter = new FooAuthorizationFilter(app.Environment.IsDevelopment()),
    //ConfigurationOptions = options
});

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
