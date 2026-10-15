/**
 * SubastaYa - Módulo API Client (REST Fetch Functions)
 * Endpoint Backend ASP.NET Core: https://localhost:65102/api
 */

// Configuración de Entornos (Dev vs Prod)
const ENV_CONFIG = {
    isDev: (typeof window !== 'undefined' && (
        window.location.hostname === 'localhost' ||
        window.location.hostname === '127.0.0.1' ||
        (typeof process !== 'undefined' && process.env && process.env.NODE_ENV !== 'production')
    )),
    showDevBadges: false,
    endpoints: {
        devPrimary: 'https://localhost:65102/api',
        devAlternative: 'http://localhost:65103/api',
        relative: (typeof window !== 'undefined' && window.location && window.location.protocol.startsWith('http') && window.location.port)
            ? `${window.location.origin}/api`
            : 'https://localhost:65102/api'
    }
};

let API_BASE = ENV_CONFIG.endpoints.relative;
let isBackendConnected = true;

/**
 * Cliente HTTP Centralizado con arquitectura de Interceptores y Manejo Global de Errores
 */
class HttpInterceptorClient {
    constructor(baseURL) {
        this.baseURL = baseURL;
        this.requestInterceptors = [];
        this.responseInterceptors = [];
    }

    addRequestInterceptor(fn) {
        this.requestInterceptors.push(fn);
    }

    addResponseInterceptor(onSuccess, onError) {
        this.responseInterceptors.push({ onSuccess, onError });
    }

    async request(endpoint, options = {}) {
        let config = {
            url: `${this.baseURL}${endpoint}`,
            method: options.method || 'GET',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json',
                ...(options.headers || {})
            },
            ...options
        };

        for (const interceptor of this.requestInterceptors) {
            config = await interceptor(config);
        }

        const controller = new AbortController();
        const timeoutMs = options.timeout || 7000;
        const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

        try {
            const response = await fetch(config.url, {
                method: config.method,
                headers: config.headers,
                body: config.body,
                signal: controller.signal
            });
            clearTimeout(timeoutId);

            let data = null;
            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('application/json')) {
                try {
                    data = await response.json();
                } catch (_) {
                    data = null;
                }
            } else {
                try {
                    data = await response.text();
                } catch (_) {}
            }

            if (!response.ok) {
                let mensajeError = `Error HTTP ${response.status}: ${response.statusText}`;
                if (data) {
                    if (typeof data === 'string' && data.trim()) {
                        mensajeError = data;
                    } else if (data.mensaje) {
                        mensajeError = data.mensaje;
                    } else if (data.message) {
                        mensajeError = data.message;
                    } else if (data.errors && typeof data.errors === 'object') {
                        mensajeError = Object.values(data.errors).flat().join(' ');
                    } else if (data.title) {
                        mensajeError = data.title;
                    }
                }

                const errorObj = {
                    status: response.status,
                    mensaje: mensajeError,
                    message: mensajeError,
                    statusText: response.statusText,
                    details: data,
                    isConflict: response.status === 409,
                    isBadRequest: response.status === 400 || response.status === 422,
                    isNotFound: response.status === 404
                };
                throw errorObj;
            }

            let finalData = data;
            for (const interceptor of this.responseInterceptors) {
                if (interceptor.onSuccess) {
                    finalData = await interceptor.onSuccess(finalData, response);
                }
            }

            updateApiConnectionStatus(true);
            if (options.includeHeaders) {
                const totalCountHeader = response.headers.get('X-Total-Count');
                return {
                    items: finalData || [],
                    total: totalCountHeader ? parseInt(totalCountHeader, 10) : (Array.isArray(finalData) ? finalData.length : 0),
                    headers: response.headers
                };
            }
            return finalData;

        } catch (error) {
            clearTimeout(timeoutId);

            let interceptedError = error;
            for (const interceptor of this.responseInterceptors) {
                if (interceptor.onError) {
                    try {
                        interceptedError = await interceptor.onError(interceptedError);
                    } catch (e) {
                        interceptedError = e;
                    }
                }
            }

            if (interceptedError && typeof interceptedError.status === 'number') {
                throw interceptedError;
            }

            throw {
                status: 0,
                mensaje: 'No hay conexión con el servidor',
                message: 'No hay conexión con el servidor',
                isNetworkError: true
            };
        }
    }
}

const ApiClient = new HttpInterceptorClient(API_BASE);

// Inyector global de Headers / Metadatos en Request
ApiClient.addRequestInterceptor(async (config) => {
    if (typeof usuarioActual !== 'undefined' && usuarioActual && usuarioActual.id) {
        config.headers['X-Usuario-Id'] = String(usuarioActual.id);
    }
    return config;
});

