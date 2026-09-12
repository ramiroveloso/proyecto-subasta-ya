process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
const url = 'http://127.0.0.1:62102/api/Subastas/1/pujas'; // Usa el puerto correcto que te funcionó antes (ej. 62103)

const payload = {
    usuarioId: 2,
    monto: 60000,
    version: 1 // Ambas peticiones envían la misma versión inicial
};

async function enviarPuja(id) {
    try {
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const data = await response.json();
        console.log(`Petición ${id}: Status ${response.status}`, data);
    } catch (error) {
        console.error(`Petición ${id} error:`, error.message);
    }
}

// Ejecutamos ambas peticiones en paralelo exacto
Promise.all([enviarPuja(1), enviarPuja(2)]);