using System.Text.Encodings.Web;
using System.Text.Json;
using RedisUI.Models;

namespace RedisUI.Pages;

public static class Layout
{
    public static string Build(LayoutModel model, RedisUISettings settings)
    {
        var encode = HtmlEncoder.Default;
        var config = JsonSerializer.Serialize(new
        {
            basePath = settings.Path, csrfToken = model.AntiForgeryToken, csrfHeaderName = settings.AntiForgeryHeaderName,
            connection = model.Connection, database = model.CurrentDb, canEdit = model.CanEdit, canDelete = model.CanDelete,
            canAudit = model.CanAudit, canEditElements = model.CanEditElements,
            stringPreviewCount = Math.Clamp(settings.StringPreviewBytes, 1, Math.Max(1, settings.MaxStringPreviewBytes)),
            collectionPreviewCount = Math.Clamp(settings.ValuePreviewPageSize, 1, Math.Max(1, settings.MaxValuePreviewPageSize)),
            maxPageSize = Math.Max(1, settings.MaxPageSize)
        });
        var connections = new[] { "default" }.Concat(settings.Connections.Select(x => x.Name));
        var options = string.Join("", connections.Select(name => $"<option value=\"{encode.Encode(name)}\"{(name == model.Connection ? " selected" : "")}>{encode.Encode(name)}</option>"));
        var databases = string.Join("", model.DbList.Select(db => db.StartsWith("db") ? db[2..] : db)
            .Append(model.CurrentDb.ToString()).Distinct().Select(db => $"<option value=\"{encode.Encode(db)}\"></option>"));
        return $$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>RedisUI · {{encode.Encode(model.Environment)}}</title>
            <link rel="stylesheet" href="{{encode.Encode(settings.CssLink)}}" crossorigin="anonymous">
            <style>{{Resources.Read("app.css")}}</style></head><body>
            <header class="topbar"><a id="keysLink" class="brand" href="{{encode.Encode(settings.Path)}}">RedisUI</a>
            <span class="environment {{(model.Environment.Equals("production", StringComparison.OrdinalIgnoreCase) ? "production" : "")}}">{{encode.Encode(model.Environment)}}</span>
            <label>Connection <select id="connectionSelect">{{options}}</select></label>
            <label>Database <input id="databaseInput" type="number" min="0" list="databases" value="{{model.CurrentDb}}"></label>
            <datalist id="databases">{{databases}}</datalist><button id="databaseGo">Open DB</button>
            <span><span id="databaseSize">{{encode.Encode(model.DbSize)}}</span> keys</span>
            <span class="permissions">{{(model.CanEdit ? "Can edit" : "Editing disabled")}} · {{(model.CanDelete ? "Can delete" : "Deletion disabled")}}</span>
            <a id="statisticsLink" href="{{encode.Encode(settings.Path)}}/statistics">Statistics</a>
            </header>
            <main><div id="notice" role="status" aria-live="polite" hidden></div>{{model.Section}}</main>
            <script id="redis-config" type="application/json">{{config}}</script>
            <script>{{Resources.Read("app.js")}}</script>
            </body></html>
            """;
    }
}
