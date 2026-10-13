/**
 * test-concurrencia.js
 * Script de prueba de concurrencia para el sistema SubastaYa.
 * 
 * Uso:
 *   node test-concurrencia.js [--url=http://localhost:5000] [--subasta=1] [--subasta2=2] [--u1=2] [--u2=3] [--escenario=all|A|B]
 * 
 * Escenarios:
 *   - Escenario A: Dos postores ofertan el mismo monto con la misma versión en la misma subasta (esperado: 1 éxito, 1 conflicto 409).
 *   - Escenario B: Un único postor puja simultáneamente en dos subastas comprometiendo más de su saldo disponible (esperado: a lo sumo 1 éxito, nunca dos).
 */

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

// ========================================================================
// 1. Parseo de argumentos de línea de comandos
// ========================================================================
function parseArgs() {
    const args = process.argv.slice(2);
    const config = {
        baseUrl: process.env.SUBASTA_API_URL || 'http://localhost:5000',
        subastaId: 1,
        subasta2Id: 2,
        usuario1: 2,
        usuario2: 3,
        escenario: 'all'
    };

    for (const arg of args) {
        if (arg.startsWith('--url=')) config.baseUrl = arg.split('=')[1].replace(/\/+$/, '');
        else if (arg.startsWith('--subasta=')) config.subastaId = parseInt(arg.split('=')[1], 10);
        else if (arg.startsWith('--subasta2=')) config.subasta2Id = parseInt(arg.split('=')[1], 10);
        else if (arg.startsWith('--u1=')) config.usuario1 = parseInt(arg.split('=')[1], 10);
        else if (arg.startsWith('--u2=')) config.usuario2 = parseInt(arg.split('=')[1], 10);
        else if (arg.startsWith('--escenario=')) config.escenario = arg.split('=')[1].toLowerCase();
    }
    return config;
}

// ========================================================================
// 2. Funciones auxiliares HTTP
// ========================================================================
async function obtenerSubasta(baseUrl, id) {
    const res = await fetch(`${baseUrl}/api/Subastas/${id}`);
    if (!res.ok) {
        throw new Error(`Error al consultar Subasta #${id}: HTTP ${res.status}`);
    }
    return await res.json();
}

async function obtenerBilletera(baseUrl, usuarioId) {
    const res = await fetch(`${baseUrl}/api/Billeteras/${usuarioId}`);
    if (!res.ok) {
        throw new Error(`Error al consultar Billetera de Usuario #${usuarioId}: HTTP ${res.status}`);
    }
    return await res.json();
}

