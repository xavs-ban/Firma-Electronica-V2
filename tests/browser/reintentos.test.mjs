import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';
const source = readFileSync(new URL('../../src/FirmaElectronica.Web/wwwroot/js/site.js', import.meta.url), 'utf8');

test('Un envío incierto libera el botón y permite reenviar sin el aviso de convocatoria registrada', async () => {
    const button = { disabled: false }, messages = [], requests = [], avisos = [], elements = new Map();
    const form = { reportValidity: () => true, querySelector: () => button, querySelectorAll: () => [], append: () => {},
        elements: { 'nombre-0': { value: 'Ana' }, 'correo-0': { value: 'ana@example.com' }, 'telefono-0': { value: '5512345678' } } };
    const $ = selector => selector === '#form-enviar-firmas' ? form : elements.has(selector) ? elements.get(selector) : (elements.set(selector, { open: true }), elements.get(selector));
    const context = vm.createContext({ console: { warn: (...args) => avisos.push(args) }, $, esc: String, roles: {}, icono: () => '', versionFirmas: 1, paginaDocumentosCargada: true,
        AbortController, setTimeout: () => 1, clearTimeout: () => {}, document: { createElement: () => ({}) },
        ocupado: async (b, _, accion) => { b.disabled = true; try { await accion(); } finally { b.disabled = false; } },
        esperarPreparacion: accion => accion(), ventana: async opciones => messages.push(opciones), abrirFirmas: async () => {},
        api: { solicitar: async (...args) => {
            requests.push(args);
            if (requests.length === 1) throw Object.assign(new Error('Respuesta interrumpida'), { incierto: true });
            return { nuevos: 0, reenviadas: 1, recuperada: true, avisoContacto: "Quiter rechazó la actualización (HTTP 400)." };
        } }
    });
    vm.runInContext(source.slice(source.indexOf('    function pintarFirmantes('), source.indexOf("    $('#modal-firmas').addEventListener('close'")), context);
    context.pintarFirmantes([{ tipoFirmante: 0 }], 'ref', { id: 'd', agencia: '306' }, 1);
    const evento = { preventDefault() {}, currentTarget: form };
    await form.onsubmit(evento);
    assert.equal(button.disabled, false);
    assert.match($('#firmas-mensaje').textContent, /Puedes volver a intentar/);
    await form.onsubmit(evento);
    assert.equal(requests.length, 2);
    assert.equal(messages[0].title, 'Invitaciones reenviadas correctamente');
    assert.doesNotMatch(messages[0].text, /Quiter|HTTP 400/);
    assert.match(avisos[0][1], /HTTP 400/);
    assert.equal(button.disabled, false);
});

test('Reintentar generación manda una nueva solicitud sin consultar candidatos ni asociar', async () => {
    const requests = [];
    const context = vm.createContext({ pendiente: a => ['EnCola', 'Procesando'].includes(a.estado), versionSesion: 1,
        pintarActividad() {}, guardarActividad() {}, nuevoId: () => 'nueva-operacion',
        api: { solicitar: async (...args) => { requests.push(args); return { id: 'trabajo-nuevo', estado: 'EnCola' }; } }
    });
    vm.runInContext(source.slice(source.indexOf('    async function recuperar('), source.indexOf("    document.addEventListener('click'", source.indexOf('    async function recuperar('))), context);
    const actividad = { estado: 'Error', solicitud: { operacionId: 'anterior', solicitud: { referencia: '22579391' } } };
    await context.recuperar(actividad);
    assert.equal(requests.length, 1);
    assert.equal(requests[0][0], '/api/documentos/trabajos');
    assert.equal(requests[0][1].datos.operacionId, 'nueva-operacion');
    assert.equal(actividad.id, 'trabajo-nuevo');
    await context.recuperar(actividad);
    assert.equal(requests.length, 1, 'Mientras está en cola no vuelve a enviar');
});
