
# SubastaYa 🚀

Sistema de subastas en línea desarrollado bajo los principios de **Arquitectura Limpia (Clean Architecture)** y **Entity Framework Core Code-First**, implementado como proyecto de integración tecnológica.

---

## 📋 Requisitos Previos

Asegúrate de contar con las siguientes herramientas instaladas en tu entorno de desarrollo:
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) o superior.
* [MySQL Server](https://dev.mysql.com/downloads/mysql/) configurado y corriendo localmente.

---

## ⚙️ Configuración Inicial

1. **Clonar el repositorio:**
   ```bash
   git clone <url-del-repositorio>
   cd proyecto-subasta-ya

```

2. **Configurar la cadena de conexión:**
Abre el archivo `appsettings.json` ubicado en el proyecto `PROYECTO_SUBASTA.Api` y actualiza los parámetros de acceso (`Server`, `Database`, `User`, `Password`) en la sección de conexiones:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=subastadb;Uid=tu_usuario;Pwd=tu_contraseña;"
}

```



---

## 🗄️ Base de datos y Migraciones

El sistema requiere la creación previa de la base de datos y la aplicación de migraciones para poblar los registros semilla obligatorios (usuarios, billeteras, categorías y subastas de prueba).

Desde la raíz de la solución, ejecuta los siguientes comandos:

```bash
# 1. Generar la migración inicial (si no existiera en el repositorio)
dotnet ef migrations add InitialCreate --project PROYECTO_SUBASTA.Infrastructure --startup-project PROYECTO_SUBASTA.Api

# 2. Aplicar las migraciones e impactar la base de datos MySQL
dotnet ef database update --project PROYECTO_SUBASTA.Infrastructure --startup-project PROYECTO_SUBASTA.Api

```

---

## ▶️ Ejecución de la Aplicación

Para poner en marcha el servidor backend (API):

```bash
dotnet run --project PROYECTO_SUBASTA.Api

```

* **Documentación Interactiva (Swagger):** Una vez iniciado el servidor, copia la URL que arroja la consola (ej. `https://localhost:XXXXX`) y añade `/swagger` para probar los endpoints directamente.
* **Panel Web (Frontend Estático):** Abre el archivo `index.html` en tu navegador para interactuar con la interfaz de control local conectada al backend.

```

```
