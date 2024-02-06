namespace RedisUI
{
    public class RedisUISettings
    {
        /// <summary>
        /// Gets or sets the connection string for the Redis server.
        /// </summary>
        public string ConnectionString { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the path for the Redis server.
        /// </summary>
        public string Path { get; set; } = "/redis";
    }
}
