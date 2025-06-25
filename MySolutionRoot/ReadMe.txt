Чтобы запустить докер надо 
В Program.cs поменять айпи и порт (builder.WebHost.UseUrls("http://localhost:5000"); //поменять для докера на http://0.0.0.0:80)

перейти в командной строке в папку Server
1-ая команда - docker build -t my-server .
2-ая команда - docker run -p 5000:80 my-server