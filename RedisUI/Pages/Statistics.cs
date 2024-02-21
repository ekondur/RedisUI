using RedisUI.Models;
using System.Text;
using RedisUI.Helpers;
using RedisUI.Contents;

namespace RedisUI.Pages
{
    public static class Statistics
    {
        public static string Build(StatisticsVm model)
        {
            var tbody = new StringBuilder();
            foreach (var keyspace in model.Keyspaces)
            {
                tbody.Append($"<tr><td>{keyspace.Db}</td><td>{keyspace.Keys}</td><td>{keyspace.Expires}</td><td>{keyspace.Avg_Ttl}</td></tr>");
            }

            var tbodyInfo = new StringBuilder();
            foreach (var info in model.AllInfo)
            {
                tbodyInfo.Append($"<tr><td>{info.Key}</td><td>{info.Value}</td></tr>");
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
                    <li class=""list-group-item"">Redis Version: <strong>{model.Server.RedisVersion}</strong></li>
                    <li class=""list-group-item"">Redis Mode: <strong>{model.Server.RedisMode}</strong></li>
                    <li class=""list-group-item"">TCP Port: <strong>{model.Server.TcpPort}</strong></li>
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
                    <li class=""list-group-item"">Total Connections Received: <strong>{model.Stats.TotalConnectionsReceived}</strong></li>
                    <li class=""list-group-item"">Total Commands Processed: <strong>{model.Stats.TotalCommandsProcessed}</strong></li>
                    <li class=""list-group-item"">Expired Keys: <strong>{model.Stats.ExpiredKeys}</strong></li>
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
