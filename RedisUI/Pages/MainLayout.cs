namespace RedisUI.Pages
{
    public static class MainLayout
    {
        public static string Build(string section)
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Redis Integrated UI</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            margin: 0;
            padding: 0;
        }}

        header {{
            background-color: #333;
            color: #fff;
            padding: 5px;
            text-align: center;
        }}

        nav {{
            background-color: #eee;
            padding: 10px;
        }}

        nav a {{
            text-decoration: none;
            color: #333;
            margin: 0 5px;
        }}

        section {{
            padding: 20px;
        }}

        footer {{
            background-color: #333;
            color: #fff;
            text-align: center;
            padding: 10px;
            position: fixed;
            bottom: 0;
            width: 100%;
        }}

        .active {{
            font-weight: bolder;
            text-decoration: underline;
        }}
    </style>

<script>
    function setdb(db){{
        var currentPath = window.location.href.replace(window.location.search, '');
        window.location = currentPath + '?page=0&db=' + db;
    }}
</script>

</head>
<body>
    <header>
        <h2>RedisUI</h2>
    </header>
    
    <nav>
        <a>DB</a>
        <a id=""nav0"" href=""javascript:setdb(0);"">0</a>
        <a id=""nav1"" href=""javascript:setdb(1);"">1</a>
        <a id=""nav2"" href=""javascript:setdb(2);"">2</a>
        <a id=""nav3"" href=""javascript:setdb(3);"">3</a>
        <a id=""nav4"" href=""javascript:setdb(4);"">4</a>
        <a id=""nav5"" href=""javascript:setdb(5);"">5</a>
        <a id=""nav6"" href=""javascript:setdb(6);"">6</a>
        <a id=""nav7"" href=""javascript:setdb(7);"">7</a>
        <a id=""nav8"" href=""javascript:setdb(8);"">8</a>
        <a id=""nav9"" href=""javascript:setdb(9);"">9</a>
        <a id=""nav10"" href=""javascript:setdb(10);"">10</a>
        <a id=""nav11"" href=""javascript:setdb(11);"">11</a>
        <a id=""nav12"" href=""javascript:setdb(12);"">12</a>
        <a id=""nav13"" href=""javascript:setdb(13);"">13</a>
        <a id=""nav14"" href=""javascript:setdb(14);"">14</a>
        <a id=""nav15"" href=""javascript:setdb(15);"">15</a>
    </nav>

    <section>
        {section}
    </section>

    <footer>
        &copy; 2024 RedisUI
    </footer>
</body>
</html>
";


        }
    }
}
