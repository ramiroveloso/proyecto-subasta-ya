/**
 * SubastaYa - Lógica de Aplicación Frontend & Controlador de Módulos
 */

let usuarioActual = null;
let usuariosDisponibles = [];

let subastasCache = [];
let categoriasCache = [];
let subastaSeleccionadaSala = null;
let timerSalaInterval = null;
let timerCardsInterval = null;
let contadorSyncInterval = 0;

let timerPollingSala = null;
let sincronizandoSala = false;
let timerPollingCatalogo = null;
let sincronizandoCatalogo = false;

/* ==========================================================================
   HELPERS DE SANITIZACIÓN & SEGURIDAD XSS
   ========================================================================== */
function escaparHtml(valor) {
    if (valor === null || valor === undefined) return '';
    return String(valor)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function sanitizarUrlImagen(url) {
    if (!url || typeof url !== 'string') return 'assets/images/watch.png';
    const trimmed = url.trim();
    if (trimmed.startsWith('http://') || trimmed.startsWith('https://')) {
        return escaparHtml(trimmed);
    }
    return 'assets/images/watch.png';
}

/* ==========================================================================
   GESTIÓN DE SESIÓN & AUTENTICACIÓN MULTI-PERFIL
   ========================================================================== */
const KEY_SESION = 'subastaya_usuario_sesion';

function obtenerSesionGuardada() {
    try {
        const sesion = localStorage.getItem(KEY_SESION);
        return sesion ? JSON.parse(sesion) : null;
    } catch (e) {
        return null;
    }
}

function guardarSesion(usuario) {
    if (usuario) {
        localStorage.setItem(KEY_SESION, JSON.stringify({
            id: usuario.id ?? usuario.Id,
            nombre: usuario.nombre ?? usuario.Nombre,
            email: usuario.email ?? usuario.Email
        }));
    }
}

function borrarSesion() {
    localStorage.removeItem(KEY_SESION);
}

function mostrarModalLogin() {
    const overlay = document.getElementById('login-overlay');
    if (overlay) {
        overlay.classList.remove('d-none');
        cargarPerfilesEnLogin();
    }
}

function ocultarModalLogin() {
    const overlay = document.getElementById('login-overlay');
    if (overlay) {
        overlay.classList.add('d-none');
    }
}

async function iniciarSesion(usuario) {
    usuarioActual = {
        id: usuario.id ?? usuario.Id,
        nombre: usuario.nombre ?? usuario.Nombre,
        email: usuario.email ?? usuario.Email,
        rol: usuario.rol ?? usuario.Rol ?? (((usuario.email || '').toLowerCase().includes('admin')) ? 'ADMIN' : undefined)
    };
    guardarSesion(usuarioActual);
    ocultarModalLogin();

    actualizarVisibilidadAuditoria();
    await renderizarSelectorPerfilesSemilla();
    await actualizarBilleteraUI();
    actualizarTarjetasCatalogo();
    await cargarMisActividades();

    if (usuarioActual && usuarioActual.rol === 'ADMIN') {
        await cargarLogsAuditoria();
    }

    if (subastaSeleccionadaSala) {
        actualizarMonitorPujasSala();
        validarPujaEnVivo();
    }

    const tabSala = document.getElementById('content-sala');
    if (tabSala && tabSala.classList.contains('active')) {
        iniciarPollingSala();
    } else {
        iniciarPollingCatalogo();
    }

    const rol = obtenerRolUsuario(usuarioActual);
    mostrarToast(`¡Bienvenido/a <strong>${escaparHtml(usuarioActual.nombre)}</strong>! Rol activo: <span class="badge bg-light text-dark border">${escaparHtml(rol)}</span>`, 'Sesión Iniciada', 'success');
}

function cerrarSesion() {
    detenerPollingSala();
    detenerPollingCatalogo();
    borrarSesion();
    usuarioActual = null;
    actualizarVisibilidadAuditoria();

    // Limpiar UI del usuario en navbar superior y dropdown
    const navAvatar = document.getElementById('nav-user-avatar');
    if (navAvatar) navAvatar.textContent = '--';
    const userInfo = document.getElementById('wallet-usuario-info');
    if (userInfo) userInfo.textContent = 'Sin Sesión';
    const userEmail = document.getElementById('wallet-usuario-email');
    if (userEmail) userEmail.textContent = 'Inicie sesión';
    const navBadge = document.getElementById('nav-user-saldo');
    if (navBadge) {
        navBadge.textContent = '$0';
        navBadge.className = 'badge bg-secondary font-monospace ms-1';
    }
    const dropdownMenu = document.getElementById('dropdown-perfiles-semilla');
    if (dropdownMenu) {
        dropdownMenu.innerHTML = `
            <li class="p-3 text-center text-muted small">
                <i class="fa-solid fa-user-slash d-block fs-4 mb-2 text-secondary"></i>
                No hay una sesión activa.
            </li>
        `;
    }

    mostrarModalLogin();
    mostrarToast('Has cerrado la sesión de forma segura.', 'Sesión Finalizada', 'info');
}

function obtenerRolUsuario(usuario) {
    if (!usuario) return 'Usuario';
    if (usuario.rol === 'ADMIN' || usuario.Rol === 'ADMIN') return 'Administrador';
    const email = (usuario.email || '').toLowerCase();
    const nombre = (usuario.nombre || '').toLowerCase();
    if (email.includes('admin') || nombre.includes('administrador')) return 'Administrador';
    if (email.includes('vendedor') || nombre.includes('vendedor')) return 'Vendedor';
    if (email.includes('comprador1') || nombre.includes('líder') || nombre.includes('lider')) return 'Comprador Líder';
    if (email.includes('comprador2') || nombre.includes('habilitado')) return 'Comprador Habilitado';
    if (email.includes('sinfondos') || nombre.includes('sin fondos')) return 'Usuario Sin Saldo';
    return 'Comprador';
}

function obtenerIniciales(nombre) {
    if (!nombre) return 'US';
    const partes = nombre.trim().split(/\s+/);
    if (partes.length >= 2) {
        return (partes[0][0] + partes[1][0]).toUpperCase();
    }
    return nombre.substring(0, 2).toUpperCase();
}

async function cargarPerfilesEnLogin() {
    const grid = document.getElementById('login-profiles-grid');
    if (!grid) return;

    grid.innerHTML = `
        <div class="col-12 text-center py-4">
            <div class="spinner-border text-primary-custom" role="status"></div>
            <div class="small text-muted mt-2">Cargando perfiles y saldos en tiempo real...</div>
        </div>
    `;

    try {
        const usuarios = await fetchObtenerUsuarios();
        if (usuarios && usuarios.length > 0) {
            usuariosDisponibles = usuarios.map(u => ({
                id: u.id ?? u.Id,
                nombre: u.nombre ?? u.Nombre,
                email: u.email ?? u.Email,
                rol: u.rol ?? u.Rol ?? (((u.email || u.Email || '').toLowerCase().includes('admin')) ? 'ADMIN' : undefined)
            }));
        } else {
            usuariosDisponibles = [];
        }

        const billeteras = await Promise.all(
            usuariosDisponibles.map(p => fetchObtenerBilletera(p.id).catch(() => null))
        );

        grid.innerHTML = '';
        if (usuariosDisponibles.length === 0) {
            grid.innerHTML = '<div class="col-12 text-center text-muted py-3">No hay perfiles disponibles en el sistema.</div>';
            return;
        }

        usuariosDisponibles.forEach((p, idx) => {
            const b = billeteras[idx];
            const saldoDisp = b ? (b.saldoDisponible ?? b.SaldoDisponible ?? 0) : 0;
            const rol = obtenerRolUsuario(p);
            const iniciales = obtenerIniciales(p.nombre);
            const esActivo = Boolean(usuarioActual && p.id === usuarioActual.id);

            let avatarBg = '#581845';
            let badgeRolClass = 'bg-primary-subtle text-primary border border-primary-subtle';
            if (rol.includes('Administrador')) {
                avatarBg = '#212529';
                badgeRolClass = 'bg-dark text-white border border-dark';
            } else if (rol.includes('Vendedor')) {
                avatarBg = '#c25e00';
                badgeRolClass = 'bg-warning-subtle text-warning-emphasis border border-warning-subtle';
            } else if (rol.includes('Líder')) {
                avatarBg = '#198754';
                badgeRolClass = 'bg-success-subtle text-success border border-success-subtle';
            } else if (rol.includes('Sin Saldo')) {
                avatarBg = '#dc3545';
                badgeRolClass = 'bg-danger-subtle text-danger border border-danger-subtle';
            }

            const col = document.createElement('div');
            col.className = 'col-sm-6';
            col.innerHTML = `
                <div class="profile-card-option ${esActivo ? 'active-profile' : ''}" onclick="seleccionarPerfilParaLogin(${Number(p.id)})">
                    <div class="d-flex align-items-center mb-2">
                        <div class="profile-avatar me-3 shadow-sm" style="background-color: ${avatarBg};">
                            ${escaparHtml(iniciales)}
                        </div>
                        <div class="text-truncate flex-grow-1">
                            <div class="fw-bold text-dark text-truncate">${escaparHtml(p.nombre)}</div>
                            <div class="extra-small text-muted text-truncate">${escaparHtml(p.email)}</div>
                        </div>
                    </div>
                    <div class="d-flex justify-content-between align-items-center pt-2 border-top mt-2">
                        <span class="badge ${badgeRolClass} extra-small">${escaparHtml(rol)}</span>
                        <span class="font-monospace fw-bold ${saldoDisp > 0 ? 'text-success' : 'text-danger'} small">
                            $${saldoDisp.toLocaleString('es-AR')}
                        </span>
                    </div>
                    <div class="mt-2 text-end">
                        <button class="btn btn-sm ${esActivo ? 'btn-success' : 'btn-outline-primary-custom'} w-100 extra-small fw-semibold py-1">
                            ${esActivo ? '<i class="fa-solid fa-check me-1"></i> Perfil Activo' : '<i class="fa-solid fa-arrow-right-to-bracket me-1"></i> Ingresar'}
                        </button>
                    </div>
                </div>
            `;
            grid.appendChild(col);
        });
    } catch (e) {
        console.error("Error al cargar perfiles en login", e);
        grid.innerHTML = `<div class="col-12 text-center text-danger py-3">Error al conectar con la API de usuarios.</div>`;
    }
}

function iniciarSesionPorId(perfilId) {
    seleccionarPerfilParaLogin(perfilId);
}

async function procesarLoginCredenciales(event) {
    event.preventDefault();
    const usuarioInput = document.getElementById('login-input-usuario')?.value.trim();
    const passwordInput = document.getElementById('login-input-password')?.value;
    const alertError = document.getElementById('login-error-alert');
    const textError = document.getElementById('login-error-text');

    if (!usuarioInput) {
        if (alertError && textError) {
            textError.textContent = 'Debe ingresar un usuario o correo electrónico.';
            alertError.classList.remove('d-none');
        }
        return;
    }

    setBotonCargando('btn-login-submit', 'spinner-login', 'text-login', true, '<i class="fa-solid fa-circle-notch fa-spin me-2"></i> Verificando...');
    if (alertError) alertError.classList.add('d-none');

    try {
        const user = await fetchLogin(usuarioInput, passwordInput);
        await iniciarSesion(user);
        document.getElementById('form-login-credenciales')?.reset();
    } catch (err) {
        if (alertError && textError) {
            textError.textContent = err.mensaje || err.message || 'Usuario o contraseña no reconocidos.';
            alertError.classList.remove('d-none');
        }
        if (err.status === 0 || err.status >= 500) {
            mostrarToast('Servidor no disponible.', 'Error de Conexión', 'danger', () => procesarLoginCredenciales(event));
        }
    } finally {
        setBotonCargando('btn-login-submit', 'spinner-login', 'text-login', false);
    }
}

/* Helper para estados de carga interactivos (spinners) en botones */
function setBotonCargando(btnId, spinnerId, textId, cargando, textoCarga = 'Procesando...') {
    const btn = typeof btnId === 'string' ? document.getElementById(btnId) : btnId;
    if (!btn) return;
    const spinner = typeof spinnerId === 'string' ? document.getElementById(spinnerId) : spinnerId;
    const text = typeof textId === 'string' ? document.getElementById(textId) : textId;

    if (cargando) {
        btn.disabled = true;
        btn.classList.add('btn-loading');
        if (spinner) spinner.classList.remove('d-none');
        if (text) {
            btn.dataset.originalText = text.innerHTML;
            text.innerHTML = textoCarga;
        }
    } else {
        btn.disabled = false;
        btn.classList.remove('btn-loading');
        if (spinner) spinner.classList.add('d-none');
        if (text && btn.dataset.originalText) {
            text.innerHTML = btn.dataset.originalText;
        }
    }
}

/* ==========================================================================
   CONFIGURACIÓN DE HUSOS HORARIOS (UTC EN BASE DE DATOS / LOCAL EN FRONTEND)
   ========================================================================== */
const TIMEZONE_UTC3 = 'America/Argentina/Buenos_Aires';

/**
 * Parsea de manera determinista cualquier fecha garantizando que se interprete en UTC.
 * Si el string de la API no contiene 'Z' ni offset +/-XX:XX, le agrega 'Z'.
 */
function parseUtcDate(dateStr) {
    if (!dateStr) return null;
    if (dateStr instanceof Date) return dateStr;
    let s = String(dateStr).trim();
    if (!s.endsWith('Z') && !/[+-]\d{2}:\d{2}$/.test(s)) {
        s += 'Z';
    }
    const d = new Date(s);
    return isNaN(d.getTime()) ? null : d;
}

/**
 * Formatea una fecha y hora local (Argentina): "DD/MM/AAAA, HH:mm:ss"
 */
function formatFechaHoraUTC3(dateInput) {
    const d = parseUtcDate(dateInput);
    if (!d) return '-';
    return d.toLocaleString('es-AR', {
        timeZone: TIMEZONE_UTC3,
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false
    });
}

/**
 * Formatea una fecha y hora corta local (Argentina): "DD/MM/AAAA HH:mm"
 */
function formatFechaHoraCortaUTC3(dateInput) {
    const d = parseUtcDate(dateInput);
    if (!d) return '-';
    return d.toLocaleString('es-AR', {
        timeZone: TIMEZONE_UTC3,
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        hour12: false
    });
}

/**
 * Formatea únicamente la hora en horario local: "HH:mm:ss"
 */
function formatHoraUTC3(dateInput) {
    const d = parseUtcDate(dateInput);
    if (!d) return '-';
    return d.toLocaleTimeString('es-AR', {
        timeZone: TIMEZONE_UTC3,
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false
    });
}

/**
 * Convierte un timestamp/Date a formato "YYYY-MM-DDTHH:mm" local
 * para ser consumido por un <input type="datetime-local">.
 */
function formatDatetimeLocalUTC3(dateInput) {
    const d = (dateInput instanceof Date) ? dateInput : new Date(dateInput);
    const formatter = new Intl.DateTimeFormat('en-CA', {
        timeZone: TIMEZONE_UTC3,
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        hour12: false
    });
    const parts = formatter.formatToParts(d);
    const getPart = (type) => parts.find(p => p.type === type)?.value || '00';
    const year = getPart('year');
    const month = getPart('month');
    const day = getPart('day');
    let hour = getPart('hour');
    if (hour === '24') hour = '00';
    const minute = getPart('minute');
    return `${year}-${month}-${day}T${hour}:${minute}`;
}

/**
 * Convierte el valor local de un <input type="datetime-local">
 * a una cadena ISO UTC terminada en "Z" para almacenar en la Base de Datos.
 */
function utc3InputToIsoUtc(datetimeLocalVal) {
    if (!datetimeLocalVal) return null;
    const valConSegundos = datetimeLocalVal.length === 16 ? `${datetimeLocalVal}:00` : datetimeLocalVal;
    const fechaUTC3 = new Date(`${valConSegundos}-03:00`);
    return fechaUTC3.toISOString();
}

/**
 * Determina el estado real y sincronizado de una subasta en tiempo real:
 * - PROGRAMADA: Su fecha de inicio está en el futuro.
 * - ACTIVA: Ya inició y aún no concluyó su fecha de fin.
 * - FINALIZADA: Su fecha de fin ya expiró y tiene ofertas registradas.
 * - DESIERTA: Su fecha de fin ya expiró y no tuvo ofertas.
 */
function determinarEstadoSubasta(sub) {
    if (!sub) return 'DESIERTA';
    const ahora = Date.now();
    const inicioMs = parseUtcDate(sub.fechaInicio ?? sub.FechaInicio)?.getTime() || 0;
    const finMs = parseUtcDate(sub.fechaFin ?? sub.FechaFin)?.getTime() || 0;
    const estadoDb = (sub.estado ?? sub.Estado ?? '').toUpperCase();
    const pujas = sub.pujas ?? sub.Pujas ?? [];
    const tienePujas = pujas.length > 0;

    if (finMs <= ahora || estadoDb === 'FINALIZADA' || estadoDb === 'DESIERTA') {
        return (tienePujas || estadoDb === 'FINALIZADA') ? 'FINALIZADA' : 'DESIERTA';
    }

    if (inicioMs > ahora) {
        return 'PROGRAMADA';
    }

    return 'ACTIVA';
}

/* ==========================================================================
   MODO OSCURO / CLARO (THEME CONTROLLER)
   ========================================================================== */

function inicializarModoOscuro() {
    const temaGuardado = localStorage.getItem('subastaya-theme') || 'light';
    aplicarTema(temaGuardado);
}

function toggleDarkMode() {
    const temaActual = document.documentElement.getAttribute('data-bs-theme') || 'light';
    const nuevoTema = (temaActual === 'dark') ? 'light' : 'dark';
    aplicarTema(nuevoTema);
    localStorage.setItem('subastaya-theme', nuevoTema);
}

function aplicarTema(tema) {
    document.documentElement.setAttribute('data-bs-theme', tema);
    const icon = document.getElementById('theme-toggle-icon');
    const text = document.getElementById('theme-toggle-text');
    const btn = document.getElementById('btn-theme-toggle');

    if (tema === 'dark') {
        if (icon) icon.className = 'fa-solid fa-sun text-warning';
        if (text) text.textContent = 'Modo Claro';
        if (btn) btn.setAttribute('title', 'Cambiar a Modo Claro');
    } else {
        if (icon) icon.className = 'fa-solid fa-moon text-warning';
        if (text) text.textContent = 'Modo Oscuro';
        if (btn) btn.setAttribute('title', 'Cambiar a Modo Oscuro');
    }
}

document.addEventListener("DOMContentLoaded", async () => {
    inicializarModoOscuro();
    inicializarFechasFormulario();
    configurarEventosUI();

    // Conectar el formulario de login por credenciales
    const formLoginCredenciales = document.getElementById('form-login-credenciales');
    if (formLoginCredenciales) {
        formLoginCredenciales.addEventListener('submit', procesarLoginCredenciales);
    }

    // 1. Cargar perfiles dinámicamente desde la Base de Datos / API
    let usuariosRemotos = [];
    try {
        usuariosRemotos = await fetchObtenerUsuarios();
    } catch (_) {
        usuariosRemotos = [];
    }

    if (usuariosRemotos && usuariosRemotos.length > 0) {
        usuariosDisponibles = usuariosRemotos.map(u => ({
            id: u.id ?? u.Id,
            nombre: u.nombre ?? u.Nombre,
            email: u.email ?? u.Email,
            rol: u.rol ?? u.Rol ?? (((u.email || u.Email || '').toLowerCase().includes('admin')) ? 'ADMIN' : undefined)
        }));
    }

    // 2. Verificar si existe una sesión guardada en localStorage
    const sesionGuardada = obtenerSesionGuardada();
    if (sesionGuardada) {
        const usuarioEncontrado = usuariosDisponibles.find(p => p.id === sesionGuardada.id || p.email === sesionGuardada.email);
        usuarioActual = usuarioEncontrado || sesionGuardada;
        ocultarModalLogin();
    } else {
        usuarioActual = usuariosDisponibles[1] || usuariosDisponibles[0] || null;
        if (!usuarioActual) {
            mostrarModalLogin();
        } else {
            guardarSesion(usuarioActual);
            ocultarModalLogin();
        }
    }

    actualizarVisibilidadAuditoria();
    await renderizarSelectorPerfilesSemilla();
    await cargarCategorias();
    await aplicarFiltros();
    iniciarPollingCatalogo();
    await actualizarBilleteraUI();
    await cargarMisActividades();
    if (usuarioActual && usuarioActual.rol === 'ADMIN') {
        await cargarLogsAuditoria();
    }

    timerCardsInterval = setInterval(actualizarTemporizadoresCatalogo, 1000);
});

/* ==========================================================================
   SINCRONISMO DE SALDOS EN TIEMPO REAL
   ========================================================================== */

async function renderizarSelectorPerfilesSemilla() {
    const dropdownMenu = document.getElementById('dropdown-perfiles-semilla');
    if (!dropdownMenu) return;

    if (!usuarioActual) {
        dropdownMenu.innerHTML = `
            <li class="p-3 text-center text-muted small">
                <i class="fa-solid fa-user-slash d-block fs-4 mb-2 text-secondary"></i>
                No hay una sesión activa.
            </li>
        `;
        return;
    }

    // Consultamos la billetera del usuario activo en tiempo real
    let bActual = null;
    try {
        bActual = await fetchObtenerBilletera(usuarioActual.id);
    } catch (err) {
        console.warn("[SubastaYa] No se pudo obtener la billetera del usuario activo:", err);
    }

    const saldoActualDisp = bActual ? (bActual.saldoDisponible ?? bActual.SaldoDisponible ?? (usuarioActual.saldoInicial || 0)) : (usuarioActual.saldoInicial || 0);
    const saldoActualRet = bActual ? (bActual.saldoRetenido ?? bActual.SaldoRetenido ?? 0) : 0;
    const saldoActualTotal = bActual ? (bActual.saldoTotal ?? bActual.SaldoTotal ?? (saldoActualDisp + saldoActualRet)) : saldoActualDisp;

    // Sincronizar Avatar, Nombre y Saldo en el botón de la barra superior
    const navAvatar = document.getElementById('nav-user-avatar');
    if (navAvatar) {
        navAvatar.textContent = obtenerIniciales(usuarioActual.nombre);
    }

    const userInfo = document.getElementById('wallet-usuario-info');
    if (userInfo) {
        userInfo.textContent = usuarioActual.nombre;
    }

    const userEmail = document.getElementById('wallet-usuario-email');
    if (userEmail) {
        userEmail.textContent = usuarioActual.email;
    }

    const navBadge = document.getElementById('nav-user-saldo');
    if (navBadge) {
        navBadge.textContent = `$${saldoActualDisp.toLocaleString('es-AR')}`;
        navBadge.className = `badge font-monospace ms-1 ${saldoActualDisp > 0 ? 'bg-success' : 'bg-danger'}`;
    }

    const rol = obtenerRolUsuario(usuarioActual);

    // Contenido del menú desplegable: Tarjeta de perfil activo con información completa y botón de cerrar sesión
    dropdownMenu.innerHTML = `
        <li class="p-3">
            <div class="d-flex align-items-center mb-3">
                <div class="navbar-user-avatar me-3 shadow-sm" style="width: 44px; height: 44px; font-size: 1.05rem;">
                    ${escaparHtml(obtenerIniciales(usuarioActual.nombre))}
                </div>
                <div class="text-truncate">
                    <div class="fw-bold text-dark text-truncate fs-6">${escaparHtml(usuarioActual.nombre)}</div>
                    <div class="extra-small text-muted text-truncate">${escaparHtml(usuarioActual.email)}</div>
                </div>
            </div>
            <div class="d-flex justify-content-between align-items-center mb-3">
                <span class="badge ${usuarioActual.rol === 'ADMIN' ? 'bg-dark text-white' : 'bg-primary-subtle text-primary border border-primary-subtle'} extra-small px-2 py-1">${escaparHtml(rol)}</span>
                <span class="extra-small text-muted font-monospace">ID Usuario: #${Number(usuarioActual.id)}</span>
            </div>
            <div class="bg-light p-2 rounded-3 border mb-1">
                <div class="row g-2 text-center font-monospace">
                    <div class="col-6">
                        <div class="bg-white p-2 rounded border">
                            <span class="text-muted d-block" style="font-size: 0.65rem;">DISPONIBLE</span>
                            <span class="fw-bold text-success small">$${saldoActualDisp.toLocaleString('es-AR')}</span>
                        </div>
                    </div>
                    <div class="col-6">
                        <div class="bg-white p-2 rounded border">
                            <span class="text-muted d-block" style="font-size: 0.65rem;">RETENIDO</span>
                            <span class="fw-bold text-warning small">$${saldoActualRet.toLocaleString('es-AR')}</span>
                        </div>
                    </div>
                </div>
                <div class="d-flex justify-content-between align-items-center mt-2 px-1 extra-small text-muted font-monospace">
                    <span>Saldo Total:</span>
                    <span class="fw-bold text-dark">$${saldoActualTotal.toLocaleString('es-AR')}</span>
                </div>
            </div>
        </li>
        <li><hr class="dropdown-divider my-0"></li>
        <li class="p-2">
            <a class="dropdown-item text-danger d-flex align-items-center rounded-2 py-2 fw-semibold extra-small" href="#" onclick="cerrarSesion(); return false;">
                <i class="fa-solid fa-right-from-bracket me-2 text-danger fs-6"></i> Cerrar Sesión
            </a>
        </li>
    `;
}

async function sincronizarSaldosDropdown() {
    try {
        if (!usuarioActual) return;
        const bActual = await fetchObtenerBilletera(usuarioActual.id).catch(() => null);
        if (bActual) {
            const saldoActualDisp = bActual.saldoDisponible ?? bActual.SaldoDisponible ?? 0;
            const navBadge = document.getElementById('nav-user-saldo');
            if (navBadge) {
                navBadge.textContent = `$${saldoActualDisp.toLocaleString('es-AR')}`;
                navBadge.className = `badge font-monospace ms-1 ${saldoActualDisp > 0 ? 'bg-success' : 'bg-danger'}`;
            }
        }
        await renderizarSelectorPerfilesSemilla();
    } catch (err) {
        console.warn("[SubastaYa] Error al sincronizar saldos en dropdown:", err);
    }
}

function actualizarBadgeUsuarioEnDropdown(usuarioId, nuevoSaldo, nuevoSaldoRetenido = 0) {
    const item = document.querySelector(`[data-dropdown-user-id="${usuarioId}"]`);
    if (item) {
        const esActivo = Boolean(usuarioActual && usuarioId === usuarioActual.id);
        const badgeUsuario = item.querySelector('.badge-saldo');
        if (badgeUsuario) {
            badgeUsuario.textContent = `$${nuevoSaldo.toLocaleString('es-AR')}`;
            badgeUsuario.className = `badge badge-saldo font-monospace ${
                nuevoSaldo > 0 ? 
                    (esActivo ? 'bg-light text-dark' : 'bg-success-subtle text-success border border-success-subtle') : 
                    'bg-danger-subtle text-danger border border-danger-subtle'
            }`;
        }
        const retInfo = item.querySelector('.retencion-info');
        if (retInfo) {
            if (nuevoSaldoRetenido > 0) {
                retInfo.textContent = `(Ret: $${nuevoSaldoRetenido.toLocaleString('es-AR')})`;
                retInfo.className = `retencion-info extra-small ${esActivo ? 'text-warning' : 'text-warning-emphasis'}`;
            } else {
                retInfo.textContent = '';
                retInfo.className = 'retencion-info extra-small d-none';
            }
        }
    }

    if (usuarioActual && usuarioId === usuarioActual.id) {
        const navBadge = document.getElementById('nav-user-saldo');
        if (navBadge) {
            navBadge.textContent = `$${nuevoSaldo.toLocaleString('es-AR')}`;
            navBadge.className = `badge font-monospace ${nuevoSaldo > 0 ? 'bg-success' : 'bg-danger'}`;
        }
    }
}

async function cambiarPerfilSemilla(usuarioId) {
    const perfil = usuariosDisponibles.find(p => p.id === usuarioId);
    if (!perfil) return;
    await iniciarSesion(perfil);
}

/** Carga de Saldo Rápida */
async function cargarSaldoLibreRapido(monto) {
    try {
        await fetchCargarSaldo(usuarioActual.id, monto);
        mostrarToast(`⚡ <strong>¡Carga Acreditada!</strong> +$${monto.toLocaleString()} añadidos a la cuenta de ${escaparHtml(usuarioActual.nombre)}.`, 'Fondos Acreditados', 'success');
        
        await actualizarBilleteraUI();
        await renderizarSelectorPerfilesSemilla();
        await cargarLogsAuditoria();
    } catch (e) {
        if (e && (e.status === 0 || e.status >= 500)) {
            mostrarToast('Servidor no disponible.', 'Error de Conexión', 'danger', () => cargarSaldoLibreRapido(monto));
        } else {
            mostrarToast(`Error al recargar: ${escaparHtml(e.mensaje || e.message)}`, 'Error', 'danger');
        }
    }
}

function setMontoCargaLibre(monto) {
    const input = document.getElementById('modal-cargar-monto');
    if (input) input.value = monto;
}
/* ==========================================================================
   MÓDULO 1: CATÁLOGO Y EXPLORACIÓN
   ========================================================================== */

async function cargarCategorias() {
    try {
        categoriasCache = await fetchObtenerCategorias();
        const selectFiltro = document.getElementById('filtro-categoria');
        const selectCrear = document.getElementById('crear-categoriaId');
        
        if (selectFiltro) {
            selectFiltro.innerHTML = '<option value="">Todas las categorías</option>';
            categoriasCache.forEach(c => {
                selectFiltro.innerHTML += `<option value="${Number(c.id)}">${escaparHtml(c.nombre)}</option>`;
            });
        }

        if (selectCrear) {
            selectCrear.innerHTML = '<option value="">Seleccione una categoría</option>';
            categoriasCache.forEach(c => {
                selectCrear.innerHTML += `<option value="${Number(c.id)}">${escaparHtml(c.nombre)}</option>`;
            });
        }
    } catch (e) {
        console.error("Error al cargar categorías", e);
    }
}

function renderizarSkeletonsCatalogo(cantidad = 6) {
    const grid = document.getElementById('catalogo-grid');
    if (!grid) return;
    let skeletonsHtml = '';
    for (let i = 0; i < cantidad; i++) {
        skeletonsHtml += `
            <div class="col">
                <div class="skeleton-card shadow-sm p-0">
                    <div class="skeleton-box skeleton-img mb-2"></div>
                    <div class="p-3">
                        <div class="skeleton-box mb-2" style="width: 70%; height: 18px;"></div>
                        <div class="skeleton-box mb-3" style="width: 90%; height: 14px;"></div>
                        <div class="bg-light p-2 rounded border mb-3">
                            <div class="d-flex justify-content-between mb-1">
                                <div class="skeleton-box" style="width: 40%; height: 12px;"></div>
                                <div class="skeleton-box" style="width: 35%; height: 16px;"></div>
                            </div>
                            <div class="d-flex justify-content-between">
                                <div class="skeleton-box" style="width: 30%; height: 10px;"></div>
                                <div class="skeleton-box" style="width: 25%; height: 10px;"></div>
                            </div>
                        </div>
                        <div class="skeleton-box" style="width: 100%; height: 32px; border-radius: 8px;"></div>
                    </div>
                </div>
            </div>
        `;
    }
    grid.innerHTML = skeletonsHtml;
}

function renderizarSkeletonsBilletera(filas = 4) {
    const tbody = document.getElementById('wallet-tabla-movimientos');
    if (!tbody) return;
    let skeletonsHtml = '';
    for (let i = 0; i < filas; i++) {
        skeletonsHtml += `
            <tr>
                <td><div class="skeleton-box" style="width: 120px; height: 20px; border-radius: 12px;"></div></td>
                <td><div class="skeleton-box" style="width: 80px; height: 18px;"></div></td>
                <td><div class="skeleton-box" style="width: 130px; height: 14px;"></div></td>
                <td><div class="skeleton-box" style="width: 160px; height: 14px;"></div></td>
                <td><div class="skeleton-box" style="width: 90px; height: 14px;"></div></td>
            </tr>
        `;
    }
    tbody.innerHTML = skeletonsHtml;
}

let catalogoPaginaActual = 1;
const catalogoTamanoPagina = 6;
let catalogoTotalSubastas = 0;

function cambiarFiltroCatalogo() {
    catalogoPaginaActual = 1;
    aplicarFiltros(1);
}

async function aplicarFiltros(pagina = catalogoPaginaActual, silencioso = false) {
    const grid = document.getElementById('catalogo-grid');
    if (!grid) return;

    catalogoPaginaActual = (typeof pagina === 'number' && pagina > 0) ? pagina : 1;
    if (!silencioso) {
        renderizarSkeletonsCatalogo(catalogoTamanoPagina);
    }

    try {
        const catVal = document.getElementById('filtro-categoria')?.value;
        const categoriaId = catVal ? parseInt(catVal, 10) : null;
        const estado = document.getElementById('filtro-estado')?.value || null;
        const minVal = document.getElementById('filtro-precio-min')?.value;
        const maxVal = document.getElementById('filtro-precio-max')?.value;
        const precioMin = (minVal !== '' && minVal !== undefined && !isNaN(parseFloat(minVal))) ? parseFloat(minVal) : null;
        const precioMax = (maxVal !== '' && maxVal !== undefined && !isNaN(parseFloat(maxVal))) ? parseFloat(maxVal) : null;
        const orden = document.getElementById('filtro-orden')?.value || 'cierre';

        const res = await fetchObtenerSubastas({
            categoriaId: (categoriaId && categoriaId > 0) ? categoriaId : null,
            estado: estado || null,
            precioMin: precioMin,
            precioMax: precioMax,
            orden: orden,
            pagina: catalogoPaginaActual,
            tamanoPagina: catalogoTamanoPagina
        });

        const subastas = res.items || (Array.isArray(res) ? res : []);
        catalogoTotalSubastas = typeof res.total === 'number' ? res.total : subastas.length;
        subastasCache = subastas;

        grid.innerHTML = '';

        if (subastas.length === 0) {
            grid.innerHTML = `
                <div class="col-12 text-center text-muted py-5">
                    <i class="fa-solid fa-box-open fa-3x mb-3 text-secondary opacity-50"></i>
                    <h5 class="fw-bold">No se encontraron subastas</h5>
                    <p class="small">Ajusta los filtros de búsqueda o categoría.</p>
                </div>
            `;
            renderizarPaginacionCatalogo(0, catalogoPaginaActual, catalogoTamanoPagina);
            return;
        }

        subastas.forEach(sub => {
            const subId = Number(sub.id ?? sub.Id);
            const subTitulo = escaparHtml(sub.titulo ?? sub.Titulo ?? `Subasta #${subId}`);
            const subCatId = sub.categoriaId ?? sub.CategoriaId;
            const categoria = escaparHtml(categoriasCache.find(c => c.id == subCatId)?.nombre || 'General');
            const pujaActual = obtenerPujaMaxima(sub);
            const subEstado = determinarEstadoSubasta(sub);
            const badgeClass = getEstadoBadgeClass(subEstado);
            const imagenUrl = sanitizarUrlImagen(sub.urlImagen ?? sub.UrlImagen);
            const pujas = sub.pujas ?? sub.Pujas ?? [];
            const totalOfertas = pujas.length;
            const subPrecioBase = sub.precioBase ?? sub.PrecioBase ?? 0;
            const fechaInicioStr = sub.fechaInicio ?? sub.FechaInicio;
            const fechaFinStr = sub.fechaFin ?? sub.FechaFin;
            const subDescripcion = escaparHtml(sub.descripcion ?? sub.Descripcion ?? 'Sin descripción disponible.');

            // Determinar estado de liderazgo del usuario activo en esta subasta
            const misPujasEnSub = pujas.filter(p => (p.usuarioId ?? p.UsuarioId) === usuarioActual?.id);
            let userBadgeHtml = '';
            if (misPujasEnSub.length > 0) {
                const miMax = Math.max(...misPujasEnSub.map(p => (p.monto ?? p.Monto)));
                const esLider = (miMax === pujaActual);
                if (subEstado === 'FINALIZADA') {
                    userBadgeHtml = esLider ? 
                        `<span class="badge bg-success shadow-sm extra-small"><i class="fa-solid fa-crown me-1"></i>¡Ganaste!</span>` : 
                        `<span class="badge bg-secondary shadow-sm extra-small">Finalizada</span>`;
                } else if (subEstado === 'DESIERTA') {
                    userBadgeHtml = `<span class="badge bg-dark shadow-sm extra-small">Desierta</span>`;
                } else {
                    userBadgeHtml = esLider ? 
                        `<span class="badge bg-success-subtle text-success border border-success-subtle extra-small"><i class="fa-solid fa-crown me-1"></i>Vas ganando</span>` : 
                        `<span class="badge bg-danger-subtle text-danger border border-danger-subtle extra-small"><i class="fa-solid fa-triangle-exclamation me-1"></i>Te superaron</span>`;
                }
            }

            let labelTiempo = 'Cierre:';
            let badgeTimerHtml = '';
            let btnText = 'Entrar a Sala en Vivo';
            let btnIcon = 'fa-solid fa-gavel';
            let btnClass = 'btn-primary-custom';

            if (subEstado === 'PROGRAMADA') {
                labelTiempo = 'Inicia:';
                btnText = 'Ver Subasta';
                btnIcon = 'fa-regular fa-clock';
                btnClass = 'btn-outline-warning text-dark';
                badgeTimerHtml = `
                    <span class="badge bg-warning-subtle text-dark border border-warning-subtle fw-semibold card-timer" 
                          data-subasta-id="${subId}" data-fecha-inicio="${fechaInicioStr}" data-fecha-fin="${fechaFinStr}"
                          title="Inicia el ${formatFechaHoraCortaUTC3(fechaInicioStr)}">
                        Cargando inicio...
                    </span>
                `;
            } else if (subEstado === 'ACTIVA') {
                labelTiempo = 'Cierre:';
                btnText = 'Pujar Ahora';
                btnIcon = 'fa-solid fa-gavel';
                btnClass = 'btn-primary-custom';
                badgeTimerHtml = `
                    <span class="badge bg-danger-subtle text-danger border border-danger-subtle fw-semibold card-timer" 
                          data-subasta-id="${subId}" data-fecha-inicio="${fechaInicioStr}" data-fecha-fin="${fechaFinStr}"
                          title="Cierre estimado: ${formatFechaHoraCortaUTC3(fechaFinStr)}">
                        Cargando...
                    </span>
                `;
            } else if (subEstado === 'FINALIZADA') {
                labelTiempo = 'Cerró:';
                btnText = 'Ver Subasta';
                btnIcon = 'fa-solid fa-flag-checkered';
                btnClass = 'btn-secondary';
                badgeTimerHtml = `
                    <span class="badge bg-secondary text-white border fw-semibold card-timer" 
                          data-subasta-id="${subId}" data-fecha-inicio="${fechaInicioStr}" data-fecha-fin="${fechaFinStr}">
                        ${formatFechaHoraCortaUTC3(fechaFinStr)}
                    </span>
                `;
            } else { // DESIERTA
                labelTiempo = 'Cerró:';
                btnText = 'Ver Subasta';
                btnIcon = 'fa-solid fa-ban';
                btnClass = 'btn-dark';
                badgeTimerHtml = `
                    <span class="badge bg-dark text-white border fw-semibold card-timer" 
                          data-subasta-id="${subId}" data-fecha-inicio="${fechaInicioStr}" data-fecha-fin="${fechaFinStr}">
                        DESIERTA (${formatFechaHoraCortaUTC3(fechaFinStr)})
                    </span>
                `;
            }

            const cardCol = document.createElement('div');
            cardCol.className = 'col';
            cardCol.setAttribute('data-subasta-card-id', subId);
            cardCol.innerHTML = `
                <div class="card auction-card h-100 shadow-sm">
                    <div class="card-img-wrapper">
                        <img src="${imagenUrl}" class="card-img-top" alt="${subTitulo}" onerror="this.src='assets/images/watch.png'">
                        <span class="badge-categoria"><i class="fa-solid fa-tag me-1"></i>${categoria}</span>
                        <span class="badge badge-status ${badgeClass}">${subEstado}</span>
                    </div>
                    <div class="card-body d-flex flex-column p-3">
                        <div class="d-flex justify-content-between align-items-start mb-1">
                            <h5 class="card-title text-dark fs-6 fw-bold mb-0 text-truncate flex-grow-1" title="${subTitulo}">${subTitulo}</h5>
                            <span class="card-user-leadership ms-2 flex-shrink-0">${userBadgeHtml}</span>
                        </div>
                        <p class="card-text text-muted small flex-grow-1 text-truncate-2" style="display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; height: 38px;">
                            ${subDescripcion}
                        </p>
                        
                        <div class="bg-light p-2 rounded-3 my-2 border">
                            <div class="d-flex justify-content-between align-items-center mb-1">
                                <span class="text-muted small">Puja Mayor Actual:</span>
                                <span id="card-puja-${subId}" class="fw-bold text-success fs-6 card-puja-monto">$${pujaActual.toLocaleString('es-AR')}</span>
                            </div>
                            <div class="d-flex justify-content-between align-items-center extra-small text-muted">
                                <span>Base: $${subPrecioBase.toLocaleString('es-AR')}</span>
                                <span class="card-pujas-count">${totalOfertas} oferta${totalOfertas === 1 ? '' : 's'}</span>
                            </div>
                        </div>

                        <div class="d-flex justify-content-between align-items-center mb-3">
                            <span class="text-muted small card-time-label"><i class="fa-regular fa-clock me-1"></i>${labelTiempo}</span>
                            ${badgeTimerHtml}
                        </div>

                        <button class="btn ${btnClass} btn-sm w-100 mt-auto card-action-btn" onclick="abrirSalaEnVivo(${Number(subId)})">
                            <i class="${btnIcon} me-1"></i> ${btnText}
                        </button>
                    </div>
                </div>
            `;
            grid.appendChild(cardCol);
        });

        actualizarTemporizadoresCatalogo();
        actualizarSelectorSalaDirecto();
        renderizarPaginacionCatalogo(catalogoTotalSubastas, catalogoPaginaActual, catalogoTamanoPagina);

    } catch (error) {
        grid.innerHTML = `
            <div class="col-12 text-center text-danger py-4">
                <i class="fa-solid fa-circle-exclamation fa-2x mb-2"></i>
                <p>Error al cargar el catálogo: ${escaparHtml(error.mensaje || error.message)}</p>
            </div>
        `;
        renderizarPaginacionCatalogo(0, 1, catalogoTamanoPagina);
    }
}

function renderizarPaginacionCatalogo(total, paginaActual, tamanoPagina) {
    const container = document.getElementById('catalogo-paginacion-container');
    const info = document.getElementById('catalogo-paginacion-info');
    const paginacionUl = document.getElementById('catalogo-paginacion');
    if (!container || !info || !paginacionUl) return;

    if (total <= 0) {
        info.textContent = '0 subastas encontradas';
        paginacionUl.innerHTML = '';
        return;
    }

    const totalPaginas = Math.ceil(total / tamanoPagina);
    const desde = (paginaActual - 1) * tamanoPagina + 1;
    const hasta = Math.min(paginaActual * tamanoPagina, total);

    info.innerHTML = `Mostrando <strong>${desde}</strong> a <strong>${hasta}</strong> de <strong>${total}</strong> subastas`;

    let html = '';

    const prevDisabled = paginaActual <= 1 ? 'disabled' : '';
    html += `
        <li class="page-item ${prevDisabled}">
            <button class="page-link" onclick="aplicarFiltros(${paginaActual - 1})" ${prevDisabled ? 'tabindex="-1" aria-disabled="true"' : ''}>
                <i class="fa-solid fa-chevron-left extra-small"></i>
            </button>
        </li>
    `;

    for (let p = 1; p <= totalPaginas; p++) {
        if (p === 1 || p === totalPaginas || (p >= paginaActual - 1 && p <= paginaActual + 1)) {
            const activa = p === paginaActual ? 'active fw-bold' : '';
            html += `
                <li class="page-item ${activa}">
                    <button class="page-link" onclick="aplicarFiltros(${p})">${p}</button>
                </li>
            `;
        } else if (p === paginaActual - 2 || p === paginaActual + 2) {
            html += `<li class="page-item disabled"><span class="page-link">…</span></li>`;
        }
    }

    const nextDisabled = paginaActual >= totalPaginas ? 'disabled' : '';
    html += `
        <li class="page-item ${nextDisabled}">
            <button class="page-link" onclick="aplicarFiltros(${paginaActual + 1})" ${nextDisabled ? 'tabindex="-1" aria-disabled="true"' : ''}>
                <i class="fa-solid fa-chevron-right extra-small"></i>
            </button>
        </li>
    `;

    paginacionUl.innerHTML = html;
}

function obtenerPujaMaxima(subasta) {
    if (!subasta) return 0;
    const pujas = subasta.pujas || subasta.Pujas || [];
    if (pujas.length === 0) return (subasta.precioBase ?? subasta.PrecioBase ?? 0);
    return Math.max(...pujas.map(p => (p.monto ?? p.Monto ?? 0)));
}

function getEstadoBadgeClass(estado) {
    switch (estado) {
        case 'ACTIVA': return 'bg-success text-white';
        case 'PROGRAMADA': return 'bg-warning text-dark';
        case 'FINALIZADA': return 'bg-secondary text-white';
        case 'DESIERTA': return 'bg-dark text-white';
        default: return 'bg-primary text-white';
    }
}

/**
 * Actualiza en tiempo real los datos (monto, ofertas, badge de liderazgo y estado)
 * de las miniaturas en el explorador de catálogo sin recargar toda la vista.
 */
function actualizarTarjetasCatalogo() {
    subastasCache.forEach(sub => {
        actualizarTarjetaCatalogo(sub.id ?? sub.Id);
    });
}

function actualizarTarjetaCatalogo(subastaId, nuevoMontoAnimar = null) {
    const cardCol = document.querySelector(`[data-subasta-card-id="${subastaId}"]`);
    const sub = subastasCache.find(s => (s.id ?? s.Id) == subastaId);
    if (!sub || !cardCol) return;

    const pujaActual = obtenerPujaMaxima(sub);
    const subEstado = determinarEstadoSubasta(sub);
    const fechaInicioStr = sub.fechaInicio ?? sub.FechaInicio;
    const fechaFinStr = sub.fechaFin ?? sub.FechaFin;

    // 1. Actualizar monto con animación de destello
    const montoElem = cardCol.querySelector('.card-puja-monto');
    if (montoElem) {
        montoElem.textContent = `$${pujaActual.toLocaleString('es-AR')}`;
        if (nuevoMontoAnimar !== null) {
            montoElem.classList.remove('puja-updated-flash');
            void montoElem.offsetWidth; // Force DOM reflow
            montoElem.classList.add('puja-updated-flash');
        }
    }

    // 2. Actualizar contador de ofertas
    const countElem = cardCol.querySelector('.card-pujas-count');
    if (countElem) {
        const totalPujas = (sub.pujas || sub.Pujas || []).length;
        countElem.textContent = `${totalPujas} oferta${totalPujas === 1 ? '' : 's'}`;
    }

    // 3. Actualizar badge de estado (ACTIVA / PROGRAMADA / FINALIZADA / DESIERTA)
    const badgeStatus = cardCol.querySelector('.badge-status');
    if (badgeStatus) {
        badgeStatus.className = `badge badge-status ${getEstadoBadgeClass(subEstado)}`;
        badgeStatus.textContent = subEstado;
    }

    // 4. Actualizar botón de acción de la tarjeta
    const actionBtn = cardCol.querySelector('.card-action-btn');
    if (actionBtn) {
        if (subEstado === 'PROGRAMADA') {
            actionBtn.className = 'btn btn-outline-warning text-dark btn-sm w-100 mt-auto card-action-btn';
            actionBtn.innerHTML = '<i class="fa-regular fa-clock me-1"></i> Ver Subasta';
        } else if (subEstado === 'ACTIVA') {
            actionBtn.className = 'btn btn-primary-custom btn-sm w-100 mt-auto card-action-btn';
            actionBtn.innerHTML = '<i class="fa-solid fa-gavel me-1"></i> Pujar Ahora';
        } else if (subEstado === 'FINALIZADA') {
            actionBtn.className = 'btn btn-secondary btn-sm w-100 mt-auto card-action-btn';
            actionBtn.innerHTML = '<i class="fa-solid fa-flag-checkered me-1"></i> Ver Subasta';
        } else {
            actionBtn.className = 'btn btn-dark btn-sm w-100 mt-auto card-action-btn';
            actionBtn.innerHTML = '<i class="fa-solid fa-ban me-1"></i> Ver Subasta';
        }
    }

    // 5. Actualizar etiqueta y temporizador
    const timeLabel = cardCol.querySelector('.card-time-label');
    const timerElem = cardCol.querySelector('.card-timer');
    if (timeLabel) {
        if (subEstado === 'PROGRAMADA') {
            timeLabel.innerHTML = '<i class="fa-regular fa-clock me-1"></i>Inicia:';
        } else if (subEstado === 'ACTIVA') {
            timeLabel.innerHTML = '<i class="fa-regular fa-clock me-1"></i>Cierre:';
        } else {
            timeLabel.innerHTML = '<i class="fa-solid fa-flag-checkered me-1"></i>Cerró:';
        }
    }
    if (timerElem) {
        timerElem.setAttribute('data-fecha-inicio', fechaInicioStr);
        timerElem.setAttribute('data-fecha-fin', fechaFinStr);
        if (subEstado === 'FINALIZADA') {
            timerElem.className = 'badge bg-secondary text-white border fw-semibold card-timer';
            timerElem.textContent = formatFechaHoraCortaUTC3(fechaFinStr);
        } else if (subEstado === 'DESIERTA') {
            timerElem.className = 'badge bg-dark text-white border fw-semibold card-timer';
            timerElem.textContent = `DESIERTA (${formatFechaHoraCortaUTC3(fechaFinStr)})`;
        }
    }

    // 6. Actualizar badge de liderazgo del usuario activo
    const leaderBadge = cardCol.querySelector('.card-user-leadership');
    if (leaderBadge) {
        const pujas = sub.pujas || sub.Pujas || [];
        const misPujas = pujas.filter(p => (p.usuarioId ?? p.UsuarioId) === usuarioActual?.id);
        if (misPujas.length > 0) {
            const miMax = Math.max(...misPujas.map(p => (p.monto ?? p.Monto)));
            const esLider = (miMax === pujaActual);
            if (subEstado === 'FINALIZADA') {
                leaderBadge.innerHTML = esLider ? 
                    `<span class="badge bg-success shadow-sm extra-small"><i class="fa-solid fa-crown me-1"></i>¡Ganaste!</span>` : 
                    `<span class="badge bg-secondary shadow-sm extra-small">Finalizada</span>`;
            } else if (subEstado === 'DESIERTA') {
                leaderBadge.innerHTML = `<span class="badge bg-dark shadow-sm extra-small">Desierta</span>`;
            } else {
                leaderBadge.innerHTML = esLider ? 
                    `<span class="badge bg-success-subtle text-success border border-success-subtle extra-small"><i class="fa-solid fa-crown me-1"></i>Vas ganando</span>` : 
                    `<span class="badge bg-danger-subtle text-danger border border-danger-subtle extra-small"><i class="fa-solid fa-triangle-exclamation me-1"></i>Te superaron</span>`;
            }
        } else {
            leaderBadge.innerHTML = '';
        }
    }
}

function actualizarTemporizadoresCatalogo() {
    const timers = document.querySelectorAll('.card-timer');
    const ahora = Date.now();

    timers.forEach(t => {
        const subId = parseInt(t.getAttribute('data-subasta-id'));
        const sub = subastasCache.find(s => (s.id ?? s.Id) == subId);
        if (!sub) return;

        const estadoReal = determinarEstadoSubasta(sub);
        const inicioMs = parseUtcDate(sub.fechaInicio ?? sub.FechaInicio)?.getTime() || 0;
        const finMs = parseUtcDate(sub.fechaFin ?? sub.FechaFin)?.getTime() || 0;
        const format = (n) => n.toString().padStart(2, '0');

        if (estadoReal === 'PROGRAMADA') {
            const diffInicio = inicioMs - ahora;
            if (diffInicio <= 0) {
                // La subasta acaba de iniciar: sincronizar tarjeta completa
                actualizarTarjetaCatalogo(subId);
            } else {
                const horas = Math.floor(diffInicio / (1000 * 60 * 60));
                const minutos = Math.floor((diffInicio % (1000 * 60 * 60)) / (1000 * 60));
                const segundos = Math.floor((diffInicio % (1000 * 60)) / 1000);
                t.innerHTML = `Inicia en: ${format(horas)}h ${format(minutos)}m ${format(segundos)}s`;
                t.className = 'badge bg-warning-subtle text-dark border border-warning-subtle fw-semibold card-timer';
            }
        } else if (estadoReal === 'ACTIVA') {
            const diffFin = finMs - ahora;
            if (diffFin <= 0) {
                // La subasta acaba de concluir: sincronizar tarjeta completa
                actualizarTarjetaCatalogo(subId);
            } else {
                const horas = Math.floor(diffFin / (1000 * 60 * 60));
                const minutos = Math.floor((diffFin % (1000 * 60 * 60)) / (1000 * 60));
                const segundos = Math.floor((diffFin % (1000 * 60)) / 1000);

                if (diffFin <= 60000) {
                    t.className = 'badge bg-danger text-white border border-danger fw-bold card-timer animate-pulse';
                } else if (diffFin <= 300000) {
                    t.className = 'badge bg-warning text-dark border border-warning fw-semibold card-timer';
                } else {
                    t.className = 'badge bg-danger-subtle text-danger border border-danger-subtle fw-semibold card-timer';
                }
                t.innerHTML = `${format(horas)}h ${format(minutos)}m ${format(segundos)}s`;
            }
        } else if (estadoReal === 'FINALIZADA') {
            t.innerHTML = formatFechaHoraCortaUTC3(sub.fechaFin ?? sub.FechaFin);
            t.className = 'badge bg-secondary text-white border fw-semibold card-timer';
        } else { // DESIERTA
            t.innerHTML = `DESIERTA (${formatFechaHoraCortaUTC3(sub.fechaFin ?? sub.FechaFin)})`;
            t.className = 'badge bg-dark text-white border fw-semibold card-timer';
        }
    });

    // Sincronización periódica de saldos de usuarios y tarjetas cada 4 segundos
    contadorSyncInterval++;
    if (contadorSyncInterval % 4 === 0) {
        sincronizarSaldosDropdown();
        actualizarTarjetasCatalogo();
    }
}

/* ==========================================================================
   MÓDULO 2: CREACIÓN DE SUBASTAS
   ========================================================================== */

function inicializarFechasFormulario() {
    const ahora = Date.now();
    const despues = ahora + 24 * 60 * 60 * 1000;

    const inputInicio = document.getElementById('crear-fechaInicio');
    const inputFin = document.getElementById('crear-fechaFin');

    if (inputInicio) inputInicio.value = formatDatetimeLocalUTC3(ahora);
    if (inputFin) inputFin.value = formatDatetimeLocalUTC3(despues);
}

async function guardarSubasta(event) {
    event.preventDefault();

    const titulo = document.getElementById('crear-titulo').value.trim();
    const categoriaId = parseInt(document.getElementById('crear-categoriaId').value);
    const descripcion = document.getElementById('crear-descripcion').value.trim();
    const urlImagen = document.getElementById('crear-urlImagen').value.trim();
    const precioBase = parseFloat(document.getElementById('crear-precioBase').value);
    const incrementoMinimo = parseFloat(document.getElementById('crear-incrementoMinimo').value);
    const fechaInicioVal = document.getElementById('crear-fechaInicio').value;
    const fechaFinVal = document.getElementById('crear-fechaFin').value;

    const feedback = document.getElementById('crear-subasta-feedback');
    if (feedback) feedback.classList.add('d-none');

    if (!titulo || !categoriaId) {
        if (feedback) {
            feedback.className = 'validation-feedback-box alert alert-warning py-2 small mb-3';
            feedback.innerHTML = '<i class="fa-solid fa-triangle-exclamation me-1"></i> Por favor complete el título y seleccione una categoría.';
            feedback.classList.remove('d-none');
        }
        mostrarToast('Complete el título y la categoría.', 'Campos Requeridos', 'warning');
        return;
    }

    if (precioBase <= 0 || isNaN(precioBase)) {
        if (feedback) {
            feedback.className = 'validation-feedback-box alert alert-danger py-2 small mb-3';
            feedback.innerHTML = '<i class="fa-solid fa-circle-exclamation me-1"></i> El precio base debe ser mayor a $0.';
            feedback.classList.remove('d-none');
        }
        mostrarToast('El precio base debe ser mayor a 0.', 'Error de Validación', 'danger');
        return;
    }

    if (incrementoMinimo <= 0 || isNaN(incrementoMinimo)) {
        if (feedback) {
            feedback.className = 'validation-feedback-box alert alert-danger py-2 small mb-3';
            feedback.innerHTML = '<i class="fa-solid fa-circle-exclamation me-1"></i> El incremento mínimo debe ser mayor a $0.';
            feedback.classList.remove('d-none');
        }
        mostrarToast('El incremento mínimo debe ser mayor a 0.', 'Error de Validación', 'danger');
        return;
    }

    if (!fechaInicioVal || !fechaFinVal) {
        if (feedback) {
            feedback.className = 'validation-feedback-box alert alert-warning py-2 small mb-3';
            feedback.innerHTML = '<i class="fa-solid fa-calendar-xmark me-1"></i> Complete ambas fechas: inicio y cierre.';
            feedback.classList.remove('d-none');
        }
        mostrarToast('Complete las fechas de inicio y cierre.', 'Campos Requeridos', 'warning');
        return;
    }

    // Convertir fecha de inicio y fin ingresadas hacia formato ISO UTC estándar con "Z"
    const fechaInicioIso = utc3InputToIsoUtc(fechaInicioVal);
    const fechaFinIso = utc3InputToIsoUtc(fechaFinVal);

    if (new Date(fechaInicioIso) >= new Date(fechaFinIso)) {
        if (feedback) {
            feedback.className = 'validation-feedback-box alert alert-danger py-2 small mb-3';
            feedback.innerHTML = '<i class="fa-solid fa-clock me-1"></i> La fecha de inicio debe ser anterior a la fecha de finalización.';
            feedback.classList.remove('d-none');
        }
        mostrarToast('La fecha de inicio debe ser anterior a la fecha de finalización.', 'Error de Fechas', 'danger');
        return;
    }

    setBotonCargando('btn-crear-subasta', 'spinner-crear', 'text-crear', true, '<i class="fa-solid fa-circle-notch fa-spin me-2"></i> Publicando subasta...');

    const esProgramada = new Date(fechaInicioIso) > new Date();
    const nuevaSubastaDto = {
        titulo,
        descripcion,
        urlImagen: urlImagen || 'assets/images/watch.png',
        precioBase,
        incrementoMinimo,
        fechaInicio: fechaInicioIso,
        fechaFin: fechaFinIso,
        estado: esProgramada ? 'PROGRAMADA' : 'ACTIVA',
        categoriaId,
        vendedorId: usuarioActual.id
    };

    try {
        const creada = await fetchCrearSubasta(nuevaSubastaDto);
        mostrarToast(`¡La subasta "${creada.titulo || titulo}" fue publicada con éxito! Guardada en UTC.`, 'Publicación Exitosa', 'success');

        document.getElementById('form-crear-subasta').reset();
        inicializarFechasFormulario();
        document.getElementById('crear-img-preview').src = 'assets/images/watch.png';
        if (feedback) feedback.classList.add('d-none');

        await aplicarFiltros();
        await cargarMisActividades();
        const tabCatalogoBtn = document.getElementById('tab-catalogo');
        if (tabCatalogoBtn) {
            const bsTab = new bootstrap.Tab(tabCatalogoBtn);
            bsTab.show();
        }

    } catch (err) {
        if (feedback) {
            feedback.className = 'validation-feedback-box alert alert-danger py-2 small mb-3';
            feedback.innerHTML = `<i class="fa-solid fa-circle-exclamation me-1"></i> Error al publicar: ${escaparHtml(err.mensaje || err.message)}`;
            feedback.classList.remove('d-none');
        }
        if (err.status === 0 || err.status >= 500) {
            mostrarToast('Servidor no disponible.', 'Error de Conexión', 'danger', () => guardarSubasta(event));
        } else {
            mostrarToast(`Error al crear subasta: ${escaparHtml(err.mensaje || err.message)}`, 'Error de Servidor', 'danger');
        }
    } finally {
        setBotonCargando('btn-crear-subasta', 'spinner-crear', 'text-crear', false);
    }
}

/* ==========================================================================
   MÓDULO 3: SALA EN VIVO & ANTI-SNIPING
   ========================================================================== */

function abrirSalaEnVivo(subastaId) {
    const tabSalaBtn = document.getElementById('tab-sala');
    if (tabSalaBtn) {
        const bsTab = new bootstrap.Tab(tabSalaBtn);
        bsTab.show();
    }
    cargarSalaEnVivo(subastaId);
}

function actualizarSelectorSalaDirecto() {
    const select = document.getElementById('sala-subasta-selector');
    if (!select) return;

    select.innerHTML = '<option value="">-- Seleccionar Subasta --</option>';
    subastasCache.forEach(s => {
        const est = determinarEstadoSubasta(s);
        select.innerHTML += `<option value="${Number(s.id)}">[${est}] ${escaparHtml(s.titulo)} - ($${obtenerPujaMaxima(s).toLocaleString('es-AR')})</option>`;
    });

    if (subastaSeleccionadaSala) {
        select.value = subastaSeleccionadaSala.id;
    }
}

async function cargarSalaEnVivo(subastaId) {
    if (!subastaId) {
        if (subastasCache.length > 0) subastaId = subastasCache[0].id;
        else return;
    }

    try {
        subastaSeleccionadaSala = await fetchObtenerSubastaPorId(subastaId);
        if (!subastaSeleccionadaSala.pujas) subastaSeleccionadaSala.pujas = [];

        document.getElementById('sala-titulo').textContent = subastaSeleccionadaSala.titulo;
        document.getElementById('sala-descripcion').textContent = subastaSeleccionadaSala.descripcion || 'Sin descripción.';
        document.getElementById('sala-imagen').src = sanitizarUrlImagen(subastaSeleccionadaSala.urlImagen);
        
        const catNombre = categoriasCache.find(c => c.id == subastaSeleccionadaSala.categoriaId)?.nombre || 'General';
        document.getElementById('sala-categoria-badge').textContent = catNombre;

        const estadoReal = determinarEstadoSubasta(subastaSeleccionadaSala);
        const estadoBadge = document.getElementById('sala-estado-badge');
        if (estadoBadge) {
            estadoBadge.className = `badge ${getEstadoBadgeClass(estadoReal)}`;
            if (estadoReal === 'PROGRAMADA') {
                estadoBadge.textContent = `PROGRAMADA (Inicia: ${formatFechaHoraCortaUTC3(subastaSeleccionadaSala.fechaInicio)})`;
            } else if (estadoReal === 'FINALIZADA') {
                estadoBadge.textContent = `FINALIZADA (${formatFechaHoraCortaUTC3(subastaSeleccionadaSala.fechaFin)})`;
            } else if (estadoReal === 'DESIERTA') {
                estadoBadge.textContent = 'DESIERTA (Sin ofertas)';
            } else {
                estadoBadge.textContent = 'ACTIVA (En Vivo)';
            }
        }

        document.getElementById('sala-vendedor').textContent = `Vendedor ID: #${subastaSeleccionadaSala.vendedorId}`;
        document.getElementById('sala-precio-base').textContent = `$${subastaSeleccionadaSala.precioBase.toLocaleString('es-AR')}`;
        document.getElementById('sala-incremento-min').textContent = `$${subastaSeleccionadaSala.incrementoMinimo.toLocaleString('es-AR')}`;

        if (timerSalaInterval) clearInterval(timerSalaInterval);
        timerSalaInterval = setInterval(actualizarRelojSala, 1000);
        actualizarRelojSala();

        configurarBotonesPujaRapida(subastaSeleccionadaSala.incrementoMinimo);
        actualizarMonitorPujasSala();

        const select = document.getElementById('sala-subasta-selector');
        if (select) select.value = subastaSeleccionadaSala.id;

        await validarPujaEnVivo();

        iniciarPollingSala();
        detenerPollingCatalogo();

    } catch (e) {
        mostrarToast('No se pudo cargar la sala en vivo.', 'Error', 'danger');
    }
}

/* ==========================================================================
   MÓDULO DE SINCRONIZACIÓN EN VIVO (POLLING RECURSIVO CON SETTIMEOUT)
   ========================================================================== */

function calcularSiUsuarioEsLider(subasta, usuarioId) {
    if (!subasta || !usuarioId) return false;
    const pujas = subasta.pujas || subasta.Pujas || [];
    if (pujas.length === 0) return false;
    const sorted = [...pujas].sort((a, b) => (b.monto ?? b.Monto ?? 0) - (a.monto ?? a.Monto ?? 0));
    const mayor = sorted[0];
    return (mayor.usuarioId ?? mayor.UsuarioId) === usuarioId;
}

function iniciarPollingSala() {
    detenerPollingSala();
    if (!subastaSeleccionadaSala || !usuarioActual) return;
    timerPollingSala = setTimeout(ejecutarPollingSala, 2500);
}

function detenerPollingSala() {
    if (timerPollingSala) {
        clearTimeout(timerPollingSala);
        timerPollingSala = null;
    }
    sincronizandoSala = false;
}

async function ejecutarPollingSala() {
    const tabSala = document.getElementById('content-sala');
    if (!tabSala || !tabSala.classList.contains('active') || !subastaSeleccionadaSala || !usuarioActual) {
        detenerPollingSala();
        return;
    }

    if (document.hidden) {
        timerPollingSala = setTimeout(ejecutarPollingSala, 2500);
        return;
    }

    if (sincronizandoSala) {
        timerPollingSala = setTimeout(ejecutarPollingSala, 2500);
        return;
    }

    sincronizandoSala = true;
    try {
        const subId = subastaSeleccionadaSala.id ?? subastaSeleccionadaSala.Id;
        const [subastaFresca, billeteraFresca] = await Promise.all([
            fetchObtenerSubastaPorId(subId),
            fetchObtenerBilletera(usuarioActual.id).catch(() => null)
        ]);

        if (subastaFresca && subastaSeleccionadaSala && (subastaSeleccionadaSala.id ?? subastaSeleccionadaSala.Id) === subId) {
            const versionPrevia = Number(subastaSeleccionadaSala.version ?? subastaSeleccionadaSala.Version ?? 0);
            const versionNueva = Number(subastaFresca.version ?? subastaFresca.Version ?? 0);
            const cantPujasPrevia = (subastaSeleccionadaSala.pujas || subastaSeleccionadaSala.Pujas || []).length;
            const cantPujasNueva = (subastaFresca.pujas || subastaFresca.Pujas || []).length;
            const fechaFinPrevia = subastaSeleccionadaSala.fechaFin ?? subastaSeleccionadaSala.FechaFin;
            const fechaFinNueva = subastaFresca.fechaFin ?? subastaFresca.FechaFin;

            const eraLider = calcularSiUsuarioEsLider(subastaSeleccionadaSala, usuarioActual.id);
            const esLiderAhora = calcularSiUsuarioEsLider(subastaFresca, usuarioActual.id);

            // Toast cuando el usuario deja de ser líder
            if (eraLider && !esLiderAhora) {
                const pujaActual = obtenerPujaMaxima(subastaFresca);
                mostrarToast(`⚡ <strong>¡Tu oferta ha sido superada!</strong> La puja líder actual es de $${pujaActual.toLocaleString('es-AR')}.`, 'Oferta Superada (Outbid)', 'warning');
            }

            const huboCambio = (versionNueva !== versionPrevia) || 
                              (cantPujasNueva !== cantPujasPrevia) ||
                              (fechaFinNueva !== fechaFinPrevia);

            if (huboCambio) {
                subastaSeleccionadaSala = subastaFresca;
                if (!subastaSeleccionadaSala.pujas) subastaSeleccionadaSala.pujas = [];

                actualizarMonitorPujasSala();
                actualizarRelojSala();
                configurarBotonesPujaRapida(subastaSeleccionadaSala.incrementoMinimo ?? subastaSeleccionadaSala.IncrementoMinimo ?? 1000);
                await validarPujaEnVivo();

                const estadoReal = determinarEstadoSubasta(subastaSeleccionadaSala);
                const estadoBadge = document.getElementById('sala-estado-badge');
                if (estadoBadge) {
                    estadoBadge.className = `badge ${getEstadoBadgeClass(estadoReal)}`;
                    if (estadoReal === 'PROGRAMADA') {
                        estadoBadge.textContent = `PROGRAMADA (Inicia: ${formatFechaHoraCortaUTC3(subastaSeleccionadaSala.fechaInicio)})`;
                    } else if (estadoReal === 'FINALIZADA') {
                        estadoBadge.textContent = `FINALIZADA (${formatFechaHoraCortaUTC3(subastaSeleccionadaSala.fechaFin)})`;
                    } else if (estadoReal === 'DESIERTA') {
                        estadoBadge.textContent = 'DESIERTA (Sin ofertas)';
                    } else {
                        estadoBadge.textContent = 'ACTIVA (En Vivo)';
                    }
                }

                const idx = subastasCache.findIndex(s => (s.id ?? s.Id) === subId);
                if (idx !== -1) {
                    subastasCache[idx] = { ...subastasCache[idx], ...subastaFresca };
                    actualizarTarjetaCatalogo(subId, obtenerPujaMaxima(subastaFresca));
                }
            }

            if (billeteraFresca) {
                await actualizarBilleteraUI();
                await sincronizarSaldosDropdown();
            }
        }
    } catch (err) {
        console.warn("[SubastaYa Polling Sala] Error al sincronizar sala:", err);
    } finally {
        sincronizandoSala = false;
        if (timerPollingSala !== null) {
            timerPollingSala = setTimeout(ejecutarPollingSala, 2500);
        }
    }
}

function iniciarPollingCatalogo() {
    detenerPollingCatalogo();
    timerPollingCatalogo = setTimeout(ejecutarPollingCatalogo, 5000);
}

function detenerPollingCatalogo() {
    if (timerPollingCatalogo) {
        clearTimeout(timerPollingCatalogo);
        timerPollingCatalogo = null;
    }
    sincronizandoCatalogo = false;
}

async function ejecutarPollingCatalogo() {
    const tabCat = document.getElementById('content-catalogo');
    if (!tabCat || !tabCat.classList.contains('active')) {
        detenerPollingCatalogo();
        return;
    }

    if (document.hidden) {
        timerPollingCatalogo = setTimeout(ejecutarPollingCatalogo, 5000);
        return;
    }

    if (sincronizandoCatalogo) {
        timerPollingCatalogo = setTimeout(ejecutarPollingCatalogo, 5000);
        return;
    }

    sincronizandoCatalogo = true;
    try {
        await aplicarFiltros(catalogoPaginaActual, true);
    } catch (e) {
        console.warn("[SubastaYa Polling Catálogo] Error en refresco:", e);
    } finally {
        sincronizandoCatalogo = false;
        if (timerPollingCatalogo !== null) {
            timerPollingCatalogo = setTimeout(ejecutarPollingCatalogo, 5000);
        }
    }
}

function actualizarRelojSala() {
    if (!subastaSeleccionadaSala) return;

    const timerBox = document.getElementById('sala-timer-box');
    const timerElem = document.getElementById('sala-timer-digits');
    const antiSnipingBadge = document.getElementById('sala-antisniping-alert');
    const btnPuja = document.getElementById('btn-realizar-puja');
    if (!timerElem) return;

    const estadoReal = determinarEstadoSubasta(subastaSeleccionadaSala);
    const ahora = Date.now();
    const format = (n) => n.toString().padStart(2, '0');

    if (estadoReal === 'PROGRAMADA') {
        const inicioMs = parseUtcDate(subastaSeleccionadaSala.fechaInicio).getTime();
        const diffInicio = inicioMs - ahora;

        if (diffInicio <= 0) {
            // Acaba de iniciar: refrescar
            cargarSalaEnVivo(subastaSeleccionadaSala.id);
            return;
        }

        const hrs = Math.floor(diffInicio / (1000 * 60 * 60));
        const mins = Math.floor((diffInicio % (1000 * 60 * 60)) / (1000 * 60));
        const secs = Math.floor((diffInicio % (1000 * 60)) / 1000);

        timerElem.textContent = `INICIA EN: ${format(hrs)}:${format(mins)}:${format(secs)}`;
        if (timerBox) timerBox.className = 'timer-box bg-warning-subtle text-dark border-warning';
        if (antiSnipingBadge) antiSnipingBadge.className = 'd-none';

        if (btnPuja && !btnPuja.classList.contains('btn-loading')) {
            btnPuja.disabled = true;
            const textPuja = document.getElementById('text-puja');
            if (textPuja) textPuja.innerHTML = '<i class="fa-regular fa-clock me-2"></i> Subasta Programada';
        }
    } else if (estadoReal === 'FINALIZADA' || estadoReal === 'DESIERTA') {
        timerElem.textContent = `00:00:00 - ${estadoReal}`;
        if (timerBox) timerBox.className = 'timer-box bg-secondary border-secondary';
        if (antiSnipingBadge) antiSnipingBadge.className = 'd-none';

        if (btnPuja && !btnPuja.classList.contains('btn-loading')) {
            btnPuja.disabled = true;
            const textPuja = document.getElementById('text-puja');
            if (textPuja) textPuja.innerHTML = `<i class="fa-solid fa-flag-checkered me-2"></i> Subasta Concluida (${estadoReal})`;
        }
    } else { // ACTIVA
        const finMs = parseUtcDate(subastaSeleccionadaSala.fechaFin).getTime();
        const diff = finMs - ahora;

        if (diff <= 0) {
            // Acaba de finalizar: refrescar
            cargarSalaEnVivo(subastaSeleccionadaSala.id);
            return;
        }

        const hrs = Math.floor(diff / (1000 * 60 * 60));
        const mins = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
        const secs = Math.floor((diff % (1000 * 60)) / 1000);
        timerElem.textContent = `${format(hrs)}:${format(mins)}:${format(secs)}`;

        if (diff <= 60000) {
            if (timerBox) timerBox.className = 'timer-box timer-critical-alert';
            if (antiSnipingBadge) {
                antiSnipingBadge.className = 'anti-sniping-badge bg-danger text-white mt-2';
                antiSnipingBadge.innerHTML = '<i class="fa-solid fa-shield-cat me-1"></i> ¡ZONA CRÍTICA ANTI-SNIPING! Nueva puja extenderá +120s';
            }
        } else if (diff <= 300000) {
            if (timerBox) timerBox.className = 'timer-box timer-warning-alert';
            if (antiSnipingBadge) {
                antiSnipingBadge.className = 'anti-sniping-badge bg-warning text-dark mt-2';
                antiSnipingBadge.innerHTML = '<i class="fa-solid fa-triangle-exclamation me-1"></i> Próximo a cerrar (Cierre inminente)';
            }
        } else {
            if (timerBox) timerBox.className = 'timer-box';
            if (antiSnipingBadge) antiSnipingBadge.className = 'd-none';
        }
    }
}

function configurarBotonesPujaRapida(incrementoMinimo) {
    const contenedor = document.getElementById('quick-bids-container');
    if (!contenedor) return;

    const val1 = incrementoMinimo;
    const val2 = incrementoMinimo * 2;
    const val3 = incrementoMinimo * 5;

    contenedor.innerHTML = `
        <button type="button" class="btn btn-outline-primary-custom btn-sm" onclick="setMontoPujaSugerido(${val1})">+$${val1.toLocaleString('es-AR')}</button>
        <button type="button" class="btn btn-outline-primary-custom btn-sm" onclick="setMontoPujaSugerido(${val2})">+$${val2.toLocaleString('es-AR')}</button>
        <button type="button" class="btn btn-outline-primary-custom btn-sm" onclick="setMontoPujaSugerido(${val3})">+$${val3.toLocaleString('es-AR')}</button>
    `;

    const pujaActual = obtenerPujaMaxima(subastaSeleccionadaSala);
    const montoRecomendado = pujaActual + incrementoMinimo;
    const inputPuja = document.getElementById('input-monto-puja');
    if (inputPuja) inputPuja.value = montoRecomendado;
}

function setMontoPujaSugerido(offset) {
    if (!subastaSeleccionadaSala) return;
    const pujaActual = obtenerPujaMaxima(subastaSeleccionadaSala);
    const inputPuja = document.getElementById('input-monto-puja');
    if (inputPuja) {
        inputPuja.value = pujaActual + offset;
        validarPujaEnVivo();
    }
}

function actualizarMonitorPujasSala() {
    if (!subastaSeleccionadaSala) return;

    const pujaActual = obtenerPujaMaxima(subastaSeleccionadaSala);
    document.getElementById('sala-puja-actual').textContent = `$${pujaActual.toLocaleString('es-AR')}`;

    const bannerLiderazgo = document.getElementById('sala-banner-liderazgo');
    const pujas = subastaSeleccionadaSala.pujas || subastaSeleccionadaSala.Pujas || [];

    if (pujas.length > 0) {
        const pujasSorted = [...pujas].sort((a, b) => (b.monto ?? b.Monto) - (a.monto ?? a.Monto));
        const mayorPuja = pujasSorted[0];
        const esMiPuja = (mayorPuja.usuarioId ?? mayorPuja.UsuarioId) === usuarioActual.id;

        if (esMiPuja) {
            bannerLiderazgo.className = 'status-banner-leading p-3 mb-3 d-flex align-items-center';
            bannerLiderazgo.innerHTML = `
                <i class="fa-solid fa-trophy fs-3 me-3 text-warning"></i>
                <div>
                    <h6 class="fw-bold mb-0">¡ESTÁS LIDERANDO LA PUJA! (${escaparHtml(usuarioActual.nombre)})</h6>
                    <small>Tu oferta de $${(mayorPuja.monto ?? mayorPuja.Monto).toLocaleString('es-AR')} es la mayor actual y se encuentra retenida en garantía Escrow.</small>
                </div>
            `;
        } else {
            const usuarioParticipo = pujas.some(p => (p.usuarioId ?? p.UsuarioId) === usuarioActual.id);
            if (usuarioParticipo) {
                bannerLiderazgo.className = 'status-banner-outbid p-3 mb-3 d-flex align-items-center';
                bannerLiderazgo.innerHTML = `
                    <i class="fa-solid fa-triangle-exclamation fs-3 me-3 text-danger"></i>
                    <div>
                        <h6 class="fw-bold mb-0">¡HAS SIDO SUPERADO! (OUTBID)</h6>
                        <small>Otro postor ha realizado una oferta de $${(mayorPuja.monto ?? mayorPuja.Monto).toLocaleString('es-AR')}. Incrementa tu oferta para recuperar la delantera.</small>
                    </div>
                `;
            } else {
                bannerLiderazgo.className = 'alert alert-secondary p-3 mb-3 d-flex align-items-center';
                bannerLiderazgo.innerHTML = `
                    <i class="fa-solid fa-info-circle fs-3 me-3"></i>
                    <div>
                        <h6 class="fw-bold mb-0">Puja Líder Actual: $${(mayorPuja.monto ?? mayorPuja.Monto).toLocaleString('es-AR')}</h6>
                        <small>Inicia tu puja para ingresar al remate en vivo.</small>
                    </div>
                `;
            }
        }
    } else {
        const precioBase = subastaSeleccionadaSala.precioBase ?? subastaSeleccionadaSala.PrecioBase ?? 0;
        bannerLiderazgo.className = 'alert alert-light border p-3 mb-3 d-flex align-items-center';
        bannerLiderazgo.innerHTML = `
            <i class="fa-solid fa-gavel fs-3 me-3 text-secondary"></i>
            <div>
                <h6 class="fw-bold mb-0">Sin ofertas registradas</h6>
                <small>Sé el primer postor realizando una oferta desde el precio base de $${precioBase.toLocaleString('es-AR')}.</small>
            </div>
        `;
    }

    const listaHistorial = document.getElementById('sala-historial-pujas');
    if (!listaHistorial) return;

    if (pujas.length === 0) {
        listaHistorial.innerHTML = `<div class="text-center text-muted py-4 small">Aún no hay ofertas en esta sala.</div>`;
        return;
    }

    listaHistorial.innerHTML = '';
    const pujasOrdenadas = [...pujas].sort((a, b) => (b.monto ?? b.Monto) - (a.monto ?? a.Monto));

    pujasOrdenadas.forEach((p, index) => {
        const esLider = index === 0;
        const esPropia = (p.usuarioId ?? p.UsuarioId) === usuarioActual.id;
        const pMonto = p.monto ?? p.Monto ?? 0;
        const fechaFormat = formatFechaHoraCortaUTC3(p.fechaCreacion ?? p.FechaCreacion);
        const pUid = Number(p.usuarioId ?? p.UsuarioId);
        const rawAnonHandle = p.postorAnonimo || `Postor #${(pUid * 33 + 100).toString(16).toUpperCase()}`;
        const anonHandle = escaparHtml(rawAnonHandle);

        const item = document.createElement('div');
        item.className = `bid-item p-2 mb-2 ${esLider ? 'highest' : ''}`;
        item.innerHTML = `
            <div class="d-flex justify-content-between align-items-center">
                <div>
                    ${esPropia ? 
                        `<span class="badge-postor-tu"><i class="fa-solid fa-user me-1"></i> Tú (${anonHandle})</span>` : 
                        `<span class="badge-postor-anon"><i class="fa-solid fa-user-secret me-1"></i> ${anonHandle}</span>`}
                    ${esLider ? '<span class="badge bg-warning text-dark ms-1">Oferta Líder</span>' : ''}
                    <div class="text-muted extra-small mt-1">${fechaFormat}</div>
                </div>
                <div class="text-end">
                    <span class="fw-bold fs-6 ${esLider ? 'text-success' : 'text-dark'}">$${pMonto.toLocaleString('es-AR')}</span>
                    <div class="extra-small text-muted">${esLider ? 'Garantía Escrow Retenida' : 'Garantía Liberada'}</div>
                </div>
            </div>
        `;
        listaHistorial.appendChild(item);
    });
}

async function validarPujaEnVivo() {
    const inputMonto = document.getElementById('input-monto-puja');
    const btnPuja = document.getElementById('btn-realizar-puja');
    const textPuja = document.getElementById('text-puja');
    const feedbackBox = document.getElementById('bid-validation-feedback');
    const badgeMinimo = document.getElementById('puja-minima-badge');

    if (!inputMonto || !btnPuja || !feedbackBox) return false;

    if (!subastaSeleccionadaSala) {
        btnPuja.disabled = true;
        feedbackBox.classList.add('d-none');
        return false;
    }

    const estadoReal = determinarEstadoSubasta(subastaSeleccionadaSala);
    const precioBase = subastaSeleccionadaSala.precioBase ?? subastaSeleccionadaSala.PrecioBase ?? 0;
    const incrementoMin = subastaSeleccionadaSala.incrementoMinimo ?? subastaSeleccionadaSala.IncrementoMinimo ?? 1000;
    const pujaActual = obtenerPujaMaxima(subastaSeleccionadaSala);
    const pujas = subastaSeleccionadaSala.pujas || subastaSeleccionadaSala.Pujas || [];

    const montoMinimoPermitido = pujas.length > 0 ? (pujaActual + incrementoMin) : precioBase;
    if (badgeMinimo) {
        badgeMinimo.textContent = `Mínimo requerido: $${montoMinimoPermitido.toLocaleString('es-AR')}`;
    }

    // 1. Validar estado de la subasta
    if (estadoReal !== 'ACTIVA') {
        inputMonto.disabled = true;
        btnPuja.disabled = true;
        if (textPuja) textPuja.innerHTML = `<i class="fa-solid fa-lock me-2"></i> Subasta ${estadoReal}`;
        feedbackBox.className = 'validation-feedback-box alert alert-secondary py-2 small mb-0';
        feedbackBox.innerHTML = `<i class="fa-solid fa-lock me-1"></i> Esta subasta se encuentra <strong>${estadoReal}</strong>. No acepta nuevas ofertas en este momento.`;
        feedbackBox.classList.remove('d-none');
        return false;
    }

    // 2. Validar si el usuario logueado es el vendedor
    const vendedorId = subastaSeleccionadaSala.vendedorId ?? subastaSeleccionadaSala.VendedorId;
    if (usuarioActual && vendedorId === usuarioActual.id) {
        inputMonto.disabled = true;
        btnPuja.disabled = true;
        if (textPuja) textPuja.innerHTML = '<i class="fa-solid fa-hand me-2"></i> No puedes autopujar';
        feedbackBox.className = 'validation-feedback-box alert alert-warning py-2 small mb-0';
        feedbackBox.innerHTML = `<i class="fa-solid fa-hand me-1"></i> Eres el <strong>Vendedor</strong> de este artículo. Las reglas del sistema impiden que ofertes en tu propia publicación.`;
        feedbackBox.classList.remove('d-none');
        return false;
    }

    // Si la subasta está activa y no es el vendedor, el input está habilitado
    inputMonto.disabled = false;

    // 3. Validar si ya es el postor líder
    let esMiPujaLider = false;
    let montoLiderPrevio = 0;
    if (pujas.length > 0) {
        const pujasSorted = [...pujas].sort((a, b) => (b.monto ?? b.Monto) - (a.monto ?? a.Monto));
        const mayorPuja = pujasSorted[0];
        esMiPujaLider = (mayorPuja.usuarioId ?? mayorPuja.UsuarioId) === (usuarioActual ? usuarioActual.id : null);
        if (esMiPujaLider) {
            montoLiderPrevio = mayorPuja.monto ?? mayorPuja.Monto ?? 0;
        }
    }

    // 4. Validar monto ingresado
    const montoIngresado = parseFloat(inputMonto.value);
    if (isNaN(montoIngresado) || montoIngresado <= 0) {
        btnPuja.disabled = true;
        if (textPuja) textPuja.innerHTML = '<i class="fa-solid fa-bolt me-2"></i> Confirmar y Enviar Puja';
        feedbackBox.className = 'validation-feedback-box alert alert-light border py-1 px-2 extra-small mb-0 text-muted';
        feedbackBox.innerHTML = `<i class="fa-solid fa-calculator me-1"></i> Ingrese un monto de al menos <strong>$${montoMinimoPermitido.toLocaleString('es-AR')}</strong> para ofertar.`;
        feedbackBox.classList.remove('d-none');
        return false;
    }

    if (montoIngresado < montoMinimoPermitido) {
        btnPuja.disabled = true;
        if (textPuja) textPuja.innerHTML = '<i class="fa-solid fa-circle-exclamation me-2"></i> Monto Insuficiente';
        feedbackBox.className = 'validation-feedback-box alert alert-danger py-2 small mb-0';
        feedbackBox.innerHTML = `<i class="fa-solid fa-circle-exclamation me-1"></i> Oferta insuficiente. El incremento mínimo es de $${incrementoMin.toLocaleString('es-AR')} (Mínimo a ofertar: <strong>$${montoMinimoPermitido.toLocaleString('es-AR')}</strong>).`;
        feedbackBox.classList.remove('d-none');
        return false;
    }

    // 5. Validar saldo disponible en billetera (si ya es líder, solo requiere cubrir el incremento)
    let saldoDisponible = 0;
    try {
        const b = await fetchObtenerBilletera(usuarioActual.id);
        saldoDisponible = b ? (b.saldoDisponible ?? b.SaldoDisponible ?? 0) : 0;
    } catch (_) {
        saldoDisponible = 0;
    }

    const garantiaRequerida = esMiPujaLider ? Math.max(0, montoIngresado - montoLiderPrevio) : montoIngresado;

    if (saldoDisponible < garantiaRequerida) {
        btnPuja.disabled = true;
        if (textPuja) textPuja.innerHTML = '<i class="fa-solid fa-wallet me-2"></i> Saldo Insuficiente';
        const faltante = garantiaRequerida - saldoDisponible;
        feedbackBox.className = 'validation-feedback-box alert alert-danger py-2 small mb-0 d-flex justify-content-between align-items-center';
        feedbackBox.innerHTML = `
            <div>
                <i class="fa-solid fa-wallet me-1"></i> Saldo disponible: <strong>$${saldoDisponible.toLocaleString('es-AR')}</strong>. Te faltan <strong>$${faltante.toLocaleString('es-AR')}</strong> para respaldar la garantía.
            </div>
            <button type="button" class="btn btn-sm btn-success py-0 px-2 fw-bold extra-small" onclick="cargarSaldoLibreRapido(${Math.max(100000, Math.ceil(faltante))})">
                <i class="fa-solid fa-plus me-1"></i>Cargar
            </button>
        `;
        feedbackBox.classList.remove('d-none');
        return false;
    }

    // Todo es válido para ofertar
    btnPuja.disabled = false;
    if (textPuja) textPuja.innerHTML = '<i class="fa-solid fa-bolt me-2"></i> Confirmar y Enviar Puja';
    
    if (esMiPujaLider) {
        feedbackBox.className = 'validation-feedback-box alert alert-info py-1 px-2 extra-small mb-0';
        feedbackBox.innerHTML = `<i class="fa-solid fa-crown text-warning me-1"></i> Ya eres el postor líder. Al confirmar, tu nueva oferta de <strong>$${montoIngresado.toLocaleString('es-AR')}</strong> retendrá únicamente el incremento ($${(garantiaRequerida).toLocaleString('es-AR')}) en Escrow.`;
    } else {
        feedbackBox.className = 'validation-feedback-box alert alert-success-subtle border border-success-subtle text-success py-1 px-2 extra-small mb-0';
        feedbackBox.innerHTML = `<i class="fa-solid fa-shield-check me-1"></i> Oferta válida por <strong>$${montoIngresado.toLocaleString('es-AR')}</strong>. Se retendrá en garantía Escrow preventiva.`;
    }
    feedbackBox.classList.remove('d-none');
    return true;
}

async function enviarPuja(event) {
    event.preventDefault();

    if (!subastaSeleccionadaSala) return;

    // Validación preventiva en cliente antes de procesar cualquier transacción
    const esValida = await validarPujaEnVivo();
    if (!esValida) return;

    const montoInput = parseFloat(document.getElementById('input-monto-puja').value);
    const pujaActual = obtenerPujaMaxima(subastaSeleccionadaSala);
    const incrementoMin = subastaSeleccionadaSala.incrementoMinimo ?? subastaSeleccionadaSala.IncrementoMinimo ?? 1000;

    // Activar estado de carga interactivo en botón de puja (previene doble clic)
    setBotonCargando('btn-realizar-puja', 'spinner-puja', 'text-puja', true, '<i class="fa-solid fa-circle-notch fa-spin me-2"></i> Procesando puja en Escrow...');

    // 1. Obtener la subasta fresca de la API para asegurar que la versión esté sincronizada
    try {
        const subastaFresca = await fetchObtenerSubastaPorId(subastaSeleccionadaSala.id);
        subastaSeleccionadaSala.version = subastaFresca.version ?? subastaFresca.Version ?? 1;
        subastaSeleccionadaSala.pujas = subastaFresca.pujas ?? subastaFresca.Pujas ?? [];
    } catch (e) {
        console.warn("No se pudo refrescar la subasta antes de pujar, usando versión local.", e);
    }

    try {
        // Llamada formal a la API REST para asentar la puja, retener Escrow y validar concurrencia optimista de forma atómica
        const versionActual = Number(subastaSeleccionadaSala.version ?? subastaSeleccionadaSala.Version ?? 1);
        const resPuja = await fetchRegistrarPuja(subastaSeleccionadaSala.id, usuarioActual.id, montoInput, versionActual);

        const nuevaPuja = resPuja ? resPuja.puja : null;
        if (!subastaSeleccionadaSala.pujas) subastaSeleccionadaSala.pujas = [];
        if (nuevaPuja && !subastaSeleccionadaSala.pujas.some(p => p.id === nuevaPuja.id)) {
            subastaSeleccionadaSala.pujas.push(nuevaPuja);
        }

        if (resPuja && resPuja.version !== undefined) {
            subastaSeleccionadaSala.version = resPuja.version;
        }
        if (resPuja && resPuja.fechaFin) {
            subastaSeleccionadaSala.fechaFin = resPuja.fechaFin;
        }

        const antiSnipingActivado = Boolean(resPuja && resPuja.antiSnipingActivado);

        const indexSub = subastasCache.findIndex(s => s.id === subastaSeleccionadaSala.id);
        if (indexSub !== -1) {
            if (!subastasCache[indexSub].pujas) subastasCache[indexSub].pujas = [];
            if (nuevaPuja && !subastasCache[indexSub].pujas.some(p => p.id === nuevaPuja.id)) {
                subastasCache[indexSub].pujas.push(nuevaPuja);
            }
            subastasCache[indexSub].fechaFin = subastaSeleccionadaSala.fechaFin;
            subastasCache[indexSub].version = subastaSeleccionadaSala.version;
        }

        if (antiSnipingActivado) {
            mostrarToast(`⚡ <strong>¡REGLA ANTI-SNIPING ACTIVADA!</strong> Se han añadido +120 segundos al temporizador por oferta de último minuto.`, 'Anti-Sniping Activado', 'warning');
        } else {
            mostrarToast(`¡Oferta enviada por $${montoInput.toLocaleString('es-AR')}! Retenido en Escrow.`, 'Oferta Exitosa', 'success');
        }

        actualizarMonitorPujasSala();
        configurarBotonesPujaRapida(incrementoMin);
        actualizarTarjetaCatalogo(subastaSeleccionadaSala.id, montoInput);
        actualizarTarjetasCatalogo();
        actualizarSelectorSalaDirecto();
        await actualizarBilleteraUI();
        await sincronizarSaldosDropdown();
        await cargarMisActividades();
        await cargarLogsAuditoria();

    } catch (err) {
        await cargarLogsAuditoria();
        const errMsg = (err.mensaje || err.message || '').toLowerCase();
        if (err.status === 400 && (errMsg.includes('saldo') || errMsg.includes('insuficiente') || errMsg.includes('garantía') || errMsg.includes('garantia') || errMsg.includes('fondos'))) {
            await abrirModalSaldoInsuficiente(montoInput);
        } else if (err.status === 409 || err.isConflict) {
            // Manejo específico de concurrencia optimista (HTTP 409 Conflict):
            // Refrescar subasta del backend y permitir reintentar con la nueva versión
            try {
                const subFresca = await fetchObtenerSubastaPorId(subastaSeleccionadaSala.id);
                subastaSeleccionadaSala.version = subFresca.version ?? subFresca.Version ?? 1;
                subastaSeleccionadaSala.pujas = subFresca.pujas ?? subFresca.Pujas ?? [];
                subastaSeleccionadaSala.fechaFin = subFresca.fechaFin ?? subFresca.FechaFin;
                
                const idx = subastasCache.findIndex(s => (s.id ?? s.Id) === subastaSeleccionadaSala.id);
                if (idx !== -1) {
                    subastasCache[idx] = { ...subastasCache[idx], ...subFresca };
                }

                actualizarMonitorPujasSala();
                actualizarTarjetasCatalogo();
                configurarBotonesPujaRapida(subastaSeleccionadaSala.incrementoMinimo ?? subastaSeleccionadaSala.IncrementoMinimo ?? 1000);
            } catch (eRefresh) {
                console.warn("No se pudo refrescar automáticamente la subasta tras conflicto 409:", eRefresh);
            }

            mostrarBannerConcurrencia(`⚡ <strong>Tu puja coincidió con otra oferta reciente (Conflicto 409).</strong> La sala se ha sincronizado con la versión ${subastaSeleccionadaSala.version}. Puedes reintentar tu oferta con los valores actualizados.`);
            mostrarToast(`Tu puja coincidió con otra oferta reciente. La sala se actualizó a la versión ${subastaSeleccionadaSala.version}. Puedes reintentar ahora.`, 'Conflicto de Concurrencia (409)', 'warning');
        } else if (err.status === 0 || err.status >= 500) {
            mostrarToast('Servidor no disponible.', 'Error de Conexión', 'danger', () => enviarPuja(event));
        } else {
            mostrarToast(`No se pudo procesar la puja: ${escaparHtml(err.mensaje || err.message)}`, 'Error de Operación', 'danger');
        }
    } finally {
        setBotonCargando('btn-realizar-puja', 'spinner-puja', 'text-puja', false);
        await validarPujaEnVivo();
    }
}

/* ==========================================================================
   MÓDULO 4: BILLETERA VIRTUAL (MÉTRICAS Y ESCROW)
   ========================================================================== */

async function actualizarBilleteraUI() {
    renderizarSkeletonsBilletera(3);
    try {
        const billetera = await fetchObtenerBilletera(usuarioActual.id);
        const movimientos = await fetchObtenerMovimientos(usuarioActual.id);

        const saldoDisp = billetera.saldoDisponible ?? billetera.SaldoDisponible ?? 0;
        const saldoRet = billetera.saldoRetenido ?? billetera.SaldoRetenido ?? 0;
        const saldoTot = billetera.saldoTotal ?? billetera.SaldoTotal ?? 0;

        document.getElementById('wallet-usuario-info').textContent = `${usuarioActual.nombre} (${usuarioActual.email})`;
        
        const navSaldo = document.getElementById('nav-user-saldo');
        if (navSaldo) {
            navSaldo.textContent = `$${saldoDisp.toLocaleString('es-AR')}`;
            navSaldo.className = `badge font-monospace ${saldoDisp > 0 ? 'bg-success' : 'bg-danger'}`;
        }

        document.getElementById('wallet-saldo-total').textContent = `$${saldoTot.toLocaleString('es-AR')}`;
        document.getElementById('wallet-saldo-retenido').textContent = `$${saldoRet.toLocaleString('es-AR')}`;
        document.getElementById('wallet-saldo-disponible').textContent = `$${saldoDisp.toLocaleString('es-AR')}`;

        // Sincronizar saldos de los usuarios en el menú desplegable superior derecho
        await sincronizarSaldosDropdown();

        const tbody = document.getElementById('wallet-tabla-movimientos');
        if (!tbody) return;

        if (!movimientos || movimientos.length === 0) {
            tbody.innerHTML = `<tr><td colspan="5" class="text-center text-muted py-4">No hay movimientos contables registrados para este usuario.</td></tr>`;
            return;
        }

        tbody.innerHTML = '';
        movimientos.forEach(m => {
            const fechaStr = formatFechaHoraUTC3(m.fecha);
            const tipoInfo = getTipoMovimientoInfo(m.tipo);

            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td><span class="badge ${tipoInfo.badge}">${tipoInfo.nombre}</span></td>
                <td class="fw-bold font-monospace ${tipoInfo.esPositivo ? 'text-success' : 'text-danger'}">
                    ${tipoInfo.esPositivo ? '+' : '-'}$${m.monto.toLocaleString('es-AR')}
                </td>
                <td><small class="text-muted">${fechaStr}</small></td>
                <td>${escaparHtml(m.concepto || (m.subastaId ? `Subasta #${Number(m.subastaId)}` : 'Operación General'))}</td>
                <td><i class="fa-solid fa-circle-check text-success me-1"></i> Asentado</td>
            `;
            tbody.appendChild(tr);
        });

    } catch (e) {
        console.error("Error al actualizar UI de Billetera", e);
    }
}

function getTipoMovimientoInfo(tipoInt) {
    switch (parseInt(tipoInt)) {
        case 0: return { nombre: 'Depósito / Carga Libre', badge: 'bg-success', esPositivo: true };
        case 1: return { nombre: 'Retención Escrow (Garantía)', badge: 'bg-warning text-dark', esPositivo: false };
        case 2: return { nombre: 'Liberación de Garantía', badge: 'bg-info text-dark', esPositivo: true };
        case 3: return { nombre: 'Pago Definitivo Subasta', badge: 'bg-danger', esPositivo: false };
        case 4: return { nombre: 'Cobro Venta', badge: 'bg-primary-custom', esPositivo: true };
        default: return { nombre: 'Movimiento Ledger', badge: 'bg-secondary', esPositivo: true };
    }
}

async function procesarCargaSaldoModal(event) {
    event.preventDefault();
    const monto = parseFloat(document.getElementById('modal-cargar-monto').value);

    if (isNaN(monto) || monto <= 0) {
        mostrarToast('Ingrese un monto válido mayor a 0.', 'Monto Inválido', 'warning');
        return;
    }

    setBotonCargando('btn-modal-cargar-saldo', 'spinner-cargar', 'text-cargar', true, '<i class="fa-solid fa-circle-notch fa-spin me-1"></i> Acreditando...');

    try {
        await fetchCargarSaldo(usuarioActual.id, monto);
        mostrarToast(`¡Se han acreditado <strong>+$${monto.toLocaleString('es-AR')}</strong> libremente a la cuenta de ${escaparHtml(usuarioActual.nombre)}!`, 'Acreditación Exitosa', 'success');

        const modalElem = document.getElementById('modalCargarSaldo');
        const modal = bootstrap.Modal.getInstance(modalElem);
        if (modal) modal.hide();

        document.getElementById('form-cargar-saldo').reset();
        await actualizarBilleteraUI();
        await renderizarSelectorPerfilesSemilla();
        await cargarLogsAuditoria();
        if (subastaSeleccionadaSala) {
            await validarPujaEnVivo();
        }

    } catch (err) {
        if (err.status === 0 || err.status >= 500) {
            mostrarToast('Servidor no disponible.', 'Error de Conexión', 'danger', () => procesarCargaSaldoModal(event));
        } else {
            mostrarToast(`Error al cargar saldo: ${escaparHtml(err.mensaje || err.message)}`, 'Error', 'danger');
        }
    } finally {
        setBotonCargando('btn-modal-cargar-saldo', 'spinner-cargar', 'text-cargar', false);
    }
}



async function procesarCrearUsuario(event) {
    event.preventDefault();
    const nombre = document.getElementById('modal-usuario-nombre').value.trim();
    const email = document.getElementById('modal-usuario-email').value.trim();
    const password = document.getElementById('modal-usuario-password')?.value || '';

    if (!nombre || !email || !password) {
        mostrarToast('Complete el nombre, correo electrónico y contraseña.', 'Campos Requeridos', 'warning');
        return;
    }

    if (password.length < 4) {
        mostrarToast('La contraseña debe tener al menos 4 caracteres.', 'Validación', 'warning');
        return;
    }

    setBotonCargando('btn-modal-usuario', 'spinner-usuario', 'text-usuario', true, '<i class="fa-solid fa-circle-notch fa-spin me-1"></i> Registrando...');

    try {
        const res = await fetchCrearUsuario(nombre, email, password);
        mostrarToast('Usuario y billetera creados correctamente', 'Usuario Creado', 'success');

        const loggedUser = await fetchLogin(email, password);
        await iniciarSesion(loggedUser);

        const modalElem = document.getElementById('modalCrearUsuario');
        const modal = bootstrap.Modal.getInstance(modalElem);
        if (modal) modal.hide();
        document.getElementById('form-modal-usuario').reset();

    } catch (e) {
        if (e.status === 0 || e.status >= 500) {
            mostrarToast('Servidor no disponible.', 'Error de Conexión', 'danger', () => procesarCrearUsuario(event));
        } else {
            mostrarToast(`Error al crear usuario: ${escaparHtml(e.mensaje || e.message)}`, 'Error', 'danger');
        }
    } finally {
        setBotonCargando('btn-modal-usuario', 'spinner-usuario', 'text-usuario', false);
    }
}

/* ==========================================================================
   MÓDULO 5: MIS ACTIVIDADES
   ========================================================================== */

async function cargarMisActividades() {
    const contenedorPujas = document.getElementById('actividades-mis-pujas');
    const contenedorPublicaciones = document.getElementById('actividades-mis-publicaciones');

    if (!contenedorPujas || !contenedorPublicaciones) return;

    if (!usuarioActual || !usuarioActual.id) {
        contenedorPujas.innerHTML = `
            <div class="col-12 text-center py-4 text-muted border rounded-3 bg-light">
                <i class="fa-solid fa-user-lock fa-2x mb-2 text-secondary opacity-50"></i>
                <p class="mb-1 fw-bold">Sesión requerida</p>
                <p class="extra-small mb-3">Inicia sesión para consultar tus participaciones.</p>
            </div>
        `;
        contenedorPublicaciones.innerHTML = `
            <div class="col-12 text-center py-4 text-muted border rounded-3 bg-light">
                <i class="fa-solid fa-user-lock fa-2x mb-2 text-secondary opacity-50"></i>
                <p class="mb-1 fw-bold">Sesión requerida</p>
                <p class="extra-small mb-3">Inicia sesión para consultar tus publicaciones.</p>
            </div>
        `;
        return;
    }

    // Estado de carga inicial
    contenedorPujas.innerHTML = `
        <div class="col-12 text-center py-5 text-muted">
            <div class="spinner-border text-primary-custom mb-2" role="status">
                <span class="visually-hidden">Cargando...</span>
            </div>
            <p class="small mb-0">Cargando tus participaciones...</p>
        </div>
    `;
    contenedorPublicaciones.innerHTML = `
        <div class="col-12 text-center py-5 text-muted">
            <div class="spinner-border text-primary-custom mb-2" role="status">
                <span class="visually-hidden">Cargando...</span>
            </div>
            <p class="small mb-0">Cargando tus publicaciones...</p>
        </div>
    `;

    try {
        const data = await fetchObtenerActividades(usuarioActual.id);
        const participaciones = data?.participaciones ?? data?.Participaciones ?? [];
        const publicaciones = data?.publicaciones ?? data?.Publicaciones ?? [];
        const metricas = data?.metricas ?? data?.Metricas ?? {};

        // Actualizar métricas numéricas desde el backend
        const statPujas = document.getElementById('stat-total-pujas');
        const statCreadas = document.getElementById('stat-subastas-creadas');
        const statGanadas = document.getElementById('stat-subastas-ganadas');

        if (statPujas) statPujas.textContent = metricas.totalPujas ?? metricas.TotalPujas ?? 0;
        if (statCreadas) statCreadas.textContent = metricas.subastasPublicadas ?? metricas.SubastasPublicadas ?? 0;
        if (statGanadas) statGanadas.textContent = metricas.subastasGanadas ?? metricas.SubastasGanadas ?? 0;

        // Renderizar Participaciones
        contenedorPujas.innerHTML = '';
        if (participaciones.length === 0) {
            contenedorPujas.innerHTML = `
                <div class="col-12 text-center py-4 text-muted border rounded-3 bg-light">
                    <i class="fa-solid fa-gavel fa-2x mb-2 text-secondary opacity-50"></i>
                    <p class="mb-1 fw-bold">Sin ofertas registradas con ${escaparHtml(usuarioActual.nombre)}</p>
                    <p class="extra-small mb-3">Aún no has realizado pujas en las subastas disponibles.</p>
                    <button class="btn btn-primary-custom btn-sm" onclick="document.getElementById('tab-catalogo').click()">
                        <i class="fa-solid fa-compass me-1"></i>Explorar Catálogo
                    </button>
                </div>
            `;
        } else {
            participaciones.forEach(part => {
                const subId = Number(part.subastaId ?? part.SubastaId);
                const subTitulo = escaparHtml(part.titulo ?? part.Titulo ?? `Subasta #${subId}`);
                const subImg = sanitizarUrlImagen(part.urlImagen ?? part.UrlImagen);
                const catNombre = escaparHtml(part.categoriaNombre ?? part.CategoriaNombre ?? 'General');
                const subEstado = part.estado ?? part.Estado ?? 'ACTIVA';
                const miMayorPuja = part.miMejorOferta ?? part.MiMejorOferta ?? 0;
                const ofertaLiderActual = part.ofertaLiderActual ?? part.OfertaLiderActual ?? 0;
                const esLider = !!(part.esLider ?? part.EsLider);
                const ganadorId = part.ganadorId ?? part.GanadorId;
                const precioFinal = part.precioFinal ?? part.PrecioFinal ?? miMayorPuja;
                const cantMisPujas = part.cantidadMisPujas ?? part.CantidadMisPujas ?? 0;
                const cantTotalPujas = part.cantidadPujasTotal ?? part.CantidadPujasTotal ?? 0;

                const haFinalizado = (subEstado === 'FINALIZADA' || subEstado === 'DESIERTA');
                const esGanador = (haFinalizado && ganadorId === usuarioActual.id);

                let badgeLiderazgoHtml = '';
                let escrowInfoHtml = '';
                let cardBorderClass = 'border';

                if (haFinalizado) {
                    if (esGanador) {
                        cardBorderClass = 'border-success border-2';
                        badgeLiderazgoHtml = `<span class="badge bg-success shadow-sm"><i class="fa-solid fa-crown me-1"></i>¡Subasta Ganada!</span>`;
                        escrowInfoHtml = `<span class="text-success extra-small fw-bold"><i class="fa-solid fa-circle-check me-1"></i>Adjudicada por $${Number(precioFinal).toLocaleString('es-AR')}</span>`;
                    } else {
                        badgeLiderazgoHtml = `<span class="badge bg-secondary"><i class="fa-solid fa-flag-checkered me-1"></i>Finalizada</span>`;
                        escrowInfoHtml = `<span class="text-muted extra-small"><i class="fa-solid fa-rotate-left text-secondary me-1"></i>Superada · Garantía liberada</span>`;
                    }
                } else {
                    if (esLider) {
                        cardBorderClass = 'border-success';
                        badgeLiderazgoHtml = `<span class="badge bg-success-subtle text-success border border-success-subtle"><i class="fa-solid fa-trophy me-1"></i>Vas ganando (Líder)</span>`;
                        escrowInfoHtml = `<span class="text-warning-emphasis extra-small fw-semibold"><i class="fa-solid fa-shield-halved me-1 text-warning"></i>$${Number(miMayorPuja).toLocaleString('es-AR')} retenidos en Escrow</span>`;
                    } else {
                        cardBorderClass = 'border-danger-subtle';
                        badgeLiderazgoHtml = `<span class="badge bg-danger-subtle text-danger border border-danger-subtle"><i class="fa-solid fa-triangle-exclamation me-1"></i>Te superaron (Outbid)</span>`;
                        escrowInfoHtml = `<span class="text-info extra-small fw-semibold"><i class="fa-solid fa-arrow-rotate-left me-1"></i>Garantía liberada ($${Number(miMayorPuja).toLocaleString('es-AR')} disponibles)</span>`;
                    }
                }

                const col = document.createElement('div');
                col.className = 'col-lg-6 col-12 mb-3';
                col.innerHTML = `
                    <div class="card h-100 ${cardBorderClass} p-3 shadow-sm activity-card">
                        <div class="d-flex gap-3">
                            <img src="${subImg}" alt="${subTitulo}" class="rounded-3 border object-fit-cover flex-shrink-0" style="width: 76px; height: 76px;" onerror="this.src='assets/images/watch.png'">
                            <div class="flex-grow-1 min-w-0">
                                <div class="d-flex justify-content-between align-items-start gap-2 mb-1">
                                    <h6 class="fw-bold text-dark mb-0 text-truncate" title="${subTitulo}">${subTitulo}</h6>
                                    <div class="flex-shrink-0">${badgeLiderazgoHtml}</div>
                                </div>
                                <div class="extra-small text-muted mb-2">
                                    <span class="badge bg-light text-dark border me-1">${catNombre}</span>
                                    <span>${cantMisPujas} oferta${cantMisPujas === 1 ? '' : 's'} tuya${cantMisPujas === 1 ? '' : 's'} (${cantTotalPujas} en total)</span>
                                </div>
                                <div class="bg-light p-2 rounded-2 border extra-small mb-2">
                                    <div class="d-flex justify-content-between">
                                        <span class="text-muted">Tu mejor oferta:</span>
                                        <span class="fw-bold text-primary-custom">$${Number(miMayorPuja).toLocaleString('es-AR')}</span>
                                    </div>
                                    <div class="d-flex justify-content-between">
                                        <span class="text-muted">Oferta líder actual:</span>
                                        <span class="fw-bold ${esLider ? 'text-success' : 'text-danger'}">$${Number(ofertaLiderActual).toLocaleString('es-AR')}</span>
                                    </div>
                                </div>
                                <div class="d-flex justify-content-between align-items-center pt-1 border-top mt-1">
                                    <div>${escrowInfoHtml}</div>
                                    <button class="btn ${!haFinalizado && !esLider ? 'btn-warning text-dark fw-bold' : 'btn-outline-primary-custom'} btn-sm px-3" onclick="abrirSalaEnVivo(${Number(subId)})">
                                        ${!haFinalizado && !esLider ? '<i class="fa-solid fa-arrow-up me-1"></i>Pujar' : '<i class="fa-solid fa-gavel me-1"></i>Ver Sala'}
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                `;
                contenedorPujas.appendChild(col);
            });
        }

        // Renderizar Publicaciones
        contenedorPublicaciones.innerHTML = '';
        if (publicaciones.length === 0) {
            contenedorPublicaciones.innerHTML = `
                <div class="col-12 text-center py-4 text-muted border rounded-3 bg-light">
                    <i class="fa-solid fa-store fa-2x mb-2 text-secondary opacity-50"></i>
                    <p class="mb-1 fw-bold">Sin publicaciones con ${escaparHtml(usuarioActual.nombre)}</p>
                    <p class="extra-small mb-3">No tienes artículos publicados a subasta como vendedor.</p>
                    <button class="btn btn-primary-custom btn-sm" onclick="document.getElementById('tab-publicar').click()">
                        <i class="fa-solid fa-plus me-1"></i>Publicar una Subasta
                    </button>
                </div>
            `;
        } else {
            publicaciones.forEach(pub => {
                const subId = Number(pub.subastaId ?? pub.SubastaId);
                const subTitulo = escaparHtml(pub.titulo ?? pub.Titulo ?? `Subasta #${subId}`);
                const subImg = sanitizarUrlImagen(pub.urlImagen ?? pub.UrlImagen);
                const catNombre = escaparHtml(pub.categoriaNombre ?? pub.CategoriaNombre ?? 'General');
                const subEstado = pub.estado ?? pub.Estado ?? 'ACTIVA';
                const haFinalizado = (subEstado === 'FINALIZADA' || subEstado === 'DESIERTA');
                const subPrecioBase = pub.precioBase ?? pub.PrecioBase ?? 0;
                const ofertaLiderActual = pub.ofertaLiderActual ?? pub.OfertaLiderActual ?? subPrecioBase;
                const cantPujas = pub.cantidadPujas ?? pub.CantidadPujas ?? 0;

                const colPub = document.createElement('div');
                colPub.className = 'col-lg-6 col-12 mb-3';
                colPub.innerHTML = `
                    <div class="card h-100 border p-3 shadow-sm activity-card">
                        <div class="d-flex gap-3">
                            <img src="${subImg}" alt="${subTitulo}" class="rounded-3 border object-fit-cover flex-shrink-0" style="width: 76px; height: 76px;" onerror="this.src='assets/images/watch.png'">
                            <div class="flex-grow-1 min-w-0">
                                <div class="d-flex justify-content-between align-items-start gap-2 mb-1">
                                    <h6 class="fw-bold text-dark mb-0 text-truncate" title="${subTitulo}">${subTitulo}</h6>
                                    <span class="badge ${haFinalizado ? 'bg-secondary' : (subEstado === 'PROGRAMADA' ? 'bg-warning text-dark' : 'bg-primary-custom')}">${subEstado}</span>
                                </div>
                                <div class="extra-small text-muted mb-2">
                                    <span class="badge bg-light text-dark border me-1">${catNombre}</span>
                                    <span>${cantPujas} oferta${cantPujas === 1 ? '' : 's'} recibida${cantPujas === 1 ? '' : 's'}</span>
                                </div>
                                <div class="bg-light p-2 rounded-2 border extra-small mb-2">
                                    <div class="d-flex justify-content-between">
                                        <span class="text-muted">Precio base:</span>
                                        <span class="fw-semibold text-dark">$${Number(subPrecioBase).toLocaleString('es-AR')}</span>
                                    </div>
                                    <div class="d-flex justify-content-between">
                                        <span class="text-muted">Puja máxima actual:</span>
                                        <span class="fw-bold text-success">$${Number(ofertaLiderActual).toLocaleString('es-AR')}</span>
                                    </div>
                                </div>
                                <div class="d-flex justify-content-between align-items-center pt-1 border-top mt-1">
                                    <span class="extra-small text-muted">
                                        ${cantPujas > 0 ? `<i class="fa-solid fa-users me-1 text-primary-custom"></i>${cantPujas} oferta(s)` : '<i class="fa-regular fa-clock me-1"></i>Sin ofertas aún'}
                                    </span>
                                    <button class="btn btn-outline-primary-custom btn-sm px-3" onclick="abrirSalaEnVivo(${Number(subId)})">
                                        <i class="fa-solid fa-sliders me-1"></i>Administrar
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                `;
                contenedorPublicaciones.appendChild(colPub);
            });
        }
    } catch (err) {
        console.error("Error al cargar actividades del usuario:", err);
        const mensajeError = err?.mensaje || err?.message || 'No se pudieron recuperar las actividades.';

        contenedorPujas.innerHTML = `
            <div class="col-12 text-center py-4 text-muted border rounded-3 bg-light">
                <i class="fa-solid fa-triangle-exclamation fa-2x mb-2 text-warning"></i>
                <p class="mb-1 fw-bold text-dark">Error al cargar participaciones</p>
                <p class="extra-small text-muted mb-3">${escaparHtml(mensajeError)}</p>
                <button class="btn btn-outline-primary-custom btn-sm" onclick="cargarMisActividades()">
                    <i class="fa-solid fa-rotate-right me-1"></i>Reintentar
                </button>
            </div>
        `;
        contenedorPublicaciones.innerHTML = `
            <div class="col-12 text-center py-4 text-muted border rounded-3 bg-light">
                <i class="fa-solid fa-triangle-exclamation fa-2x mb-2 text-warning"></i>
                <p class="mb-1 fw-bold text-dark">Error al cargar publicaciones</p>
                <p class="extra-small text-muted mb-3">${escaparHtml(mensajeError)}</p>
                <button class="btn btn-outline-primary-custom btn-sm" onclick="cargarMisActividades()">
                    <i class="fa-solid fa-rotate-right me-1"></i>Reintentar
                </button>
            </div>
        `;
    }
}

