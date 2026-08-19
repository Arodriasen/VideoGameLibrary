# Contribuir a VideoGameLibrary

Gracias por el interés en el proyecto. Esta guía cubre lo mínimo para levantar el
entorno, correr los tests y enviar cambios.

## Poner el proyecto en marcha

1. Requisitos: Windows 10/11 de 64 bits, el [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0),
   y un proyecto Postgres gratuito en [Neon](https://neon.tech) (ver "Configurar la base de datos"
   en el [README](README.md)) — la app no arranca sin una cadena de conexión.
2. Clona el repositorio y abre `VideoGameLibrary.sln` con Visual Studio 2022, o desde consola:
   ```
   dotnet run --project VideoGameLibrary.csproj
   ```
3. Al primer arranque la app pedirá la cadena de conexión y te dejará añadir claves de
   API — todas son opcionales, puedes omitirlas (ver la tabla de claves en el [README](README.md)).

## Correr los tests

El proyecto `VideoGameLibrary.Tests` (xUnit) cubre `ImportService`, `GameRepository`,
`MainViewModel` y las utilidades internas de `GameApiService` (normalización de código
de barras, extracción de año, etc.). Los tests de `GameRepository`/`MainViewModel` usan
una base de datos PostgreSQL **local** (no Neon): instala PostgreSQL (por ejemplo
`winget install PostgreSQL.PostgreSQL.17`) con el rol `postgres` y contraseña `Admin123!`
(la que usan los tests y el CI — ver connection string en `GameRepositoryTests.cs`), o
cambia esa contraseña en ambos sitios si prefieres otra:

```
dotnet test VideoGameLibrary.Tests\VideoGameLibrary.Tests.csproj
```

El CI (`.github/workflows/build.yml`) instala PostgreSQL en el runner, compila, verifica
el formato y corre estos tests en cada push a `master`/`features/*` y en cada Pull Request
contra `master` — un PR con el build en rojo no se puede fusionar con garantías.

## Estilo de código

El repositorio incluye un `.editorconfig` en la raíz con la indentación y convenciones
del proyecto (Visual Studio y VS Code lo aplican automáticamente al escribir o dar
formato). El CI ejecuta `dotnet format VideoGameLibrary.sln --verify-no-changes` y falla
si algo no cumple el `.editorconfig` (imports desordenados, fin de línea, etc.). Antes de
subir cambios, corrige cualquier aviso en local con:

```
dotnet format VideoGameLibrary.sln
```

## Flujo de ramas

- `master` es la rama estable — de ahí salen las releases.
- El trabajo nuevo entra por `features/dev` (o una rama propia a partir de ella) y se
  fusiona a `master` cuando está probado.
- Para un cambio pequeño, una rama propia a partir de `features/dev` y un Pull Request
  contra `master` es el camino más simple.

## Antes de abrir un Pull Request

- Que el proyecto compile y los tests pasen en local (`dotnet build` / `dotnet test`).
- No incluir claves de API, cadenas de conexión reales (Neon u otras), rutas de disco
  personales, ni el archivo `config.json` — todo eso vive fuera del repo (ver `.gitignore`).
- Describe brevemente el qué y el porqué del cambio en la descripción del PR.

## Reportar un bug o proponer una idea

Abre un [issue](https://github.com/Arodriasen/VideoGameLibrary/issues) describiendo
qué esperabas que pasara y qué pasó en realidad. Si es un error, adjunta el log
correspondiente si puedes — se genera en `%AppData%\VideoGameLibrary\logs\` y también
es visible desde el icono ⚠ de la barra de herramientas dentro de la app.
