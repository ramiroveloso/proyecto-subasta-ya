/**
 * SubastaYa - Lógica de Aplicación Frontend & Controlador de Módulos
 * Incluye Carga de Saldo Libre para Perfil Tester: Ramiro Veloso
 */

let usuarioActual = PERFILES_SEMILLA[0]; // Ramiro Veloso Tester por defecto

let subastasCache = [];
let categoriasCache = [];
let subastaSeleccionadaSala = null;
let timerSalaInterval = null;
let timerCardsInterval = null;

document.addEventListener("DOMContentLoaded", async () => {
    inicializarFechasFormulario();
    renderizarSelectorPerfilesSemilla();
    configurarEventosUI();
    
    await cargarCategorias();
    await aplicarFiltros();
    await actualizarBilleteraUI();
    await cargarMisActividades();

    timerCardsInterval = setInterval(actualizarTemporizadoresCatalogo, 1000);
});

/* ==========================================================================
   PERFILES SEMILLA & USUARIO TESTER (RAMIRO VELOSO)
   ========================================================================== */

function renderizarSelectorPerfilesSemilla() {
    const dropdownMenu = document.getElementById('dropdown-perfiles-semilla');
    if (!dropdownMenu) return;

    dropdownMenu.innerHTML = '<li><h6 class="dropdown-header text-uppercase extra-small fw-bold">Perfil Tester Principal</h6></li>';
    
    PERFILES_SEMILLA.forEach(p => {
        const esRamiro = p.email === 'ramiro.veloso@tester.com';
        dropdownMenu.innerHTML += `
            <li>
                <a class="dropdown-item d-flex align-items-center justify-content-between py-2 ${p.id === usuarioActual.id ? 'active fw-bold' : ''}" href="#" onclick="cambiarPerfilSemilla(${p.id})">
                    <div>
                        <div class="fw-bold">${esRamiro ? '<i class="fa-solid fa-star text-warning me-1"></i>' : ''}${p.nombre}</div>
                        <div class="extra-small opacity-75">${p.email}</div>
                    </div>
                    <span class="badge ${p.saldoInicial > 0 ? 'bg-success-subtle text-success border border-success-subtle' : 'bg-danger-subtle text-danger border border-danger-subtle'}">
                        $${p.saldoInicial.toLocaleString()}
                    </span>
                </a>
            </li>
        `;
    });
    
    dropdownMenu.innerHTML += '<li><hr class="dropdown-divider"></li>';
    dropdownMenu.innerHTML += `
        <li class="px-2 py-1">
            <button class="btn btn-success btn-sm w-100 fw-bold shadow-sm" onclick="cargarSaldoLibreRapido(100000)">
                <i class="fa-solid fa-coins me-1"></i> Carga Libre +$100.000 a ${usuarioActual.nombre.split(' ')[0]}
            </button>
        </li>
        <li><a class="dropdown-item" href="#" data-bs-toggle="modal" data-bs-target="#modalCargarSaldo"><i class="fa-solid fa-wallet me-2 text-primary-custom"></i>Consola Carga Libre de Saldo...</a></li>
        <li><a class="dropdown-item" href="#" data-bs-toggle="modal" data-bs-target="#modalCrearUsuario"><i class="fa-solid fa-user-plus me-2 text-secondary"></i>Crear Nuevo Usuario</a></li>
    `;
}