/* ==========================================================================
   MODALES DE EXCEPCIONES HTTP & DIÁLOGOS DE ERROR
   ========================================================================== */

async function abrirModalSaldoInsuficiente(montoRequerido) {
    let saldoDisp = 0;
    try {
        const billetera = await fetchObtenerBilletera(usuarioActual.id);
        saldoDisp = billetera ? (billetera.saldoDisponible ?? billetera.SaldoDisponible ?? 0) : 0;
    } catch (_) {
        saldoDisp = 0;
    }

    const faltante = Math.max(0, montoRequerido - saldoDisp);

    const elDisp = document.getElementById('error-saldo-disponible');
    if (elDisp) elDisp.textContent = `$${saldoDisp.toLocaleString('es-AR')}`;
    const elReq = document.getElementById('error-monto-requerido');
    if (elReq) elReq.textContent = `$${montoRequerido.toLocaleString('es-AR')}`;
    const elFalt = document.getElementById('error-monto-faltante');
    if (elFalt) elFalt.textContent = `$${faltante.toLocaleString('es-AR')}`;
    
    const inputCargar = document.getElementById('modal-cargar-monto');
    if (inputCargar) inputCargar.value = Math.ceil(faltante > 0 ? faltante : 10000);

    const modalElem = document.getElementById('modalSaldoInsuficiente');
    if (modalElem) {
        const modal = new bootstrap.Modal(modalElem);
        modal.show();
    }
}

