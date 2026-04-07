using LaAbejita.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System.Diagnostics;

namespace LaAbejita.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration config)
        {
            _logger = logger;
            _configuration = config;
        }

        [HttpGet]
        public IActionResult Registro()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Registro([FromBody] RegisterRequest register)
        {
            if (register == null ||
                string.IsNullOrWhiteSpace(register.Username) ||
                string.IsNullOrWhiteSpace(register.NumeroCelular) ||
                string.IsNullOrWhiteSpace(register.Nombre) ||
                string.IsNullOrWhiteSpace(register.Contrasena) ||
                (register.Rol != RolUsuario.Administrador && register.Rol != RolUsuario.Cocinero))
            {
                return BadRequest("CamposObligatorios");
            }
            if (register.Username.Length < 5 || register.Contrasena.Length < 5)
            {
                return BadRequest("LongitudErronea");
            }
            if (!(register.NumeroCelular.Length == 10))
            {
                return BadRequest("NumeroErroneo");
            }
            var connectionString = _configuration.GetConnectionString("SQLServer");

            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                // Validar Username único
                const string checkUserQuery = "SELECT COUNT(1) FROM Usuarios WHERE Username = @Username";
                using (var cmdCheckUser = new SqlCommand(checkUserQuery, conn))
                {
                    cmdCheckUser.Parameters.AddWithValue("@Username", register.Username);
                    if ((int)cmdCheckUser.ExecuteScalar() > 0)
                    {
                        //el nombre de usuario en uso
                        return BadRequest("UsernameEnUso");
                    }
                }

                // Validar NumeroCelular único
                const string checkPhoneQuery = "SELECT COUNT(1) FROM Usuarios WHERE NumeroCelular = @NumeroCelular";
                using (var cmdCheckPhone = new SqlCommand(checkPhoneQuery, conn))
                {
                    cmdCheckPhone.Parameters.AddWithValue("@NumeroCelular", register.NumeroCelular);
                    if ((int)cmdCheckPhone.ExecuteScalar() > 0)
                    {
                        return BadRequest("TelefonoEnUso");
                    }
                }

                // Si todo está bien, seguimos bien 
                const string query = @"INSERT INTO Restaurante.dbo.Usuarios
            (Nombre, ApellidoPaterno, ApellidoMaterno, Username, Rol, NumeroCelular, Contrasena)
            VALUES (@Nombre, @ApellidoPaterno, @ApellidoMaterno, @Username, @Rol, @NumeroCelular, @Contrasena)";

                using var cmd = new SqlCommand(query, conn) { CommandTimeout = 300 };
                cmd.Parameters.AddWithValue("@Nombre", register.Nombre);
                cmd.Parameters.AddWithValue("@ApellidoPaterno", register.ApellidoPaterno);
                cmd.Parameters.AddWithValue("@ApellidoMaterno", register.ApellidoMaterno);
                cmd.Parameters.AddWithValue("@Username", register.Username);
                cmd.Parameters.AddWithValue("@Rol", (int)register.Rol);
                cmd.Parameters.AddWithValue("@NumeroCelular", register.NumeroCelular);
                cmd.Parameters.AddWithValue("@Contrasena", register.Contrasena);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Ocurrió un error al procesar el registro";
                return StatusCode(500, "Error interno del servidor");
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult UserData([FromBody] LoginRequest login)
        {
            if (login == null ||
                string.IsNullOrWhiteSpace(login.Username) ||
                string.IsNullOrWhiteSpace(login.Contrasena) ||
                (login.Rol != RolUsuarioLogin.Administrador && login.Rol != RolUsuarioLogin.Cocinero))
            {
                return BadRequest("CamposObligatorios");
            }

            var connectionString = _configuration.GetConnectionString("SQLServer");

            using var conn = new SqlConnection(connectionString);
            conn.Open();

            // Obtener el nombre rol y contraseña del usuario
            const string sql = "SELECT Contrasena, Rol FROM Usuarios WHERE Username = @Username";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Username", login.Username);

            using var reader = cmd.ExecuteReader();

            if (reader.Read()) // Si encontró al usuario...
            {
                string contrasenaBD = reader["Contrasena"].ToString();
                int rolBD = Convert.ToInt32(reader["Rol"]);

                // 2. Comparamos la contraseña (Importante: distingue mayúsculas/minúsculas)
                if (contrasenaBD == login.Contrasena)
                {
                    Console.WriteLine("encontrado");
                }
                else if (!(contrasenaBD == login.Contrasena))
                {
                    return BadRequest("ContrasenaIncorrecta");
                }
                // 3. Comparamos el rol
                // Convertimos el Enum a su valor numérico (1 o 2) para comparar contra el int de la BD
                if (rolBD == (int)login.Rol)
                {
                    Console.WriteLine("Encontrado");
                }
                else if (!(rolBD == (int)login.Rol))
                {
                    return BadRequest("RolIncorrecto");
                }
            }
            else
            {
                return BadRequest("UsuarioNoEncontrado");
            }
            return RedirectToAction("Index");
        }


        //saca los platillos que existen en la base de datos y los muestra al usuario en la vista
        [HttpGet]
        public IActionResult Index() //desdpues de que el usuario haya agregado un platillo
        {
            var connectionString = _configuration.GetConnectionString("SQLServer"); //saca la configuración de la cadena de conexión del appsettings.json
            var lista = new List<Inventory>(); //IMPORTANTE: se usara para meter todos los platillos que encuentre en la base de datos
            const string query = @"SELECT Nombre, Descripcion, Precio 
                           FROM Restaurante.dbo.Inventory 
                           WHERE isActive = 1";
            using (var conn = new SqlConnection(connectionString))
            {
                using (var cmd = new SqlCommand(query, conn)) //se crea la conexión a la base de datos y se ejecuta la consulta SQL que obtiene todos los platillos del inventario
                {
                    conn.Open(); 

                    using (var reader = cmd.ExecuteReader()) //lo que dice esto es como: base de datos ejecuta esta consulta y dame acceso a TODOS los resultados de mi tabla inventario
                    {                                        //reader contiene las filas 
                        while (reader.Read()) //Reader recorre cada fila y lee todas las columnas hasta que ya no haya mas 
                        {
                            lista.Add(new Inventory //mete todo en el objeto invebntory
                            {
                                NombrePlatillo = reader["Nombre"].ToString(),
                                Descripcion = reader["Descripcion"]?.ToString(),
                                Precio = Convert.ToDecimal(reader["Precio"])
                            });
                        }
                    }
                }
            }

            //basicamente se le pone de nombre a la lista ListaPlatillo
            ViewBag.ListaPlatillos = lista; //el controlador dice algo asi como: oye vista, toma esta lista de platillos que acabo de obtener de la base de datos y haz lo que quieras con ella, en este caso se la paso a la vista para que la muestre al usuario
            return View();

        }

        //añade los platillos a la base de datos
        [HttpPost]
        public IActionResult GetDataPlate(Inventory inventory)
        {
            var connectionString = _configuration.GetConnectionString("SQLServer"); //saca la configuración de la cadena de conexión del appsettings.json
            const string query = @"INSERT INTO Restaurante.dbo.Inventory 
        (Nombre, Descripcion, Precio)
        VALUES (@Nombre, @Descripcion, @Precio)"; //consulta SQL que inserta un nuevo platillo en la tabla inventory
            try
            {
                using var conn = new SqlConnection(connectionString); //crea una conexión a la base de datos utilizando la cadena de conexión obtenida anteriormente
                using var cmd = new SqlCommand(query, conn) //Crea un nuevo objeto de comando que llevará la instrucción SQL (query) a través de la conexión (conn) creada 
                {
                    CommandTimeout = 300
                };

                cmd.Parameters.AddWithValue("@Nombre", inventory.NombrePlatillo); //agrega el valor que manda el usuario a la base de datos
                cmd.Parameters.AddWithValue("@Descripcion",
                    (object?)inventory.Descripcion ?? DBNull.Value); //agrega el valor que manda el usuario a la base de datos, si es nulo se inserta un valor nulo en la base de datos
                cmd.Parameters.AddWithValue("@Precio", inventory.Precio); //agrega el valor que manda el usuario a la base de datos

                conn.Open(); //abre la conexión a la base de datos
                cmd.ExecuteNonQuery(); //ejecuta la consulta SQL que inserta el nuevo platillo en la tabla inventory, no devuelve ningún resultado, solo ejecuta la consulta

                TempData["SuccessMessage"] = "Platillo añadido correctamente en inventario";

            }
            catch
            {

            }

            return RedirectToAction("Index"); //unha vez el usuario haya agregado el platillo desde el front se va al metodo Index 
        }

        //elimina los platillos de la base de datos
        [HttpPost]
        public IActionResult EliminarPlatillo([FromBody] Inventory data)
        {
            if (data == null || string.IsNullOrEmpty(data.NombrePlatillo))
                return BadRequest("NoHayPlatillo");

            var connectionString = _configuration.GetConnectionString("SQLServer");

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // CAMBIO: UPDATE en lugar de DELETE
                const string sql = "UPDATE Inventory SET IsActive = 0 WHERE Nombre = @Nombre";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Nombre", data.NombrePlatillo);
                    int filas = cmd.ExecuteNonQuery();

                    if (filas > 0)
                    {
                        TempData["SuccessMessage"] = "¡El platillo se ha eliminado correctamente!";
                        return Ok(); // Respondemos que todo salió bien
                    }

                    // 4. Si no se afectaron filas, algo falló
                    return BadRequest("ErrorEliminando");
                }
            }
            }

       

        [HttpGet]
        public IActionResult CrearOrden() //esto es para mostrar la lista de los platillos en la base de datos en la otra vista
        {
            var connectionString = _configuration.GetConnectionString("SQLServer");
            var lista = new List<Inventory>();
            const string query = @"SELECT Id, Nombre, Descripcion, Precio FROM Restaurante.dbo.Inventory WHERE isActive = 1";

            using (var conn = new SqlConnection(connectionString))
            {
                using (var cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new Inventory
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                NombrePlatillo = reader["Nombre"].ToString(),
                                Descripcion = reader["Descripcion"]?.ToString(),
                                Precio = Convert.ToDecimal(reader["Precio"])
                            });
                        }
                    }
                }
            }

            ViewBag.ListaPlatillos = lista; // Pasamos la lista de platillos a la vista
            return View();
        }

        public IActionResult DetalleOrden(int id)
        {
            var connectionString = _configuration.GetConnectionString("SQLServer");

            string nombreOrden = "";
            decimal total = 0;
            string status = "";
            DateTime fecha = DateTime.Now;

            var detalles = new List<dynamic>(); //lista para guardar platillos

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // 🔹 Traer datos de la orden
                var query = "SELECT NombreOrden, Total, Status, Fecha FROM Orden WHERE IdOrden = @Id";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            nombreOrden = reader["NombreOrden"].ToString();
                            total = Convert.ToDecimal(reader["Total"]);
                            status = reader["Status"].ToString();
                            fecha = Convert.ToDateTime(reader["Fecha"]);
                        }
                    }
                }

                // Traer detalles
                var queryDetalles = @"
            SELECT i.Nombre, d.Cantidad, d.Comentarios, d.Subtotal
            FROM OrdenDetalle d
            INNER JOIN Inventory i ON d.IdPlatillo = i.Id
            WHERE d.IdOrden = @Id";

                using (var cmd = new SqlCommand(queryDetalles, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            detalles.Add(new
                            {
                                NombrePlatillo = reader["Nombre"].ToString(),
                                Cantidad = Convert.ToInt32(reader["Cantidad"]),
                                Comentarios = reader["Comentarios"]?.ToString(),
                                Subtotal = Convert.ToDecimal(reader["Subtotal"])
                            });
                        }
                    }
                }
            }

            ViewBag.NombreOrden = nombreOrden;
            ViewBag.Total = total;
            ViewBag.Status = status;
            ViewBag.Fecha = fecha;
            ViewBag.Detalles = detalles; // mandamos la lista

            return View();
        }



        [HttpPost]
        public IActionResult FinalizarOrden([FromBody] OrdenRequest request)
        {
            var connectionString = _configuration.GetConnectionString("SQLServer");
            
            int idOrden = 0;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // 1️⃣ Insertar en Orden
                var queryOrden = @"
            INSERT INTO Orden (Fecha, Total, Status, NombreOrden)
            VALUES (@Fecha, @Total, @Status, @NombreOrden);
            SELECT SCOPE_IDENTITY();";

                decimal total = request.detalles
                    .Sum(d => d.precioUnitario * d.cantidad);

                

                using (var cmd = new SqlCommand(queryOrden, conn))
                {
                    cmd.Parameters.AddWithValue("@Fecha", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Total", total);
                    cmd.Parameters.AddWithValue("@Status", "Activo");
                    cmd.Parameters.AddWithValue("@NombreOrden", request.nombreOrden);

                    idOrden = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // Insertar en OrdenDetalle
                foreach (var d in request.detalles)
                {
                    var subtotal = d.precioUnitario * d.cantidad;

                    var queryDetalle = @"
                INSERT INTO OrdenDetalle
                (IdOrden, IdPlatillo, Cantidad, Comentarios, PrecioUnitario, Subtotal)
                VALUES
                (@IdOrden, @IdPlatillo, @Cantidad, @Comentarios, @PrecioUnitario, @Subtotal)";

                    using (var cmd = new SqlCommand(queryDetalle, conn))
                    {
                        cmd.Parameters.AddWithValue("@IdOrden", idOrden);
                        cmd.Parameters.AddWithValue("@IdPlatillo", d.idPlatillo);
                        cmd.Parameters.AddWithValue("@Cantidad", d.cantidad);
                        cmd.Parameters.AddWithValue("@Comentarios", (object?)d.comentarios ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PrecioUnitario", d.precioUnitario);
                        cmd.Parameters.AddWithValue("@Subtotal", subtotal);

                        cmd.ExecuteNonQuery();
                    }
                }
            }

            return Ok(idOrden);
        }

        
       


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
