using RedisUI.Infra;
using StackExchange.Redis;

namespace RedisUI
{
    public class RedisUISettings
    {
        public bool ReadOnly { get; set; }
        public string Environment { get; set; } = "Unspecified";
        public List<RedisUIConnection> Connections { get; set; } = new();
        /// <summary>Additional operation-level authorization, checked on every request.</summary>
        public Func<Microsoft.AspNetCore.Http.HttpContext, RedisUIAccess, bool>? AuthorizeOperation { get; set; }
        public int MetadataConcurrency { get; set; } = 8;
        public int AuditCapacity { get; set; } = 500;
        /// <summary>Optional durable audit sink. Do not block; values are deliberately excluded.</summary>
        public Action<RedisUIAuditEntry>? AuditSink { get; set; }
        /// <summary>
        /// Gets or sets the connection string for the Redis server.
        /// </summary>
        public string ConnectionString { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the ConfigurationOptions instance.
        /// </summary>
        public ConfigurationOptions? ConfigurationOptions { get; set; }

        /// <summary>
        /// Gets or sets an existing multiplexer to reuse.
        /// </summary>
        public IConnectionMultiplexer? ConnectionMultiplexer { get; set; }

        /// <summary>
        /// Gets or sets a custom data provider implementation.
        /// </summary>
        public IRedisUIDataProvider? DataProvider { get; set; }

        /// <summary>
        /// Gets or sets the path for the Redis server.
        /// </summary>
        public string Path { get; set; } = "/redis";

        /// <summary>
        /// Gets or sets the Redis UI authorization filter.
        /// </summary>
        public IRedisAuthorizationFilter? AuthorizationFilter { get; set; }

        /// <summary>
        /// Gets or sets the CSS link for Bootstrap.
        /// </summary>
        public string CssLink { get; set; } = "https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css";

        /// <summary>
        /// Gets or sets the JavaScript link for Bootstrap.
        /// </summary>
        public string JsLink { get; set; } = "https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js";

        /// <summary>
        /// Gets or sets the cookie name used for CSRF protection.
        /// </summary>
        public string AntiForgeryCookieName { get; set; } = "RedisUI.AntiForgery";

        /// <summary>
        /// Gets or sets the request header name used for CSRF protection.
        /// </summary>
        public string AntiForgeryHeaderName { get; set; } = "X-RedisUI-CSRF";

        /// <summary>
        /// Gets or sets the maximum allowed page size.
        /// </summary>
        public int MaxPageSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum number of SCAN calls allowed while building a single key page.
        /// </summary>
        public int MaxScanIterationsPerPage { get; set; } = 10;

        /// <summary>
        /// Gets or sets the default number of collection items fetched for a value preview.
        /// </summary>
        public int ValuePreviewPageSize { get; set; } = 50;

        /// <summary>
        /// Gets or sets the maximum number of collection items allowed in a single value preview request.
        /// </summary>
        public int MaxValuePreviewPageSize { get; set; } = 500;

        /// <summary>
        /// Gets or sets the default number of string bytes fetched for a value preview.
        /// </summary>
        public int StringPreviewBytes { get; set; } = 4096;

        /// <summary>
        /// Gets or sets the maximum number of string bytes allowed in a single value preview request.
        /// </summary>
        public int MaxStringPreviewBytes { get; set; } = 65536;
    }
}