function abrirModalConcurrenciaOptimista() {
    const modalElem = document.getElementById('modalConcurrencia');
    if (modalElem) {
        const modal = new bootstrap.Modal(modalElem);
        modal.show();
    }
}

function mostrarBannerConcurrencia(mensajeHtml) {
    const banner = document.getElementById('sala-banner-concurrencia');
    if (!banner) return;

    banner.className = 'concurrency-alert-banner alert alert-warning d-flex align-items-center justify-content-between p-3 mb-3 shadow-sm';
    banner.innerHTML = `
        <div class="d-flex align-items-center">
            <i class="fa-solid fa-bolt-lightning text-warning fs-3 me-3"></i>
            <div>${mensajeHtml}</div>
        </div>
        <button type="button" class="btn-close ms-2" onclick="this.parentElement.classList.add('d-none')" aria-label="Cerrar"></button>
    `;
    banner.classList.remove('d-none');

    // Auto-ocultar después de 9 segundos
    setTimeout(() => {
        if (banner) banner.classList.add('d-none');
    }, 9000);
}



async function testearConexionApiDevTools() {
    const devBadge = document.getElementById('devtools-api-status');
    const devUrl = document.getElementById('devtools-api-url');
    if (devUrl) devUrl.textContent = ApiClient.baseURL;

    if (devBadge) {
        devBadge.className = 'api-status-badge badge bg-secondary text-white';
        devBadge.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i> Probando...';
    }

    try {
        await apiFetch('/Categorias');
        mostrarToast(`Conexión exitosa a la API (${ApiClient.baseURL})`, 'DevTools Ping', 'success');
        updateApiConnectionStatus(true);
    } catch (e) {
        mostrarToast(`Fallo al conectar: ${escaparHtml(e.message)}`, 'DevTools Ping Fallido', 'danger');
        updateApiConnectionStatus(false);
    }
}

