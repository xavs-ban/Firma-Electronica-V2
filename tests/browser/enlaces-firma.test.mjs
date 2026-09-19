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
    const elements = new Map(), ventanas = [], copias = [];
    const botonEnlace = { dataset: { enlaceFirma: 'firmante-123' } };
    const $ = selector => {
        if (!elements.has(selector)) elements.set(selector, { open: true, querySelectorAll: query => query === '[data-enlace-firma]' ? [botonEnlace] : [] });
        return elements.get(selector);
    };
    const requests = [];
    const context = vm.createContext({ $, versionFirmas: 0, documentoFirmas: null, esc: String,
        esperarPreparacion: accion => accion(),
        enlaceFirma: id => `https://saas.legalario.com/portal/invitacion/${id}`,
        ventana: async opciones => { ventanas.push(opciones); await opciones.preConfirm?.(); },
        navigator: { clipboard: { writeText: async texto => copias.push(texto) } },
        pintarFirmantes: () => {},
        api: { solicitar: async (...args) => { requests.push(args); return args[0].includes('/preparar-firmantes') ? { firmantes: [], referencia: 'ref' } : estado; } }
    });
    vm.runInContext(siteSource.slice(siteSource.indexOf('    async function abrirFirmas('), siteSource.indexOf('    function pintarFirmantes(')), context);
    await context.abrirFirmas({ id: 'doc-123', agencia: '457E', nombre: 'Documento' });
    return { elements, requests, botonEnlace, ventanas, copias };
}
test('Enlace junto a reenvío se muestra y copia sin convocar ni reenviar', async () => {
    const { elements, requests, botonEnlace, ventanas, copias } = await abrirEnlaces({ firmados: 0, convocados: 1, firmantes: [{ id: 'firmante-123', status: 'pending' }] });
    assert.equal(requests.length, 1);
    assert.match(requests[0][0], /\/firmas\?/);
    assert.equal(requests[0][1], undefined);
    const html = elements.get('#firmas-contenido').innerHTML;
    assert.match(html, /data-enlace-firma="firmante-123"/);
    assert.match(html, /data-reenviar="firmante-123"[^]*data-enlace-firma="firmante-123"/);
    assert.equal(elements.get('#titulo-firmas').textContent, 'Reenvío de invitaciones y enlace');
    await botonEnlace.onclick();
    assert.equal(requests.length, 1);
    assert.equal(ventanas[0].title, 'Enlace de firma');
    assert.deepEqual(copias, ['https://saas.legalario.com/portal/invitacion/firmante-123']);
});
test('Documento sin firmantes prepara el formulario sin enviar invitaciones', async () => {
    const { elements, requests } = await abrirEnlaces({ firmados: 0, convocados: 0, firmantes: [] });
    assert.equal(requests.length, 2);
    assert.ok(requests.every(r => r[1] === undefined));
    assert.equal(elements.get('#titulo-firmas').textContent, 'Convocar a firma');
});
