/**
 * SubastaYa - Módulo API Client (REST Fetch Functions)
 * Endpoint Backend ASP.NET Core: https://localhost:65102/api
 */

const API_BASE = 'https://localhost:65102/api';
let isBackendConnected = true;


// Perfiles del sistema: se cargan dinámicamente desde el backend o se usa el seed local como respaldo
let PERFILES_SEMILLA = [
    { id: 1, nombre: 'Vendedor Test', email: 'vendedor@test.com', saldoInicial: 0 },
    { id: 2, nombre: 'Comprador Líder', email: 'comprador1@test.com', saldoInicial: 150000 },
    { id: 3, nombre: 'Comprador Habilitado', email: 'comprador2@test.com', saldoInicial: 200000 },
    { id: 4, nombre: 'Usuario Sin Fondos', email: 'sinfondos@test.com', saldoInicial: 500 }
];

async function fetchObtenerUsuarios() {
    try {
        return await apiFetch('/Usuarios');
    } catch (e) {
        console.warn("No se pudieron cargar usuarios de la API, usando fallback local.", e);
        return [
            { id: 1, nombre: 'Vendedor Test', email: 'vendedor@test.com', saldoInicial: 0 },
            { id: 2, nombre: 'Comprador Líder', email: 'comprador1@test.com', saldoInicial: 150000 },
            { id: 3, nombre: 'Comprador Habilitado', email: 'comprador2@test.com', saldoInicial: 200000 },
            { id: 4, nombre: 'Usuario Sin Fondos', email: 'sinfondos@test.com', saldoInicial: 500 }
        ];
    }
}

const MOCK_CATEGORIAS = [
    { id: 1, nombre: 'Tecnología' },
    { id: 2, nombre: 'Coleccionables' },
    { id: 3, nombre: 'Indumentaria' },
    { id: 4, nombre: 'Vehículos' }
];

let MOCK_SUBASTAS = [
    {
        id: 1,
        titulo: 'Placa de Video RTX',
        descripcion: 'GPU de alta gama para diseño y gaming',
        urlImagen: 'https://images.unsplash.com/photo-1587202372775-e229f172b9d7?w=500',
        precioBase: 30000,
        incrementoMinimo: 5000,
        fechaInicio: new Date(Date.now() - 20 * 60000).toISOString(),
        fechaFin: new Date(Date.now() + 25 * 60000).toISOString(), // Cierra en 20-30 min
        estado: 'ACTIVA',
        categoriaId: 1,
        vendedorId: 1,
        ganadorId: null,
        precioFinal: null,
        version: 3,
        pujas: [
            { id: 1, subastaId: 1, usuarioId: 3, monto: 35000, fechaCreacion: new Date(Date.now() - 15 * 60000).toISOString(), postorAnonimo: 'Postor #C7' },
            { id: 2, subastaId: 1, usuarioId: 2, monto: 45000, fechaCreacion: new Date(Date.now() - 5 * 60000).toISOString(), postorAnonimo: 'Postor #A6' }
        ]
    },
    {
        id: 2,
        titulo: 'Procesador de Alta Gama',
        descripcion: 'Ideal para estaciones de trabajo',
        urlImagen: 'https://images.unsplash.com/photo-1591799264318-7e6ef8ddb7ea?w=500',
        precioBase: 50000,
        incrementoMinimo: 5000,
        fechaInicio: new Date(Date.now() - 58 * 60000).toISOString(),
        fechaFin: new Date(Date.now() + 90 * 1000).toISOString(), // Cierra en menos de 2 min (Zona crítica)
        estado: 'ACTIVA',
        categoriaId: 1,
        vendedorId: 1,
        ganadorId: null,
        precioFinal: null,
        version: 1,
        pujas: []
    },
    {
        id: 3,
        titulo: 'Figura Coleccionable Edición Limitada',
        descripcion: 'Arte y diseño exclusivo',
        urlImagen: 'https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=500',
        precioBase: 15000,
        incrementoMinimo: 2000,
        fechaInicio: new Date(Date.now() + 24 * 3600000).toISOString(), // Inicio programado a +24 hs
        fechaFin: new Date(Date.now() + 48 * 3600000).toISOString(),
        estado: 'PROGRAMADA',
        categoriaId: 2,
        vendedorId: 1,
        ganadorId: null,
        precioFinal: null,
        version: 1,
        pujas: []
    },
    {
        id: 4,
        titulo: 'Campera de Cuero Vintage',
        descripcion: 'Indumentaria clásica',
        urlImagen: 'https://images.unsplash.com/photo-1551028719-00167b16eac5?w=500',
        precioBase: 20000,
        incrementoMinimo: 2000,
        fechaInicio: new Date(Date.now() - 48 * 3600000).toISOString(),
        fechaFin: new Date(Date.now() - 2 * 3600000).toISOString(), // Fecha de fin pasada (-2 hs)
        estado: 'FINALIZADA',
        categoriaId: 3,
        vendedorId: 1,
        ganadorId: 2,
        precioFinal: 25000,
        version: 2,
        pujas: [
            { id: 3, subastaId: 4, usuarioId: 2, monto: 25000, fechaCreacion: new Date(Date.now() - 24 * 3600000).toISOString(), postorAnonimo: 'Postor #A6' }
        ]
    },
    {
        id: 5,
        titulo: 'Repuesto Clásico de Vehículo',
        descripcion: 'Sin ofertas registradas',
        urlImagen: 'https://images.unsplash.com/photo-1486006920555-c77dce18193b?w=500',
        precioBase: 80000,
        incrementoMinimo: 10000,
        fechaInicio: new Date(Date.now() - 72 * 3600000).toISOString(),
        fechaFin: new Date(Date.now() - 24 * 3600000).toISOString(), // Fecha pasada sin pujas (-24 hs)
        estado: 'DESIERTA',
        categoriaId: 4,
        vendedorId: 1,
        ganadorId: null,
        precioFinal: null,
        version: 1,
        pujas: []
    }
];

