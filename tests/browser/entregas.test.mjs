import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';
const source = readFileSync(new URL('../../src/FirmaElectronica.Web/wwwroot/js/site.js', import.meta.url), 'utf8');
const bridge = source.slice(source.indexOf('    const apertura ='), source.lastIndexOf('\n}'));
async function setup({ referrer = 'http://10.0.128.73:3000/', fail = false } = {}) {
    const messages = [], requests = [], handlers = {}, calls = [];
    const parent = { postMessage: (...args) => messages.push(args) };
    const context = vm.createContext({
        URL, URLSearchParams,
        location: { hash: '#entregas=1&origen=http%3A%2F%2F10.0.128.73%3A3000&canal=prueba', hostname: '10.0.128.73', protocol: 'http:', pathname: '/firma-digital/', search: '' },
        history: { replaceState: () => calls.push('limpiar-url') },
        document: { referrer },
        window: { parent, addEventListener: (name, handler) => handlers[name] = handler },
        entrar: async () => {}, mostrarAcceso: message => calls.push(message),
        prepararDesdeEntregas: () => calls.push('consultar'),
        $: () => ({}), referenciaEntregasPendiente: null,
        ApiFirma: class {
            async csrf() {}
            async solicitar(...args) { requests.push(args); if (fail) throw new Error('Acceso rechazado'); }
        }
    });
    vm.runInContext(bridge, context);
    await new Promise(setImmediate);
    const send = (changes = {}) => handlers.message({ source: parent, origin: 'http://10.0.128.73:3000', data: { tipo: 'firma-acceso', canal: 'prueba', usuario: 'demo', password: 'clave-prueba', referencia: '00123456' }, ...changes });
    return { send, messages, requests, calls, context };
}
test('Autentica por API y consulta referencia una sola vez', async () => {
    const app = await setup();
    assert.equal(app.messages[0][0].tipo, 'firma-lista');
    assert.equal(app.messages[0][1], 'http://10.0.128.73:3000');
    await app.send(); await app.send();
    assert.equal(app.requests.length, 1);
    assert.equal(app.requests[0][0], '/api/sesion');
    assert.equal(app.requests[0][1].datos.contrasena, 'clave-prueba');
    assert.equal(app.context.referenciaEntregasPendiente, '00123456');
    assert.equal(app.calls.filter(c => c === 'consultar').length, 1);
});
test('Rechaza otro origen, ventana, canal o referencia no numérica', async () => {
    const app = await setup();
    await app.send({ origin: 'http://otra.example' });
    await app.send({ source: {} });
    await app.send({ data: { tipo: 'firma-acceso', canal: 'otro' } });
    await app.send({ data: { tipo: 'firma-acceso', canal: 'prueba', usuario: 'demo', password: 'clave', referencia: 'abc' } });
    assert.equal(app.requests.length, 0);
});
test('No inicia intercambio sin coincidencia con el origen del padre', async () => {
    const app = await setup({ referrer: 'http://otra.example/' });
    await app.send();
    assert.equal(app.messages.length, 0);
    assert.equal(app.requests.length, 0);
});
test('Si falla autenticación conserva referencia para login manual y no consulta', async () => {
    const app = await setup({ fail: true });
    await app.send();
    assert.equal(app.context.referenciaEntregasPendiente, '00123456');
    assert.ok(app.calls.includes('Acceso rechazado'));
    assert.ok(!app.calls.includes('consultar'));
});
