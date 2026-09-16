process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const url = 'https://localhost:65102/api/Subastas/1/pujas';

const payload = {
    usuarioId: 1,
    monto: 85000,
    version: 0 // Asegúrate de que coincida con la versión actual en tu BD
};

async function enviarPuja(id) {
    try {
        const inicio = performance.now();
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        
        // Leemos la respuesta como texto para evitar el fallo de JSON vacío
        const text = await response.text();
        let data;
        try {
            data = text ? JSON.parse(text) : text;
        } catch {
            data = text; // Si es un mensaje de texto plano
        }

        const tiempo = (performance.now() - inicio).toFixed(2);
        console.log(`[Petición ${id}] -> Status HTTP: ${response.status} (${tiempo}ms)`, data);
    } catch (error) {
        console.error(`[Petición ${id}] Error de red:`, error.message);
    }
}

console.log("--- LANZANDO PUJAS CONCURRENTES ---");
Promise.all([
    enviarPuja(1),
    enviarPuja(2)
]);