/* ==========================================================================
   HELPERS UI Y NOTIFICACIONES
   ========================================================================== */

function configurarEventosUI() {
    const formCrear = document.getElementById('form-crear-subasta');
    if (formCrear) formCrear.addEventListener('submit', guardarSubasta);

    const formPuja = document.getElementById('form-realizar-puja');
    if (formPuja) formPuja.addEventListener('submit', enviarPuja);

    const formCargar = document.getElementById('form-cargar-saldo');
    if (formCargar) formCargar.addEventListener('submit', procesarCargaSaldoModal);



    const formUsuario = document.getElementById('form-modal-usuario');
    if (formUsuario) formUsuario.addEventListener('submit', procesarCrearUsuario);

    const selectSala = document.getElementById('sala-subasta-selector');
    if (selectSala) {
        selectSala.addEventListener('change', (e) => {
            if (e.target.value) cargarSalaEnVivo(parseInt(e.target.value));
        });
    }

    const urlInput = document.getElementById('crear-urlImagen');
    const previewImg = document.getElementById('crear-img-preview');
    if (urlInput && previewImg) {
        urlInput.addEventListener('input', () => {
            previewImg.src = urlInput.value.trim() || 'assets/images/watch.png';
        });
    }

    // Sincronización Reactiva en Tiempo Real del Formulario Crear Subasta
    const inputTitulo = document.getElementById('crear-titulo');
    const previewTitulo = document.getElementById('crear-preview-titulo');
    if (inputTitulo && previewTitulo) {
        inputTitulo.addEventListener('input', () => {
            previewTitulo.textContent = inputTitulo.value.trim() || 'Título de tu subasta';
        });
    }

    const selectCat = document.getElementById('crear-categoriaId');
    const previewCat = document.getElementById('crear-preview-categoria');
    if (selectCat && previewCat) {
        selectCat.addEventListener('change', () => {
            const catNombre = selectCat.options[selectCat.selectedIndex]?.text || 'Previsualización';
            previewCat.textContent = catNombre.startsWith('Seleccione') ? 'Previsualización' : catNombre;
        });
    }

    const inputDesc = document.getElementById('crear-descripcion');
    const previewDesc = document.getElementById('crear-preview-descripcion');
    if (inputDesc && previewDesc) {
        inputDesc.addEventListener('input', () => {
            previewDesc.textContent = inputDesc.value.trim() || 'Descripción de la publicación...';
        });
    }

    const inputBase = document.getElementById('crear-precioBase');
    const previewBase = document.getElementById('crear-preview-precio');
    if (inputBase && previewBase) {
        inputBase.addEventListener('input', () => {
            const val = parseFloat(inputBase.value) || 0;
            previewBase.textContent = `$${val.toLocaleString('es-AR')}`;
        });
    }

    const tabElList = document.querySelectorAll('button[data-bs-toggle="pill"]');
    tabElList.forEach(tabEl => {
        tabEl.addEventListener('shown.bs.tab', async (event) => {
            const targetId = event.target.getAttribute('data-bs-target');
            if (targetId === '#content-catalogo') {
                detenerPollingSala();
                iniciarPollingCatalogo();
                await aplicarFiltros();
            } else if (targetId === '#content-sala') {
                detenerPollingCatalogo();
                if (!subastaSeleccionadaSala && subastasCache.length > 0) {
                    await cargarSalaEnVivo(subastasCache[0].id);
                } else if (subastaSeleccionadaSala) {
                    iniciarPollingSala();
                }
            } else {
                detenerPollingSala();
                detenerPollingCatalogo();
                if (targetId === '#content-billetera') {
                    await actualizarBilleteraUI();
                } else if (targetId === '#content-actividades') {
                    await cargarMisActividades();
                } else if (targetId === '#content-auditoria') {
                    if (usuarioActual && usuarioActual.rol === 'ADMIN') {
                        await cargarLogsAuditoria();
                    }
                }
            }
        });
    });

    document.addEventListener('visibilitychange', () => {
        if (!document.hidden) {
            const tabSala = document.getElementById('content-sala');
            if (tabSala && tabSala.classList.contains('active')) {
                iniciarPollingSala();
            }
            const tabCat = document.getElementById('content-catalogo');
            if (tabCat && tabCat.classList.contains('active')) {
                iniciarPollingCatalogo();
            }
        }
    });

    const dropdownUserContainer = document.getElementById('dropdown-perfiles-semilla')?.closest('.dropdown');
    if (dropdownUserContainer) {
        dropdownUserContainer.addEventListener('show.bs.dropdown', async () => {
            await sincronizarSaldosDropdown();
        });
    }
}

