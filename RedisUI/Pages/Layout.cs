using RedisUI.Contents;

namespace RedisUI.Pages
{
    public static class Layout
    {
        public static string Build(string section, string dbSize, int db, RedisUISettings settings)
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Redis Integrated UI</title>
    <link href=""{settings.CssLink}"" rel=""stylesheet"" crossorigin=""anonymous"">
    <script src=""{settings.JsLink}"" crossorigin=""anonymous""></script>

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

    function setdb(db){{
        var currentPath = window.location.href.replace(window.location.search, '');
        window.location = currentPath.replace('#', '') + '?page=0&db=' + db;
    }}

    function setSize(size){{
        let currentSize = 10;
        let currentKey = '';
        let currentDb = 0;

        var searchParams = new URLSearchParams(window.location.search);
        var paramDb = searchParams.get('db');
        var paramKey = searchParams.get('key');
        var paramSize = searchParams.get('size');

        if (paramDb) {{
            currentDb = paramDb;
		}}

        if (paramKey) {{
            currentKey = paramKey;
        }}

        if (paramSize) {{
            currentSize = paramSize;
        }}

        var currentPath = window.location.href.replace(window.location.search, '');

		newQueryString = ""&db="" + currentDb + ""&size="" + size + ""&key="" + currentKey;

		newUrl = currentPath + (currentPath.indexOf('?') !== -1 ? '&' : '?') + newQueryString;
		
        window.location = newUrl.replace('#', '');
    }}

</script>

</head>
<body>
    
    <nav class=""navbar navbar-expand-lg bg-dark navbar-dark"">
        <div class=""container-fluid"">
            <a class=""navbar-brand"" href=""..{settings.Path}"">RedisUI</a>
            <div class=""collapse navbar-collapse"" id=""navbarSupportedContent"">
              <ul class=""navbar-nav me-auto mb-2 mb-lg-0"">
                <a class=""navbar-brand"" title=""Keys"">
                    {Icons.KeyLg}      
                    {dbSize}
                </a>
                <li class=""nav-item dropdown"">
                  <a id=""dblink"" class=""nav-link dropdown-toggle"" href=""#"" role=""button"" data-bs-toggle=""dropdown"" aria-expanded=""false"">
                    DB ({db})
                  </a>
                  <ul class=""dropdown-menu"">
                    <li><a class=""dropdown-item"" id=""nav0"" href=""javascript:setdb(0);"">0</a></li>
                    <li><a class=""dropdown-item"" id=""nav1"" href=""javascript:setdb(1);"">1</a></li>
                    <li><a class=""dropdown-item"" id=""nav2"" href=""javascript:setdb(2);"">2</a></li>
                    <li><a class=""dropdown-item"" id=""nav3"" href=""javascript:setdb(3);"">3</a></li>
                    <li><a class=""dropdown-item"" id=""nav4"" href=""javascript:setdb(4);"">4</a></li>
                    <li><a class=""dropdown-item"" id=""nav5"" href=""javascript:setdb(5);"">5</a></li>
                    <li><a class=""dropdown-item"" id=""nav6"" href=""javascript:setdb(6);"">6</a></li>
                    <li><a class=""dropdown-item"" id=""nav7"" href=""javascript:setdb(7);"">7</a></li>
                    <li><a class=""dropdown-item"" id=""nav8"" href=""javascript:setdb(8);"">8</a></li>
                    <li><a class=""dropdown-item"" id=""nav9"" href=""javascript:setdb(9);"">9</a></li>
                    <li><a class=""dropdown-item"" id=""nav10"" href=""javascript:setdb(10);"">10</a></li>
                    <li><a class=""dropdown-item"" id=""nav11"" href=""javascript:setdb(11);"">11</a></li>
                    <li><a class=""dropdown-item"" id=""nav12"" href=""javascript:setdb(12);"">12</a></li>
                    <li><a class=""dropdown-item"" id=""nav13"" href=""javascript:setdb(13);"">13</a></li>
                    <li><a class=""dropdown-item"" id=""nav14"" href=""javascript:setdb(14);"">14</a></li>
                    <li><a class=""dropdown-item"" id=""nav15"" href=""javascript:setdb(15);"">15</a></li>
                  </ul> 
                </li>
              </ul>
            </div>            
            <a class=""navbar-brand"" title=""Statistics"" href=""..{settings.Path}/statistics"">
                {Icons.Statistic}
            </a>
        </div>
    </nav>

    <div class=""container"">
        <br/>
            {section}
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
    }
}
