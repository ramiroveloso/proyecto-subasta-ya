/**
 * SubastaYa - Módulo API Client (REST Fetch Functions)
 * Endpoint Backend ASP.NET Core: https://localhost:65102/api
 */

const API_BASE = 'https://localhost:65102/api';
let isBackendConnected = true;

// Perfiles Semilla del Sistema con Ramiro Veloso Tester como Perfil Principal
const PERFILES_SEMILLA = [
    { id: 100, nombre: 'Ramiro Veloso', email: 'ramiro.veloso@tester.com', rol: 'Usuario Tester Principal', saldoInicial: 500000 },
    { id: 1, nombre: 'Juan Pérez', email: 'comprador1@test.com', rol: 'Comprador Activo', saldoInicial: 250000 },
    { id: 2, nombre: 'Carlos SinFondos', email: 'sinfondos@test.com', rol: 'Comprador sin Fondos', saldoInicial: 0 },
    { id: 3, nombre: 'Ana Vendedora', email: 'vendedor@test.com', rol: 'Vendedor Corporativo', saldoInicial: 500000 }
];

const MOCK_CATEGORIAS = [
    { id: 1, nombre: 'Tecnología' },
    { id: 2, nombre: 'Coleccionables' },
    { id: 3, nombre: 'Indumentaria' },
    { id: 4, nombre: 'Vehículos' }
];

let MOCK_SUBASTAS = [
    {
        id: 101,
        titulo: 'Reloj de Lujo Smartwatch Pro',
        descripcion: 'Reloj inteligente de edición limitada con caja de titanio, monitor cardíaco avanzado y resistencia al agua 50m.',
        urlImagen: 'assets/images/watch.png',
        precioBase: 150000,
        incrementoMinimo: 5000,
        fechaInicio: new Date(Date.now() - 3600000 * 2).toISOString(),
        fechaFin: new Date(Date.now() + 120000).toISOString(), // Quedan 2 min (Zona crítica)
        estado: 'ACTIVA',
        categoriaId: 1,
        vendedorId: 3,
        ganadorId: null,
        precioFinal: null,
        version: 4,
        pujas: [
            { id: 1, subastaId: 101, usuarioId: 1, monto: 155000, fechaCreacion: new Date(Date.now() - 1800000).toISOString(), postorAnonimo: 'Postor #A83' },
            { id: 2, subastaId: 101, usuarioId: 3, monto: 160000, fechaCreacion: new Date(Date.now() - 600000).toISOString(), postorAnonimo: 'Postor #K19' }
        ]
    },
    {
        id: 102,
        titulo: 'Cámara Fotográfica Vintage 1970',
        descripcion: 'Cámara réflex clásica de colección en perfecto estado de funcionamiento, incluye estuche de cuero original.',
        urlImagen: 'assets/images/camera.png',
        precioBase: 85000,
        incrementoMinimo: 2500,
        fechaInicio: new Date(Date.now() - 3600000 * 24).toISOString(),
        fechaFin: new Date(Date.now() + 3600000 * 5).toISOString(),
        estado: 'ACTIVA',
        categoriaId: 2,
        vendedorId: 3,
        ganadorId: null,
        precioFinal: null,
        version: 2,
        pujas: [
            { id: 3, subastaId: 102, usuarioId: 1, monto: 90000, fechaCreacion: new Date(Date.now() - 3600000).toISOString(), postorAnonimo: 'Postor #A83' }
        ]
    },
    {
        id: 103,
        titulo: 'Auriculares Inalámbricos Hi-Fi',
        descripcion: 'Cancelación de ruido activa de última generación con 40 horas de autonomía y códec de audio de alta resolución.',
        urlImagen: 'assets/images/headphones.png',
        precioBase: 45000,
        incrementoMinimo: 1500,
        fechaInicio: new Date(Date.now() + 3600000 * 12).toISOString(),
        fechaFin: new Date(Date.now() + 3600000 * 36).toISOString(),
        estado: 'PROGRAMADA',
        categoriaId: 1,
        vendedorId: 3,
        ganadorId: null,
        precioFinal: null,
        version: 1,
        pujas: []
    }
];

let MOCK_BILLETERAS = {
    100: {
        id: 100,
        usuarioId: 100, // Ramiro Veloso Tester
        saldoTotal: 500000,
        saldoRetenido: 0,
        saldoDisponible: 500000,
        version: 1,
        movimientos: [
            { id: 1, billeteraId: 100, tipo: 0, monto: 500000, fecha: new Date(Date.now() - 3600000).toISOString(), subastaId: null, concepto: 'Fondo Inicial Tester Acreditado' }
        ]
    },
    1: {
        id: 1,
        usuarioId: 1,
        saldoTotal: 250000,
        saldoRetenido: 40000,
        saldoDisponible: 210000,
        version: 1,
        movimientos: [
            { id: 10, billeteraId: 1, tipo: 0, monto: 250000, fecha: new Date(Date.now() - 86400000 * 2).toISOString(), subastaId: null, concepto: 'Depósito Inicial Acreditado' },
            { id: 11, billeteraId: 1, tipo: 1, monto: 40000, fecha: new Date(Date.now() - 3600000).toISOString(), subastaId: 101, concepto: 'Retención en Garantía (Escrow) - Puja Subasta #101' }
        ]
    },
    2: {
        id: 2,
        usuarioId: 2, // sinfondos@test.com
        saldoTotal: 0,
        saldoRetenido: 0,
        saldoDisponible: 0,
        version: 1,
        movimientos: []
    },
    3: {
        id: 3,
        usuarioId: 3, // vendedor@test.com
        saldoTotal: 500000,
        saldoRetenido: 0,
        saldoDisponible: 500000,
        version: 1,
        movimientos: [
            { id: 12, billeteraId: 3, tipo: 0, monto: 500000, fecha: new Date(Date.now() - 86400000 * 5).toISOString(), subastaId: null, concepto: 'Fondo Vendedor Registrado' }
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
            badge.innerHTML = '<i class="fa-solid fa-triangle-exclamation me-1"></i> Modo Simulación Local (Ramiro Veloso Tester Active)';
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
            id: MOCK_SUBASTAS.length + 101,
            ...subastaData,
            version: 1,
            pujas: []
        };
        MOCK_SUBASTAS.push(nuevaSubasta);
        return nuevaSubasta;
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
                saldoTotal: 100000,
                saldoRetenido: 0,
                saldoDisponible: 100000,
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