/**
 * Control de Acceso y Visibilidad del Módulo de Auditoría de Eventos:
 * Solo visible y accesible cuando usuarioActual.rol === 'ADMIN'.
 * Para cualquier usuario regular (comprador/vendedor), esta sección permanece completamente oculta.
 * Si un usuario regular se encontraba en esta pestaña, es redirigido al Catálogo.
 */
function actualizarVisibilidadAuditoria() {
    const tabAuditoria = document.getElementById('tab-auditoria');
    const contentAuditoria = document.getElementById('content-auditoria');
    const esAdmin = Boolean(usuarioActual && usuarioActual.rol === 'ADMIN');

    if (tabAuditoria) {
        if (esAdmin) {
            tabAuditoria.classList.remove('d-none');
            tabAuditoria.style.display = '';
        } else {
            tabAuditoria.classList.add('d-none');
            tabAuditoria.style.display = 'none';
        }
    }

    if (contentAuditoria) {
        if (esAdmin) {
            contentAuditoria.classList.remove('d-none');
        } else {
            contentAuditoria.classList.add('d-none');
            const tabActivo = tabAuditoria && tabAuditoria.classList.contains('active');
            const contentActivo = contentAuditoria.classList.contains('active') || contentAuditoria.classList.contains('show');
            if (tabActivo || contentActivo) {
                if (tabAuditoria) {
                    tabAuditoria.classList.remove('active');
                    tabAuditoria.setAttribute('aria-selected', 'false');
                }
                contentAuditoria.classList.remove('active', 'show');

                const tabCatalogo = document.getElementById('tab-catalogo');
                if (tabCatalogo && typeof bootstrap !== 'undefined') {
                    const bsTab = bootstrap.Tab.getOrCreateInstance(tabCatalogo);
                    bsTab.show();
                }
            }
        }
    }
}

