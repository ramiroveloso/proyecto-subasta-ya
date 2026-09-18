
# SubastaYa 

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
Aquí tienes una documentación técnica estructurada y lista para incluir en tu memoria de proyecto o repositorio, detallando la metodología, las herramientas y el script utilizado para verificar la **Concurrencia Optimista**.

---

# Documentación Técnica: Pruebas de Concurrencia Optimista

## 1. Introducción y Metodología

Para garantizar la integridad de los datos en un escenario de múltiples usuarios intentando ofertar sobre la misma subasta en el mismo instante, se implementó un mecanismo de **Control de Concurrencia Optimista (Optimistic Concurrency Control - OCC)**.

La metodología se basa en:

* **Uso de un Token de Concurrencia (`Version`)**: Cada entidad `Subasta` cuenta con una propiedad numérica de versión que se incrementa en cada modificación exitosa.
* **Validación de Estado**: Cuando un cliente envía una puja, adjunta la versión que tenía la subasta al momento de cargar la vista.
* **Detección de Colisiones**: Si dos solicitudes llegan de forma concurrente, la primera que procesa la base de datos actualiza el registro y eleva la versión. La segunda solicitud, al intentar guardar con una versión ya desactualizada, genera una excepción de concurrencia en Entity Framework Core, la cual es interceptada para evitar sobrescrituras y retornar un **HTTP 409 Conflict**.

---

## 2. Herramientas Utilizadas

* **Backend**: .NET API bajo Clean Architecture y Entity Framework Core.
* **Base de Datos**: MySQL gestionada mediante MySQL Workbench para la verificación de esquemas y registros.
* **Entorno de Pruebas Automatizadas**: Node.js (utilizando la API nativa `fetch` y `Promise.all` para simular solicitudes en paralelo estricto).

---

## 3. Pasos para la Ejecución de la Prueba

1. **Verificación de Versión Base**: Consultar la tabla `subastas` en MySQL Workbench para identificar el valor actual de la columna `Version` para una subasta específica (por ejemplo, `Version: 3`).
2. **Configuración del Payload**: Preparar un script automatizado que envíe dos solicitudes HTTP `POST` simultáneas al endpoint de pujas, ambas con el mismo número de versión inicial.
3. **Ejecución Simultánea**: Disparar las peticiones en paralelo mediante el motor de Node.js para eliminar la latencia e interferencia del factor humano.
4. **Validación de Resultados**: Comprobar que una de las peticiones sea procesada exitosamente (**HTTP 200**) y la otra colisione de inmediato (**HTTP 409**).

---

## 4. Script de Prueba Automatizada (`test-concurrencia.js`)

El siguiente script en Node.js fue utilizado para simular la colisión concurrente de dos pujas enviadas exactamente al mismo tiempo:

```javascript
// Desactiva temporalmente la validación de certificados SSL autofirmados en entorno de desarrollo local
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const url = 'https://localhost:65102/api/Subastas/1/pujas';

const payload = {
    usuarioId: 1,
    monto: 135000,
    version: 3 // Versión exacta esperada según el estado actual en la Base de Datos
};

async function enviarPuja(id) {
    try {
        const inicio = performance.now();
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        
        // Lectura de la respuesta en texto para manejar tanto JSON como mensajes planos
        const text = await response.text();
        let data;
        try {
            data = text ? JSON.parse(text) : text;
        } catch {
            data = text;
        }

        const tiempo = (performance.now() - inicio).toFixed(2);
        console.log(`[Petición ${id}] -> Status HTTP: ${response.status} (${tiempo}ms)`, data);
    } catch (error) {
        console.error(`[Petición ${id}] Error de red:`, error.message);
    }
}

console.log("--- LANZANDO PUJAS CONCURRENTES ---");
// Promise.all ejecuta ambas solicitudes en paralelo estricto dentro del event loop
Promise.all([
    enviarPuja(1),
    enviarPuja(2)
]);

```

---

## 5. Resultados Obtenidos

Al ejecutar el script en la terminal, se obtuvo la siguiente traza de respuestas:

* **[Petición 1] -> Status HTTP: 409**
* *Respuesta*: `{ mensaje: 'Conflicto de concurrencia (409): La subasta fue modificada por otro usuario en simultáneo. Intente nuevamente.' }`


* **[Petición 2] -> Status HTTP: 200**
* *Respuesta*: `{ mensaje: 'Puja registrada con éxito y saldo retenido en Escrow.' }`



**Conclusión de la prueba**: El sistema responde correctamente ante situaciones de alta concurrencia, protegiendo la regla de negocio y asegurando que ninguna puja sobreescriba de manera silenciosa los cambios de otro usuario.

¡Excelente iniciativa! Documentar el proceso de despliegue en el `README.md` después de hacer el merge a `main` le dará un valor agregado enorme al proyecto frente al docente, demostrando un enfoque profesional de ingeniería de software DevOps.

Aquí tienes una propuesta completa y redactada con terminología técnica precisa para que puedas copiar, adaptar y pegar directamente en tu archivo `README.md`:

---

## 🚀 Despliegue en la Nube (Cloud Deployment)

La arquitectura de **SubastaYa** se encuentra desplegada en un entorno cloud híbrido y desacoplado, separando la infraestructura del Backend, el Frontend y la Base de Datos para garantizar alta disponibilidad y rendimiento:

* **Backend (API REST ASP.NET Core):** Alojado en **Render** mediante un contenedor Docker optimizado.
* **Frontend (SPA - HTML/JS/CSS):** Distribuido a través de la CDN global de **Vercel**.
* **Base de datos Relacional (MySQL):** Gestionada en el servicio cloud administrado de **Aiven**.

---

### 1. Configuración y Despliegue del Backend (Render + Docker)

Para empaquetar y desplegar la API desarrollada en ASP.NET Core bajo Clean Architecture, se implementó un archivo **`Dockerfile`** en la raíz del repositorio. Este contenedor compila la solución y expone el servicio en el puerto correspondiente:

1. **Creación del Web Service en Render:**
* Se conectó el repositorio de GitHub seleccionando la rama de producción.
* Se configuró el entorno como **Docker** (Render detecta automáticamente el `Dockerfile` raíz).
* El comando de inicio (*Start Command*) se dejó en blanco, ya que el contenedor ejecuta nativamente el `ENTRYPOINT` apuntando a `PROYECTO_SUBASTA.Api.dll`.


2. **Variables de Entorno configuradas en Render:**
* `ASPNETCORE_ENVIRONMENT`: `Production`
* `ConnectionStrings__DefaultConnection`: Cadena de conexión segura hacia la base de datos MySQL en Aiven (con `SslMode=Required`).



---

### 2. Configuración y Despliegue del Frontend (Vercel)

La interfaz de usuario basada en Single Page Application (SPA) se configuró para consumir los endpoints públicos de la API en la nube:

1. **Despliegue estático:**
* Conectado directamente al repositorio en Vercel, optimizando la compilación de archivos estáticos.


2. **Integración con la API:**
* Se ajustó la capa de servicios (`api.js`) para apuntar las peticiones HTTP directamente a la URL pública del backend en Render, permitiendo la comunicación segura mediante HTTPS de punta a punta.



---

### 3. Conectividad y Base de Datos (Aiven MySQL)

* **Servidor:** Instancia MySQL gestionada en la nube de Aiven con cifrado SSL obligatorio (`SslMode=Required`).
* **Migraciones:** Aplicadas mediante Entity Framework Core utilizando migraciones estructuradas para poblar esquemas, catálogos y datos iniciales de prueba (incluyendo los perfiles con rol `ADMIN`).

---

### 🔗 Enlaces de Acceso Producción

* **Interfaz Web (Frontend en Vercel):** [https://ramiroveloso-proyecto-subasta-ya.vercel.app/]
* **API REST / Documentación (Backend en Render):** [https://proyecto-subasta-ya.onrender.com]

---

¡Mucho éxito con ese merge y la presentación final ante el docente! Tienen un producto sumamente sólido y bien estructurado.