async function enviarPuja(baseUrl, subastaId, payload, etiqueta) {
    const inicio = performance.now();
    try {
        const response = await fetch(`${baseUrl}/api/Subastas/${subastaId}/pujas`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const text = await response.text();
        let data;
        try { data = text ? JSON.parse(text) : text; } catch { data = text; }

        const duracion = (performance.now() - inicio).toFixed(2);
        console.log(`  [${etiqueta}] -> HTTP ${response.status} (${duracion}ms) | Respuesta:`, data);
        return { status: response.status, data };
    } catch (err) {
        console.error(`  [${etiqueta}] Error de conexión:`, err.message);
        return { status: 0, error: err.message };
    }
}

// ========================================================================
// 3. Ejecución de Escenarios
// ========================================================================

async function ejecutarEscenarioA(config) {
    console.log(`\n======================================================`);
    console.log(`ESCENARIO A: Misma Subasta (#${config.subastaId}) - Pujas Concurrentes`);
    console.log(`======================================================`);

    // 1. Obtener versión actual dinámicamente con GET
    const subasta = await obtenerSubasta(config.baseUrl, config.subastaId);
    console.log(`Estado actual de Subasta #${subasta.id}:`);
    console.log(`  - Título: ${subasta.titulo}`);
    console.log(`  - Versión actual: ${subasta.version}`);
    console.log(`  - Precio base/actual: $${subasta.precioFinal || subasta.precioBase}`);

    const maxPuja = (subasta.pujas && subasta.pujas.length > 0)
        ? Math.max(...subasta.pujas.map(p => Number(p.monto)))
        : Number(subasta.precioFinal || subasta.precioBase);
    const baseMonto = maxPuja;
    const incremento = subasta.incrementoMinimo || 1000;
    const montoOfertado = Number(baseMonto) + Number(incremento);

    console.log(`\nLanzando dos ofertas simultáneas de $${montoOfertado} con Version ${subasta.version}...`);

    const p1 = { usuarioId: config.usuario1, monto: montoOfertado, version: subasta.version };
    const p2 = { usuarioId: config.usuario2, monto: montoOfertado, version: subasta.version };

    const [r1, r2] = await Promise.all([
        enviarPuja(config.baseUrl, config.subastaId, p1, `Usuario #${config.usuario1}`),
        enviarPuja(config.baseUrl, config.subastaId, p2, `Usuario #${config.usuario2}`)
    ]);

    const exitos = [r1, r2].filter(r => r.status === 200).length;
    const conflictos = [r1, r2].filter(r => r.status === 409 || r.status === 400).length;

    console.log(`\nResultado Escenario A:`);
    console.log(`  - Peticiones aceptadas (200 OK): ${exitos}`);
    console.log(`  - Conflictos / Rechazos controlados: ${conflictos}`);

    if (exitos === 1 && conflictos === 1) {
        console.log(`  [OK] Control de concurrencia optimista verificado con éxito.`);
    } else if (exitos > 1) {
        console.error(`  [FALLO] Condición de carrera detectada: ambas peticiones retornaron 200.`);
    } else {
        console.log(`  [INFO] Ambas rechazadas (posible saldo insuficiente o subasta no activa).`);
    }
}

async function ejecutarEscenarioB(config) {
    console.log(`\n======================================================`);
    console.log(`ESCENARIO B: Misma Billetera en Dos Subastas (#${config.subastaId} y #${config.subasta2Id})`);
    console.log(`======================================================`);

    const usuarioId = config.usuario1;
    const billetera = await obtenerBilletera(config.baseUrl, usuarioId);
    console.log(`Billetera Usuario #${usuarioId}:`);
    console.log(`  - Saldo Total: $${billetera.saldoTotal}`);
    console.log(`  - Saldo Disponible: $${billetera.saldoDisponible}`);
    console.log(`  - Saldo Retenido: $${billetera.saldoRetenido}`);

    const s1 = await obtenerSubasta(config.baseUrl, config.subastaId);
    const s2 = await obtenerSubasta(config.baseUrl, config.subasta2Id);

    // Calcular un monto que individualmente cubra pero cuya suma exceda el disponible
    const disponible = Number(billetera.saldoDisponible);
    const montoPuja = Math.floor(disponible * 0.7); // 70% en cada una -> 140% del disponible

    if (montoPuja <= 0) {
        console.warn(`  [AVISO] El usuario #${usuarioId} no tiene saldo disponible suficiente para ejecutar la prueba.`);
        return;
    }

    console.log(`\nLanzando dos ofertas de $${montoPuja} (Total comprometido: $${montoPuja * 2} > Disponible: $${disponible})...`);

    const p1 = { usuarioId, monto: montoPuja, version: s1.version };
    const p2 = { usuarioId, monto: montoPuja, version: s2.version };

    const [r1, r2] = await Promise.all([
        enviarPuja(config.baseUrl, config.subastaId, p1, `Subasta #${s1.id}`),
        enviarPuja(config.baseUrl, config.subasta2Id, p2, `Subasta #${s2.id}`)
    ]);

    const exitos = [r1, r2].filter(r => r.status === 200).length;

    console.log(`\nResultado Escenario B:`);
    console.log(`  - Peticiones aceptadas (200 OK): ${exitos}`);

    if (exitos <= 1) {
        console.log(`  [OK] Integridad de fondos preservada: nunca se comprometió más del disponible.`);
    } else {
        console.error(`  [FALLO] Inconsistencia financiera: se aceptaron ambas pujas superando el saldo disponible.`);
    }
}

// ========================================================================
// 4. Punto de entrada principal
// ========================================================================
async function main() {
    const config = parseArgs();
    console.log(`Iniciando prueba de concurrencia contra: ${config.baseUrl}`);

    try {
        if (config.escenario === 'all' || config.escenario === 'a') {
            await ejecutarEscenarioA(config);
        }
        if (config.escenario === 'all' || config.escenario === 'b') {
            await ejecutarEscenarioB(config);
        }
        console.log(`\nPruebas finalizadas.`);
    } catch (err) {
        console.error(`\nError fatal durante la ejecución del script:`, err.message);
        process.exit(1);
    }
}

main();