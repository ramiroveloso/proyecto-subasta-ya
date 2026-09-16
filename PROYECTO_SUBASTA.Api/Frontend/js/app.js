/**
 * SubastaYa - Lógica de Aplicación Frontend & Controlador de Módulos
 * Incluye Carga de Saldo Libre para Perfil Tester: Ramiro Veloso
 */

let usuarioActual = (typeof PERFILES_SEMILLA !== 'undefined' && PERFILES_SEMILLA.length > 0) ? (PERFILES_SEMILLA[1] || PERFILES_SEMILLA[0]) : null;

let subastasCache = [];
let categoriasCache = [];
let subastaSeleccionadaSala = null;
let timerSalaInterval = null;
let timerCardsInterval = null;
let contadorSyncInterval = 0;

/* ==========================================================================
   CONFIGURACIÓN DE HUSOS HORARIOS (UTC EN BASE DE DATOS / UTC-3 EN FRONTEND)
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
 * Formatea una fecha y hora en UTC-3 (Argentina): "DD/MM/AAAA, HH:mm:ss"
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
 * Formatea una fecha y hora corta en UTC-3 (Argentina): "DD/MM/AAAA HH:mm"
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
 * Formatea únicamente la hora en UTC-3: "HH:mm:ss"
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
 * Convierte un timestamp/Date a formato "YYYY-MM-DDTHH:mm" en UTC-3
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
 * Convierte el valor local en UTC-3 de un <input type="datetime-local">
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

    // 1. Cargar perfiles dinámicamente desde la Base de Datos / API
    const usuariosRemotos = await fetchObtenerUsuarios();
    if (usuariosRemotos && usuariosRemotos.length > 0) {
        PERFILES_SEMILLA = usuariosRemotos.map(u => ({
            id: u.id ?? u.Id,
            nombre: u.nombre ?? u.Nombre,
            email: u.email ?? u.Email,
            saldoInicial: 0 // El saldo real se consulta dinámicamente de su billetera
        }));
        const usuarioEncontrado = usuarioActual ? PERFILES_SEMILLA.find(p => p.id === usuarioActual.id) : null;
        usuarioActual = usuarioEncontrado || PERFILES_SEMILLA[1] || PERFILES_SEMILLA[0];
    }

    await renderizarSelectorPerfilesSemilla();
    configurarEventosUI();

    await cargarCategorias();
    await aplicarFiltros();
    await actualizarBilleteraUI();
    await cargarMisActividades();

    timerCardsInterval = setInterval(actualizarTemporizadoresCatalogo, 1000);
});

/* ==========================================================================
   PERFILES SEMILLA & USUARIO TESTER (RAMIRO VELOSO)
   SINCRONISMO DE SALDOS EN TIEMPO REAL
   ========================================================================== */

async function renderizarSelectorPerfilesSemilla() {
    const dropdownMenu = document.getElementById('dropdown-perfiles-semilla');
    if (!dropdownMenu) return;

    dropdownMenu.innerHTML = '<li><h6 class="dropdown-header text-uppercase extra-small fw-bold">Perfiles de Prueba / Tester</h6></li>';
    
    // Obtenemos los saldos actuales de todas las billeteras en paralelo
    const billeteras = await Promise.all(
        PERFILES_SEMILLA.map(p => fetchObtenerBilletera(p.id).catch(() => null))
    );

    PERFILES_SEMILLA.forEach((p, idx) => {
        const b = billeteras[idx];
        const saldoDisp = b ? (b.saldoDisponible ?? b.SaldoDisponible ?? p.saldoInicial) : p.saldoInicial;
        const saldoRet = b ? (b.saldoRetenido ?? b.SaldoRetenido ?? 0) : 0;
        const esRamiro = p.email === 'ramiro.veloso@tester.com';
        const esActivo = Boolean(usuarioActual && p.id === usuarioActual.id);

        dropdownMenu.innerHTML += `
            <li>
                <a class="dropdown-item d-flex align-items-center justify-content-between py-2 ${esActivo ? 'active fw-bold' : ''}" 
                   href="#" onclick="cambiarPerfilSemilla(${p.id}); return false;" data-dropdown-user-id="${p.id}">
                    <div class="me-2">
                        <div class="fw-bold">${esRamiro ? '<i class="fa-solid fa-star text-warning me-1"></i>' : ''}${p.nombre}</div>
                        <div class="extra-small opacity-75">${p.email}</div>
                    </div>
                    <div class="text-end">
                        <span class="badge badge-saldo font-monospace ${saldoDisp > 0 ? (esActivo ? 'bg-light text-dark' : 'bg-success-subtle text-success border border-success-subtle') : 'bg-danger-subtle text-danger border border-danger-subtle'}">
                            $${saldoDisp.toLocaleString('es-AR')}
                        </span>
                        <div class="retencion-info extra-small ${saldoRet > 0 ? (esActivo ? 'text-warning' : 'text-warning-emphasis') : 'd-none'}" style="font-size: 0.72rem;">
                            ${saldoRet > 0 ? `(Ret: $${saldoRet.toLocaleString('es-AR')})` : ''}
                        </div>
                    </div>
                </a>
            </li>
        `;
    });
    
    dropdownMenu.innerHTML += '<li><hr class="dropdown-divider"></li>';
    dropdownMenu.innerHTML += `
        <li class="px-2 py-1">
            <button class="btn btn-success btn-sm w-100 fw-bold shadow-sm" onclick="cargarSaldoLibreRapido(100000)">
                <i class="fa-solid fa-coins me-1"></i> Carga Libre +$100.000 a ${usuarioActual ? usuarioActual.nombre.split(' ')[0] : 'Usuario'}
            </button>
        </li>
        <li><a class="dropdown-item" href="#" data-bs-toggle="modal" data-bs-target="#modalCargarSaldo"><i class="fa-solid fa-wallet me-2 text-primary-custom"></i>Consola Carga Libre de Saldo...</a></li>
        <li><a class="dropdown-item" href="#" data-bs-toggle="modal" data-bs-target="#modalCrearUsuario"><i class="fa-solid fa-user-plus me-2 text-secondary"></i>Crear Nuevo Usuario</a></li>
    `;

    // Sincronizar nombre de usuario y badge de saldo en el botón de la barra superior
    const userInfo = document.getElementById('wallet-usuario-info');
    if (userInfo && usuarioActual) {
        userInfo.textContent = `${usuarioActual.nombre} (${usuarioActual.email})`;
    }

    const bActual = billeteras.find(b => b && usuarioActual && (b.usuarioId === usuarioActual.id || b.UsuarioId === usuarioActual.id));
    const saldoActualDisp = bActual ? (bActual.saldoDisponible ?? bActual.SaldoDisponible ?? (usuarioActual ? usuarioActual.saldoInicial : 0)) : (usuarioActual ? usuarioActual.saldoInicial : 0);
    const navBadge = document.getElementById('nav-user-saldo');
    if (navBadge) {
        navBadge.textContent = `$${saldoActualDisp.toLocaleString('es-AR')}`;
        navBadge.className = `badge font-monospace ${saldoActualDisp > 0 ? 'bg-success' : 'bg-danger'}`;
    }
}

