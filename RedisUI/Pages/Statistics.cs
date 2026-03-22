using System.Text;
using System.Text.Encodings.Web;
using RedisUI.Contents;
using RedisUI.Helpers;
using RedisUI.Models;

namespace RedisUI.Pages
{
    public static class Statistics
    {
        public static string Build(StatisticsVm model)
        {
            var encoder = HtmlEncoder.Default;
            var tbody = new StringBuilder();

            foreach (var keyspace in model.Keyspaces)
            {
                tbody.Append($@"<tr><td>{encoder.Encode(keyspace.Db)}</td><td>{encoder.Encode(keyspace.Keys)}</td><td>{encoder.Encode(keyspace.Expires)}</td><td>{encoder.Encode(keyspace.Avg_Ttl)}</td></tr>");
            }

            var tbodyInfo = new StringBuilder();
            foreach (var info in model.AllInfo)
            {
                tbodyInfo.Append($@"<tr><td>{encoder.Encode(info.Key)}</td><td>{encoder.Encode(info.Value)}</td></tr>");
            }

            return $@"
<div class=""row"">
    <div class=""col-4"">
        <div class=""card border-info mb-3 sticky-top"">
            <div class=""card-header"">
                <strong>
                    <span>
                        {Icons.Server}
                    </span>Server
                </strong>
            </div>
            <div class=""card-body"">
                <ul class=""list-group list-group-flush"">
                    <li class=""list-group-item"">Redis Version: <strong>{encoder.Encode(model.Server.RedisVersion)}</strong></li>
                    <li class=""list-group-item"">Redis Mode: <strong>{encoder.Encode(model.Server.RedisMode)}</strong></li>
                    <li class=""list-group-item"">TCP Port: <strong>{encoder.Encode(model.Server.TcpPort)}</strong></li>
                </ul>
            </div>
        </div>
    </div>
    <div class=""col-4"">
        <div class=""card border-info mb-3 sticky-top"">
            <div class=""card-header"">
                <strong>
                    <span>
                        {Icons.Memory}
                    </span>Memory
                </strong>
            </div>
            <div class=""card-body"">
                <ul class=""list-group list-group-flush"">
                    <li class=""list-group-item"">Used Memory: <strong>{model.Memory.UsedMemory.ToMegabytes()}</strong>M</li>
                    <li class=""list-group-item"">Used Memory Peak: <strong>{model.Memory.UsedMemoryPeak.ToMegabytes()}</strong>M</li>
                    <li class=""list-group-item"">Used Memory Lua: <strong>{model.Memory.UsedMemoryLua.ToMegabytes()}</strong>M</li>
                </ul>
            </div>
        </div>
    </div>
    <div class=""col-4"">
        <div class=""card border-info mb-3 sticky-top"">
            <div class=""card-header"">
                <strong>
                    <span>
                        {Icons.Stats}
                    </span>Stats
                </strong>
            </div>
            <div class=""card-body"">
                <ul class=""list-group list-group-flush"">
                    <li class=""list-group-item"">Total Connections Received: <strong>{encoder.Encode(model.Stats.TotalConnectionsReceived)}</strong></li>
                    <li class=""list-group-item"">Total Commands Processed: <strong>{encoder.Encode(model.Stats.TotalCommandsProcessed)}</strong></li>
                    <li class=""list-group-item"">Expired Keys: <strong>{encoder.Encode(model.Stats.ExpiredKeys)}</strong></li>
                </ul>
            </div>
        </div>
    </div>
</div>
<div class=""row"">
    <div class=""col"">
        <table class=""table table-hover"">
          <thead>
            <tr class=""table-active"">
              <th colspan=""4"">
                <span>
                    {Icons.KeySm}
                </span>Key Statistics
            </th>
            </tr>
            <tr>
              <th scope=""col"">DB</th>
              <th scope=""col"">Keys</th>
              <th scope=""col"">Expires</th>
              <th scope=""col"">Avg Ttl</th>
            </tr>
          </thead>
          <tbody>
            {tbody}
          </tbody>
        </table>
    </div>
</div>
<div class=""row"">
    <div class=""col"">
        <table class=""table table-hover"">
          <thead>
            <tr class=""table-active"">
              <th colspan=""4"">
                <span>
                    {Icons.Info}
                </span>All Information
            </th>
            </tr>
            <tr>
              <th scope=""col"">Key</th>
              <th scope=""col"">Value</th>
            </tr>
          </thead>
          <tbody>
             {tbodyInfo}
          </tbody>
        </table>
    </div>
</div>
";
        }
    }
}
