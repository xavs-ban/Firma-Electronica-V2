import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';
const source = readFileSync(new URL('../../src/FirmaElectronica.Web/wwwroot/js/api.js', import.meta.url), 'utf8');
function setup() {
    const context = vm.createContext({ URL, DOMException, setTimeout: fn => { queueMicrotask(fn); return 1; }, clearTimeout() {} });
    vm.runInContext(source.replaceAll('export ', '') + '\nthis.ErrorApi = ErrorApi;', context);
    return context;
}
const doc = { id: 'documento-existente', agencia: '306' };
test('Vista usa la liga como el flujo anterior sin descargar otro archivo', async () => {
    const { consultarVistaPdf } = setup();
    const rutas = [];
    const vista = await consultarVistaPdf({ solicitar: async ruta => { rutas.push(ruta); return { url: 'https://storage.example.com/file.pdf' }; }, pdf: () => assert.fail('No debe descargar PDF') }, doc);
    assert.equal(vista.url, 'https://storage.example.com/file.pdf');
    assert.match(rutas[0], /documento-existente\/vista/);
});
test('Error de URL se conserva y no queda oculto por un fallo de descarga binaria', async () => {
    const { consultarVistaPdf, ErrorApi } = setup();
    const error = new ErrorApi('Error real', 503, false, true);
    await assert.rejects(consultarVistaPdf({ solicitar: async () => { throw error; }, pdf: () => assert.fail('No debe ocultar el error') }, doc), e => e === error);
});
test('Respuesta sin liga permite probar PDF con la misma señal de cancelación', async () => {
    const { consultarVistaPdf } = setup();
    const signal = new AbortController().signal;
    const vista = await consultarVistaPdf({ solicitar: async () => ({ url: null }), pdf: async (d, opts) => { assert.equal(d, doc); assert.equal(opts.signal, signal); return 'pdf'; } }, doc, signal);
    assert.equal(vista.pdf, 'pdf');
});
test('Error temporal vuelve a consultar el mismo ID y después muestra la liga', async () => {
    const { consultarVistaPdf, esperarPreparacion, ErrorApi } = setup();
    let consultas = 0;
    const api = { solicitar: async ruta => { assert.match(ruta, /documento-existente\/vista/); if (++consultas === 1) throw new ErrorApi('Demora', 503, false, true); return { url: 'https://storage.example.com/file.pdf' }; } };
    assert.ok((await esperarPreparacion(() => consultarVistaPdf(api, doc), { reintentos: 2 })).url);
    assert.equal(consultas, 2);
});
