﻿using RedisUI.Models;
using System.Text;
using RedisUI.Helpers;

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

            return @$"
<div class=""row"">
    <div class=""col-4"">
        <div class=""card border-info mb-3 sticky-top"">
            <div class=""card-header"">
                <span>
                    <svg xmlns=""http://www.w3.org/2000/svg"" width=""16"" height=""16"" fill=""currentColor"" class=""bi bi-hdd-stack"" viewBox=""0 0 16 16"">
                      <path d=""M14 10a1 1 0 0 1 1 1v1a1 1 0 0 1-1 1H2a1 1 0 0 1-1-1v-1a1 1 0 0 1 1-1zM2 9a2 2 0 0 0-2 2v1a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-1a2 2 0 0 0-2-2z""/>
                      <path d=""M5 11.5a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0m-2 0a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0M14 3a1 1 0 0 1 1 1v1a1 1 0 0 1-1 1H2a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1zM2 2a2 2 0 0 0-2 2v1a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2z""/>
                      <path d=""M5 4.5a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0m-2 0a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0""/>
                    </svg>
                </span>Server
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
                <span>
                    <svg xmlns=""http://www.w3.org/2000/svg"" width=""16"" height=""16"" fill=""currentColor"" class=""bi bi-sd-card"" viewBox=""0 0 16 16"">
                      <path d=""M6.25 3.5a.75.75 0 0 0-1.5 0v2a.75.75 0 0 0 1.5 0zm2 0a.75.75 0 0 0-1.5 0v2a.75.75 0 0 0 1.5 0zm2 0a.75.75 0 0 0-1.5 0v2a.75.75 0 0 0 1.5 0zm2 0a.75.75 0 0 0-1.5 0v2a.75.75 0 0 0 1.5 0z""/>
                      <path fill-rule=""evenodd"" d=""M5.914 0H12.5A1.5 1.5 0 0 1 14 1.5v13a1.5 1.5 0 0 1-1.5 1.5h-9A1.5 1.5 0 0 1 2 14.5V3.914c0-.398.158-.78.44-1.06L4.853.439A1.5 1.5 0 0 1 5.914 0M13 1.5a.5.5 0 0 0-.5-.5H5.914a.5.5 0 0 0-.353.146L3.146 3.561A.5.5 0 0 0 3 3.914V14.5a.5.5 0 0 0 .5.5h9a.5.5 0 0 0 .5-.5z""/>
                    </svg>
                </span>Memory
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
                <span>
                    <svg xmlns=""http://www.w3.org/2000/svg"" width=""16"" height=""16"" fill=""currentColor"" class=""bi bi-speedometer2"" viewBox=""0 0 16 16"">
                      <path d=""M8 4a.5.5 0 0 1 .5.5V6a.5.5 0 0 1-1 0V4.5A.5.5 0 0 1 8 4M3.732 5.732a.5.5 0 0 1 .707 0l.915.914a.5.5 0 1 1-.708.708l-.914-.915a.5.5 0 0 1 0-.707M2 10a.5.5 0 0 1 .5-.5h1.586a.5.5 0 0 1 0 1H2.5A.5.5 0 0 1 2 10m9.5 0a.5.5 0 0 1 .5-.5h1.5a.5.5 0 0 1 0 1H12a.5.5 0 0 1-.5-.5m.754-4.246a.39.39 0 0 0-.527-.02L7.547 9.31a.91.91 0 1 0 1.302 1.258l3.434-4.297a.39.39 0 0 0-.029-.518z""/>
                      <path fill-rule=""evenodd"" d=""M0 10a8 8 0 1 1 15.547 2.661c-.442 1.253-1.845 1.602-2.932 1.25C11.309 13.488 9.475 13 8 13c-1.474 0-3.31.488-4.615.911-1.087.352-2.49.003-2.932-1.25A8 8 0 0 1 0 10m8-7a7 7 0 0 0-6.603 9.329c.203.575.923.876 1.68.63C4.397 12.533 6.358 12 8 12s3.604.532 4.923.96c.757.245 1.477-.056 1.68-.631A7 7 0 0 0 8 3""/>
                    </svg>
                </span>Stats
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
                    <svg xmlns=""http://www.w3.org/2000/svg"" width=""16"" height=""16"" fill=""currentColor"" class=""bi bi-key"" viewBox=""0 0 16 16"">
                      <path d=""M0 8a4 4 0 0 1 7.465-2H14a.5.5 0 0 1 .354.146l1.5 1.5a.5.5 0 0 1 0 .708l-1.5 1.5a.5.5 0 0 1-.708 0L13 9.207l-.646.647a.5.5 0 0 1-.708 0L11 9.207l-.646.647a.5.5 0 0 1-.708 0L9 9.207l-.646.647A.5.5 0 0 1 8 10h-.535A4 4 0 0 1 0 8m4-3a3 3 0 1 0 2.712 4.285A.5.5 0 0 1 7.163 9h.63l.853-.854a.5.5 0 0 1 .708 0l.646.647.646-.647a.5.5 0 0 1 .708 0l.646.647.646-.647a.5.5 0 0 1 .708 0l.646.647.793-.793-1-1h-6.63a.5.5 0 0 1-.451-.285A3 3 0 0 0 4 5""/>
                      <path d=""M4 8a1 1 0 1 1-2 0 1 1 0 0 1 2 0""/>
                </svg>
                </span>Key Statistics
            </th>
            </tr>
            <tr>
              <th scope=""col"">DB</th>
              <th scope=""col"">Keys</th>
              <th scope=""col"">Expires</th>
              <th scope=""col"">Avg_Ttl</th>
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
                    <svg xmlns=""http://www.w3.org/2000/svg"" width=""16"" height=""16"" fill=""currentColor"" class=""bi bi-list-ul"" viewBox=""0 0 16 16"">
                      <path fill-rule=""evenodd"" d=""M5 11.5a.5.5 0 0 1 .5-.5h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1-.5-.5m0-4a.5.5 0 0 1 .5-.5h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1-.5-.5m0-4a.5.5 0 0 1 .5-.5h9a.5.5 0 0 1 0 1h-9a.5.5 0 0 1-.5-.5m-3 1a1 1 0 1 0 0-2 1 1 0 0 0 0 2m0 4a1 1 0 1 0 0-2 1 1 0 0 0 0 2m0 4a1 1 0 1 0 0-2 1 1 0 0 0 0 2""/>
                    </svg>
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
