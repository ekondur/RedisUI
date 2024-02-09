namespace RedisUI.Pages
{
    public static class MainLayout
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
        }}

        .table {{
            margin-top: 5px;
        }}

        .table tr {{
            cursor: pointer;
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
            <a class=""navbar-brand"" title=""Keys"">
                <svg xmlns=""http://www.w3.org/2000/svg"" width=""20"" height=""20"" fill=""currentColor"" class=""bi bi-key"" viewBox=""0 0 16 16"">
                  <path d=""M0 8a4 4 0 0 1 7.465-2H14a.5.5 0 0 1 .354.146l1.5 1.5a.5.5 0 0 1 0 .708l-1.5 1.5a.5.5 0 0 1-.708 0L13 9.207l-.646.647a.5.5 0 0 1-.708 0L11 9.207l-.646.647a.5.5 0 0 1-.708 0L9 9.207l-.646.647A.5.5 0 0 1 8 10h-.535A4 4 0 0 1 0 8m4-3a3 3 0 1 0 2.712 4.285A.5.5 0 0 1 7.163 9h.63l.853-.854a.5.5 0 0 1 .708 0l.646.647.646-.647a.5.5 0 0 1 .708 0l.646.647.646-.647a.5.5 0 0 1 .708 0l.646.647.793-.793-1-1h-6.63a.5.5 0 0 1-.451-.285A3 3 0 0 0 4 5""/>
                  <path d=""M4 8a1 1 0 1 1-2 0 1 1 0 0 1 2 0""/>
                </svg>      
                {dbSize}
            </a>
            <a class=""navbar-brand"" title=""Statistics"" href=""..{settings.Path}/statistics"">
                <svg xmlns=""http://www.w3.org/2000/svg"" width=""28"" height=""28"" fill=""currentColor"" class=""bi bi-speedometer2"" viewBox=""0 0 16 16"">
                  <path d=""M8 4a.5.5 0 0 1 .5.5V6a.5.5 0 0 1-1 0V4.5A.5.5 0 0 1 8 4M3.732 5.732a.5.5 0 0 1 .707 0l.915.914a.5.5 0 1 1-.708.708l-.914-.915a.5.5 0 0 1 0-.707M2 10a.5.5 0 0 1 .5-.5h1.586a.5.5 0 0 1 0 1H2.5A.5.5 0 0 1 2 10m9.5 0a.5.5 0 0 1 .5-.5h1.5a.5.5 0 0 1 0 1H12a.5.5 0 0 1-.5-.5m.754-4.246a.39.39 0 0 0-.527-.02L7.547 9.31a.91.91 0 1 0 1.302 1.258l3.434-4.297a.39.39 0 0 0-.029-.518z""/>
                  <path fill-rule=""evenodd"" d=""M0 10a8 8 0 1 1 15.547 2.661c-.442 1.253-1.845 1.602-2.932 1.25C11.309 13.488 9.475 13 8 13c-1.474 0-3.31.488-4.615.911-1.087.352-2.49.003-2.932-1.25A8 8 0 0 1 0 10m8-7a7 7 0 0 0-6.603 9.329c.203.575.923.876 1.68.63C4.397 12.533 6.358 12 8 12s3.604.532 4.923.96c.757.245 1.477-.056 1.68-.631A7 7 0 0 0 8 3""/>
                </svg>
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