async function sincronizarSaldosDropdown() {
    try {
        const billeteras = await Promise.all(
            PERFILES_SEMILLA.map(p => fetchObtenerBilletera(p.id).catch(() => null))
        );

        PERFILES_SEMILLA.forEach((p, idx) => {
            const b = billeteras[idx];
            if (!b) return;
            const saldoDisp = b.saldoDisponible ?? b.SaldoDisponible ?? 0;
            const saldoRet = b.saldoRetenido ?? b.SaldoRetenido ?? 0;
            actualizarBadgeUsuarioEnDropdown(p.id, saldoDisp, saldoRet);
        });

        // Actualizar el saldo del usuario activo en la barra superior
        if (usuarioActual) {
            const bActual = billeteras.find(b => b && (b.usuarioId === usuarioActual.id || b.UsuarioId === usuarioActual.id));
            if (bActual) {
                const saldoActualDisp = bActual.saldoDisponible ?? bActual.SaldoDisponible ?? 0;
                const navBadge = document.getElementById('nav-user-saldo');
                if (navBadge) {
                    navBadge.textContent = `$${saldoActualDisp.toLocaleString('es-AR')}`;
                    navBadge.className = `badge font-monospace ${saldoActualDisp > 0 ? 'bg-success' : 'bg-danger'}`;
                }
            }
        }
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
    const perfil = PERFILES_SEMILLA.find(p => p.id === usuarioId);
    if (!perfil) return;

    usuarioActual = perfil;
    document.getElementById('wallet-usuario-info').textContent = `${perfil.nombre} (${perfil.email})`;

    await renderizarSelectorPerfilesSemilla();
    await actualizarBilleteraUI();
    actualizarTarjetasCatalogo();
    await cargarMisActividades();

    if (subastaSeleccionadaSala) {
        actualizarMonitorPujasSala();
    }

    mostrarToast(`Perfil activo cambiado a: <strong>${perfil.nombre}</strong> (${perfil.email})`, 'Perfil Activo Actualizado', 'info');
}

/** Carga de Saldo Libre Inmediata (1-Click) para Pruebas del Tester */
async function cargarSaldoLibreRapido(monto) {
    try {
        await fetchCargarSaldo(usuarioActual.id, monto);
        mostrarToast(`⚡ <strong>¡Carga Libre Acreditada!</strong> +$${monto.toLocaleString()} añadidos al disponible de ${usuarioActual.nombre}.`, 'Fondos Acreditados (Modo Tester)', 'success');
        
        usuarioActual.saldoInicial = (usuarioActual.saldoInicial || 0) + monto;
        await actualizarBilleteraUI();
        await renderizarSelectorPerfilesSemilla();
    } catch (e) {
        mostrarToast(`Error al recargar: ${e.message}`, 'Error', 'danger');
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
                selectFiltro.innerHTML += `<option value="${c.id}">${c.nombre}</option>`;
            });
        }

        if (selectCrear) {
            selectCrear.innerHTML = '<option value="">Seleccione una categoría</option>';
            categoriasCache.forEach(c => {
                selectCrear.innerHTML += `<option value="${c.id}">${c.nombre}</option>`;
            });
        }
    } catch (e) {
        console.error("Error al cargar categorías", e);
    }
}

