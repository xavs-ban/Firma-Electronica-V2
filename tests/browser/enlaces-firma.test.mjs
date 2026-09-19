import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';
const apiSource = readFileSync(new URL('../../src/FirmaElectronica.Web/wwwroot/js/api.js', import.meta.url), 'utf8');
const siteSource = readFileSync(new URL('../../src/FirmaElectronica.Web/wwwroot/js/site.js', import.meta.url), 'utf8');
test('El enlace se construye con el ID del firmante, sin invocar la API', () => {
    const context = vm.createContext({});
    vm.runInContext(apiSource.replaceAll('export ', ''), context);
    assert.equal(context.enlaceFirma('firmante-123'), 'https://saas.legalario.com/portal/invitacion/firmante-123');
    for (const id of [undefined, '', '../otro', 'https://example.com', '<script>']) assert.throws(() => context.enlaceFirma(id));
});
async function abrirEnlaces(estado) {
    const elements = new Map();
    const $ = selector => {
        if (!elements.has(selector)) elements.set(selector, { open: true, querySelectorAll: () => [] });
        return elements.get(selector);
    };
    const requests = [];
    const context = vm.createContext({ $, versionFirmas: 0, documentoFirmas: null, esc: String,
        esperarPreparacion: accion => accion(),
        api: { solicitar: async (...args) => { requests.push(args); return estado; } }
    });
    vm.runInContext(siteSource.slice(siteSource.indexOf('    async function abrirFirmas('), siteSource.indexOf('    function pintarFirmantes(')), context);
    await context.abrirFirmas({ id: 'doc-123', agencia: '457E', nombre: 'Documento' }, true);
    return { elements, requests };
}
test('Obtener enlaces sólo consulta firmantes y nunca convoca ni reenvía', async () => {
    const { elements, requests } = await abrirEnlaces({ firmados: 0, convocados: 1, firmantes: [{ id: 'firmante-123', status: 'pending' }] });
    assert.equal(requests.length, 1);
    assert.match(requests[0][0], /\/firmas\?/);
    assert.equal(requests[0][1], undefined);
    const html = elements.get('#firmas-contenido').innerHTML;
    assert.match(html, /data-enlace-firma="firmante-123"/);
    assert.doesNotMatch(html, /data-reenviar/);
});
test('Documento sin firmantes informa que no existen enlaces y no prepara un envío', async () => {
    const { elements, requests } = await abrirEnlaces({ firmados: 0, convocados: 0, firmantes: [] });
    assert.equal(requests.length, 1);
    assert.match(elements.get('#firmas-contenido').textContent, /todavía no tiene firmantes/);
});