let MOCK_BILLETERAS = {
    1: {
        id: 1,
        usuarioId: 1, // vendedor@test.com
        saldoTotal: 0,
        saldoRetenido: 0,
        saldoDisponible: 0,
        version: 1,
        movimientos: []
    },
    2: {
        id: 2,
        usuarioId: 2, // comprador1@test.com (Comprador Líder)
        saldoTotal: 150000,
        saldoRetenido: 45000,
        saldoDisponible: 105000,
        version: 1,
        movimientos: [
            { id: 2, billeteraId: 2, tipo: 1, monto: 45000, fecha: new Date(Date.now() - 5 * 60000).toISOString(), subastaId: 1, concepto: 'Retención en Garantía (Escrow) - Subasta #1' },
            { id: 1, billeteraId: 2, tipo: 0, monto: 150000, fecha: new Date(Date.now() - 86400000).toISOString(), subastaId: null, concepto: 'Depósito Inicial Acreditado' }
        ]
    },
    3: {
        id: 3,
        usuarioId: 3, // comprador2@test.com (Comprador Habilitado)
        saldoTotal: 200000,
        saldoRetenido: 0,
        saldoDisponible: 200000,
        version: 1,
        movimientos: [
            { id: 5, billeteraId: 3, tipo: 2, monto: 35000, fecha: new Date(Date.now() - 5 * 60000).toISOString(), subastaId: 1, concepto: 'Liberación de Garantía (Escrow) por Superación de Oferta - Subasta #1' },
            { id: 4, billeteraId: 3, tipo: 1, monto: 35000, fecha: new Date(Date.now() - 15 * 60000).toISOString(), subastaId: 1, concepto: 'Retención en Garantía (Escrow) - Subasta #1' },
            { id: 3, billeteraId: 3, tipo: 0, monto: 200000, fecha: new Date(Date.now() - 86400000).toISOString(), subastaId: null, concepto: 'Depósito Inicial Acreditado' }
        ]
    },
    4: {
        id: 4,
        usuarioId: 4, // sinfondos@test.com (Usuario Sin Fondos)
        saldoTotal: 500,
        saldoRetenido: 0,
        saldoDisponible: 500,
        version: 1,
        movimientos: [
            { id: 6, billeteraId: 4, tipo: 0, monto: 500, fecha: new Date(Date.now() - 86400000).toISOString(), subastaId: null, concepto: 'Depósito Inicial Acreditado' }
        ]
    }
};

