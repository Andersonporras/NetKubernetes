# Capturas del README

Las imágenes que aparecen en [`README.es.md`](../../README.es.md) y
[`README.md`](../../README.md).

| Archivo | Qué muestra |
| --- | --- |
| `swagger-endpoints.png` | Swagger UI con los 7 endpoints y el botón **Authorize** |
| `swagger-login.png` | Respuesta de `POST /api/Usuario/login` con el token JWT |

## Cómo regenerarlas

Levanta la API con cualquiera de las dos opciones:

```bash
# Opción A: todo con Docker (descarga SQL Server la primera vez)
cp .env.example .env     # edita los valores
docker compose up --build

# Opción B: con un SQL Server que ya tengas
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:SQLServerConnection" "Data Source=localhost;Initial Catalog=InmuebleDB;User Id=sa;Password=TuPassword123$;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "una-clave-aleatoria-de-32-caracteres-o-mas"
dotnet run
```

Abre `http://localhost:5000/swagger`.

**`swagger-endpoints.png`** — La vista inicial, con los grupos `Inmueble` y `Usuario`
desplegados y el botón *Authorize* visible.

**`swagger-login.png`** — Ejecuta `POST /api/Usuario/login` con el usuario de
demostración y captura la respuesta:

```json
{
  "email": "demo@netkubernetes.local",
  "password": "Demo_Password123$"
}
```

Si tu base ya tiene usuarios, el seed no corre y ese usuario no existe. Créalo antes
con `POST /api/Usuario/registrar`:

```json
{
  "nombre": "Usuario",
  "apellido": "Demo",
  "telefono": "3000000000",
  "email": "demo@netkubernetes.local",
  "userName": "demo",
  "password": "Demo_Password123$"
}
```

## Antes de subir una captura

- **Usa siempre el usuario de demostración.** Swagger muestra el cuerpo de la petición
  en el bloque *Curl*, contraseña incluida, y la respuesta trae correo y teléfono. Con
  una cuenta real eso queda publicado en el repositorio.
- Captura solo la ventana del navegador, no la pantalla completa.
- Ancho de unos 1500 px: nítido en GitHub sin que el archivo pese de más.
