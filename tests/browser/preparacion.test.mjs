import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';
const source = readFileSync(new URL('../../src/FirmaElectronica.Web/wwwroot/js/api.js', import.meta.url), 'utf8');
function setup(fetch = () => { throw new Error('Solicitud inesperada'); }) {
    const context = vm.createContext({
        DOMException, Event, fetch, document: { querySelector: () => null }, window: { dispatchEvent() {} },
        setTimeout: fn => { queueMicrotask(fn); return 1; }, clearTimeout() {}
    });
    vm.runInContext(source.replaceAll('export ', '') + '\nthis.clases = { ApiFirma, ErrorApi, esperarPreparacion };', context);
    return context.clases;
}
test('Reintenta la preparación y devuelve el resultado sin duplicar el éxito', async () => {
    const { esperarPreparacion, ErrorApi } = setup();
    let consultas = 0, avisos = 0;
    const result = await esperarPreparacion(async () => {
        if (++consultas < 3) throw new ErrorApi('Preparando', 503, false, true);
        return 'listo';
    }, { onEspera: () => avisos++ });
    assert.equal(result, 'listo');
    assert.equal(consultas, 3);
    assert.equal(avisos, 2);
});
test('No repite errores inciertos, de red, permisos ni rechazos definitivos', async () => {
    const { esperarPreparacion, ErrorApi } = setup();
    for (const error of [new ErrorApi('Incierto', 502, true, true), new ErrorApi('Red', 0, true), new ErrorApi('Prohibido', 403), new ErrorApi('Datos inválidos', 400)]) {
        let envios = 0;
        await assert.rejects(esperarPreparacion(async () => { envios++; throw error; }), e => e === error);
        assert.equal(envios, 1);
    }
});
test('Acota los reintentos de convocatoria a uno adicional', async () => {
    const { esperarPreparacion, ErrorApi } = setup();
    let envios = 0;
    await assert.rejects(esperarPreparacion(async () => { envios++; throw new ErrorApi('Preparando', 503, false, true); }, { reintentos: 1 }));
    assert.equal(envios, 2);
});
test('Cerrar el modal o cancelar impide el siguiente envío', async () => {
    const { esperarPreparacion, ErrorApi } = setup();
    let envios = 0, abierta = true;
    await assert.rejects(esperarPreparacion(async () => { envios++; throw new ErrorApi('Preparando', 503, false, true); }, {
        vigente: () => abierta, onEspera: () => { abierta = false; }
    }), { name: 'AbortError' });
    assert.equal(envios, 1);
    const controller = new AbortController();
    await assert.rejects(esperarPreparacion(async () => { throw new ErrorApi('Preparando', 503, false, true); }, {
        signal: controller.signal, onEspera: () => controller.abort()
    }), { name: 'AbortError' });
});
test('El cliente conserva la señal de reintento del servidor y prioriza la incertidumbre', async () => {
    for (const incierto of [false, true]) {
        const { ApiFirma } = setup(async () => ({ ok: false, status: 503, json: async () => ({ mensaje: 'Preparando', reintentable: true, resultadoIncierto: incierto }) }));
        await assert.rejects(new ApiFirma().solicitar('/api/documentos/d/firmas'), e => e.reintentable === !incierto && e.incierto === incierto);
    }
});
