using Microsoft.AspNetCore.Identity;
using NetKubernetes.Models;

namespace NetKubernetes.Data;

public class LoadDatabase {

    public static async Task InsertarData(AppDbContext context, UserManager<Usuario> usuarioManager)
    {
        // Usuario de demostracion para poder probar la API recien levantada.
        // Credenciales documentadas en el README; no uses este seed en produccion.
        if(!usuarioManager.Users.Any())
        {
            var usuario = new Usuario {
                Nombre = "Usuario",
                Apellido = "Demo",
                Email = "demo@netkubernetes.local",
                UserName = "demo",
                Telefono = "3000000000"
            };

            await usuarioManager.CreateAsync(usuario, "Demo_Password123$");

        }

        if(!context.Inmuebles!.Any())
        {
            context.Inmuebles!.AddRange(
                new Inmueble{
                    Nombre = "Casa de Playa",
                    Direccion = "Av. El Sol 32",
                    Precio = 4500M,
                    FechaCreacion = DateTime.Now
                },
                new Inmueble{
                    Nombre = "Casa de Invierno",
                    Direccion = "Av. La Roca 101",
                    Precio = 3500M,
                    FechaCreacion = DateTime.Now
                }
            );
        }
        
        context.SaveChanges();
    }

}