function mostrarToast(mensaje, titulo = 'Notificación', tipo = 'info', onRetry = null) {
    const container = document.getElementById('toast-container');
    if (!container) return;

    const toastId = 'toast-' + Date.now();
    let bgHeader = 'bg-primary-custom text-white';
    if (tipo === 'success') bgHeader = 'bg-success text-white';
    if (tipo === 'danger') bgHeader = 'bg-danger text-white';
    if (tipo === 'warning') bgHeader = 'bg-warning text-dark';

    const retryBtnHtml = onRetry ? `
        <div class="mt-2 pt-2 border-top text-end">
            <button type="button" class="btn btn-sm btn-outline-danger py-1 px-2 fw-semibold" id="${toastId}-retry-btn">
                <i class="fa-solid fa-rotate-right me-1"></i> Reintentar
            </button>
        </div>
    ` : '';

    const toastHtml = `
        <div id="${toastId}" class="toast shadow-lg" role="alert" aria-live="assertive" aria-atomic="true" data-bs-delay="${onRetry ? '8000' : '4500'}">
            <div class="toast-header ${bgHeader}">
                <strong class="me-auto"><i class="fa-solid fa-bell me-2"></i>${escaparHtml(titulo)}</strong>
                <small class="${tipo === 'warning' ? 'text-dark-50' : 'text-white-50'}">Ahora</small>
                <button type="button" class="btn-close ${tipo === 'warning' ? '' : 'btn-close-white'} ms-2" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
            <div class="toast-body bg-white text-dark">
                <div>${mensaje}</div>
                ${retryBtnHtml}
            </div>
        </div>
    `;

    container.insertAdjacentHTML('beforeend', toastHtml);
    const toastElem = document.getElementById(toastId);
    const bsToast = new bootstrap.Toast(toastElem);
    bsToast.show();

    if (onRetry) {
        const retryBtn = document.getElementById(`${toastId}-retry-btn`);
        if (retryBtn) {
            retryBtn.addEventListener('click', async () => {
                bsToast.hide();
                try {
                    await onRetry();
                } catch (_) {}
            });
        }
    }

    toastElem.addEventListener('hidden.bs.toast', () => {
        toastElem.remove();
    });
}

