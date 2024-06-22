# Redis Integrated UI
[![NuGet](http://img.shields.io/nuget/v/RedisUI.svg)](https://www.nuget.org/packages/RedisUI/)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/0d07393843a7422788c7a49a7875b99e)](https://app.codacy.com/gh/ekondur/RedisUI/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)

Redis Integrated UI is a .NET project designed to simplify the integration of a Redis User Interface (UI) page into your web applications. With this project, users can easily incorporate a Redis UI page, enabling them to interact with Redis keys and view Redis server statistics seamlessly.

## Features

- **Integration Ease**: Simplifies the process of integrating a Redis UI page into web applications.
- **Redis Key Management**: Provides functionality to interact with Redis keys conveniently.
  
  ![image](https://github.com/ekondur/RedisUI/assets/4971326/cef9e4e3-4669-4e5d-98ab-772cab09428f)

- **Server Statistics**: Displays statistics related to the Redis server for monitoring and analysis purposes.

  ![image](https://github.com/ekondur/RedisUI/assets/4971326/64000f2c-a2a7-4551-b1f8-4bdb0bbbb7f3)

## Getting Started

To integrate the Redis UI into your application, follow these steps:

- Install [RedisUI](https://www.nuget.org/packages/RedisUI/) from the NuGet Gallery.

```
PM> Install-Package RedisUI
```
- Add the middleware to your project.
```csharp
using RedisUI;

app.UseRedisUI();
```
- Run your project and browse **/redis** path. easy peasy!

## Settings
- The ```Path``` is "/redis" by default, set a new path.
```csharp
app.UseRedisUI(new RedisUISettings
{
    Path = "/myredisui",
});
```
- The ```ConnectionString``` is "localhost" by default, set the connection string.
```csharp
app.UseRedisUI(new RedisUISettings
{
    ConnectionString = "1.1.1.1:6379",
});
```
- Use ```ConfigurationOptions``` for detailed settings.

```csharp
ConfigurationOptions options = new ConfigurationOptions
{
    EndPoints = { { "my-redis.cloud.redislabs.com", 6379 } },
    User = "default",  // use your Redis user. More info https://redis.io/docs/management/security/acl/
    Password = "secret", // use your Redis password
    Ssl = true,
    SslProtocols = System.Security.Authentication.SslProtocols.Tls12                
};
```
```csharp
app.UseRedisUI(new RedisUISettings
{
    ConfigurationOptions = options
});
```

- The UI is using Bootstrap 5.3.2 version from [CDN](https://getbootstrap.com/), you can get it from locally via setting properties below:
```csharp
app.UseRedisUI(new RedisUISettings
{
    CssLink = "..\\mypath\\bootstrap.min.cs",
    JsLink = "..\\mypath\\bootstrap.js"
});
```
## Authorization
You can limit access to Redis data in the production environment.

- Add a new authorization filter and implement ```IRedisAuthorizationFilter```
```csharp
using RedisUI;

public class MyAuthorizationFilter : IRedisAuthorizationFilter
{
    private readonly bool _isDevelopment;

    public MyAuthorizationFilter(bool isDevelopment)
    {
        _isDevelopment = isDevelopment;
    }

    public bool Authorize(HttpContext context)
    {
        return _isDevelopment || (context.User.Identity != null && context.User.Identity.IsAuthenticated);
    }
}
```
```csharp
app.UseRedisUI(new RedisUISettings
{
    AuthorizationFilter = new MyAuthorizationFilter(app.Environment.IsDevelopment())
});
```
## Contributing

Contributions are welcome! If you'd like to contribute to Redis Integrated UI, please follow these guidelines:

1. Fork the repository.
2. Create your feature branch (`git checkout -b feature/YourFeature`).
3. Commit your changes (`git commit -am 'Add some feature'`).
4. Push to the branch (`git push origin feature/YourFeature`).
5. Create a new Pull Request.

## License

This project is licensed under the [MIT License](LICENSE.txt).

## Contact

For any inquiries or support regarding Redis Integrated UI, feel free to contact the project maintainer:
- Email: [emrahkondur@hotmail.com](mailto:emrahkondur@hotmail.com)
- GitHub: [ekondur](https://github.com/ekondur)


