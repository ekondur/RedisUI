# Redis Integrated UI

[![NuGet](http://img.shields.io/nuget/v/RedisUI.svg)](https://www.nuget.org/packages/RedisUI/)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/0d07393843a7422788c7a49a7875b99e)](https://app.codacy.com/gh/ekondur/RedisUI/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)

Redis Integrated UI is a .NET project designed to simplify the integration of a Redis User Interface (UI) page into your web applications. With this project, users can easily incorporate a Redis UI page, enabling them to interact with Redis keys and view Redis server statistics seamlessly.

## Features

- **Integration Ease**: Simplifies the process of integrating a Redis UI page into web applications.

- **Redis Key Management**: Provides functionality to interact with Redis keys conveniently. Supports viewing and inserting all major Redis data types:

  | Type       | View | Insert                   |
  |------------|------|--------------------------|
  | String     | ✅   | ✅                       |
  | List       | ✅   | ✅ (RPUSH)               |
  | Set        | ✅   | ✅ (SADD)                |
  | Hash       | ✅   | ✅ (HSET field/value)    |
  | Sorted Set | ✅   | ✅ (ZADD member/score)   |
  | Stream     | ✅   | ✅ (XADD JSON fields)    |

- **TTL & Expiration Management**: Full control over key expiry directly from the UI:
  - TTL column in the key list — `∞` for persistent keys, `Xs` for expiring ones
  - Value inspector shows remaining TTL in the metadata line
  - **Set Expiry**: enter a TTL in seconds and apply it to any selected key (`EXPIRE`)
  - **Persist**: remove expiry from a key to make it permanent (`PERSIST`)
  - **Expiring filter**: toggle button to show only keys that have a TTL set
  - Optional TTL field in the Add/Edit modal to set expiry at insert time

- **Key Metadata**: Displays value size and TTL (time-to-live) for each key in the value inspector panel.

  <img width="1234" height="499" alt="image" src="https://github.com/user-attachments/assets/baf001ab-a4f8-4b07-9e67-3e7e8cb9f62a" />

- **Server Statistics**: Displays statistics related to the Redis server for monitoring and analysis purposes.

  ![image](https://github.com/ekondur/RedisUI/assets/4971326/64000f2c-a2a7-4551-b1f8-4bdb0bbbb7f3)

## Getting Started

### New management features

- **Read-only access and operation permissions** enforced on the server for view, edit, delete and audit endpoints.
- **Connection profiles** with an explicit environment label; credentials remain on the server. Production labels are highlighted.
- **JSON editor** with validation, formatting and a before/after comparison. Complete text previews can be edited; partial and binary previews stay read-only. String edits compare the original value atomically and return a conflict if it changed.
- **Collection editing** for hash fields, list indices, set members and sorted-set members/scores. Individual stream entries can be deleted; stream values are appended as new entries. List edits check the original value at the selected index.
- **Expiry policies**: preserve existing TTL (the default), set a new TTL, or make a key persistent. String writes and their expiry policy are atomic. Collection insertion and an explicitly changed expiry remain separate operations.
- **Key groups** built from colon-separated prefixes on the current page. Selecting a prefix starts a pattern search. Saved searches are stored in the browser, separately for each UI path, connection and database.
- **Live charts** for memory, operations/second, interval cache hit rate and evictions/second. Sampling runs every five seconds, keeps the last 60 samples and pauses when the tab is hidden. Metrics are server-wide, not database-specific; unavailable counters and intervals without cache requests display `—`.
- **Operation history** records time, user, connection, database, operation, key and success/failure, without values or credentials. The bounded in-memory history can be forwarded to a durable audit sink.
- **In-place updates** after mutations, progress controls and inline errors; no full page reload is needed.

The built-in provider requires Redis **6.0.6 or newer** for all editing features (`KEEPTTL`, Lua scripts and `LPOS`). See the Redis [SET](https://redis.io/docs/latest/commands/set/) and [LPOS](https://redis.io/docs/latest/commands/lpos/) documentation. The configured Redis ACL must permit the commands used by enabled operations, including scripting for atomic edits.

Key scans have a configurable iteration budget and metadata concurrency. The **Expiring keys on this page** checkbox filters only already-loaded keys; it does not scan the entire database. Key groups have the same page scope. Redis scans are not snapshots and can repeat keys while data changes.

To integrate the Redis UI into your application, follow these steps:

- Install [RedisUI](https://www.nuget.org/packages/RedisUI/) from the NuGet Gallery.

```shell
PM> Install-Package RedisUI
```

- Add the middleware to your project.

```csharp
using RedisUI;

app.UseRedisUI();
```

- Run your project and browse **/redis** path. easy peasy!

## Settings

- The `Path` is "/redis" by default, set a new path.

```csharp
app.UseRedisUI(new RedisUISettings
{
    Path = "/myredisui",
});
```

- The `ConnectionString` is "localhost" by default, set the connection string.

```csharp
app.UseRedisUI(new RedisUISettings
{
    ConnectionString = "1.1.1.1:6379",
});
```

- Use `ConfigurationOptions` for detailed settings.

```csharp
ConfigurationOptions options = new ConfigurationOptions
{
    EndPoints = { { "my-redis.cloud.redislabs.com", 6379 } },
    User = "default",  // use your Redis user. More info https://redis.io/docs/management/security/acl/
    Password = "secret", // use your Redis password
    Ssl = true,
    SslProtocols = System.Security.Authentication.SslProtocols.Tls13
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
    CssLink = "/assets/bootstrap.min.css"
});
```

UI HTML, CSS and JavaScript are embedded in the NuGet package. No separate frontend build or Bootstrap JavaScript is needed. `JsLink` remains available for source compatibility but is no longer used by the UI.

### Connections, permissions and audit history

```csharp
app.UseAuthentication(); // when the host application has configured authentication
app.UseAuthorization();
app.UseRedisUI(new RedisUISettings
{
    ConnectionString = builder.Configuration.GetConnectionString("Redis")!,
    Environment = app.Environment.EnvironmentName,
    AuthorizationFilter = new MyAuthorizationFilter(app.Environment.IsDevelopment()),
    ReadOnly = false, // true disables edits and deletes on every connection
    Connections = new()
    {
        new RedisUIConnection
        {
            Name = "production",
            Environment = "Production",
            ConnectionString = builder.Configuration.GetConnectionString("ProductionRedis")!,
            ReadOnly = true
        }
    },
    AuthorizeOperation = (context, access) => access.Operation switch
    {
        RedisUIOperation.View => context.User.IsInRole("RedisViewer"),
        RedisUIOperation.Edit => context.User.IsInRole("RedisEditor"),
        RedisUIOperation.Delete => context.User.IsInRole("RedisAdmin"),
        RedisUIOperation.Audit => context.User.IsInRole("RedisAuditor"),
        _ => false
    },
    MetadataConcurrency = 8, // bounded to 1–64 simultaneous key metadata tasks
    MaxScanIterationsPerPage = 10,
    AuditCapacity = 500,
    AuditSink = entry => auditLogger.LogInformation(
        "RedisUI {Operation} by {User} on {Connection}/{Database}/{Key}: {Succeeded}",
        entry.Operation, entry.User, entry.Connection, entry.Database, entry.Key, entry.Succeeded)
});
```

The original connection is named `default`. Additional names must be unique and cannot use that name. `AuthorizeOperation` is an additional restriction on top of `AuthorizationFilter`; view permission is required for every UI/API request, so editors also need view access. The callback receives the selected connection and database and may restrict either. It is operation-level authorization, not per-key filtering. If omitted, operations are allowed subject to `AuthorizationFilter` and `ReadOnly`.

History is isolated by connection/database and resets on application restart; it is not a shared or durable audit database. The optional sink is synchronous and should enqueue work rather than block. Sink failures are logged without failing an already-applied Redis operation. Set `AuditCapacity = 0` to retain no in-memory entries. Values are never recorded, but key names are included.

Existing `ConnectionMultiplexer` and custom `DataProvider` instances can be supplied globally or per profile; RedisUI does not dispose externally owned instances. Custom providers can implement `IRedisUIEditorProvider` to enable advanced editing. Without it, collection editing and optimistic string editing are unavailable; the existing provider methods remain supported. The default provider's `SetStringAsync` now preserves TTL.

### Development and verification

```sh
dotnet build RedisUI.sln -c Release
dotnet test RedisUI.Tests/RedisUI.Tests.csproj -c Release
```

Set `REDISUI_TEST_REDIS=localhost:6379` to include real Redis integration tests. They use unique `redisui-test:<guid>:` keys, delete only keys they created, and never flush a database. Without the variable, these tests are explicitly skipped.

For browser checks, set `REDISUI_BROWSER_FIXTURE` to an absolute output filename before running the .NET tests. The tests generate HTML from the embedded resources. Then run:

```sh
pnpm --dir tests/browser install --frozen-lockfile
pnpm --dir tests/browser exec playwright install chromium
node tests/browser/smoke.cjs
```

The browser suite uses mocked HTTP responses with the real rendered UI; Redis behavior is covered separately by the integration tests. `REDISUI_BROWSER_EXECUTABLE` can select an installed Chromium browser. CI builds all target frameworks, tests against Redis 6.2/7.4/8.0, and runs desktop/mobile browser checks. Publishing to NuGet is gated by the .NET and Redis tests.

## Authorization

You can limit access to Redis data in the production environment.

- Add a new authorization filter and implement `IRedisAuthorizationFilter`

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