// Función centralizada para llamadas a la API
async function apiFetch(endpoint, options = {}) {
    try {
        const res = await ApiClient.request(endpoint, options);
        updateApiConnectionStatus(true);
        return res;
    } catch (error) {
        if (error.status && error.status !== 0) {
            updateApiConnectionStatus(true);
            throw error;
        }

        // Intento de fallback automático si HTTPS local falla
        if (ApiClient.baseURL.startsWith('https://localhost:65102')) {
            try {
                const altBase = ENV_CONFIG.endpoints.devAlternative;
                ApiClient.baseURL = altBase;
                API_BASE = altBase;
                const res = await ApiClient.request(endpoint, options);
                updateApiConnectionStatus(true);
                return res;
            } catch (errAlt) {
                if (errAlt.status && errAlt.status !== 0) {
                    updateApiConnectionStatus(true);
                    throw errAlt;
                }
            }
        }

        updateApiConnectionStatus(false);
        throw {
            status: 0,
            mensaje: 'No hay conexión con el servidor',
            message: 'No hay conexión con el servidor'
        };
    }
}

function updateApiConnectionStatus(connected) {
    isBackendConnected = connected;
    const badge = document.getElementById('api-status-badge');
    const devBadge = document.getElementById('devtools-api-status');

    const updateElement = (elem) => {
        if (!elem) return;
        if (connected) {
            elem.className = 'api-status-badge badge bg-success text-white';
            elem.innerHTML = '<i class="fa-solid fa-circle-check me-1"></i> API Conectada';
        } else {
            elem.className = 'api-status-badge badge bg-danger text-white';
            elem.innerHTML = '<i class="fa-solid fa-circle-xmark me-1"></i> Desconectada';
        }
    };

    updateElement(badge);
    updateElement(devBadge);

    if (badge && !ENV_CONFIG.showDevBadges) {
        badge.classList.add('d-none');
    }
}

async function fetchObtenerUsuarios() {
    return await apiFetch('/Usuarios');
}

async function fetchLogin(usuario, password) {
    const res = await apiFetch('/Usuarios/sesiones', {
        method: 'POST',
        body: JSON.stringify({ usuario, password })
    });
    return res.usuario;
}

async function fetchObtenerSubastas(params = {}) {
    const query = new URLSearchParams();
    if (params.categoriaId) query.append('categoriaId', params.categoriaId);
    if (params.estado) query.append('estado', params.estado);
    if (params.precioMin !== undefined && params.precioMin !== null && params.precioMin !== '') query.append('precioMin', params.precioMin);
    if (params.precioMax !== undefined && params.precioMax !== null && params.precioMax !== '') query.append('precioMax', params.precioMax);
    if (params.orden) query.append('orden', params.orden);
    if (params.pagina) query.append('pagina', params.pagina);
    if (params.tamanoPagina) query.append('tamanoPagina', params.tamanoPagina);

    const queryString = query.toString();
    const endpoint = `/Subastas${queryString ? `?${queryString}` : ''}`;
    return await apiFetch(endpoint, { includeHeaders: true });
}

async function fetchObtenerSubastaPorId(id) {
    return await apiFetch(`/Subastas/${id}`);
}

async function fetchCrearSubasta(subastaData) {
    return await apiFetch('/Subastas', {
        method: 'POST',
        body: JSON.stringify(subastaData)
    });
}

async function fetchRegistrarPuja(subastaId, usuarioId, monto, version) {
    return await apiFetch(`/Subastas/${subastaId}/pujas`, {
        method: 'POST',
        body: JSON.stringify({ usuarioId, monto, version })
    });
}

async function fetchObtenerCategorias() {
    return await apiFetch('/Categorias');
}

async function fetchCrearUsuario(nombre, email, password) {
    return await apiFetch('/Usuarios', {
        method: 'POST',
        body: JSON.stringify({ nombre, email, password })
    });
}

async function fetchObtenerBilletera(usuarioId) {
    return await apiFetch(`/Billeteras/${usuarioId}`);
}

async function fetchObtenerMovimientos(usuarioId) {
    return await apiFetch(`/Billeteras/${usuarioId}/movimientos`);
}

async function fetchCargarSaldo(usuarioId, monto) {
    return await apiFetch(`/Billeteras/${usuarioId}/depositos`, {
        method: 'POST',
        body: JSON.stringify({ monto })
    });
}

async function fetchObtenerAuditoria(limite = 100) {
    return await apiFetch(`/Auditorias?limite=${limite}`);
}

async function fetchProcesarSubastasVencidas() {
    return await apiFetch('/Subastas/cierres', {
        method: 'POST'
    });
}

async function fetchObtenerActividades(usuarioId) {
    return await apiFetch(`/Usuarios/${usuarioId}/actividades`);
}

