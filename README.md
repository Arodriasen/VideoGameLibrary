# Mi Colección de Juegos

Aplicación de escritorio para Windows que permite catalogar tu colección personal de videojuegos escaneando el código de barras (UPC/EAN) de la caja. Busca automáticamente título, plataforma, género, año y portada, y guarda todo en una base de datos local (SQLite) en tu propio equipo.

## Características

- Alta rapidez escaneando el código de barras (con lector USB o escribiéndolo a mano).
- Búsqueda manual por nombre si el código de barras no encuentra el juego.
- Ficha con portada, plataforma, género, año y notas.
- Puntuación por estrellas (1 a 5) y filtro por puntuación.
- Filtros por plataforma, género y año.
- Exportación de la colección a Excel (.xlsx) o CSV.
- Tema claro/oscuro.
- Registro de errores dentro de la app (icono ⚠ en la barra de herramientas) para diagnosticar problemas sin depurador.
- Todos los datos se guardan localmente: no hay cuentas ni servidores propios.

## Requisitos

- Windows 10/11 de 64 bits.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) para compilar el proyecto (no hace falta si solo vas a ejecutar un `.exe` ya publicado).

## Compilar y ejecutar

1. Clona el repositorio.
2. Para probarlo directamente sin generar un ejecutable:
   ```
   dotnet run --project VideoGameLibrary.csproj
   ```
3. Para generar un `.exe` autocontenido (no necesita tener .NET instalado en el equipo donde se ejecute):
   ```
   dotnet publish VideoGameLibrary.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
   ```
   El ejecutable quedará en la carpeta `publish\VideoGameLibrary.exe`.

También puedes abrir `VideoGameLibrary.sln` directamente con Visual Studio 2022.

> **Nota:** al no estar firmado digitalmente, Windows SmartScreen puede mostrar un aviso la primera vez que ejecutes el `.exe` ("Más información" → "Ejecutar de todas formas").

## Primer uso

Al arrancar por primera vez, la app te pedirá elegir o crear el archivo de base de datos (`.db`) donde se guardará tu colección, y te abrirá la ventana de Ajustes para introducir claves de API (todas opcionales, ver más abajo). Puedes omitir este paso y añadirlas más tarde desde el icono de engranaje de la barra de herramientas.

## Claves de API (opcionales)

La app funciona sin ninguna clave: el escaneo usará únicamente UPCitemdb, que no requiere registro. Añadir claves mejora la tasa de acierto y la calidad de los datos (portadas, género, plataforma):

| Servicio | Para qué se usa | Dónde conseguirla |
|---|---|---|
| [ScanDex](https://scandex.gamery.app/) | Resolución del código de barras específica de videojuegos | Web de ScanDex |
| [IGDB](https://api-docs.igdb.com/) | Enriquecimiento por nombre (portada, género, plataforma) | Client ID y Secret desde una app registrada en la [consola de desarrolladores de Twitch](https://dev.twitch.tv/console/apps) |
| [RAWG](https://rawg.io/apidocs) | Enriquecimiento por nombre, segunda fuente | Clave gratuita en rawg.io |
| [TheGamesDB](https://thegamesdb.net/) | Portada como último recurso | Clave gratuita solicitándola en su foro |

Ninguna clave se sube a ningún sitio: se guardan solo en tu equipo, en `%AppData%\VideoGameLibrary\config.json`.

## Tecnologías

WPF (.NET 8), Entity Framework Core + SQLite, CommunityToolkit.Mvvm, MaterialDesignInXAML, ClosedXML.