/* ==========================================================================
   MÓDULO 6: AUDITORÍA DE EVENTOS Y TRAZABILIDAD (AUDIT LOG)
   ========================================================================== */

let logsAuditoriaCache = [];

async function cargarLogsAuditoria() {
    if (!usuarioActual || usuarioActual.rol !== 'ADMIN') return;
    const tabla = document.getElementById('tabla-logs-auditoria');
    if (!tabla) return;

    try {
        const logs = await fetchObtenerAuditoria(100);
        logsAuditoriaCache = logs || [];

        actualizarMetricasAuditoria(logsAuditoriaCache);
        renderizarLogsAuditoriaFiltrados();
    } catch (e) {
        console.error("Error al cargar logs de auditoría:", e);
        if (tabla) {
            tabla.innerHTML = `<tr><td colspan="5" class="text-center text-danger py-4">Error al cargar registros de auditoría.</td></tr>`;
        }
    }
}

function actualizarMetricasAuditoria(logs) {
    const statEstados = document.getElementById('stat-auditoria-estados');
    const statSniping = document.getElementById('stat-auditoria-sniping');
    const statRechazadas = document.getElementById('stat-auditoria-rechazadas');
    const statAcreditaciones = document.getElementById('stat-auditoria-acreditaciones');

    if (!statEstados || !logs) return;

    let cEstados = 0;
    let cSniping = 0;
    let cRechazos = 0;
    let cAcreditaciones = 0;

    logs.forEach(l => {
        const accion = (l.accion ?? l.Accion ?? '').toUpperCase();
        if (accion.includes('ESTADO') || accion.includes('VENTA')) cEstados++;
        if (accion.includes('SNIPING')) cSniping++;
        if (accion.includes('RECHAZ')) cRechazos++;
        if (accion.includes('ACREDITACION') || accion.includes('CARGA')) cAcreditaciones++;
    });

    statEstados.textContent = cEstados;
    statSniping.textContent = cSniping;
    statRechazadas.textContent = cRechazos;
    statAcreditaciones.textContent = cAcreditaciones;
}