async function aplicarFiltros() {
    const grid = document.getElementById('catalogo-grid');
    if (!grid) return;

    grid.innerHTML = `
        <div class="col-12 text-center py-5">
            <div class="spinner-border text-primary-custom" role="status"></div>
            <p class="text-muted mt-2 small">Cargando catálogo en tiempo real...</p>
        </div>
    `;

    try {
        subastasCache = await fetchObtenerSubastas();
        
        const textoFiltro = (document.getElementById('filtro-busqueda')?.value || '').toLowerCase().trim();
        const estadoFiltro = document.getElementById('filtro-estado')?.value || '';
        const categoriaFiltro = document.getElementById('filtro-categoria')?.value || '';
        const ordenFiltro = document.getElementById('filtro-orden')?.value || 'tiempo';

        let subastasFiltradas = subastasCache.filter(sub => {
            const estadoReal = determinarEstadoSubasta(sub);
            const coincideTexto = !textoFiltro || 
                (sub.titulo || '').toLowerCase().includes(textoFiltro) || 
                (sub.descripcion && sub.descripcion.toLowerCase().includes(textoFiltro));
            
            let coincideEstado = true;
            if (estadoFiltro) {
                if (estadoFiltro === 'FINALIZADA') {
                    coincideEstado = (estadoReal === 'FINALIZADA' || estadoReal === 'DESIERTA');
                } else {
                    coincideEstado = (estadoReal === estadoFiltro);
                }
            }

            const subCatId = sub.categoriaId ?? sub.CategoriaId;
            const coincideCategoria = !categoriaFiltro || subCatId == categoriaFiltro;
            return coincideTexto && coincideEstado && coincideCategoria;
        });

        if (ordenFiltro === 'tiempo') {
            subastasFiltradas.sort((a, b) => {
                const finA = parseUtcDate(a.fechaFin ?? a.FechaFin)?.getTime() || 0;
                const finB = parseUtcDate(b.fechaFin ?? b.FechaFin)?.getTime() || 0;
                return finA - finB;
            });
        } else if (ordenFiltro === 'puja') {
            subastasFiltradas.sort((a, b) => obtenerPujaMaxima(b) - obtenerPujaMaxima(a));
        } else if (ordenFiltro === 'precio') {
            subastasFiltradas.sort((a, b) => (a.precioBase ?? a.PrecioBase ?? 0) - (b.precioBase ?? b.PrecioBase ?? 0));
        }

        grid.innerHTML = '';

        if (subastasFiltradas.length === 0) {
            grid.innerHTML = `
                <div class="col-12 text-center text-muted py-5">
                    <i class="fa-solid fa-box-open fa-3x mb-3 text-secondary opacity-50"></i>
                    <h5 class="fw-bold">No se encontraron subastas</h5>
                    <p class="small">Ajusta los filtros de búsqueda o categoría.</p>
                </div>
            `;
            return;
        }

        subastasFiltradas.forEach(sub => {
            const subId = sub.id ?? sub.Id;
            const subTitulo = sub.titulo ?? sub.Titulo ?? `Subasta #${subId}`;
            const subCatId = sub.categoriaId ?? sub.CategoriaId;
            const categoria = categoriasCache.find(c => c.id == subCatId)?.nombre || 'General';
            const pujaActual = obtenerPujaMaxima(sub);
            const subEstado = determinarEstadoSubasta(sub);
            const badgeClass = getEstadoBadgeClass(subEstado);
            const imagenUrl = sub.urlImagen ?? sub.UrlImagen ?? 'assets/images/watch.png';
            const pujas = sub.pujas ?? sub.Pujas ?? [];
            const totalOfertas = pujas.length;
            const subPrecioBase = sub.precioBase ?? sub.PrecioBase ?? 0;
            const fechaInicioStr = sub.fechaInicio ?? sub.FechaInicio;
            const fechaFinStr = sub.fechaFin ?? sub.FechaFin;

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
                btnText = 'Ver Subasta Programada';
                btnIcon = 'fa-regular fa-clock';
                btnClass = 'btn-outline-warning text-dark';
                badgeTimerHtml = `
                    <span class="badge bg-warning-subtle text-dark border border-warning-subtle fw-semibold card-timer" 
                          data-subasta-id="${subId}" data-fecha-inicio="${fechaInicioStr}" data-fecha-fin="${fechaFinStr}"
                          title="Inicia el ${formatFechaHoraCortaUTC3(fechaInicioStr)} (UTC-3)">
                        Cargando inicio...
                    </span>
                `;
            } else if (subEstado === 'ACTIVA') {
                labelTiempo = 'Cierre:';
                btnText = 'Entrar a Sala en Vivo';
                btnIcon = 'fa-solid fa-gavel';
                btnClass = 'btn-primary-custom';
                badgeTimerHtml = `
                    <span class="badge bg-danger-subtle text-danger border border-danger-subtle fw-semibold card-timer" 
                          data-subasta-id="${subId}" data-fecha-inicio="${fechaInicioStr}" data-fecha-fin="${fechaFinStr}"
                          title="Cierre estimado: ${formatFechaHoraCortaUTC3(fechaFinStr)} (UTC-3)">
                        Cargando...
                    </span>
                `;
            } else if (subEstado === 'FINALIZADA') {
                labelTiempo = 'Cerró (UTC-3):';
                btnText = 'Ver Sala / Resultados';
                btnIcon = 'fa-solid fa-flag-checkered';
                btnClass = 'btn-secondary';
                badgeTimerHtml = `
                    <span class="badge bg-secondary text-white border fw-semibold card-timer" 
                          data-subasta-id="${subId}" data-fecha-inicio="${fechaInicioStr}" data-fecha-fin="${fechaFinStr}">
                        ${formatFechaHoraCortaUTC3(fechaFinStr)}
                    </span>
                `;
            } else { // DESIERTA
                labelTiempo = 'Cerró (UTC-3):';
                btnText = 'Ver Sala / Sin Ofertas';
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
                            ${sub.descripcion ?? sub.Descripcion ?? 'Sin descripción disponible.'}
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

                        <button class="btn ${btnClass} btn-sm w-100 mt-auto card-action-btn" onclick="abrirSalaEnVivo(${subId})">
                            <i class="${btnIcon} me-1"></i> ${btnText}
                        </button>
                    </div>
                </div>
            `;
            grid.appendChild(cardCol);
        });

        actualizarTemporizadoresCatalogo();
        actualizarSelectorSalaDirecto();

    } catch (error) {
        grid.innerHTML = `
            <div class="col-12 text-center text-danger py-4">
                <i class="fa-solid fa-circle-exclamation fa-2x mb-2"></i>
                <p>Error al cargar el catálogo: ${error.message}</p>
            </div>
        `;
    }
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
            actionBtn.innerHTML = '<i class="fa-regular fa-clock me-1"></i> Ver Subasta Programada';
        } else if (subEstado === 'ACTIVA') {
            actionBtn.className = 'btn btn-primary-custom btn-sm w-100 mt-auto card-action-btn';
            actionBtn.innerHTML = '<i class="fa-solid fa-gavel me-1"></i> Entrar a Sala en Vivo';
        } else if (subEstado === 'FINALIZADA') {
            actionBtn.className = 'btn btn-secondary btn-sm w-100 mt-auto card-action-btn';
            actionBtn.innerHTML = '<i class="fa-solid fa-flag-checkered me-1"></i> Ver Sala / Resultados';
        } else {
            actionBtn.className = 'btn btn-dark btn-sm w-100 mt-auto card-action-btn';
            actionBtn.innerHTML = '<i class="fa-solid fa-ban me-1"></i> Ver Sala / Sin Ofertas';
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
            timeLabel.innerHTML = '<i class="fa-solid fa-flag-checkered me-1"></i>Cerró (UTC-3):';
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
            t.innerHTML = 'DESIERTA';
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

    if (!titulo || !categoriaId) {
        mostrarToast('Complete el título y la categoría.', 'Campos Requeridos', 'warning');
        return;
    }

    if (precioBase <= 0 || isNaN(precioBase)) {
        mostrarToast('El precio base debe ser mayor a 0.', 'Error de Validación', 'danger');
        return;
    }

    if (incrementoMinimo <= 0 || isNaN(incrementoMinimo)) {
        mostrarToast('El incremento mínimo debe ser mayor a 0.', 'Error de Validación', 'danger');
        return;
    }

    if (!fechaInicioVal || !fechaFinVal) {
        mostrarToast('Complete las fechas de inicio y cierre.', 'Campos Requeridos', 'warning');
        return;
    }

    // Convertir fecha de inicio y fin ingresadas en UTC-3 hacia formato ISO UTC estándar con "Z"
    const fechaInicioIso = utc3InputToIsoUtc(fechaInicioVal);
    const fechaFinIso = utc3InputToIsoUtc(fechaFinVal);

    if (new Date(fechaInicioIso) >= new Date(fechaFinIso)) {
        mostrarToast('La fecha de inicio debe ser anterior a la fecha de finalización.', 'Error de Fechas', 'danger');
        return;
    }

    const btnSubmit = document.getElementById('btn-crear-subasta');
    btnSubmit.disabled = true;
    btnSubmit.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> Publicando...';

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

        await aplicarFiltros();
        const tabCatalogoBtn = document.getElementById('tab-catalogo');
        if (tabCatalogoBtn) {
            const bsTab = new bootstrap.Tab(tabCatalogoBtn);
            bsTab.show();
        }

    } catch (err) {
        mostrarToast(`Error al crear subasta: ${err.message}`, 'Error de Servidor', 'danger');
    } finally {
        btnSubmit.disabled = false;
        btnSubmit.innerHTML = '<i class="fa-solid fa-paper-plane me-2"></i> Publicar Subasta';
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
        select.innerHTML += `<option value="${s.id}">[${est}] ${s.titulo} - ($${obtenerPujaMaxima(s).toLocaleString('es-AR')})</option>`;
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
        document.getElementById('sala-imagen').src = subastaSeleccionadaSala.urlImagen || 'assets/images/watch.png';
        
        const catNombre = categoriasCache.find(c => c.id == subastaSeleccionadaSala.categoriaId)?.nombre || 'General';
        document.getElementById('sala-categoria-badge').textContent = catNombre;

        const estadoReal = determinarEstadoSubasta(subastaSeleccionadaSala);
        const estadoBadge = document.getElementById('sala-estado-badge');
        if (estadoBadge) {
            estadoBadge.className = `badge ${getEstadoBadgeClass(estadoReal)}`;
            if (estadoReal === 'PROGRAMADA') {
                estadoBadge.textContent = `PROGRAMADA (Inicia: ${formatFechaHoraCortaUTC3(subastaSeleccionadaSala.fechaInicio)} UTC-3)`;
            } else if (estadoReal === 'FINALIZADA') {
                estadoBadge.textContent = `FINALIZADA (${formatFechaHoraCortaUTC3(subastaSeleccionadaSala.fechaFin)} UTC-3)`;
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

    } catch (e) {
        mostrarToast('No se pudo cargar la sala en vivo.', 'Error', 'danger');
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

        if (btnPuja) {
            btnPuja.disabled = true;
            btnPuja.innerHTML = '<i class="fa-regular fa-clock me-2"></i> Subasta Programada (Ofertas bloqueadas hasta el inicio)';
        }
    } else if (estadoReal === 'FINALIZADA' || estadoReal === 'DESIERTA') {
        timerElem.textContent = `00:00:00 - ${estadoReal}`;
        if (timerBox) timerBox.className = 'timer-box bg-secondary border-secondary';
        if (antiSnipingBadge) antiSnipingBadge.className = 'd-none';

        if (btnPuja) {
            btnPuja.disabled = true;
            btnPuja.innerHTML = `<i class="fa-solid fa-flag-checkered me-2"></i> Subasta Concluida (${estadoReal})`;
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

        if (btnPuja) {
            btnPuja.disabled = false;
            btnPuja.innerHTML = '<i class="fa-solid fa-bolt me-2"></i> Confirmar y Enviar Puja';
        }

        if (diff <= 60000) {
            if (timerBox) timerBox.className = 'timer-box timer-critical-alert';
            if (antiSnipingBadge) {
                antiSnipingBadge.className = 'anti-sniping-badge bg-danger text-white mt-2';
                antiSnipingBadge.innerHTML = '<i class="fa-solid fa-shield-cat me-1"></i> ¡ZONA CRÍTICA ANTI-SNIPING! Nueva puja extenderá +60s';
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
        <button type="button" class="btn btn-outline-primary-custom btn-sm" onclick="setMontoPujaSugerido(${val1})">+$${val1.toLocaleString()}</button>
        <button type="button" class="btn btn-outline-primary-custom btn-sm" onclick="setMontoPujaSugerido(${val2})">+$${val2.toLocaleString()}</button>
        <button type="button" class="btn btn-outline-primary-custom btn-sm" onclick="setMontoPujaSugerido(${val3})">+$${val3.toLocaleString()}</button>
    `;

    const pujaActual = obtenerPujaMaxima(subastaSeleccionadaSala);
    const montoRecomendado = pujaActual + incrementoMinimo;
    document.getElementById('input-monto-puja').value = montoRecomendado;
}

function setMontoPujaSugerido(offset) {
    if (!subastaSeleccionadaSala) return;
    const pujaActual = obtenerPujaMaxima(subastaSeleccionadaSala);
    document.getElementById('input-monto-puja').value = pujaActual + offset;
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
                    <h6 class="fw-bold mb-0">¡ESTÁS LIDERANDO LA PUJA! (${usuarioActual.nombre})</h6>
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
        const pUid = p.usuarioId ?? p.UsuarioId;
        const anonHandle = p.postorAnonimo || `Postor #${(pUid * 33 + 100).toString(16).toUpperCase()}`;

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

async function enviarPuja(event) {
    event.preventDefault();

    if (!subastaSeleccionadaSala) return;

    // 1. Obtener la subasta fresca de la API para asegurar que la versión esté sincronizada
    try {
        const subastaFresca = await fetchObtenerSubastaPorId(subastaSeleccionadaSala.id);
        subastaSeleccionadaSala.version = subastaFresca.version ?? subastaFresca.Version ?? 1;
        subastaSeleccionadaSala.pujas = subastaFresca.pujas ?? subastaFresca.Pujas ?? [];
    } catch (e) {
        console.warn("No se pudo refrescar la subasta antes de pujar, usando versión local.", e);
    }

    const montoInput = parseFloat(document.getElementById('input-monto-puja').value);
    const pujaActual = obtenerPujaMaxima(subastaSeleccionadaSala);
    const incrementoMin = subastaSeleccionadaSala.incrementoMinimo ?? subastaSeleccionadaSala.IncrementoMinimo ?? 1000;

    // ... (el resto de tus validaciones de monto e incremento siguen exactamente igual)

    if (isNaN(montoInput) || montoInput <= pujaActual) {
        mostrarToast(`La puja debe superar la oferta actual de $${pujaActual.toLocaleString('es-AR')}.`, 'Oferta Inválida', 'warning');
        return;
    }

    const pujasPrevias = subastaSeleccionadaSala.pujas || subastaSeleccionadaSala.Pujas || [];
    if ((montoInput - pujaActual) < incrementoMin && pujasPrevias.length > 0) {
        mostrarToast(`El incremento mínimo requerido es de $${incrementoMin.toLocaleString('es-AR')}.`, 'Incremento Insuficiente', 'warning');
        return;
    }

    try {
        let pujaPreviaLiberar = null;
        if (pujasPrevias.length > 0) {
            const sortedPrevias = [...pujasPrevias].sort((a, b) => (b.monto ?? b.Monto) - (a.monto ?? a.Monto));
            pujaPreviaLiberar = sortedPrevias[0];
        }

        const prevUsuarioId = pujaPreviaLiberar ? (pujaPreviaLiberar.usuarioId ?? pujaPreviaLiberar.UsuarioId) : null;
        const prevMonto = pujaPreviaLiberar ? (pujaPreviaLiberar.monto ?? pujaPreviaLiberar.Monto) : 0;

        if (pujaPreviaLiberar && prevUsuarioId === usuarioActual.id) {
            // Mismo usuario incrementa su oferta: liberar la retención anterior para contar con los fondos
            await fetchLiberarSaldo(usuarioActual.id, prevMonto, subastaSeleccionadaSala.id);
        }

        await fetchRetenerSaldo(usuarioActual.id, montoInput, subastaSeleccionadaSala.id);

        if (pujaPreviaLiberar && prevUsuarioId !== usuarioActual.id) {
            // Se superó la oferta del postor anterior (Outbid): liberar su garantía Escrow
            await fetchLiberarSaldo(prevUsuarioId, prevMonto, subastaSeleccionadaSala.id);
        }

        // Llamada formal a la API REST (o simulación local) para asentar la puja y validar concurrencia optimista
        const versionActual = subastaSeleccionadaSala.version || subastaSeleccionadaSala.Version || 1;
        const resPuja = await fetchRegistrarPuja(subastaSeleccionadaSala.id, usuarioActual.id, montoInput, versionActual);

        const ahora = Date.now();
        const fechaFinMs = parseUtcDate(subastaSeleccionadaSala.fechaFin).getTime();
        let antiSnipingActivado = false;

        if ((fechaFinMs - ahora) <= 60000 && (fechaFinMs - ahora) > 0) {
            subastaSeleccionadaSala.fechaFin = new Date(fechaFinMs + 60000).toISOString();
            antiSnipingActivado = true;
        }

        const anonHandle = `Postor #${(usuarioActual.id * 33 + 100).toString(16).toUpperCase()}`;
        const nuevaPuja = (resPuja && resPuja.puja) ? resPuja.puja : {
            id: Date.now(),
            subastaId: subastaSeleccionadaSala.id,
            usuarioId: usuarioActual.id,
            monto: montoInput,
            fechaCreacion: new Date().toISOString(),
            postorAnonimo: anonHandle
        };

        if (!subastaSeleccionadaSala.pujas) subastaSeleccionadaSala.pujas = [];
        if (!subastaSeleccionadaSala.pujas.some(p => p.id === nuevaPuja.id)) {
            subastaSeleccionadaSala.pujas.push(nuevaPuja);
        }

        if (resPuja && resPuja.version) {
            subastaSeleccionadaSala.version = resPuja.version;
        } else {
            subastaSeleccionadaSala.version = (subastaSeleccionadaSala.version || 1) + 1;
        }

        const indexSub = subastasCache.findIndex(s => s.id === subastaSeleccionadaSala.id);
        if (indexSub !== -1) {
            if (!subastasCache[indexSub].pujas) subastasCache[indexSub].pujas = [];
            if (!subastasCache[indexSub].pujas.some(p => p.id === nuevaPuja.id)) {
                subastasCache[indexSub].pujas.push(nuevaPuja);
            }
            subastasCache[indexSub].fechaFin = subastaSeleccionadaSala.fechaFin;
            subastasCache[indexSub].version = subastaSeleccionadaSala.version;
        }

        if (typeof MOCK_SUBASTAS !== 'undefined' && antiSnipingActivado) {
            const mockItem = MOCK_SUBASTAS.find(s => s.id === subastaSeleccionadaSala.id);
            if (mockItem) mockItem.fechaFin = subastaSeleccionadaSala.fechaFin;
        }

        if (antiSnipingActivado) {
            mostrarToast(`⚡ <strong>¡REGLA ANTI-SNIPING ACTIVADA!</strong> Se han añadido +60 segundos al temporizador por oferta de último minuto.`, 'Anti-Sniping Activado', 'warning');
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

    } catch (err) {
        if (err.status === 400 || err.status === 422 || (err.message && err.message.toLowerCase().includes('saldo insuficiente'))) {
            abrirModalSaldoInsuficiente(montoInput);
        } else if (err.status === 409) {
            abrirModalConcurrenciaOptimista();
        } else {
            mostrarToast(`No se pudo procesar la puja: ${err.message}`, 'Error de Operación', 'danger');
        }
    }
}

/* ==========================================================================
   MÓDULO 4: BILLETERA VIRTUAL (MÉTRICAS Y ESCROW)
   ========================================================================== */

async function actualizarBilleteraUI() {
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
                <td class="fw-bold ${tipoInfo.esPositivo ? 'text-success' : 'text-dark'}">
                    ${tipoInfo.esPositivo ? '+' : '-'}$${m.monto.toLocaleString()}
                </td>
                <td><small class="text-muted">${fechaStr}</small></td>
                <td>${m.concepto || (m.subastaId ? `Subasta #${m.subastaId}` : 'Operación General')}</td>
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

    try {
        await fetchCargarSaldo(usuarioActual.id, monto);
        mostrarToast(`¡Se han acreditado $${monto.toLocaleString()} libremente a la cuenta de ${usuarioActual.nombre}!`, 'Acreditación Exitosa', 'success');

        const modalElem = document.getElementById('modalCargarSaldo');
        const modal = bootstrap.Modal.getInstance(modalElem);
        if (modal) modal.hide();

        document.getElementById('form-cargar-saldo').reset();
        usuarioActual.saldoInicial = (usuarioActual.saldoInicial || 0) + monto;
        await actualizarBilleteraUI();
        await renderizarSelectorPerfilesSemilla();

    } catch (err) {
        mostrarToast(`Error al cargar saldo: ${err.message}`, 'Error', 'danger');
    }
}

async function procesarRetencionManual(event) {
    event.preventDefault();
    const monto = parseFloat(document.getElementById('test-retener-monto').value);
    const subastaId = parseInt(document.getElementById('test-retener-subastaId').value) || 101;

    try {
        await fetchRetenerSaldo(usuarioActual.id, monto, subastaId);
        mostrarToast(`Saldo de $${monto.toLocaleString()} retenido preventivamente en Escrow.`, 'Retención Ejecutada', 'info');
        await actualizarBilleteraUI();
        await sincronizarSaldosDropdown();
    } catch (e) {
        if (e.status === 400 || e.status === 422 || e.message.includes('saldo insuficiente')) {
            abrirModalSaldoInsuficiente(monto);
        } else {
            mostrarToast(e.message, 'Error', 'danger');
        }
    }
}

async function procesarLiberacionManual(event) {
    event.preventDefault();
    const monto = parseFloat(document.getElementById('test-liberar-monto').value);
    const subastaId = parseInt(document.getElementById('test-liberar-subastaId').value) || 101;

    try {
        await fetchLiberarSaldo(usuarioActual.id, monto, subastaId);
        mostrarToast(`Saldo de $${monto.toLocaleString()} liberado de Escrow.`, 'Fondos Liberados', 'success');
        await actualizarBilleteraUI();
        await sincronizarSaldosDropdown();
    } catch (e) {
        mostrarToast(e.message, 'Error', 'danger');
    }
}

async function procesarCrearUsuario(event) {
    event.preventDefault();
    const nombre = document.getElementById('modal-usuario-nombre').value.trim();
    const email = document.getElementById('modal-usuario-email').value.trim();

    try {
        const res = await fetchCrearUsuario(nombre, email);
        mostrarToast(res.mensaje, 'Usuario Creado', 'success');

        const nuevoPerfil = {
            id: res.usuarioId || Date.now(),
            nombre: nombre,
            email: email,
            rol: 'Nuevo Usuario',
            saldoInicial: 0
        };

        PERFILES_SEMILLA.push(nuevoPerfil);
        await cambiarPerfilSemilla(nuevoPerfil.id);

        const modalElem = document.getElementById('modalCrearUsuario');
        const modal = bootstrap.Modal.getInstance(modalElem);
        if (modal) modal.hide();

    } catch (e) {
        mostrarToast(`Error al crear usuario: ${e.message}`, 'Error', 'danger');
    }
}

/* ==========================================================================
   MÓDULO 5: MIS ACTIVIDADES
   ========================================================================== */

async function cargarMisActividades() {
    const contenedorPujas = document.getElementById('actividades-mis-pujas');
    const contenedorPublicaciones = document.getElementById('actividades-mis-publicaciones');

    if (!contenedorPujas || !contenedorPublicaciones) return;

    if (!subastasCache || subastasCache.length === 0) {
        try {
            subastasCache = await fetchObtenerSubastas();
        } catch (e) {
            console.warn("No se pudieron cargar subastas para Mis Actividades", e);
        }
    }

    let misPujasCount = 0;
    let misPublicacionesCount = 0;
    let misGanadasCount = 0;

    contenedorPujas.innerHTML = '';
    contenedorPublicaciones.innerHTML = '';

    subastasCache.forEach(sub => {
        const subId = sub.id ?? sub.Id;
        const subTitulo = sub.titulo ?? sub.Titulo ?? `Subasta #${subId}`;
        const subImg = sub.urlImagen ?? sub.UrlImagen ?? 'assets/images/watch.png';
        const subPrecioBase = sub.precioBase ?? sub.PrecioBase ?? 0;
        const subEstado = determinarEstadoSubasta(sub);
        const subVendedorId = sub.vendedorId ?? sub.VendedorId;
        const pujas = sub.pujas ?? sub.Pujas ?? [];
        const catId = sub.categoriaId ?? sub.CategoriaId;
        const catNombre = categoriasCache.find(c => c.id == catId)?.nombre || 'General';

        const haFinalizado = (subEstado === 'FINALIZADA' || subEstado === 'DESIERTA');
        const pujaAbsolutaMax = obtenerPujaMaxima(sub);

        // 1. Subastas en las que el usuario actual ha realizado ofertas
        const misPujasEnSub = pujas.filter(p => (p.usuarioId ?? p.UsuarioId) === usuarioActual.id);
        if (misPujasEnSub.length > 0) {
            misPujasCount += misPujasEnSub.length;
            const miMayorPuja = Math.max(...misPujasEnSub.map(p => (p.monto ?? p.Monto)));
            const esLider = (miMayorPuja === pujaAbsolutaMax);

            if (haFinalizado && esLider) {
                misGanadasCount++;
            }

            let badgeLiderazgoHtml = '';
            let escrowInfoHtml = '';
            let cardBorderClass = 'border';

            if (haFinalizado) {
                if (esLider) {
                    cardBorderClass = 'border-success border-2';
                    badgeLiderazgoHtml = `<span class="badge bg-success shadow-sm"><i class="fa-solid fa-crown me-1"></i>¡Subasta Ganada!</span>`;
                    escrowInfoHtml = `<span class="text-success extra-small fw-bold"><i class="fa-solid fa-circle-check me-1"></i>Adjudicada por $${miMayorPuja.toLocaleString('es-AR')}</span>`;
                } else {
                    badgeLiderazgoHtml = `<span class="badge bg-secondary"><i class="fa-solid fa-flag-checkered me-1"></i>Finalizada</span>`;
                    escrowInfoHtml = `<span class="text-muted extra-small"><i class="fa-solid fa-rotate-left text-secondary me-1"></i>Superada · Garantía liberada</span>`;
                }
            } else {
                if (esLider) {
                    cardBorderClass = 'border-success';
                    badgeLiderazgoHtml = `<span class="badge bg-success-subtle text-success border border-success-subtle"><i class="fa-solid fa-trophy me-1"></i>Vas ganando (Líder)</span>`;
                    escrowInfoHtml = `<span class="text-warning-emphasis extra-small fw-semibold"><i class="fa-solid fa-shield-halved me-1 text-warning"></i>$${miMayorPuja.toLocaleString('es-AR')} retenidos en Escrow</span>`;
                } else {
                    cardBorderClass = 'border-danger-subtle';
                    badgeLiderazgoHtml = `<span class="badge bg-danger-subtle text-danger border border-danger-subtle"><i class="fa-solid fa-triangle-exclamation me-1"></i>Te superaron (Outbid)</span>`;
                    escrowInfoHtml = `<span class="text-info extra-small fw-semibold"><i class="fa-solid fa-arrow-rotate-left me-1"></i>Garantía liberada ($${miMayorPuja.toLocaleString('es-AR')} disponibles)</span>`;
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
                                <span>${misPujasEnSub.length} oferta${misPujasEnSub.length === 1 ? '' : 's'} tuya${misPujasEnSub.length === 1 ? '' : 's'} (${pujas.length} en total)</span>
                            </div>
                            <div class="bg-light p-2 rounded-2 border extra-small mb-2">
                                <div class="d-flex justify-content-between">
                                    <span class="text-muted">Tu mejor oferta:</span>
                                    <span class="fw-bold text-primary-custom">$${miMayorPuja.toLocaleString('es-AR')}</span>
                                </div>
                                <div class="d-flex justify-content-between">
                                    <span class="text-muted">Oferta líder actual:</span>
                                    <span class="fw-bold ${esLider ? 'text-success' : 'text-danger'}">$${pujaAbsolutaMax.toLocaleString('es-AR')}</span>
                                </div>
                            </div>
                            <div class="d-flex justify-content-between align-items-center pt-1 border-top mt-1">
                                <div>${escrowInfoHtml}</div>
                                <button class="btn ${!haFinalizado && !esLider ? 'btn-warning text-dark fw-bold' : 'btn-outline-primary-custom'} btn-sm px-3" onclick="abrirSalaEnVivo(${subId})">
                                    ${!haFinalizado && !esLider ? '<i class="fa-solid fa-arrow-up me-1"></i>Pujar' : '<i class="fa-solid fa-gavel me-1"></i>Ver Sala'}
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            contenedorPujas.appendChild(col);
        }

        // 2. Publicaciones creadas por el usuario actual como vendedor
        if (subVendedorId === usuarioActual.id) {
            misPublicacionesCount++;
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
                                <span>${pujas.length} oferta${pujas.length === 1 ? '' : 's'} recibida${pujas.length === 1 ? '' : 's'}</span>
                            </div>
                            <div class="bg-light p-2 rounded-2 border extra-small mb-2">
                                <div class="d-flex justify-content-between">
                                    <span class="text-muted">Precio base:</span>
                                    <span class="fw-semibold text-dark">$${subPrecioBase.toLocaleString('es-AR')}</span>
                                </div>
                                <div class="d-flex justify-content-between">
                                    <span class="text-muted">Puja máxima actual:</span>
                                    <span class="fw-bold text-success">$${pujaAbsolutaMax.toLocaleString('es-AR')}</span>
                                </div>
                            </div>
                            <div class="d-flex justify-content-between align-items-center pt-1 border-top mt-1">
                                <span class="extra-small text-muted">
                                    ${pujas.length > 0 ? `<i class="fa-solid fa-users me-1 text-primary-custom"></i>${pujas.length} oferta(s)` : '<i class="fa-regular fa-clock me-1"></i>Sin ofertas aún'}
                                </span>
                                <button class="btn btn-outline-primary-custom btn-sm px-3" onclick="abrirSalaEnVivo(${subId})">
                                    <i class="fa-solid fa-sliders me-1"></i>Administrar
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            contenedorPublicaciones.appendChild(colPub);
        }
    });

    if (contenedorPujas.children.length === 0) {
        contenedorPujas.innerHTML = `
            <div class="col-12 text-center py-4 text-muted border rounded-3 bg-light">
                <i class="fa-solid fa-gavel fa-2x mb-2 text-secondary opacity-50"></i>
                <p class="mb-1 fw-bold">Sin ofertas registradas con ${usuarioActual.nombre}</p>
                <p class="extra-small mb-3">Aún no has realizado pujas en las subastas disponibles.</p>
                <button class="btn btn-primary-custom btn-sm" onclick="document.getElementById('tab-catalogo').click()">
                    <i class="fa-solid fa-compass me-1"></i>Explorar Catálogo
                </button>
            </div>
        `;
    }

    if (contenedorPublicaciones.children.length === 0) {
        contenedorPublicaciones.innerHTML = `
            <div class="col-12 text-center py-4 text-muted border rounded-3 bg-light">
                <i class="fa-solid fa-store fa-2x mb-2 text-secondary opacity-50"></i>
                <p class="mb-1 fw-bold">Sin publicaciones con ${usuarioActual.nombre}</p>
                <p class="extra-small mb-3">No tienes artículos publicados a subasta como vendedor.</p>
                <button class="btn btn-primary-custom btn-sm" onclick="document.getElementById('tab-crear').click()">
                    <i class="fa-solid fa-plus me-1"></i>Publicar una Subasta
                </button>
            </div>
        `;
    }

    const statPujas = document.getElementById('stat-total-pujas');
    const statCreadas = document.getElementById('stat-subastas-creadas');
    const statGanadas = document.getElementById('stat-subastas-ganadas');

    if (statPujas) statPujas.textContent = misPujasCount;
    if (statCreadas) statCreadas.textContent = misPublicacionesCount;
    if (statGanadas) statGanadas.textContent = misGanadasCount;
}

/* ==========================================================================
   MODALES DE EXCEPCIONES HTTP & DIÁLOGOS DE ERROR
   ========================================================================== */

async function abrirModalSaldoInsuficiente(montoRequerido) {
    const billetera = await fetchObtenerBilletera(usuarioActual.id);
    const faltante = Math.max(0, montoRequerido - billetera.saldoDisponible);

    document.getElementById('error-saldo-disponible').textContent = `$${billetera.saldoDisponible.toLocaleString()}`;
    document.getElementById('error-monto-requerido').textContent = `$${montoRequerido.toLocaleString()}`;
    document.getElementById('error-monto-faltante').textContent = `$${faltante.toLocaleString()}`;
    
    document.getElementById('modal-cargar-monto').value = Math.ceil(faltante);

    const modalElem = document.getElementById('modalSaldoInsuficiente');
    const modal = new bootstrap.Modal(modalElem);
    modal.show();
}

function abrirModalConcurrenciaOptimista() {
    const modalElem = document.getElementById('modalConcurrencia');
    const modal = new bootstrap.Modal(modalElem);
    modal.show();
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

    const formRetener = document.getElementById('form-test-retener');
    if (formRetener) formRetener.addEventListener('submit', procesarRetencionManual);

    const formLiberar = document.getElementById('form-test-liberar');
    if (formLiberar) formLiberar.addEventListener('submit', procesarLiberacionManual);

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

    const tabElList = document.querySelectorAll('button[data-bs-toggle="pill"]');
    tabElList.forEach(tabEl => {
        tabEl.addEventListener('shown.bs.tab', async (event) => {
            const targetId = event.target.getAttribute('data-bs-target');
            if (targetId === '#content-catalogo') {
                await aplicarFiltros();
            } else if (targetId === '#content-sala') {
                if (!subastaSeleccionadaSala && subastasCache.length > 0) {
                    await cargarSalaEnVivo(subastasCache[0].id);
                }
            } else if (targetId === '#content-billetera') {
                await actualizarBilleteraUI();
            } else if (targetId === '#content-actividades') {
                await cargarMisActividades();
            }
        });
    });

    const dropdownUserContainer = document.getElementById('dropdown-perfiles-semilla')?.closest('.dropdown');
    if (dropdownUserContainer) {
        dropdownUserContainer.addEventListener('show.bs.dropdown', async () => {
            await sincronizarSaldosDropdown();
        });
    }
}

function mostrarToast(mensaje, titulo = 'Notificación', tipo = 'info') {
    const container = document.getElementById('toast-container');
    if (!container) return;

    const toastId = 'toast-' + Date.now();
    let bgHeader = 'bg-primary-custom text-white';
    if (tipo === 'success') bgHeader = 'bg-success text-white';
    if (tipo === 'danger') bgHeader = 'bg-danger text-white';
    if (tipo === 'warning') bgHeader = 'bg-warning text-dark';

    const toastHtml = `
        <div id="${toastId}" class="toast shadow-lg" role="alert" aria-live="assertive" aria-atomic="true" data-bs-delay="4500">
            <div class="toast-header ${bgHeader}">
                <strong class="me-auto"><i class="fa-solid fa-bell me-2"></i>${titulo}</strong>
                <small class="text-white-50">Ahora</small>
                <button type="button" class="btn-close btn-close-white ms-2" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
            <div class="toast-body bg-white text-dark">
                ${mensaje}
            </div>
        </div>
    `;

    container.insertAdjacentHTML('beforeend', toastHtml);
    const toastElem = document.getElementById(toastId);
    const bsToast = new bootstrap.Toast(toastElem);
    bsToast.show();

    toastElem.addEventListener('hidden.bs.toast', () => {
        toastElem.remove();
    });
}
