using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using RedisUI.Contents;
using RedisUI.Models;

namespace RedisUI.Pages
{
    public static class Layout
    {
        public static string Build(LayoutModel model, RedisUISettings settings)
        {
            var encoder = HtmlEncoder.Default;
            var basePath = NormalizePath(settings.Path);
            var dbList = new StringBuilder();

            foreach (var item in model.DbList)
            {
                dbList.Append($@"<li><button type=""button"" class=""dropdown-item"" id=""nav{encoder.Encode(item)}"" data-db=""{encoder.Encode(item)}"">{encoder.Encode(item)}</button></li>");
            }

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Redis Integrated UI</title>
    <link href=""{encoder.Encode(settings.CssLink)}"" rel=""stylesheet"" crossorigin=""anonymous"">
    <script src=""{encoder.Encode(settings.JsLink)}"" crossorigin=""anonymous""></script>

    <style>
        .pagination {{
            display: flex;
            justify-content: center;
            margin-top: 5px;
        }}

        .table {{
            margin-top: 5px;
        }}

        .dropdown-menu {{
            z-index: 1021;
        }}

    </style>

<script>
    window.redisUi = {{
        basePath: {JsonSerializer.Serialize(basePath)},
        csrfToken: {JsonSerializer.Serialize(model.AntiForgeryToken)},
        csrfHeaderName: {JsonSerializer.Serialize(settings.AntiForgeryHeaderName)}
    }};

    window.buildRedisUiUrl = function (overrides) {{
        const next = overrides || {{}};
        const current = new URLSearchParams(window.location.search);
        const hasOwn = function (key) {{
            return Object.prototype.hasOwnProperty.call(next, key);
        }};

        const db = hasOwn('db') ? next.db : (current.get('db') || '0');
        const size = hasOwn('size') ? next.size : (current.get('size') || '10');
        const page = hasOwn('page') ? next.page : (current.get('page') || '0');
        const key = hasOwn('key') ? next.key : (current.get('key') || '');

        const query = new URLSearchParams();
        query.set('page', String(page));
        query.set('db', String(db));
        query.set('size', String(size));

        if (key) {{
            query.set('key', key);
        }}

        return window.redisUi.basePath + '?' + query.toString();
    }};

    function setdb(db) {{
        window.location = window.buildRedisUiUrl({{ db: db, page: 0 }});
    }}

    function setSize(size) {{
        window.location = window.buildRedisUiUrl({{ size: size, page: 0 }});
    }}

    document.addEventListener('DOMContentLoaded', function () {{
        document.querySelectorAll('[data-db]').forEach(function (button) {{
            button.addEventListener('click', function () {{
                setdb(button.dataset.db);
            }});
        }});
    }});
</script>

</head>
<body>
    
    <nav class=""navbar navbar-expand-lg bg-dark navbar-dark"">
        <div class=""container-fluid"">
            <a class=""navbar-brand"" href=""{encoder.Encode(basePath)}"">RedisUI</a>
            <div class=""collapse navbar-collapse"" id=""navbarSupportedContent"">
              <ul class=""navbar-nav me-auto mb-2 mb-lg-0"">
                <a class=""navbar-brand"" title=""Keys"">
                    {Icons.KeyLg}
                    {encoder.Encode(model.DbSize)}
                </a>
                <li class=""nav-item dropdown"">
                  <a id=""dblink"" class=""nav-link dropdown-toggle"" href=""#"" role=""button"" data-bs-toggle=""dropdown"" aria-expanded=""false"">
                    DB ({model.CurrentDb})
                  </a>
                  <ul class=""dropdown-menu"">
                    {dbList}
                  </ul>
                </li>
              </ul>
            </div>
            <a class=""navbar-brand"" title=""Statistics"" href=""{encoder.Encode(basePath)}/statistics"">
                {Icons.Statistic}
            </a>
        </div>
    </nav>

    <div class=""container"">
        <br/>
            {model.Section}
    </div>

    <div class=""container"">
        <div class=""row"">
        <footer class=""d-flex flex-wrap justify-content-between align-items-center py-3 my-4 border-top"">
            <div class=""col-md-4 d-flex align-items-center"">
              <span class=""mb-3 mb-md-0 text-body-secondary"">© 2024 Redis Integrated UI</span>
            </div>
        </footer>
    </div></div>
</body>
</html>
";
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/redis";
            }

            var normalized = path.StartsWith('/') ? path : "/" + path;
            return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
        }
    }
}