async function apiFetch(endpoint, options = {}) {
    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 3000);
        
        const response = await fetch(`${API_BASE}${endpoint}`, {
            ...options,
            signal: controller.signal,
            headers: {
                'Content-Type': 'application/json',
                ...(options.headers || {})
            }
        });
        clearTimeout(timeoutId);
        
        if (!response.ok) {
            const errorObj = {
                status: response.status,
                message: await response.text() || `Error HTTP ${response.status}`
            };
            throw errorObj;
        }
        
        updateApiConnectionStatus(true);
        return await response.json();
    } catch (error) {
        if (error.status) {
            throw error;
        }
        console.warn(`[SubastaYa API] No se pudo conectar a ${API_BASE}${endpoint}. Ejecutando en Modo Simulación Local.`, error);
        updateApiConnectionStatus(false);
        throw { status: 0, message: 'Backend fuera de línea (Modo Simulación Local)' };
    }
}

function updateApiConnectionStatus(connected) {
    isBackendConnected = connected;
    const badge = document.getElementById('api-status-badge');
    if (badge) {
        if (connected) {
            badge.className = 'api-status-badge badge bg-success text-white';
            badge.innerHTML = '<i class="fa-solid fa-circle-check me-1"></i> API Conectada (https://localhost:65102)';
        } else {
            badge.className = 'api-status-badge badge bg-warning text-dark';
            badge.innerHTML = '<i class="fa-solid fa-triangle-exclamation me-1"></i> Modo Simulación Local';
        }
    }
}

async function fetchObtenerSubastas() {
    try {
        return await apiFetch('/Subastas');
    } catch (e) {
        return [...MOCK_SUBASTAS];
    }
}

async function fetchObtenerSubastaPorId(id) {
    try {
        return await apiFetch(`/Subastas/${id}`);
    } catch (e) {
        return MOCK_SUBASTAS.find(s => s.id == id) || MOCK_SUBASTAS[0];
    }
}

async function fetchCrearSubasta(subastaData) {
    try {
        return await apiFetch('/Subastas', {
            method: 'POST',
            body: JSON.stringify(subastaData)
        });
    } catch (e) {
        if (e.status === 400 || e.status === 422) throw e;
        
        const nuevaSubasta = {
            id: MOCK_SUBASTAS.length + 1,
            ...subastaData,
            version: 1,
            pujas: []
        };
        MOCK_SUBASTAS.push(nuevaSubasta);
        return nuevaSubasta;
    }
}

async function fetchRegistrarPuja(subastaId, usuarioId, monto, version) {
    try {
        const res = await apiFetch(`/Subastas/${subastaId}/pujas`, {
            method: 'POST',
            body: JSON.stringify({ usuarioId, monto, version })
        });
        const sub = MOCK_SUBASTAS.find(s => s.id == subastaId);
        if (sub) {
            const anonHandle = `Postor #${(usuarioId * 33 + 100).toString(16).toUpperCase()}`;
            if (!sub.pujas) sub.pujas = [];
            sub.pujas.push({
                id: Date.now(),
                subastaId: sub.id,
                usuarioId: usuarioId,
                monto: monto,
                fechaCreacion: new Date().toISOString(),
                postorAnonimo: anonHandle
            });
            sub.version = (sub.version || 1) + 1;
        }
        return res;
    } catch (e) {
        if (e.status === 400 || e.status === 409 || e.status === 422) throw e;

        // Modo Simulación Local: registrar y asentar en MOCK_SUBASTAS
        const sub = MOCK_SUBASTAS.find(s => s.id == subastaId);
        if (sub) {
            const anonHandle = `Postor #${(usuarioId * 33 + 100).toString(16).toUpperCase()}`;
            const nuevaPuja = {
                id: Date.now(),
                subastaId: sub.id,
                usuarioId: usuarioId,
                monto: monto,
                fechaCreacion: new Date().toISOString(),
                postorAnonimo: anonHandle
            };
            if (!sub.pujas) sub.pujas = [];
            sub.pujas.push(nuevaPuja);
            sub.version = (sub.version || 1) + 1;
            return {
                mensaje: 'Puja registrada con éxito y saldo retenido en Escrow.',
                puja: nuevaPuja,
                version: sub.version
            };
        }
        return { mensaje: 'Ok' };
    }
}

async function fetchObtenerCategorias() {
    try {
        return await apiFetch('/Categorias');
    } catch (e) {
        return [...MOCK_CATEGORIAS];
    }
}

