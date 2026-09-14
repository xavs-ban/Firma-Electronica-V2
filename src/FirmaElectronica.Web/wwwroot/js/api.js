export class ErrorApi extends Error {
    constructor(mensaje, estado = 0, incierto = false) { super(mensaje); this.estado = estado; this.incierto = incierto; }
}
export class ApiFirma {
    ejemplo = false;
    token = null;
    async csrf() {
        const respuesta = await fetch('/api/sesion/csrf', { credentials: 'same-origin', cache: 'no-store' });
        if (!respuesta.ok) throw new ErrorApi('No se pudo preparar la sesión. Recarga la página.', respuesta.status);
        this.token = (await respuesta.json()).token;
    }
    async solicitar(ruta, { metodo = 'GET', datos, signal } = {}) {
        if (metodo !== 'GET' && !this.token) await this.csrf();
        let respuesta;
        try {
            respuesta = await fetch(ruta, {
                method: metodo, credentials: 'same-origin', cache: 'no-store', signal,
                headers: { Accept: 'application/json', ...(datos !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(metodo !== 'GET' ? { 'X-CSRF-TOKEN': this.token } : {}) },
                ...(datos !== undefined ? { body: JSON.stringify(datos) } : {})
            });
        } catch (error) {
            if (error.name === 'AbortError') throw error;
            throw new ErrorApi(metodo === 'GET' ? 'No pudimos conectar. Comprueba tu conexión e intenta consultar de nuevo.' : 'Se interrumpió la conexión. Consulta el estado antes de volver a enviar.', 0, metodo !== 'GET');
        }
        if (respuesta.ok && metodo !== 'DELETE') window.dispatchEvent(new Event('sesion-activa'));
        if (respuesta.status === 204) return null;
        let contenido;
        try { contenido = await respuesta.json(); } catch { contenido = {}; }
        if (!respuesta.ok) {
            if (respuesta.status === 401 && !(ruta === '/api/sesion' && metodo === 'POST')) window.dispatchEvent(new Event('sesion-expirada'));
            const mensaje = contenido.mensaje || contenido.message || (respuesta.status === 401 ? 'Usuario o contraseña incorrectos, o sesión expirada.' : respuesta.status === 403 ? 'No tienes permiso para consultar este expediente.' : respuesta.status === 429 ? 'Hay demasiadas solicitudes. Espera un momento y vuelve a intentar.' : 'No se pudo completar la operación. Revisa los datos e intenta consultar su estado.');
            throw new ErrorApi(mensaje, respuesta.status, !!contenido.resultadoIncierto);
        }
        return contenido;
    }
    async pdf(documento) {
        const respuesta = await fetch(`/api/documentos/${encodeURIComponent(documento.id)}/pdf?agencia=${encodeURIComponent(documento.agencia)}`, { credentials: 'same-origin', cache: 'no-store' });
        if (respuesta.status === 401) window.dispatchEvent(new Event('sesion-expirada'));
        if (respuesta.status === 202) throw new ErrorApi('Legalario no entregó una liga ni un PDF disponible. Intenta nuevamente; esto no confirma que el documento siga en preparación.', 202);
        if (!respuesta.ok) {
            const error = await respuesta.json().catch(() => ({}));
            throw new ErrorApi(error.mensaje || 'No se pudo abrir el PDF.', respuesta.status);
        }
        if (!(respuesta.headers.get('content-type') || '').includes('application/pdf')) throw new ErrorApi('No recibimos un PDF válido. Consulta el documento de nuevo.');
        return respuesta.blob();
    }
}