async function cambiarPerfilSemilla(usuarioId) {
    const perfil = PERFILES_SEMILLA.find(p => p.id === usuarioId);
    if (!perfil) return;

    usuarioActual = perfil;
    document.getElementById('wallet-usuario-info').textContent = `${perfil.nombre} (${perfil.email})`;

    renderizarSelectorPerfilesSemilla();
    await actualizarBilleteraUI();
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
        await actualizarBilleteraUI();
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
            const coincideTexto = !textoFiltro || 
                sub.titulo.toLowerCase().includes(textoFiltro) || 
                (sub.descripcion && sub.descripcion.toLowerCase().includes(textoFiltro));
            const coincideEstado = !estadoFiltro || sub.estado === estadoFiltro;
            const coincideCategoria = !categoriaFiltro || sub.categoriaId == categoriaFiltro;
            return coincideTexto && coincideEstado && coincideCategoria;
        });

        if (ordenFiltro === 'tiempo') {
            subastasFiltradas.sort((a, b) => new Date(a.fechaFin) - new Date(b.fechaFin));
        } else if (ordenFiltro === 'puja') {
            subastasFiltradas.sort((a, b) => obtenerPujaMaxima(b) - obtenerPujaMaxima(a));
        } else if (ordenFiltro === 'precio') {
            subastasFiltradas.sort((a, b) => a.precioBase - b.precioBase);
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
            const categoria = categoriasCache.find(c => c.id == sub.categoriaId)?.nombre || 'General';
            const pujaActual = obtenerPujaMaxima(sub);
            const badgeClass = getEstadoBadgeClass(sub.estado);
            const imagenUrl = sub.urlImagen || 'assets/images/watch.png';

            const cardCol = document.createElement('div');
            cardCol.className = 'col';
            cardCol.innerHTML = `
                <div class="card auction-card h-100 shadow-sm">
                    <div class="card-img-wrapper">
                        <img src="${imagenUrl}" class="card-img-top" alt="${sub.titulo}" onerror="this.src='assets/images/watch.png'">
                        <span class="badge-categoria"><i class="fa-solid fa-tag me-1"></i>${categoria}</span>
                        <span class="badge badge-status ${badgeClass}">${sub.estado}</span>
                    </div>
                    <div class="card-body d-flex flex-column p-3">
                        <h5 class="card-title text-dark fs-6 fw-bold mb-1 text-truncate">${sub.titulo}</h5>
                        <p class="card-text text-muted small flex-grow-1 text-truncate-2" style="display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; height: 38px;">
                            ${sub.descripcion || 'Sin descripción disponible.'}
                        </p>
                        
                        <div class="bg-light p-2 rounded-3 my-2 border">
                            <div class="d-flex justify-content-between align-items-center mb-1">
                                <span class="text-muted small">Puja Mayor Actual:</span>
                                <span class="fw-bold text-success fs-6">$${pujaActual.toLocaleString()}</span>
                            </div>
                            <div class="d-flex justify-content-between align-items-center extra-small text-muted">
                                <span>Base: $${sub.precioBase.toLocaleString()}</span>
                                <span>Incremento: +$${sub.incrementoMinimo.toLocaleString()}</span>
                            </div>
                        </div>

                        <div class="d-flex justify-content-between align-items-center mb-3">
                            <span class="text-muted small"><i class="fa-regular fa-clock me-1"></i>Cierre:</span>
                            <span class="badge bg-danger-subtle text-danger border border-danger-subtle fw-semibold card-timer" data-fecha-fin="${sub.fechaFin}">
                                Cargando...
                            </span>
                        </div>

                        <button class="btn btn-primary-custom btn-sm w-100 mt-auto" onclick="abrirSalaEnVivo(${sub.id})">
                            <i class="fa-solid fa-gavel me-1"></i> Entrar a Sala en Vivo
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
    if (!subasta.pujas || subasta.pujas.length === 0) return subasta.precioBase;
    return Math.max(...subasta.pujas.map(p => p.monto));
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

function actualizarTemporizadoresCatalogo() {
    const timers = document.querySelectorAll('.card-timer');
    const ahora = new Date().getTime();

    timers.forEach(t => {
        const fechaFin = new Date(t.getAttribute('data-fecha-fin')).getTime();
        const diff = fechaFin - ahora;

        if (diff <= 0) {
            t.innerHTML = 'FINALIZADA';
            t.className = 'badge bg-secondary text-white border fw-semibold card-timer';
        } else {
            const horas = Math.floor(diff / (1000 * 60 * 60));
            const minutos = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
            const segundos = Math.floor((diff % (1000 * 60)) / 1000);
            
            const format = (n) => n.toString().padStart(2, '0');
            t.innerHTML = `${format(horas)}h ${format(minutos)}m ${format(segundos)}s`;
        }
    });
}

/* ==========================================================================
   MÓDULO 2: CREACIÓN DE SUBASTAS
   ========================================================================== */

function inicializarFechasFormulario() {
    const ahora = new Date();
    const despues = new Date(ahora.getTime() + 24 * 60 * 60 * 1000);

    const formatISO = (d) => {
        const tzOffset = d.getTimezoneOffset() * 60000;
        return (new Date(d - tzOffset)).toISOString().slice(0, 16);
    };

    const inputInicio = document.getElementById('crear-fechaInicio');
    const inputFin = document.getElementById('crear-fechaFin');

    if (inputInicio) inputInicio.value = formatISO(ahora);
    if (inputFin) inputFin.value = formatISO(despues);
}

async function guardarSubasta(event) {
    event.preventDefault();

    const titulo = document.getElementById('crear-titulo').value.trim();
    const categoriaId = parseInt(document.getElementById('crear-categoriaId').value);
    const descripcion = document.getElementById('crear-descripcion').value.trim();
    const urlImagen = document.getElementById('crear-urlImagen').value.trim();
    const precioBase = parseFloat(document.getElementById('crear-precioBase').value);
    const incrementoMinimo = parseFloat(document.getElementById('crear-incrementoMinimo').value);
    const fechaInicio = document.getElementById('crear-fechaInicio').value;
    const fechaFin = document.getElementById('crear-fechaFin').value;

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

    if (new Date(fechaInicio) >= new Date(fechaFin)) {
        mostrarToast('La fecha de inicio debe ser anterior a la fecha de finalización.', 'Error de Fechas', 'danger');
        return;
    }

    const btnSubmit = document.getElementById('btn-crear-subasta');
    btnSubmit.disabled = true;
    btnSubmit.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> Publicando...';

    const nuevaSubastaDto = {
        titulo,
        descripcion,
        urlImagen: urlImagen || 'assets/images/watch.png',
        precioBase,
        incrementoMinimo,
        fechaInicio: new Date(fechaInicio).toISOString(),
        fechaFin: new Date(fechaFin).toISOString(),
        estado: 'ACTIVA',
        categoriaId,
        vendedorId: usuarioActual.id
    };

    try {
        const creada = await fetchCrearSubasta(nuevaSubastaDto);
        mostrarToast(`¡La subasta "${creada.titulo || titulo}" fue publicada con éxito!`, 'Publicación Exitosa', 'success');

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

    select.innerHTML = '<option value="">-- Seleccionar Subasta Activa --</option>';
    subastasCache.forEach(s => {
        select.innerHTML += `<option value="${s.id}">${s.titulo} - ($${obtenerPujaMaxima(s).toLocaleString()})</option>`;
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
        document.getElementById('sala-estado-badge').textContent = subastaSeleccionadaSala.estado;
        document.getElementById('sala-vendedor').textContent = `Vendedor ID: #${subastaSeleccionadaSala.vendedorId}`;
        
        document.getElementById('sala-precio-base').textContent = `$${subastaSeleccionadaSala.precioBase.toLocaleString()}`;
        document.getElementById('sala-incremento-min').textContent = `$${subastaSeleccionadaSala.incrementoMinimo.toLocaleString()}`;

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
    if (!timerElem) return;

    const ahora = new Date().getTime();
    const fechaFin = new Date(subastaSeleccionadaSala.fechaFin).getTime();
    const diff = fechaFin - ahora;

    if (diff <= 0) {
        timerElem.textContent = "00:00:00 - FINALIZADA";
        if (timerBox) timerBox.className = 'timer-box bg-secondary border-secondary';
        if (antiSnipingBadge) antiSnipingBadge.className = 'd-none';
        document.getElementById('btn-realizar-puja').disabled = true;
    } else {
        const hrs = Math.floor(diff / (1000 * 60 * 60));
        const mins = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
        const secs = Math.floor((diff % (1000 * 60)) / 1000);
        const format = (n) => n.toString().padStart(2, '0');
        timerElem.textContent = `${format(hrs)}:${format(mins)}:${format(secs)}`;
        document.getElementById('btn-realizar-puja').disabled = false;

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
    document.getElementById('sala-puja-actual').textContent = `$${pujaActual.toLocaleString()}`;

    const bannerLiderazgo = document.getElementById('sala-banner-liderazgo');
    const pujas = subastaSeleccionadaSala.pujas || [];

    if (pujas.length > 0) {
        const ultimaPuja = pujas[pujas.length - 1];
        const esMiPuja = ultimaPuja.usuarioId === usuarioActual.id;

        if (esMiPuja) {
            bannerLiderazgo.className = 'status-banner-leading p-3 mb-3 d-flex align-items-center';
            bannerLiderazgo.innerHTML = `
                <i class="fa-solid fa-trophy fs-3 me-3 text-warning"></i>
                <div>
                    <h6 class="fw-bold mb-0">¡ESTÁS LIDERANDO LA PUJA! (${usuarioActual.nombre})</h6>
                    <small>Tu oferta de $${ultimaPuja.monto.toLocaleString()} es la mayor actual y se encuentra retenida en garantía Escrow.</small>
                </div>
            `;
        } else {
            const usuarioParticipo = pujas.some(p => p.usuarioId === usuarioActual.id);
            if (usuarioParticipo) {
                bannerLiderazgo.className = 'status-banner-outbid p-3 mb-3 d-flex align-items-center';
                bannerLiderazgo.innerHTML = `
                    <i class="fa-solid fa-triangle-exclamation fs-3 me-3 text-danger"></i>
                    <div>
                        <h6 class="fw-bold mb-0">¡HAS SIDO SUPERADO! (OUTBID)</h6>
                        <small>Otro postor ha realizado una oferta de $${ultimaPuja.monto.toLocaleString()}. Incrementa tu oferta para recuperar la delantera.</small>
                    </div>
                `;
            } else {
                bannerLiderazgo.className = 'alert alert-secondary p-3 mb-3 d-flex align-items-center';
                bannerLiderazgo.innerHTML = `
                    <i class="fa-solid fa-info-circle fs-3 me-3"></i>
                    <div>
                        <h6 class="fw-bold mb-0">Puja Líder Actual: $${ultimaPuja.monto.toLocaleString()}</h6>
                        <small>Realizada por un postor anónimo. Inicia tu puja para ingresar al remate.</small>
                    </div>
                `;
            }
        }
    } else {
        bannerLiderazgo.className = 'alert alert-light border p-3 mb-3 d-flex align-items-center';
        bannerLiderazgo.innerHTML = `
            <i class="fa-solid fa-gavel fs-3 me-3 text-secondary"></i>
            <div>
                <h6 class="fw-bold mb-0">Sin ofertas registradas</h6>
                <small>Sé el primer postor realizando una oferta desde el precio base de $${subastaSeleccionadaSala.precioBase.toLocaleString()}.</small>
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
    const pujasOrdenadas = [...pujas].reverse();

    pujasOrdenadas.forEach((p, index) => {
        const esLider = index === 0;
        const esPropia = p.usuarioId === usuarioActual.id;
        const fechaFormat = new Date(p.fechaCreacion).toLocaleTimeString();
        const anonHandle = p.postorAnonimo || `Postor #${(p.usuarioId * 33 + 100).toString(16).toUpperCase()}`;

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
                    <span class="fw-bold fs-6 ${esLider ? 'text-success' : 'text-dark'}">$${p.monto.toLocaleString()}</span>
                    <div class="extra-small text-muted">Escrow Ok</div>
                </div>
            </div>
        `;
        listaHistorial.appendChild(item);
    });
}

async function enviarPuja(event) {
    event.preventDefault();

    if (!subastaSeleccionadaSala) return;

    const montoInput = parseFloat(document.getElementById('input-monto-puja').value);
    const pujaActual = obtenerPujaMaxima(subastaSeleccionadaSala);
    const incrementoMin = subastaSeleccionadaSala.incrementoMinimo;

    if (isNaN(montoInput) || montoInput <= pujaActual) {
        mostrarToast(`La puja debe superar la oferta actual de $${pujaActual.toLocaleString()}.`, 'Oferta Inválida', 'warning');
        return;
    }

    if ((montoInput - pujaActual) < incrementoMin && subastaSeleccionadaSala.pujas.length > 0) {
        mostrarToast(`El incremento mínimo requerido es de $${incrementoMin.toLocaleString()}.`, 'Incremento Insuficiente', 'warning');
        return;
    }

    try {
        await fetchRetenerSaldo(usuarioActual.id, montoInput, subastaSeleccionadaSala.id);

        const pujasPrevias = subastaSeleccionadaSala.pujas || [];
        if (pujasPrevias.length > 0) {
            const ultimaPujaPrevia = pujasPrevias[pujasPrevias.length - 1];
            if (ultimaPujaPrevia.usuarioId !== usuarioActual.id) {
                await fetchLiberarSaldo(ultimaPujaPrevia.usuarioId, ultimaPujaPrevia.monto, subastaSeleccionadaSala.id);
            }
        }

        const ahora = new Date().getTime();
        const fechaFinMs = new Date(subastaSeleccionadaSala.fechaFin).getTime();
        let antiSnipingActivado = false;

        if ((fechaFinMs - ahora) <= 60000) {
            subastaSeleccionadaSala.fechaFin = new Date(fechaFinMs + 60000).toISOString();
            antiSnipingActivado = true;
        }

        const anonHandle = `Postor #${(usuarioActual.id * 33 + 100).toString(16).toUpperCase()}`;
        const nuevaPuja = {
            id: Date.now(),
            subastaId: subastaSeleccionadaSala.id,
            usuarioId: usuarioActual.id,
            monto: montoInput,
            fechaCreacion: new Date().toISOString(),
            postorAnonimo: anonHandle
        };

        subastaSeleccionadaSala.pujas.push(nuevaPuja);
        subastaSeleccionadaSala.version += 1;

        const indexSub = subastasCache.findIndex(s => s.id === subastaSeleccionadaSala.id);
        if (indexSub !== -1) {
            subastasCache[indexSub].pujas.push(nuevaPuja);
            subastasCache[indexSub].fechaFin = subastaSeleccionadaSala.fechaFin;
        }

        if (antiSnipingActivado) {
            mostrarToast(`⚡ <strong>¡REGLA ANTI-SNIPING ACTIVADA!</strong> Se han añadido +60 segundos al temporizador por oferta de último minuto.`, 'Anti-Sniping Activado', 'warning');
        } else {
            mostrarToast(`¡Oferta enviada por $${montoInput.toLocaleString()}! Retenido en Escrow.`, 'Oferta Exitosa', 'success');
        }

        actualizarMonitorPujasSala();
        configurarBotonesPujaRapida(incrementoMin);
        await actualizarBilleteraUI();
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

        document.getElementById('wallet-usuario-info').textContent = `${usuarioActual.nombre} (${usuarioActual.email})`;
        
        document.getElementById('wallet-saldo-total').textContent = `$${billetera.saldoTotal.toLocaleString()}`;
        document.getElementById('wallet-saldo-retenido').textContent = `$${billetera.saldoRetenido.toLocaleString()}`;
        document.getElementById('wallet-saldo-disponible').textContent = `$${billetera.saldoDisponible.toLocaleString()}`;

        const tbody = document.getElementById('wallet-tabla-movimientos');
        if (!tbody) return;

        if (!movimientos || movimientos.length === 0) {
            tbody.innerHTML = `<tr><td colspan="5" class="text-center text-muted py-4">No hay movimientos contables registrados para este usuario.</td></tr>`;
            return;
        }

        tbody.innerHTML = '';
        movimientos.forEach(m => {
            const fechaStr = new Date(m.fecha).toLocaleString();
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
        await actualizarBilleteraUI();

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
        cambiarPerfilSemilla(nuevoPerfil.id);

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

    let misPujasCount = 0;
    let misPublicacionesCount = 0;
    let misGanadasCount = 0;

    contenedorPujas.innerHTML = '';
    contenedorPublicaciones.innerHTML = '';

    subastasCache.forEach(sub => {
        const misPujasEnSub = (sub.pujas || []).filter(p => p.usuarioId === usuarioActual.id);
        if (misPujasEnSub.length > 0) {
            misPujasCount += misPujasEnSub.length;
            const miMayorPuja = Math.max(...misPujasEnSub.map(p => p.monto));
            const pujaAbsolutaMax = obtenerPujaMaxima(sub);
            const esLider = miMayorPuja === pujaAbsolutaMax;

            if (sub.estado === 'FINALIZADA' && esLider) misGanadasCount++;

            const col = document.createElement('div');
            col.className = 'col-md-6 mb-3';
            col.innerHTML = `
                <div class="card h-100 border p-3 shadow-sm">
                    <div class="d-flex justify-content-between align-items-start mb-2">
                        <h6 class="fw-bold text-dark mb-0">${sub.titulo}</h6>
                        ${esLider ? 
                            '<span class="badge bg-success"><i class="fa-solid fa-trophy me-1"></i> Vas ganando</span>' : 
                            '<span class="badge bg-danger"><i class="fa-solid fa-triangle-exclamation me-1"></i> Te superaron</span>'}
                    </div>
                    <p class="text-muted extra-small mb-2">Mi Oferta: <strong>$${miMayorPuja.toLocaleString()}</strong> | Puja Mayor: <strong>$${pujaAbsolutaMax.toLocaleString()}</strong></p>
                    <div class="d-flex justify-content-between align-items-center border-top pt-2">
                        <span class="badge bg-light text-dark">Estado: ${sub.estado}</span>
                        <button class="btn btn-outline-primary-custom btn-sm" onclick="abrirSalaEnVivo(${sub.id})">
                            <i class="fa-solid fa-arrow-right me-1"></i> Ver Sala
                        </button>
                    </div>
                </div>
            `;
            contenedorPujas.appendChild(col);
        }

        if (sub.vendedorId === usuarioActual.id) {
            misPublicacionesCount++;
            const colPub = document.createElement('div');
            colPub.className = 'col-md-6 mb-3';
            colPub.innerHTML = `
                <div class="card h-100 border p-3 shadow-sm">
                    <div class="d-flex justify-content-between align-items-start mb-2">
                        <h6 class="fw-bold text-dark mb-0">${sub.titulo}</h6>
                        <span class="badge bg-primary-custom">${sub.estado}</span>
                    </div>
                    <p class="text-muted extra-small mb-2">Precio Base: <strong>$${sub.precioBase.toLocaleString()}</strong> | Total Ofertas: <strong>${(sub.pujas || []).length}</strong></p>
                    <div class="d-flex justify-content-between align-items-center border-top pt-2">
                        <span class="fw-bold text-success">Puja Máxima: $${obtenerPujaMaxima(sub).toLocaleString()}</span>
                        <button class="btn btn-outline-primary-custom btn-sm" onclick="abrirSalaEnVivo(${sub.id})">Administrar</button>
                    </div>
                </div>
            `;
            contenedorPublicaciones.appendChild(colPub);
        }
    });

    if (contenedorPujas.children.length === 0) {
        contenedorPujas.innerHTML = `<div class="col-12 text-muted text-center py-3">Aún no has participado en ninguna subasta con este perfil.</div>`;
    }

    if (contenedorPublicaciones.children.length === 0) {
        contenedorPublicaciones.innerHTML = `<div class="col-12 text-muted text-center py-3">No tienes publicaciones activas como vendedor.</div>`;
    }

    document.getElementById('stat-total-pujas').textContent = misPujasCount;
    document.getElementById('stat-subastas-creadas').textContent = misPublicacionesCount;
    document.getElementById('stat-subastas-ganadas').textContent = misGanadasCount;
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