async function fetchCrearUsuario(nombre, email) {
    try {
        return await apiFetch('/Usuarios', {
            method: 'POST',
            body: JSON.stringify({ nombre, email })
        });
    } catch (e) {
        if (e.status === 400) throw e;
        const nuevoId = Object.keys(MOCK_BILLETERAS).length + 1;
        MOCK_BILLETERAS[nuevoId] = {
            id: nuevoId,
            usuarioId: nuevoId,
            saldoTotal: 0,
            saldoRetenido: 0,
            saldoDisponible: 0,
            version: 1,
            movimientos: []
        };
        return { mensaje: 'Usuario y billetera creados correctamente (Simulación)', usuarioId: nuevoId };
    }
}

async function fetchObtenerBilletera(usuarioId) {
    try {
        return await apiFetch(`/Billetera/${usuarioId}`);
    } catch (e) {
        if (!MOCK_BILLETERAS[usuarioId]) {
            MOCK_BILLETERAS[usuarioId] = {
                id: usuarioId,
                usuarioId: usuarioId,
                saldoTotal: 0,
                saldoRetenido: 0,
                saldoDisponible: 0,
                version: 1,
                movimientos: []
            };
        }
        return MOCK_BILLETERAS[usuarioId];
    }
}

async function fetchObtenerMovimientos(usuarioId) {
    try {
        return await apiFetch(`/Billetera/${usuarioId}/movimientos`);
    } catch (e) {
        const b = MOCK_BILLETERAS[usuarioId];
        return b ? b.movimientos : [];
    }
}

/** Cargar Saldo Libre para Usuarios / Tester */
async function fetchCargarSaldo(usuarioId, monto) {
    try {
        return await apiFetch('/Billetera/cargar', {
            method: 'POST',
            body: JSON.stringify({ usuarioId, monto })
        });
    } catch (e) {
        if (e.status === 400) throw e;
        const b = await fetchObtenerBilletera(usuarioId);
        b.saldoTotal += monto;
        b.saldoDisponible += monto;
        b.movimientos.unshift({
            id: Date.now(),
            billeteraId: b.id,
            tipo: 0, // DEPOSITO
            monto: monto,
            fecha: new Date().toISOString(),
            subastaId: null,
            concepto: `Carga Libre de Fondos Tester (Ramiro Veloso)`
        });
        return { mensaje: `¡Se han acreditado $${monto.toLocaleString()} libremente a la cuenta!` };
    }
}

async function fetchRetenerSaldo(usuarioId, monto, subastaId) {
    try {
        return await apiFetch('/Billetera/retener', {
            method: 'POST',
            body: JSON.stringify({ usuarioId, monto, subastaId })
        });
    } catch (e) {
        const b = await fetchObtenerBilletera(usuarioId);
        if (b.saldoDisponible < monto) {
            const errObj = {
                status: 400,
                message: `Saldo insuficiente en Billetera Virtual (Escrow). Saldo Disponible: $${b.saldoDisponible.toLocaleString()} | Monto Requerido: $${monto.toLocaleString()}`
            };
            throw errObj;
        }

        b.saldoDisponible -= monto;
        b.saldoRetenido += monto;
        b.movimientos.unshift({
            id: Date.now(),
            billeteraId: b.id,
            tipo: 1, // RETENCION
            monto: monto,
            fecha: new Date().toISOString(),
            subastaId: subastaId,
            concepto: `Retención en Garantía (Escrow) - Subasta #${subastaId}`
        });
        return { mensaje: 'Saldo retenido preventivamente en garantía (Escrow).' };
    }
}

async function fetchLiberarSaldo(usuarioId, monto, subastaId) {
    try {
        return await apiFetch('/Billetera/liberar', {
            method: 'POST',
            body: JSON.stringify({ usuarioId, monto, subastaId })
        });
    } catch (e) {
        const b = await fetchObtenerBilletera(usuarioId);
        b.saldoRetenido = Math.max(0, b.saldoRetenido - monto);
        b.saldoDisponible += monto;
        b.movimientos.unshift({
            id: Date.now(),
            billeteraId: b.id,
            tipo: 2, // LIBERACION
            monto: monto,
            fecha: new Date().toISOString(),
            subastaId: subastaId,
            concepto: `Liberación de Garantía (Escrow) por Superación de Oferta - Subasta #${subastaId}`
        });
        return { mensaje: 'Saldo liberado y reintegrado al disponible.' };
    }
}
