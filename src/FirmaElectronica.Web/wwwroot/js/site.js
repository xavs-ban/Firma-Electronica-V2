import { ApiFirma, ErrorApi, esperarPreparacion, enlaceFirma, consultarVistaPdf } from './api.js';
const raiz = document.querySelector('#firma-app');
if (raiz) iniciar();
function iniciar() {
    const accesoTemporal = raiz.dataset.accesoTemporal === 'true';
    const $ = selector => document.querySelector(selector);
    const esc = valor => String(valor ?? '').replace(/[&<>"']/g, caracter => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[caracter]));
    const icono = nombre => `<svg aria-hidden="true"><use href="#i-${nombre}" /></svg>`;
    const nuevoId = () => {
        if (crypto.randomUUID) return crypto.randomUUID();
        const bytes = crypto.getRandomValues(new Uint8Array(16));
        bytes[6] = (bytes[6] & 15) | 64; bytes[8] = (bytes[8] & 63) | 128;
        const hex = Array.from(bytes, n => n.toString(16).padStart(2, '0')).join('');
        return `${hex.slice(0,8)}-${hex.slice(8,12)}-${hex.slice(12,16)}-${hex.slice(16,20)}-${hex.slice(20)}`;
    };
    let referenciaEntregasPendiente = null;
    function prepararDesdeEntregas() {
        if (!referenciaEntregasPendiente) return;
        $('#referencia').value = referenciaEntregasPendiente;
        referenciaEntregasPendiente = null;
        cambiarVista('generar', false);
        $('#form-referencia').requestSubmit();
    }
    const fechaHoy = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Mexico_City', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
    const fechaCampo = valor => /^\d{4}-\d{2}-\d{2}/.test(String(valor || '')) ? String(valor).slice(0, 10) : '';
    const fechaCorta = valor => { const d = new Date(valor); return Number.isNaN(d.getTime()) ? 'Por confirmar' : d.toLocaleDateString('es-MX', { day: '2-digit', month: 'short', year: 'numeric' }); };
    const dinero = valor => valor != null && Number.isFinite(Number(String(valor).replaceAll(',', ''))) ? new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN', maximumFractionDigits: 2 }).format(Number(String(valor).replaceAll(',', ''))) : 'Por confirmar';
    const tipoNombre = { Contado: 'Contado', Financiamiento: 'Financiamiento', PersonaMoral: 'Persona moral', SeminuevosContado: 'Seminuevos', Hyundai: 'Hyundai' };
    const roles = ['CLIENTE', 'APV', 'GERENTE DE VENTAS', 'REPRESENTANTE LEGAL'];
    const opcionesVenta = [...$('#tipo-venta').options].map(o => ({ valor: o.value, nombre: o.textContent }));
    const aseguradoras = ['ALLIANZ MEXICO SA COMPAÑÍA DE SEGUROS', 'AXA SEGUROS', 'CHUBB SEGUROS MEXICO', 'CUENTA CLIENTE', 'GRUPO NACIONAL PROVINCIAL', 'NISSAN MEXICANA', 'QUALITAS COMPAÑÍA DE SEGUROS', 'SEGUROS BANORTE SA DE CV GRUPO FINANCIERO BANORTE', 'ZURICH ASEGURADORA MEXICANA'];
    let api = new ApiFirma(), usuario = null, agencias = [], referencias = [], actividades = [], documentos = [], vista = 'generar', pagina = 1, total = 0;
    let consultandoDocumentos = 0, generando = false, consultandoTrabajos = false, versionSesion = 0, pdfUrl = null, versionPdf = 0, documentoFirmas = null, versionFirmas = 0;
    let paginaDocumentosCargada = false;
    const agenciaActual = () => $('#agencia-global').value;
    const agenciaNombre = clave => agencias.find(a => a.clave === clave)?.nombre || clave;
    const claveGuardado = () => `firmaV2:trabajos:${usuario?.usuario || ''}`;
    function guardarActividad() {
        if (api.ejemplo || !usuario) return;
        try { sessionStorage.setItem(claveGuardado(), JSON.stringify(actividades.map(({ id, referencia, agencia, plantilla }) => ({ id, referencia, agencia, plantilla })).slice(-100))); } catch { }
    }
    document.addEventListener('input', e => {
        if (e.target.matches('[data-numerico]')) e.target.value = e.target.value.replace(/[^0-9]/g, '');
    }, true);
    const tema = $('#cambiar-tema');
    function aplicarTema(oscuro) {
        document.documentElement.dataset.tema = oscuro ? 'oscuro' : 'claro';
        tema.textContent = oscuro ? '☀ Modo claro' : '☾ Modo oscuro';
        tema.setAttribute('aria-label', oscuro ? 'Activar modo claro' : 'Activar modo oscuro');
        tema.setAttribute('aria-pressed', String(oscuro));
    }
    aplicarTema(false);
    tema.onclick = () => aplicarTema(document.documentElement.dataset.tema !== 'oscuro');
    // Una sola ventana a la vez, incluso cuando termina un trabajo en segundo plano.
    let colaVentanas = Promise.resolve();
    function ventana(opciones) {
        const sesion = versionSesion;
        const tarea = colaVentanas.then(async () => {
            if (sesion !== versionSesion) return { isConfirmed: false };
            // Los dialogs nativos están en la capa superior del navegador.
            const contenedor = [...document.querySelectorAll('dialog[open]')].at(-1) || document.body;
            return Swal.fire({
                target: contenedor, heightAuto: false, scrollbarPadding: false,
                confirmButtonText: 'Entendido', cancelButtonText: 'Cancelar',
                showClass: { popup: 'firma-modal-entrada' }, hideClass: { popup: 'firma-modal-salida' },
                confirmButtonColor: '#2448a5', cancelButtonColor: '#68758a',
                customClass: { popup: 'firma-swal', confirmButton: 'firma-swal-boton', cancelButton: 'firma-swal-boton' },
                ...opciones
            });
        });
        colaVentanas = tarea.catch(() => {});
        return tarea;
    }
    function notificar(mensaje, tipo = '') {
        if (avisandoExpiracion || (!usuario && !$('#acceso').hidden)) return Promise.resolve();
        return ventana({ title: tipo === 'error' ? 'Vamos a revisarlo' : tipo === 'exito' ? '¡Listo!' : 'Antes de continuar',
            text: mensaje, icon: tipo === 'error' ? 'warning' : tipo === 'exito' ? 'success' : 'info' });
    }
    async function ocupado(boton, texto, operacion) {
        if (boton.disabled) return;
        const antes = boton.innerHTML; boton.disabled = true; boton.innerHTML = `<span class="spinner" aria-hidden="true"></span>${esc(texto)}`;
        try { return await operacion(); } finally { boton.disabled = false; boton.innerHTML = antes; }
    }
    function cerrarModal(modal, valor = '') {
        if (!modal?.open || modal.classList.contains('cerrando')) return;
        if (matchMedia('(prefers-reduced-motion: reduce)').matches) { modal.close(valor); return; }
        modal.classList.add('cerrando');
        return new Promise(resolve => {
        let temporizador;
        const terminar = () => {
            clearTimeout(temporizador);
            modal.removeEventListener('animationend', alTerminar);
            modal.close(valor);
            modal.classList.remove('cerrando');
            resolve();
        };
        const alTerminar = evento => {
            if (evento.target === modal && evento.animationName === 'modal-salida') terminar();
        };
        modal.addEventListener('animationend', alTerminar);
        temporizador = setTimeout(terminar, 350);
        });
    }
    document.querySelectorAll('dialog').forEach(modal => {
        modal.addEventListener('cancel', evento => { evento.preventDefault(); cerrarModal(modal); });
    });
    function mostrarAcceso(mensaje = '') {
        versionSesion++; Swal.close(); usuario = null; referencias = []; actividades = []; documentos = []; paginaDocumentosCargada = false;
        document.querySelectorAll('dialog[open]').forEach(d => d.close());
        $('#espacio').hidden = true; $('#inicio-carga').hidden = true; $('#acceso').hidden = false;
        $('#error-acceso').textContent = mensaje; $('#error-acceso').hidden = !mensaje;
    }
    async function entrar() {
        const actual = ++versionSesion;
        const datos = await api.solicitar('/api/sesion');
        const lista = await api.solicitar('/api/agencias');
        if (actual !== versionSesion) return;
        usuario = datos; agencias = lista; renovarAvisoSesion();
        $('#usuario-nombre').textContent = datos.nombre || datos.usuario;
        $('#usuario-rol').textContent = datos.rol?.trim() || 'Sin rol asignado';
        $('#avatar').textContent = (datos.nombre || datos.usuario || 'U').split(/\s+/).slice(0, 2).map(p => p[0]).join('').toUpperCase();
        $('#agencia-global').innerHTML = lista.map(a => `<option value="${esc(a.clave)}">${esc(a.nombre)} · ${esc(a.clave)}</option>`).join('');
        $('#agencia-global').disabled = lista.length === 0;
        $('#marca-ejemplo').hidden = $('#banner-ejemplo').hidden = $('#ayuda-ejemplo').hidden = !api.ejemplo;
        $('#acceso').hidden = $('#inicio-carga').hidden = true; $('#espacio').hidden = false;
        $('#contrasena').value = '';
        actividades = [];
        if (!api.ejemplo) {
            try {
                const guardadas = JSON.parse(sessionStorage.getItem(claveGuardado()) || '[]');
                if (Array.isArray(guardadas)) actividades = guardadas.filter(a => typeof a.id === 'string' && /^[0-9a-f-]{36}$/i.test(a.id) && lista.some(g => g.clave === a.agencia)).slice(-100).map(a => ({ ...a, estado: 'EnCola', errores: 0 }));
            } catch { }
        }
        pintarReferencias(); pintarActividad(); cambiarVista('generar', false);
        await cargarPlantillas();
        if (!lista.length) notificar('Tu cuenta no tiene agencias asignadas. Solicita que revisen tu acceso.', 'error');
        consultarTrabajos();
    }
    if (accesoTemporal) {
        const form = $('#form-acceso');
        form.querySelectorAll('label').forEach(label => label.hidden = true);
        form.querySelectorAll('input').forEach(input => { input.required = false; input.disabled = true; });
        form.querySelector('[type=submit]').textContent = 'Entrar a Firma Digital';
        $('#titulo-acceso').textContent = 'Acceso temporal';
        $('#titulo-acceso').nextElementSibling.textContent = 'Ingresa para gestionar los documentos con la cuenta compartida.';
    }
    $('#form-acceso').addEventListener('submit', async evento => {
        evento.preventDefault(); const form = evento.currentTarget;
        $('#error-acceso').hidden = true;
        await ocupado(form.querySelector('[type=submit]'), 'Ingresando…', async () => {
            $('#acceso').classList.add('acceso-ingresando'); form.setAttribute('aria-busy', 'true');
            $('#error-acceso').textContent = 'Estamos preparando tu espacio…'; $('#error-acceso').hidden = false;
            try {
                api = new ApiFirma(); await api.csrf();
                await api.solicitar('/api/sesion', { metodo: 'POST', datos: accesoTemporal ? { usuario: "", contrasena: "" } : { usuario: form.usuario.value.trim(), contrasena: form.contrasena.value } });
                await api.csrf(); await entrar();
                Swal.close(); prepararDesdeEntregas();
            } catch (error) { $('#error-acceso').textContent = error.message; $('#error-acceso').hidden = false; }
            finally { $('#acceso').classList.remove('acceso-ingresando'); form.removeAttribute('aria-busy'); }
        });
    });
    $('#ver-contrasena').onclick = () => { const input = $('#contrasena'); input.type = input.type === 'password' ? 'text' : 'password'; $('#ver-contrasena').setAttribute('aria-label', input.type === 'password' ? 'Mostrar contraseña' : 'Ocultar contraseña'); };
    $('#entrar-ejemplo')?.addEventListener('click', async evento => {
        if (raiz.dataset.demoEnabled !== 'true') return;
        await ocupado(evento.currentTarget, 'Preparando ejemplo…', async () => {
            try { const { ApiEjemplo } = await import('./ejemplo.js'); api = new ApiEjemplo(); await entrar(); } catch (error) { mostrarAcceso(error.message); }
        });
    });
    async function salir() {
        try {
            await api.solicitar('/api/sesion', { metodo: 'DELETE' });
            if (!api.ejemplo) sessionStorage.removeItem(claveGuardado());
            api = new ApiFirma(); mostrarAcceso('¡Hasta pronto! Nos vemos en tu próxima sesión.');
        } catch (error) { notificar(error.message, 'error'); }
    }
    $('#cerrar-sesion').onclick = salir; $('#salir-ejemplo').onclick = salir;
    let vencimientoSesion;
    let avisandoExpiracion = false;
    async function avisarSesionExpirada() {
        if (!usuario || api.ejemplo || avisandoExpiracion) return;
        avisandoExpiracion = true;
        clearTimeout(vencimientoSesion);
        versionSesion++;
        usuario = null;
        Swal.close();
        const mensaje = 'Tu sesión expiró. Inicia sesión nuevamente para continuar.';
        const contenedor = [...document.querySelectorAll('dialog[open]')].at(-1) || document.body;
        await Swal.fire({ target: contenedor, title: 'Tu sesión expiró', text: mensaje,
            icon: 'info', confirmButtonText: 'Aceptar', allowOutsideClick: false, allowEscapeKey: false,
            heightAuto: false, scrollbarPadding: false,
            customClass: { popup: 'firma-swal', confirmButton: 'firma-swal-boton' },
            showClass: { popup: 'firma-modal-entrada' }, hideClass: { popup: 'firma-modal-salida' }
        });
        mostrarAcceso(mensaje);
        $('#form-acceso input[name=contrasena]').value = '';
        $('#form-acceso input[name=usuario]').focus();
        avisandoExpiracion = false;
    }
    function renovarAvisoSesion() {
        clearTimeout(vencimientoSesion);
        if (usuario && !api.ejemplo) vencimientoSesion = setTimeout(avisarSesionExpirada, 30 * 60 * 1000);
    }
    window.addEventListener('sesion-expirada', avisarSesionExpirada);
    window.addEventListener('sesion-activa', renovarAvisoSesion);
    function cambiarVista(nombre, enfocar = true) {
        vista = nombre;
        $('#agencia-global').closest('label').hidden = nombre !== 'documentos';
        const titulos = { generar: ['UN EXPEDIENTE, PASO A PASO', 'Nuevo expediente', 'Consulta una referencia y prepara su documentación.'], documentos: ['TODO EN SU LUGAR', 'Mis documentos', 'Consulta tus expedientes y continúa con la firma.'], actividad: ['CADA PASO A LA VISTA', 'Actividad', 'Sigue el avance de los documentos que estás preparando.'] };
        const valores = titulos[nombre]; if (!valores) return;
        $('#sobre-titulo').textContent = valores[0]; $('#titulo-vista').textContent = $('#ruta-actual').textContent = valores[1]; $('#descripcion-vista').textContent = valores[2];
        for (const id of Object.keys(titulos)) $(`#vista-${id}`).hidden = id !== nombre;
        document.querySelectorAll('.nav-item').forEach(b => { b.classList.toggle('activo', b.dataset.vista === nombre); if (b.dataset.vista === nombre) b.setAttribute('aria-current', 'page'); else b.removeAttribute('aria-current'); });
        if (nombre === 'documentos' && !paginaDocumentosCargada) prepararConsultaDocumentos();
        if (enfocar) $('#principal').focus({ preventScroll: true });
    }
    document.querySelectorAll('[data-vista]').forEach(b => b.onclick = () => cambiarVista(b.dataset.vista));
    async function cargarPlantillas() {
        const agencia = agenciaActual(), actual = versionSesion;
        $('#filtro-plantilla').innerHTML = '<option value="">Selecciona el tipo de documento</option>';
        if (!agencia) return;
        try {
            const lista = await api.solicitar(`/api/agencias/${encodeURIComponent(agencia)}/plantillas`);
            if (agencia !== agenciaActual() || actual !== versionSesion) return;
            $('#filtro-plantilla').innerHTML += lista.map(p => `<option value="${esc(p.plantillaId)}">${esc(tipoNombre[p.tipo] || p.tipo)}</option>`).join('');
        } catch (error) { notificar(error.message, 'error'); }
    }
    $('#agencia-global').onchange = async () => { pagina = 1; paginaDocumentosCargada = false; consultandoDocumentos++; documentos = []; await cargarPlantillas(); if (vista === 'documentos') prepararConsultaDocumentos(); };
    $('#usar-ejemplo').onclick = () => { $('#referencia').value = '900001'; $('#referencia').focus(); };
    $('#form-referencia').addEventListener('submit', async evento => {
        evento.preventDefault(); const ref = $('#referencia').value.trim(), tipoVenta = $('#tipo-venta').value, folio = $('#folio-consulta').value.trim(), actual = versionSesion;
        if (generando || referencias.some(r => r.estado === 'lista' || r.estado === 'preparando')) return notificar('Trabaja con un expediente a la vez. Genera o quita el expediente actual antes de consultar otro.');
        if (referencias.some(r => r.referencia === ref && r.estado !== 'enviado')) return notificar('Esta referencia ya está en la preparación. Revísala abajo.');
        await ocupado(evento.currentTarget.querySelector('[type=submit]'), 'Consultando…', async () => {
            try {
                const datos = await api.solicitar(`/api/referencias/${encodeURIComponent(ref)}`);
                if (actual !== versionSesion) return;
                const agencia = String(datos.dealer || '').trim().toUpperCase();
                referencias = [];
                referencias.push({ id: nuevoId(), referencia: ref, agencia, datos, tipoVenta, estado: 'lista', captura: { folioControl: datos.tipoExpediente === 'SEMINUEVO' ? '' : (folio || String(datos.Folio_control || datos.folio_control || '')), fechaPlanta: fechaCampo(datos.fecha_reporte_planta), aplicaSeguro: false, conectividad: String(datos.conectividad || (datos.poliza ? 'Conectividad' : 'No aplica')), aseguradora: aseguradoras[{'3917': 0, '22883': 2, '2122': 4, '6': 5, '2124': 6, '3115': 7, '2123': 8}[String(datos.cod_aseguradora)]] || '', poliza: String(datos.poliza || ''), inicio: fechaCampo(datos.fecha_inicio_seguro), fin: fechaCampo(datos.fecha_fin_seguro), editado: false } });
                pintarReferencias(); $('#folio-consulta').value = ''; $('#referencia').value = ''; $('#referencia').focus();
                notificar(`Referencia de ${agenciaNombre(agencia)} · ${agencia} consultada. Revisa los datos antes de generar.`, 'exito');
            } catch (error) { notificar(error.estado === 404 ? 'No encontramos esta referencia en nuestros registros. Revisa que esté bien capturada; si es reciente, puede que todavía no esté registrada. Puedes corregirla o consultar más tarde.' : error.message, error.estado === 404 ? '' : 'error'); }
        });
    });
    function pintarReferencias() {
        $('#numero-referencias').textContent = referencias.length;
        $('#referencias-vacias').hidden = referencias.length > 0;
        $('#generar-documentos').disabled = generando || !referencias.some(r => r.estado === 'lista');
        $('#referencias-lista').innerHTML = referencias.map(r => {
            const d = r.datos, c = r.captura, seminuevo = d.tipoExpediente === 'SEMINUEVO', bloqueada = r.estado !== 'lista';
            return `<article class="panel ref-card" data-ref="${r.id}"><div class="ref-cabecera"><span class="icono-suave">${icono('documento')}</span><div><h3>Referencia ${esc(r.referencia)}</h3><p class="muted">${esc(agenciaNombre(r.agencia))} · ${esc(d.tipoExpediente === 'PERSONA_MORAL' ? 'Persona moral' : d.tipoExpediente === 'SEMINUEVO' ? 'Seminuevo' : 'Persona física')}</p></div><button type="button" class="boton-icono" data-accion="quitar-ref" data-id="${r.id}" aria-label="Quitar referencia ${esc(r.referencia)}" ${r.estado === 'preparando' ? 'disabled' : ''}>${icono('cerrar')}</button></div><div class="ref-datos"><div class="dato"><small>Cliente</small><strong>${esc(d.nombre_completo || d.NOMBRE || 'Sin nombre')}</strong><span>${esc(d.email || 'Correo por confirmar')}</span></div><div class="dato"><small>Vehículo</small><strong>${esc([d.marca, d.modelo, d.ANIO_VEHI].filter(Boolean).join(' ') || 'Por confirmar')}</strong><span>${esc(d.vin || 'VIN por confirmar')}</span></div><div class="dato"><small>Total factura</small><strong>${esc(dinero(d.total_factura))}</strong><span>MXN</span></div></div><form class="form-captura" data-form-ref="${r.id}"><fieldset ${seminuevo ? 'hidden' : ''} ${(bloqueada || seminuevo) ? 'disabled' : ''} style="border:0;padding:0;margin:0;min-width:0"><div class="ref-campos"><label>Tipo de venta<select data-campo="tipoVenta" required>${opcionesVenta.map(o => `<option value="${o.valor}" ${r.tipoVenta === o.valor ? 'selected' : ''}>${esc(o.nombre)}</option>`).join('')}</select></label><label>Folio de control<input data-campo="folioControl" inputmode="numeric" pattern="[0-9]+" data-numerico value="${esc(c.folioControl)}" placeholder="Captura el folio" required maxlength="100" /></label><label>Fecha de planta<input type="date" data-campo="fechaPlanta" value="${esc(c.fechaPlanta)}" required /></label></div><button type="button" class="boton secundario editar-seguro" data-id="${r.id}">Configurar seguro y conectividad</button><div class="seguro-editor" hidden><div class="seguro-cabecera"><label>Conectividad<select data-campo="conectividad">${[...new Set(['No aplica', 'No se adquirió servicio', 'Nissan Connect Finder', 'Nissan Connect Services', c.conectividad].filter(Boolean))].map(v => `<option value="${esc(v)}" ${v === c.conectividad ? 'selected' : ''}>${esc(v)}</option>`).join('')}</select></label></div><div class="seguro-campos"><label>Aseguradora<select data-campo="aseguradora"><option value="">Sin aseguradora capturada</option>${aseguradoras.map(a => `<option value="${esc(a)}" ${c.aseguradora === a ? 'selected' : ''}>${esc(a)}</option>`).join('')}</select></label><label>Póliza<input data-campo="poliza" value="${esc(c.poliza)}" maxlength="100" placeholder="Número de póliza" /></label><label>Inicio de vigencia<input type="date" data-campo="inicio" value="${esc(c.inicio)}" /></label><label>Fin de vigencia<input type="date" data-campo="fin" value="${esc(c.fin)}" ${c.inicio ? `min="${esc(c.inicio)}"` : ''} /></label></div><p class="ayuda">Si no modificas el seguro, se aplicarán los datos y reglas del expediente.</p></div></fieldset>${seminuevo ? '<p class="ayuda">Seminuevo: listo para generar con la referencia. No requiere folio de control ni captura adicional.</p>' : ''}</form>${r.mensaje ? `<p class="estado-ref ${r.estado === 'lista' ? 'error-texto' : ''}">${esc(r.mensaje)}</p>` : ''}${r.estado === 'enviado' ? '<button class="enlace ir-actividad" type="button">Ver avance en Actividad →</button>' : ''}</article>`;
        }).join('');
        $('#referencias-lista').querySelectorAll('form').forEach(f => f.onsubmit = e => e.preventDefault());
        $('#referencias-lista').querySelectorAll('.ir-actividad').forEach(b => b.onclick = () => cambiarVista('actividad'));
    }
    document.addEventListener('input', evento => {
        const campo = evento.target.dataset.campo, tarjeta = evento.target.closest('[data-ref]'); if (!campo || !tarjeta) return;
        const ref = referencias.find(r => r.id === tarjeta.dataset.ref); if (!ref || ref.estado !== 'lista') return;
        if (campo === 'tipoVenta') ref.tipoVenta = evento.target.value;
        else ref.captura[campo] = evento.target.type === 'checkbox' ? evento.target.checked : evento.target.value;
        if (['aplicaSeguro', 'conectividad', 'aseguradora', 'poliza', 'inicio', 'fin'].includes(campo)) ref.captura.editado = true;
        if (['aseguradora', 'poliza', 'inicio', 'fin'].includes(campo)) ref.captura.aplicaSeguro = Boolean(ref.captura.aseguradora || ref.captura.poliza || ref.captura.inicio || ref.captura.fin);
        if (campo === 'inicio') tarjeta.querySelector('[data-campo=fin]').min = ref.captura.inicio;
    });
    let seguroOriginal = null, seguroReferencia = null;
    document.addEventListener('click', e => {
        const boton = e.target.closest('.editar-seguro'); if (!boton) return;
        const ref = referencias.find(r => r.id === boton.dataset.id); if (!ref || ref.estado !== 'lista') return;
        seguroReferencia = ref; seguroOriginal = { ...ref.captura };
        const editor = boton.parentElement.querySelector('.seguro-editor');
        editor.hidden = false; editor.dataset.ref = ref.id;
        $('#seguro-contenido').replaceChildren(editor);
        $('#modal-seguro').returnValue = ''; $('#modal-seguro').showModal();
    });
    $('#guardar-seguro').onclick = async () => {
        const campos = [...$('#seguro-contenido').querySelectorAll('input,select')];
        if (campos.some(c => !c.reportValidity())) { notificar('Revisa los campos señalados. No se guardaron los cambios.', 'error'); return; }
        const referenciaAlGuardar = seguroReferencia;
        if (!await pedirConfirmacion('Guardar seguro y conectividad', '¿Estás seguro de guardar los datos capturados para este expediente?', 'Guardar')) return;
        if (!referenciaAlGuardar || seguroReferencia !== referenciaAlGuardar || !$('#modal-seguro').open) return;
        await cerrarModal($('#modal-seguro'), 'guardar');
        notificar('Los datos de seguro y conectividad quedaron guardados para este expediente.', 'exito');
    };
    $('#modal-seguro').addEventListener('close', () => {
        if (seguroReferencia && $('#modal-seguro').returnValue !== 'guardar') seguroReferencia.captura = seguroOriginal;
        seguroReferencia = null; seguroOriginal = null; $('#seguro-contenido').replaceChildren(); pintarReferencias();
    });
    function entrada(ref) {
        const c = ref.datos.tipoExpediente === 'SEMINUEVO' ? { folioControl: '', fechaPlanta: null, aplicaSeguro: false, editado: false } : ref.captura;
        return { operacionId: ref.id, solicitud: { referencia: ref.referencia, agencia: ref.agencia, tipoVentaSeleccionado: ref.tipoVenta, fechaOperacion: fechaHoy(), aplicaSeguro: c.aplicaSeguro }, captura: { folioControl: c.folioControl, fechaPlanta: c.fechaPlanta || null, seguro: c.editado ? { aseguradora: c.aseguradora, poliza: c.poliza, inicio: c.inicio || null, fin: c.fin || null, conectividad: c.conectividad || '' } : null } };
    }
    $('#generar-documentos').onclick = async () => {
        if (generando) return;
        const listas = referencias.filter(r => r.estado === 'lista');
        for (const ref of listas) if (!$(`[data-form-ref="${ref.id}"]`).reportValidity()) return;
        const actual = versionSesion;
        generando = true; $('#generar-documentos').disabled = true;
        for (const ref of listas) {
            if (actual !== versionSesion) break;
            ref.estado = 'preparando'; ref.mensaje = 'Preparando el expediente…'; pintarReferencias();
            let encolando = false;
            try {
                const datos = entrada(ref), preparada = await api.solicitar('/api/documentos/preparar', { metodo: 'POST', datos });
                if (actual !== versionSesion) break;
                ref.plantilla = preparada.documento.plantillaId;
                encolando = true;
                const trabajo = await api.solicitar('/api/documentos/trabajos', { metodo: 'POST', datos });
                if (actual !== versionSesion) break;
                actividades.unshift({ id: trabajo.id, referencia: ref.referencia, agencia: ref.agencia, plantilla: ref.plantilla, nombre: preparada.documento.nombre, estado: trabajo.estado, errores: 0 });
                ref.estado = 'enviado'; ref.mensaje = 'En preparación. Puedes consultar otra referencia mientras termina.';
                guardarActividad(); pintarActividad(); paginaDocumentosCargada = false;
            } catch (error) {
                if (actual !== versionSesion) break;
                ref.estado = encolando ? 'enviado' : 'lista'; ref.mensaje = error.message;
                if (encolando) { actividades.unshift({ id: `incierto-${nuevoId()}`, referencia: ref.referencia, agencia: ref.agencia, plantilla: ref.plantilla, estado: 'RequiereRevision', mensaje: error.message }); pintarActividad(); }
                notificar(error.message, 'error');
            }
            pintarReferencias();
        }
        generando = false; pintarReferencias();
        if (actual === versionSesion && actividades.length) cambiarVista('actividad');
    };
    const pendiente = a => ['EnCola', 'Procesando'].includes(a.estado);
    function pintarActividad() {
        const cantidad = actividades.filter(pendiente).length;
        $('#pendientes').textContent = cantidad; $('#pendientes').hidden = !cantidad;
        $('#actividad-vacia').hidden = actividades.length > 0;
        $('#actividad-lista').innerHTML = actividades.map(a => `<article class="panel actividad-card"><span class="icono-suave">${pendiente(a) ? '<span class="spinner"></span>' : icono(a.documento ? 'check' : 'documento')}</span><div class="actividad-info"><h3>${esc(a.nombre || `Referencia ${a.referencia}`)}</h3><p>${esc(agenciaNombre(a.agencia))} · Referencia ${esc(a.referencia)}</p><span class="badge ${a.documento ? 'ok' : pendiente(a) ? 'proceso' : 'aviso'}">${esc(a.documento ? 'Documento creado' : a.estado === 'EnCola' ? 'En cola' : a.estado === 'Procesando' ? 'Preparando documento' : 'Requiere revisión')}</span>${a.mensaje ? `<p>${esc(a.mensaje)}</p>` : ''}</div><div class="actividad-acciones">${a.documento ? `<button class="boton secundario" data-accion="pdf-actividad" data-id="${esc(a.id)}">${icono('ojo')}Ver PDF</button><button class="boton primario" data-accion="firmas-actividad" data-id="${esc(a.id)}">${icono('enviar')}Convocar a firma</button>` : !pendiente(a) ? `<button class="boton secundario" data-accion="recuperar" data-id="${esc(a.id)}">Revisar generación</button>` : '<span class="muted">Puedes continuar trabajando</span>'}</div></article>`).join('');
    }
    async function consultarTrabajos() {
        if (!usuario || consultandoTrabajos) return;
        const lista = actividades.filter(pendiente), actual = versionSesion, completados = [];
        if (!lista.length) return;
        consultandoTrabajos = true;
        try {
            for (const trabajo of lista) {
                if (actual !== versionSesion) break;
                try {
                    const resultado = await api.solicitar(`/api/documentos/trabajos/${encodeURIComponent(trabajo.id)}`);
                    if (actual !== versionSesion) break;
                    Object.assign(trabajo, { estado: resultado.estado, documento: resultado.documento, mensaje: resultado.mensaje || '', errores: 0 });
                    if (resultado.documento) { trabajo.nombre = resultado.documento.nombre; paginaDocumentosCargada = false; completados.push(trabajo.referencia); }
                } catch (error) {
                    if (actual !== versionSesion) break;
                    trabajo.errores = (trabajo.errores || 0) + 1;
                    trabajo.mensaje = error.message;
                    if (trabajo.errores >= 3 || error.estado === 404) trabajo.estado = 'RequiereRevision';
                }
            }
            if (actual === versionSesion) {
                pintarActividad(); guardarActividad();
                if (completados.length) void ventana({ title: completados.length === 1 ? 'Documento disponible' : 'Documentos disponibles',
                    text: `Referencia${completados.length === 1 ? '' : 's'} ${completados.join(', ')}. Puedes abrir el PDF o seleccionar Convocar a firma.`,
                    icon: 'success', confirmButtonText: 'Aceptar' });
            }
        } finally { consultandoTrabajos = false; }
    }
    setInterval(consultarTrabajos, 2500);
    function normalizarDocumento(d, agencia) {
        return { id: String(d.id || d.legalarioDocumentId), nombre: String(d.name || d.nombre || 'Documento'), creadoEn: d.created_at || d.creadoEn, agencia, referencia: d.referencia || actividades.find(a => a.documento?.legalarioDocumentId === d.id)?.referencia || '' };
    }
    const documentoActividad = actividad => normalizarDocumento(actividad.documento, actividad.agencia);
    function prepararConsultaDocumentos() {
        consultandoDocumentos++; paginaDocumentosCargada = false; documentos = []; total = 0; pagina = 1;
        $('#documentos-tabla').innerHTML = '';
        $('#documentos-estado').hidden = false;
        $('#documentos-estado').innerHTML = '<h3>¿Qué expedientes quieres consultar?</h3><p>Elige la agencia y el tipo de documento; después pulsa Buscar. Sólo se cargará la página solicitada.</p>';
        $('#total-documentos').textContent = 'Pendiente de consulta'; $('#detalle-pagina').textContent = '';
        $('#pagina-anterior').disabled = $('#pagina-siguiente').disabled = true;
    }
    async function cargarDocumentos() {
        if (!$('#filtro-plantilla').value) { prepararConsultaDocumentos(); notificar('Selecciona un tipo de documento antes de buscar.'); return; }
        const secuencia = ++consultandoDocumentos, actual = versionSesion, agencia = agenciaActual(); if (!agencia || !usuario) return;
        $('#documentos-tabla').innerHTML = ''; $('#documentos-estado').hidden = false; $('#documentos-estado').innerHTML = '<span class="spinner"></span><p>Consultando documentos…</p>';
        $('#pagina-anterior').disabled = $('#pagina-siguiente').disabled = true;
        const consulta = new URLSearchParams({ agencia, pagina: String(pagina), tamano: '15' });
        if ($('#busqueda-documentos').value.trim()) consulta.set('busqueda', $('#busqueda-documentos').value.trim());
        if ($('#filtro-plantilla').value) consulta.set('plantilla', $('#filtro-plantilla').value);
        try {
            const respuesta = await api.solicitar(`/api/documentos?${consulta}`);
            if (secuencia !== consultandoDocumentos || actual !== versionSesion) return;
            total = respuesta.total; documentos = respuesta.documentos.map(d => normalizarDocumento(d, agencia));
            paginaDocumentosCargada = true;
            $('#total-documentos').textContent = `${total} ${total === 1 ? 'documento' : 'documentos'}`;
            $('#documentos-estado').hidden = documentos.length > 0;
            $('#documentos-estado').innerHTML = `<span class="vacio-icono">${icono('carpeta')}</span><h3>No hay documentos para esta consulta</h3><p>Prueba con otro nombre, VIN o tipo de expediente.</p>`;
            $('#documentos-tabla').innerHTML = documentos.map((d, i) => `<tr><td><div class="doc-nombre"><span class="icono-suave">${icono('documento')}</span><div><strong>${esc(d.nombre.replace(/^Documentaci[oó]n_/, '').replaceAll('_', ' · '))}</strong><small>${esc(agenciaNombre(d.agencia))}</small></div></div></td><td>${esc(fechaCorta(d.creadoEn))}</td><td><button class="enlace" data-accion="firmas-doc" data-id="${esc(d.id)}" id="conteo-${i}" aria-label="Consultar firmas de ${esc(d.nombre)}">Consultar</button></td><td><div class="acciones-tabla"><button class="boton-icono" data-accion="pdf-doc" data-id="${esc(d.id)}" title="Ver PDF" aria-label="Ver PDF de ${esc(d.nombre)}">${icono('ojo')}</button><button class="boton-icono" data-accion="firmas-doc" data-id="${esc(d.id)}" title="Convocar a firma" aria-label="Convocar a firma de ${esc(d.nombre)}">${icono('enviar')}</button><button class="boton-icono borrar" data-accion="borrar-doc" data-id="${esc(d.id)}" title="Eliminar documento" aria-label="Eliminar ${esc(d.nombre)}">${icono('borrar')}</button></div></td></tr>`).join('');
            $('#detalle-pagina').textContent = total ? `${(pagina - 1) * 15 + 1}–${Math.min(pagina * 15, total)} de ${total}` : 'Sin resultados';
            $('#pagina-actual').textContent = pagina; $('#pagina-anterior').disabled = pagina <= 1; $('#pagina-siguiente').disabled = pagina * 15 >= total;
            // Tres consultas simultáneas como máximo, evitando cargar todas las firmas a la vez.
            for (let inicio = 0; inicio < documentos.length; inicio += 3) {
                if (secuencia !== consultandoDocumentos || actual !== versionSesion) return;
                await Promise.all(documentos.slice(inicio, inicio + 3).map(async (d, offset) => {
                    try { const estado = await api.solicitar(`/api/documentos/${encodeURIComponent(d.id)}/firmas?agencia=${encodeURIComponent(d.agencia)}`); if (secuencia === consultandoDocumentos && actual === versionSesion) { const celda = $(`#conteo-${inicio + offset}`); if (celda) { const porcentaje = estado.convocados ? Math.min(100, Math.max(0, Math.round(100 * estado.firmados / estado.convocados))) : 0; const sinConvocar = estado.convocados === 0;
                        celda.innerHTML = `<span class="barra-firmas" role="progressbar" aria-label="${sinConvocar ? 'Sin convocar' : 'Avance de firmas'}" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${porcentaje}" aria-valuetext="${sinConvocar ? 'Sin convocar' : `${estado.firmados} de ${estado.convocados} firmados`}"><span class="${!sinConvocar && estado.firmados === 0 ? 'inicio-sin-firmas' : ''}" style="width:${porcentaje}%;background:${estado.firmados === 0 ? '#d63045' : porcentaje === 100 ? '#28a745' : porcentaje >= 50 ? '#548de5' : '#ffbe55'}"></span></span><strong>${sinConvocar ? 'Sin convocar' : `${estado.firmados}/${estado.convocados} Firmados`}</strong>`; celda.title = 'Abrir detalle de firmas'; } } } catch { }
                }));
            }
        } catch (error) {
            if (secuencia !== consultandoDocumentos || actual !== versionSesion) return;
            $('#documentos-estado').innerHTML = `<span class="vacio-icono">${icono('carpeta')}</span><h3>No pudimos cargar los documentos</h3><p>${esc(error.message)}</p>`;
            $('#total-documentos').textContent = ''; $('#detalle-pagina').textContent = '';
        }
    }
    $('#form-filtros').onsubmit = e => { e.preventDefault(); pagina = 1; cargarDocumentos(); };
    $('#filtro-plantilla').onchange = prepararConsultaDocumentos;
    $('#actualizar-documentos').onclick = () => cargarDocumentos();
    $('#pagina-anterior').onclick = () => { if (pagina > 1) { pagina--; cargarDocumentos(); } };
    $('#pagina-siguiente').onclick = () => { if (pagina * 15 < total) { pagina++; cargarDocumentos(); } };
    async function pedirConfirmacion(titulo, texto, accion = 'Confirmar', peligro = false) {
        const resultado = await ventana({ title: titulo, text: texto, icon: peligro ? 'warning' : 'question',
            showCancelButton: true, confirmButtonText: accion, cancelButtonText: 'Ahora no',
            focusCancel: true, allowOutsideClick: false, confirmButtonColor: peligro ? '#c63849' : '#2448a5' });
        return resultado.isConfirmed === true;
    }
    let controladorPdf;
    async function abrirPdf(documento) {
        controladorPdf?.abort();
        const controlador = controladorPdf = new AbortController();
        const limite = setTimeout(() => controlador.abort(), 45000);
        const actual = ++versionPdf, sesion = versionSesion, modal = $('#modal-pdf');
        if (pdfUrl) { URL.revokeObjectURL(pdfUrl); pdfUrl = null; }
        $('#titulo-pdf').textContent = documento.nombre; $('#pdf-marco').hidden = true; $('#pdf-marco').removeAttribute('src'); $('#descargar-pdf').hidden = true;
        $('#pdf-estado').hidden = false; $('#pdf-estado').innerHTML = '<span class="spinner"></span>Preparando la vista del documento…';
        if (!modal.open) modal.showModal();
        try {
            const vista = await esperarPreparacion(() => consultarVistaPdf(api, documento, controlador.signal), {
                signal: controlador.signal, reintentos: 2,
                vigente: () => actual === versionPdf && sesion === versionSesion && modal.open,
                onEspera: () => { if (actual === versionPdf) $('#pdf-estado').textContent = 'La consulta del PDF no está disponible temporalmente. Volviendo a consultar el mismo documento…'; }
            });
            if (actual !== versionPdf || sesion !== versionSesion || !modal.open) return;
            if (vista.url) {
                $('#pdf-marco').src = vista.url; $('#pdf-marco').hidden = false;
                $('#pdf-estado').textContent = 'Si no aparece la vista previa, abre el documento con el botón inferior.';
                $('#descargar-pdf').href = vista.url; $('#descargar-pdf').target = '_blank'; $('#descargar-pdf').rel = 'noopener noreferrer'; $('#descargar-pdf').textContent = 'Abrir PDF'; $('#descargar-pdf').hidden = false;
                return;
            }
            const pdf = vista.pdf;
            pdfUrl = URL.createObjectURL(pdf); $('#pdf-marco').src = pdfUrl; $('#pdf-marco').hidden = false; $('#pdf-estado').hidden = false; $('#pdf-estado').textContent = 'Si tu navegador no muestra la vista previa, utiliza Descargar PDF para abrir el documento.';
            $('#descargar-pdf').textContent = 'Descargar PDF'; $('#descargar-pdf').removeAttribute('target'); $('#descargar-pdf').href = pdfUrl; $('#descargar-pdf').hidden = false;
        } catch (error) {
            if (actual === versionPdf && sesion === versionSesion && modal.open) {
                $('#pdf-estado').textContent = `${error.name === 'AbortError' ? 'Se agotó el tiempo de consulta del PDF.' : error.message || 'No se pudo abrir el PDF.'} Documento: ${documento.id}. No necesitas generar otro documento para volver a consultar.`;
                const boton = document.createElement('button'); boton.type = 'button'; boton.className = 'boton secundario'; boton.textContent = 'Volver a consultar PDF'; boton.onclick = () => abrirPdf(documento); $('#pdf-estado').append(boton);
            }
        } finally { clearTimeout(limite); }
    }
    $('#modal-pdf').addEventListener('close', () => { controladorPdf?.abort(); versionPdf++; $('#pdf-marco').removeAttribute('src'); if (pdfUrl) URL.revokeObjectURL(pdfUrl); pdfUrl = null; });
    async function abrirFirmas(documento) {
        $('#titulo-firmas').textContent = 'Convocar a firma';
        const actual = ++versionFirmas; documentoFirmas = documento;
        $('#nombre-firmas').textContent = documento.nombre; $('#firmas-mensaje').textContent = '';
        $('#firmas-contenido').innerHTML = '<span class="spinner"></span>Consultando firmantes…';
        if (!$('#modal-firmas').open) $('#modal-firmas').showModal();
        try {
            const estado = await esperarPreparacion(() => api.solicitar(`/api/documentos/${encodeURIComponent(documento.id)}/firmas?agencia=${encodeURIComponent(documento.agencia)}`), {
                vigente: () => actual === versionFirmas && $('#modal-firmas').open,
                onEspera: () => { if (actual === versionFirmas) $('#firmas-contenido').textContent = 'Legalario sigue preparando el documento. Volveremos a consultar automáticamente…'; }
            });
            if (actual !== versionFirmas || !$('#modal-firmas').open) return;
            if (estado.convocados > 0) {
                $('#titulo-firmas').textContent = 'Reenvío de invitaciones y enlace';
                $('#firmas-contenido').innerHTML = `<span class="badge ${estado.firmados === 0 ? 'sin-firmas' : estado.firmados === estado.convocados ? 'ok' : 'proceso'}">${estado.firmados} de ${estado.convocados} personas han firmado</span>${estado.firmantes.map(f => `<div class="firmante-estado"><div><strong>${esc(f.fullname || f.name || 'Firmante')}</strong><p>${esc(f.type || '')} · ${esc(f.email || '')}</p></div><span class="badge ${f.status === 'confirmed' ? 'ok' : 'aviso'}">${f.status === 'confirmed' ? 'Firmado' : 'Pendiente'}</span>${f.status !== 'confirmed' ? `<button class="boton secundario" data-reenviar="${esc(f.id)}">Reenviar invitación</button>` : ''}<button class="boton secundario" data-enlace-firma="${esc(f.id)}">Obtener enlace</button></div>`).join('')}`;
                $('#firmas-contenido').querySelectorAll('[data-enlace-firma]').forEach(b => b.onclick = async () => {
                    try {
                        const url = enlaceFirma(b.dataset.enlaceFirma);
                        await ventana({ title: 'Enlace de firma', icon: 'info',
                            html: `<p>Comparte este enlace con el firmante seleccionado:</p><p><a href="${esc(url)}" target="_blank" rel="noopener noreferrer">${esc(url)}</a></p>`,
                            input: 'text', inputValue: url, inputAttributes: { readonly: 'readonly', 'aria-label': 'Enlace de firma para copiar' },
                            confirmButtonText: 'Copiar enlace', showCancelButton: true, cancelButtonText: 'Cerrar',
                            preConfirm: async () => {
                                try { await navigator.clipboard.writeText(url); return true; }
                                catch { Swal.showValidationMessage('Selecciona el enlace y cópialo manualmente con Ctrl+C o ⌘C.'); return false; }
                            }
                        });
                    } catch (error) { await notificar(error.message, 'error'); }
                });
                $('#firmas-contenido').querySelectorAll('[data-reenviar]').forEach(b => b.onclick = async () => {
                    if (!await pedirConfirmacion('Reenviar invitación', 'Se enviará una nueva invitación a este firmante.', 'Reenviar')) return;
                    await ocupado(b, 'Enviando…', async () => { try { await api.solicitar(`/api/documentos/${encodeURIComponent(documento.id)}/firmantes/${encodeURIComponent(b.dataset.reenviar)}/reenviar?agencia=${encodeURIComponent(documento.agencia)}`, { metodo: 'POST' }); $('#firmas-mensaje').textContent = api.ejemplo ? 'Reenvío simulado correctamente.' : 'Invitación reenviada.'; } catch (error) { $('#firmas-mensaje').textContent = error.message; } });
                });
            } else {
                $('#firmas-contenido').innerHTML = '<h3>Revisa los firmantes</h3><div id="form-firmantes-area"><span class="spinner"></span>Recuperando los contactos del expediente…</div>';
                const preparada = await esperarPreparacion(() => api.solicitar(`/api/documentos/${encodeURIComponent(documento.id)}/preparar-firmantes?agencia=${encodeURIComponent(documento.agencia)}`), {
                    vigente: () => actual === versionFirmas && $('#modal-firmas').open
                });
                if (actual !== versionFirmas || !$('#modal-firmas').open) return;
                pintarFirmantes(preparada.firmantes, preparada.referencia, documento, actual);
            }
        } catch (error) { if (actual === versionFirmas && $('#modal-firmas').open) $('#firmas-contenido').textContent = error.message; }
    }
    function pintarFirmantes(lista, referencia, documento, version) {
        $('#firmas-mensaje').textContent = '';
        $('#form-firmantes-area').innerHTML = `<form id="form-enviar-firmas" class="firmantes-form">${lista.map((f, i) => `<div class="firmante-fila" data-firmante="${i}"><span class="badge">${esc(roles[f.tipoFirmante] || 'FIRMANTE')}</span><label>Nombre<input name="nombre-${i}" value="${esc(f.nombre)}" required maxlength="250" autocomplete="off" /></label><label>Correo electrónico<input name="correo-${i}" value="${esc(f.correo)}" type="email" required maxlength="254" autocomplete="off" /></label><label>Teléfono<input name="telefono-${i}" value="${esc(f.telefono)}" type="tel" inputmode="numeric" pattern="[0-9]{10}" required minlength="10" maxlength="10" title="Captura exactamente 10 dígitos, sin lada internacional" autocomplete="off" /></label></div>`).join('')}<div class="convocar-pie"><p>Revisa los contactos. El correo y teléfono del cliente también se actualizarán en Quiter.</p><button type="submit" class="boton primario">${icono('enviar')}Enviar invitaciones</button></div></form>`;
        const formFirmantes = $('#form-enviar-firmas');
        const validarContacto = campo => {
            if (campo.type === 'tel') campo.setCustomValidity(/^[0-9]{10}$/.test(campo.value) ? '' : 'Captura un teléfono de exactamente 10 dígitos, sin lada internacional.');
            if (campo.type === 'email') campo.setCustomValidity(/^[^\s@.]+(?:\.[^\s@.]+)*@[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?)+$/.test(campo.value.trim()) ? '' : 'Captura un correo válido, por ejemplo nombre@empresa.com.');
        };
        formFirmantes.querySelectorAll('input[type=email],input[type=tel]').forEach(campo => {
            validarContacto(campo);
            campo.addEventListener('input', () => {
                if (campo.type === 'tel') campo.value = campo.value.replace(/[^0-9]/g, '').slice(0, 10);
                validarContacto(campo);
            });
            campo.addEventListener('change', () => { campo.value = campo.value.trim(); validarContacto(campo); });
        });
        $('#form-enviar-firmas').onsubmit = async e => {
            e.preventDefault(); const form = e.currentTarget;
            if (!form.reportValidity()) return;
            const firmantes = lista.map((f, i) => ({ nombre: form.elements[`nombre-${i}`].value.trim(), correo: form.elements[`correo-${i}`].value.trim(), telefono: form.elements[`telefono-${i}`].value.trim(), tipoFirmante: f.tipoFirmante, firmaEnTodasLasHojas: false }));
            if (version !== versionFirmas) return;
            await ocupado(form.querySelector('[type=submit]'), 'Preparando y enviando…', async () => {
                $('#firmas-mensaje').textContent = 'Validando el documento, actualizando contacto y enviando invitaciones…';
                const controladorEnvio = new AbortController();
                const limiteEnvio = setTimeout(() => controladorEnvio.abort(), 130000);
                const avisoEspera = setTimeout(() => {
                    if (version === versionFirmas) $('#firmas-mensaje').textContent = 'La respuesta está tardando. No vuelvas a enviar; al terminar podrás consultar si las invitaciones quedaron registradas.';
                }, 20000);
                try {
                    const resultado = await esperarPreparacion(() => api.solicitar(`/api/documentos/${encodeURIComponent(documento.id)}/convocar`, { metodo: 'POST', datos: { referencia, agencia: documento.agencia, firmantes }, signal: controladorEnvio.signal }), {
                        signal: controladorEnvio.signal, reintentos: 1,
                        vigente: () => version === versionFirmas && $('#modal-firmas').open,
                        onEspera: () => { if (version === versionFirmas) $('#firmas-mensaje').textContent = 'Legalario todavía no aceptó el envío. Esperando para volver a intentarlo…'; }
                    });
                    await ventana({
                        title: resultado.recuperada ? 'Convocatoria ya registrada' : api.ejemplo ? 'Convocatoria simulada' : 'Invitaciones enviadas correctamente',
                        text: resultado.avisoContacto || 'Los firmantes ya pueden abrir su invitación para firmar el documento.',
                        icon: resultado.recuperada ? 'info' : 'success', confirmButtonText: 'Aceptar', allowOutsideClick: false
                    });
                    paginaDocumentosCargada = false;
                    if (version === versionFirmas && $('#modal-firmas').open) await abrirFirmas(documento);
                } catch (error) {
                    if (version === versionFirmas) {
                        $('#firmas-mensaje').textContent = `${error.name === 'AbortError' ? 'Se agotó el tiempo de espera y no pudimos confirmar el envío.' : error.message} Consulta las firmas antes de enviar nuevamente.`;
                        if (error.incierto || error.name === 'AbortError') form.querySelector('[type=submit]').dataset.noRepetir = 'true';
                        const revisar = document.createElement('button'); revisar.type = 'button'; revisar.className = 'boton secundario'; revisar.textContent = 'Consultar firmas'; revisar.onclick = () => abrirFirmas(documento); form.append(revisar);
                    }
                } finally { clearTimeout(limiteEnvio); clearTimeout(avisoEspera); }
            });
            const boton = form.querySelector('[type=submit]'); if (boton?.dataset.noRepetir) boton.disabled = true;
        };
    }
    $('#modal-firmas').addEventListener('close', () => { versionFirmas++; documentoFirmas = null; });
    async function recuperar(actividad) {
        const modal = $('#modal-recuperar'); $('#recuperar-contenido').innerHTML = '<span class="spinner"></span>Consultando el intento…'; if (!modal.open) modal.showModal();
        const sesion = versionSesion, query = new URLSearchParams({ agencia: actividad.agencia, plantilla: actividad.plantilla });
        const ruta = `/api/intentos/${encodeURIComponent(actividad.referencia)}`;
        const controlador = new AbortController();
        const limite = setTimeout(() => controlador.abort(), 60000);
        const cancelar = () => controlador.abort();
        modal.addEventListener('close', cancelar, { once: true });
        try {
            const intento = await api.solicitar(`${ruta}?${query}`, { signal: controlador.signal }); if (!modal.open || sesion !== versionSesion) return;
            $('#recuperar-contenido').innerHTML = `<p><strong>Referencia ${esc(actividad.referencia)}</strong></p><p class="muted" style="margin:8px 0 17px">Estado del intento: ${esc(intento.estado)}.</p><div id="recuperar-acciones"></div>`;
            if (intento.documento || intento.estado === 'Rechazado') {
                if (intento.documento) { actividad.documento = intento.documento; actividad.estado = 'Completado'; actividad.mensaje = ''; pintarActividad(); }
                $('#recuperar-acciones').innerHTML = `${intento.documento ? '<p class="muted">El documento está confirmado. Puedes continuar con su revisión.</p><button id="pdf-recuperado" class="boton secundario" style="margin-top:15px">Ver PDF</button>' : '<p class="muted">La creación fue rechazada. Puedes consultar la referencia y generar nuevamente.</p>'}`;
                $('#pdf-recuperado')?.addEventListener('click', () => abrirPdf(documentoActividad(actividad)));

            } else {
                $('#recuperar-acciones').innerHTML = '<p role="status"><span class="spinner"></span>Buscando el documento en Legalario…</p><p class="ayuda">La creación quedó sin confirmar. Esta consulta busca el documento existente; no genera otro.</p>';
                const candidatos = await api.solicitar(`${ruta}/candidatos?${query}`, { signal: controlador.signal }); if (!modal.open || sesion !== versionSesion) return;
                $('#recuperar-acciones').innerHTML = `<p class="muted">Revisa el PDF y su fecha antes de asociarlo. La lista puede incluir generaciones anteriores del mismo expediente; confirma que sus datos correspondan a esta solicitud.</p>${candidatos.length ? candidatos.map((c, i) => `<div class="recuperacion-card"><p>${esc(c.name)}</p><small class="muted">${esc(fechaCorta(c.created_at))}</small><div class="acciones"><button data-candidato-pdf="${i}" class="boton secundario">Ver PDF</button><button data-candidato-confirmar="${i}" class="boton primario">Asociar documento</button></div></div>`).join('') : '<p class="ayuda">No encontramos un candidato todavía. Consulta nuevamente más tarde; no generes otro documento mientras el resultado sea incierto.</p>'}`;
                $('#recuperar-acciones').insertAdjacentHTML('beforeend', '<button id="consultar-recuperacion" class="boton secundario" style="margin-top:15px">Volver a consultar</button>');
                $('#consultar-recuperacion').onclick = () => recuperar(actividad);
                $('#recuperar-acciones').querySelectorAll('[data-candidato-pdf]').forEach(b => b.onclick = () => abrirPdf(normalizarDocumento(candidatos[Number(b.dataset.candidatoPdf)], actividad.agencia)));
                $('#recuperar-acciones').querySelectorAll('[data-candidato-confirmar]').forEach(b => b.onclick = async () => {
                    const candidato = candidatos[Number(b.dataset.candidatoConfirmar)];
                    await ocupado(b, 'Asociando…', async () => { try { actividad.documento = await api.solicitar(`${ruta}/confirmar?${query}`, { metodo: 'POST', datos: { documentoId: candidato.id } }); actividad.estado = 'Completado'; actividad.mensaje = ''; pintarActividad(); await cerrarModal(modal); notificar('Documento recuperado.', 'exito'); } catch (error) { notificar(error.message, 'error'); } });
                });
            }
        } catch (error) {
            if (modal.open && sesion === versionSesion) {
                const mensaje = error.name === 'AbortError' ? 'Legalario está tardando en responder. Aún no podemos confirmar si el documento se creó. Puedes volver a consultar sin generar otro.' : error.estado === 404 ? 'No encontramos un intento registrado. Si acabas de generar, espera unos momentos y consulta de nuevo desde Actividad.' : error.message;
                $('#recuperar-contenido').innerHTML = `<p role="status">${esc(mensaje)}</p><button id="reintentar-recuperacion" class="boton secundario" style="margin-top:15px">Volver a consultar</button>`;
                $('#reintentar-recuperacion').onclick = () => recuperar(actividad);
            }
        } finally {
            clearTimeout(limite);
            modal.removeEventListener('close', cancelar);
        }
    }
    document.addEventListener('click', async evento => {
        const cerrar = evento.target.closest('[data-cerrar]'); if (cerrar) return cerrarModal(document.getElementById(cerrar.dataset.cerrar));
        const boton = evento.target.closest('[data-accion]'); if (!boton) return;
        const accion = boton.dataset.accion, id = boton.dataset.id;
        try {
            if (accion === 'quitar-ref') { referencias = referencias.filter(r => r.id !== id); pintarReferencias(); return; }
            const actividad = actividades.find(a => a.id === id), documento = documentos.find(d => d.id === id);
            if (accion === 'pdf-doc' && documento) await abrirPdf(documento);
            if (accion === 'firmas-doc' && documento) await abrirFirmas(documento);
            if (accion === 'pdf-actividad' && actividad?.documento) await abrirPdf(documentoActividad(actividad));
            if (accion === 'firmas-actividad' && actividad?.documento) await abrirFirmas(documentoActividad(actividad));
            if (accion === 'recuperar' && actividad) await recuperar(actividad);
            if (accion === 'borrar-doc' && documento) {
                if (!await pedirConfirmacion('Eliminar documento', `Se eliminará «${documento.nombre}» de ${api.ejemplo ? 'los datos de ejemplo' : 'Legalario'}. Esta acción no se puede deshacer.`, 'Eliminar documento', true)) return;
                await ocupado(boton, '…', async () => {
                    await api.solicitar(`/api/documentos/${encodeURIComponent(documento.id)}?agencia=${encodeURIComponent(documento.agencia)}`, { metodo: 'DELETE' });
                    notificar('Documento eliminado.', 'exito'); if (documentos.length === 1 && pagina > 1) pagina--; await cargarDocumentos();
                });
            }
        } catch (error) { notificar(error.message, 'error'); }
    });
    const apertura = new URLSearchParams(location.hash.slice(1));
    // Integración con Entregas pausada durante el acceso temporal; conservar para reactivarla.
    const entregasPausadas = accesoTemporal;
    let origenEntregas = null;
    try {
        const origen = new URL(apertura.get('origen'));
        if (!entregasPausadas && window.parent !== window && apertura.get('entregas') === '1' &&
            origen.origin === new URL(document.referrer).origin && origen.hostname === location.hostname && origen.protocol === location.protocol)
            origenEntregas = origen.origin;
    } catch {}
    const canalEntregas = apertura.get('canal');
    if (apertura.has('entregas')) history.replaceState(null, '', location.pathname + location.search);
    let accesoEntregasUsado = false;
    window.addEventListener('message', async evento => {
        if (!origenEntregas || accesoEntregasUsado || evento.source !== window.parent || evento.origin !== origenEntregas ||
            evento.data?.tipo !== 'firma-acceso' || evento.data.canal !== canalEntregas) return;
        const { usuario: nombre, password, referencia } = evento.data;
        if (typeof nombre !== 'string' || typeof password !== 'string' || !nombre || !password ||
            typeof referencia !== 'string' || !/^[0-9]{1,50}$/.test(referencia)) return;
        accesoEntregasUsado = true;
        referenciaEntregasPendiente = referencia;
        mostrarAcceso('Estamos abriendo tu expediente desde Entregas…');
        try {
            api = new ApiFirma(); await api.csrf();
            await api.solicitar('/api/sesion', { metodo: 'POST', datos: { usuario: nombre, contrasena: password } });
            await api.csrf(); await entrar();
            prepararDesdeEntregas();
        } catch (error) {
            mostrarAcceso(error.message);
            $('#form-acceso input[name=usuario]').value = nombre;
            $('#referencia').value = referencia;
        }
    });
    entrar().catch(error => { mostrarAcceso(error.estado === 401 ? '' : error.message); }).finally(() => {
        if (origenEntregas && canalEntregas) window.parent.postMessage({ tipo: 'firma-lista', canal: canalEntregas }, origenEntregas);
    });
}