function renderizarLogsAuditoriaFiltrados() {
    const tabla = document.getElementById('tabla-logs-auditoria');
    if (!tabla) return;

    const filtro = document.getElementById('filtro-auditoria-accion')?.value || '';
    let filtrados = logsAuditoriaCache;
    if (filtro) {
        filtrados = logsAuditoriaCache.filter(l => (l.accion ?? l.Accion) === filtro);
    }

    if (filtrados.length === 0) {
        tabla.innerHTML = `<tr><td colspan="5" class="text-center text-muted py-4">No hay eventos registrados en este criterio de auditoría.</td></tr>`;
        return;
    }

    tabla.innerHTML = '';
    filtrados.forEach(log => {
        const id = Number(log.id ?? log.Id);
        const accion = log.accion ?? log.Accion ?? '';
        const detalle = log.detalle ?? log.Detalle ?? '';
        const usuarioId = log.usuarioId ?? log.UsuarioId;
        const fechaStr = log.fechaRegistro ?? log.FechaRegistro;

        let badgeInfo = getBadgeAuditoria(accion);
        let usuarioTexto = usuarioId ? `<span class="badge bg-light text-dark border">Usuario #${Number(usuarioId)}</span>` : '<span class="badge bg-secondary text-white">Worker / Sistema</span>';

        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td class="font-monospace text-muted small fw-bold">#${id}</td>
            <td><span class="badge ${badgeInfo.badge}">${badgeInfo.label}</span></td>
            <td class="small text-break">${escaparHtml(detalle)}</td>
            <td class="small">${usuarioTexto}</td>
            <td class="small text-nowrap font-monospace text-secondary">${formatFechaHoraCortaUTC3(fechaStr)}</td>
        `;
        tabla.appendChild(tr);
    });
}

function getBadgeAuditoria(accion) {
    switch (accion) {
        case 'PUJA_REGISTRADA':
            return { badge: 'bg-primary-custom text-white', label: '<i class="fa-solid fa-gavel me-1"></i> Puja Registrada' };
        case 'PUJA_RECHAZADA':
            return { badge: 'bg-danger text-white', label: '<i class="fa-solid fa-ban me-1"></i> Puja Rechazada (Negocio)' };
        case 'PUJA_RECHAZADA_CONCURRENCIA':
            return { badge: 'bg-dark text-white', label: '<i class="fa-solid fa-triangle-exclamation me-1"></i> Conflicto Concurrencia (409)' };
        case 'EXTENSION_ANTISNIPING':
        case 'EXTENSION_ANTI_SNIPING':
            return { badge: 'bg-warning text-dark', label: '<i class="fa-solid fa-shield-cat me-1"></i> Anti-Sniping (+120s)' };
        case 'SUBASTA_CREADA':
            return { badge: 'bg-info text-dark', label: '<i class="fa-solid fa-plus-circle me-1"></i> Subasta Creada' };
        case 'CAMBIO_ESTADO_SUBASTA':
            return { badge: 'bg-primary text-white', label: '<i class="fa-solid fa-arrows-rotate me-1"></i> Cambio de Estado' };
        case 'VENTA_REGISTRADA':
            return { badge: 'bg-success text-white', label: '<i class="fa-solid fa-handshake me-1"></i> Venta / Liquidación' };
        case 'ACREDITACION_MANUAL_SALDO':
            return { badge: 'bg-info text-dark', label: '<i class="fa-solid fa-wallet me-1"></i> Acreditación Manual' };
        case 'FALLO_CIERRE_SUBASTA':
            return { badge: 'bg-danger text-white', label: '<i class="fa-solid fa-triangle-exclamation me-1"></i> Fallo en Cierre' };
        default:
            return { badge: 'bg-secondary text-white', label: escaparHtml(accion) };
    }
}

async function forzarEjecucionWorker() {
    try {
        mostrarToast('Invocando verificación del Background Worker...', 'Worker en Proceso', 'info');
        const res = await fetchProcesarSubastasVencidas();
        if (res) {
            mostrarToast(`Worker finalizado. Activadas: ${res.activadas ?? res.Activadas ?? 0}, Adjudicadas: ${res.finalizadas ?? res.Finalizadas ?? 0}, Desiertas: ${res.desiertas ?? res.Desiertas ?? 0}`, 'Worker Ejecutado', 'success');
        } else {
            mostrarToast('Se ejecutó la verificación periódica de subastas.', 'Worker Ejecutado', 'info');
        }
        await aplicarFiltros();
        await cargarMisActividades();
        await cargarLogsAuditoria();
    } catch (e) {
        if (e && (e.status === 0 || e.status >= 500)) {
            mostrarToast('Servidor no disponible.', 'Error de Conexión', 'danger', () => forzarEjecucionWorker());
        } else {
            mostrarToast(`Error al forzar la ejecución del worker: ${escaparHtml(e.mensaje || e.message || '')}`, 'Error Worker', 'danger');
        }
    }
}

/** Alias del botón DevTools para el Background Worker */
async function ejecutarBackgroundWorkerManual() {
    return forzarEjecucionWorker();
}

/* ==========================================================================
   MÓDULO DE LOGIN CON CONTRASEÑA POR PERFIL
   ========================================================================== */

let _perfilSeleccionadoParaLogin = null;

/**
 * Muestra el recuadro de confirmación de contraseña para el perfil elegido.
 * Llamado al hacer clic en una tarjeta de perfil del login.
 */
function seleccionarPerfilParaLogin(perfilId) {
    _perfilSeleccionadoParaLogin = usuariosDisponibles.find(p => p.id === perfilId) || null;
    if (!_perfilSeleccionadoParaLogin) return;

    const box = document.getElementById('login-perfil-password-box');
    const avatarEl = document.getElementById('login-perfil-avatar-confirm');
    const nombreEl = document.getElementById('login-perfil-nombre-confirm');
    const emailEl = document.getElementById('login-perfil-email-confirm');
    const pwInput = document.getElementById('login-perfil-password');
    const errorEl = document.getElementById('login-perfil-error');

    if (avatarEl) avatarEl.textContent = obtenerIniciales(_perfilSeleccionadoParaLogin.nombre);
    if (nombreEl) nombreEl.textContent = _perfilSeleccionadoParaLogin.nombre;
    if (emailEl) emailEl.textContent = _perfilSeleccionadoParaLogin.email;
    if (pwInput) pwInput.value = '';
    if (errorEl) errorEl.classList.add('d-none');
    if (box) box.classList.remove('d-none');
}

/**
 * Confirma el login del perfil seleccionado enviando la contraseña al backend.
 */
async function confirmarLoginPerfil() {
    if (!_perfilSeleccionadoParaLogin) return;

    const pwInput = document.getElementById('login-perfil-password');
    const errorEl = document.getElementById('login-perfil-error');
    const errorText = document.getElementById('login-perfil-error-text');
    const pw = pwInput ? pwInput.value : '';

    if (errorEl) errorEl.classList.add('d-none');

    setBotonCargando('btn-login-perfil-confirmar', 'spinner-login-perfil', 'text-login-perfil', true,
        '<i class="fa-solid fa-circle-notch fa-spin me-2"></i> Ingresando...');

    try {
        const usuarioAutenticado = await fetchLogin(_perfilSeleccionadoParaLogin.email || _perfilSeleccionadoParaLogin.nombre, pw);
        await iniciarSesion(usuarioAutenticado);
        _perfilSeleccionadoParaLogin = null;
        cancelarSeleccionPerfil();
    } catch (e) {
        if (errorEl && errorText) {
            errorText.textContent = e.mensaje || e.message || 'Credenciales incorrectas.';
            errorEl.classList.remove('d-none');
        }
    } finally {
        setBotonCargando('btn-login-perfil-confirmar', 'spinner-login-perfil', 'text-login-perfil', false);
    }
}

/**
 * Cancela la selección de perfil y vuelve a la grilla de selección.
 */
function cancelarSeleccionPerfil() {
    _perfilSeleccionadoParaLogin = null;
    const box = document.getElementById('login-perfil-password-box');
    if (box) box.classList.add('d-none');
    const errorEl = document.getElementById('login-perfil-error');
    if (errorEl) errorEl.classList.add('d-none');
}

/**
 * Alterna visibilidad de la contraseña en el panel de confirmación de perfil.
 */
function togglePwVisibilityPerfil() {
    const input = document.getElementById('login-perfil-password');
    const btn = document.getElementById('btn-toggle-pw-perfil');
    if (!input) return;
    if (input.type === 'password') {
        input.type = 'text';
        if (btn) btn.innerHTML = '<i class="fa-solid fa-eye-slash"></i>';
    } else {
        input.type = 'password';
        if (btn) btn.innerHTML = '<i class="fa-solid fa-eye"></i>';
    }
}

/**
 * Alterna visibilidad de la contraseña en el formulario de credenciales (email/password).
 */
function togglePwVisibilityCredenciales() {
    const input = document.getElementById('login-input-password');
    if (!input) return;
    input.type = (input.type === 'password') ? 'text' : 'password';
}

/* ==========================================================================
   FIN DEL MÓDULO APP.JS
   ========================================================================== */

