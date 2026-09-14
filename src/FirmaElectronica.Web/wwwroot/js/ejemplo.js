import { ErrorApi } from './api.js';
const esperar = () => new Promise(resolve => setTimeout(resolve, 250));
const hoy = () => new Date().toISOString().slice(0, 10);
const agencias = [{ clave: '306', nombre: 'APIZACO', marca: 'Nissan' }, { clave: '474', nombre: 'CHOLULA', marca: 'Nissan' }, { clave: 'B20ABMS009', nombre: 'HYUNDAI COACALCO', marca: 'Hyundai' }];
const tipos = ['Contado', 'Financiamiento', 'PersonaMoral', 'SeminuevosContado'];
const plantilla = (agencia, tipo = 'Contado') => `ejemplo-${agencia}-${agencia.startsWith('B20') ? 'Hyundai' : tipo}`;
function referencia(numero, agencia) {
    return { dealer: agencia, nombre_completo: `CLIENTE DE PRUEBA ${numero.slice(-2)}`, NOMBRE: 'CLIENTE', APELLIDO1: 'DE PRUEBA', vin: `VIN-EJEMPLO-${numero}`, marca: agencia.startsWith('B20') ? 'Hyundai' : 'Nissan', modelo: agencia.startsWith('B20') ? 'Creta' : 'Versa', version: 'Advance', ANIO_VEHI: '2026', total_factura: 345900, tipo_de_venta: '1', tipoExpediente: numero === '900003' ? 'SEMINUEVO' : 'PERSONA_FISICA', Folio_control: `F-${numero}`, fecha_reporte_planta: hoy(), vendedor: 'ASESOR DE PRUEBA', email_vendedor: 'asesor@example.com', num_tel_vendedor: '5500000001', email: 'cliente@example.com', celular: '5500000002', cta_cliente: 'EJEMPLO', referencia: numero };
}
function nombre(datos) { return `Documentación_${datos.nombre_completo}_${datos.vin}`; }
export class ApiEjemplo {
    ejemplo = true;
    documentos = [];
    trabajos = new Map();
    firmas = new Map();
    intentos = new Map();
    constructor() {
        for (let i = 1; i <= 18; i++) {
            const agencia = i > 16 ? (i === 17 ? '474' : 'B20ABMS009') : '306';
            const ref = `9000${String(i).padStart(2, '0')}`;
            this.documentos.push({ id: `ejemplo-${i}`, name: nombre(referencia(ref, agencia)), created_at: new Date(Date.now() - i * 86400000).toISOString(), template_id: plantilla(agencia), agencia, referencia: ref });
            this.firmas.set(`ejemplo-${i}`, i % 3 ? [{ id: `f-${i}`, fullname: 'CLIENTE DE PRUEBA', email: 'cliente@example.com', phone: '5500000002', type: 'CLIENTE', status: i % 2 ? 'pending' : 'confirmed' }] : []);
        }
    }
    async csrf() { }
    async solicitar(ruta, { metodo = 'GET', datos } = {}) {
        await esperar();
        const url = new URL(ruta, location.origin), p = url.pathname, q = url.searchParams;
        if (p === '/api/sesion') return metodo === 'DELETE' ? null : { usuario: 'ejemplo', nombre: 'Prueba local', rol: 'Vista de ejemplo', agencias: 'TODAS' };
        if (p === '/api/agencias') return agencias;
        const t = p.match(/^\/api\/agencias\/([^/]+)\/plantillas$/);
        if (t) return (t[1].startsWith('B20') ? ['Hyundai'] : tipos).map(tipo => ({ tipo, plantillaId: plantilla(t[1], tipo) }));
        const ref = p.match(/^\/api\/referencias\/([^/]+)$/);
        if (ref) return referencia(decodeURIComponent(ref[1]), q.get('agencia') || (ref[1] === '900003' ? '474' : '306'));
        if (p === '/api/documentos/preparar') {
            const d = referencia(datos.solicitud.referencia, datos.solicitud.agencia);
            return { datos: d, documento: { referencia: d.referencia, nombre: nombre(d), plantillaId: plantilla(d.dealer), variables: { 1: 'Ejemplo' } } };
        }
        if (p === '/api/firmantes/preparar') return [
            { nombre: 'REPRESENTANTE DE PRUEBA', correo: 'representante@example.com', telefono: '5500000003', tipoFirmante: 3 },
            { nombre: 'GERENTE DE PRUEBA', correo: 'gerente@example.com', telefono: '5500000004', tipoFirmante: 2 },
            { nombre: 'ASESOR DE PRUEBA', correo: 'asesor@example.com', telefono: '5500000001', tipoFirmante: 1 },
            { nombre: referencia(datos.solicitud.referencia, datos.solicitud.agencia).nombre_completo, correo: 'cliente@example.com', telefono: '5500000002', tipoFirmante: 0 }
        ];
        if (p === '/api/documentos/trabajos') {
            const id = crypto.randomUUID(); this.trabajos.set(id, { entrada: datos, consultas: 0 });
            return { id, estado: 'EnCola' };
        }
        const trabajo = p.match(/^\/api\/documentos\/trabajos\/(.+)$/);
        if (trabajo) {
            const item = this.trabajos.get(trabajo[1]);
            if (!item) throw new ErrorApi('No se encontró el trabajo de ejemplo.', 404);
            if (++item.consultas < 2) return { id: trabajo[1], estado: 'Procesando' };
            if (!item.documento) {
                const d = referencia(item.entrada.solicitud.referencia, item.entrada.solicitud.agencia);
                const key = `${d.referencia}|${plantilla(d.dealer)}`;
                const existente = this.intentos.get(key);
                if (existente?.documento) item.documento = existente.documento;
                else {
                    const doc = { id: crypto.randomUUID(), name: nombre(d), created_at: new Date().toISOString(), template_id: plantilla(d.dealer), agencia: d.dealer, referencia: d.referencia };
                    this.documentos.unshift(doc); this.firmas.set(doc.id, []);
                    item.documento = { referencia: d.referencia, nombre: doc.name, legalarioDocumentId: doc.id, creadoEn: doc.created_at };
                    this.intentos.set(key, { estado: 'Confirmado', iniciadoEn: doc.created_at, documento: item.documento });
                }
            }
            return { id: trabajo[1], estado: 'Completado', documento: item.documento };
        }
        if (p === '/api/documentos' && metodo === 'GET') {
            const pagina = Number(q.get('pagina') || 1), tamano = Number(q.get('tamano') || 15), busqueda = (q.get('busqueda') || '').toLowerCase();
            const lista = this.documentos.filter(d => d.agencia === q.get('agencia') && (!q.get('plantilla') || d.template_id === q.get('plantilla')) && d.name.toLowerCase().includes(busqueda));
            return { documentos: lista.slice((pagina - 1) * tamano, pagina * tamano), total: lista.length, pagina, tamano };
        }
        const firmas = p.match(/^\/api\/documentos\/([^/]+)\/firmas$/);
        if (firmas) {
            const lista = this.firmas.get(firmas[1]) || [];
            return { firmados: lista.filter(f => f.status === 'confirmed').length, convocados: lista.length, firmantes: lista };
        }
        const convocatoria = p.match(/^\/api\/documentos\/([^/]+)\/convocar$/);
        if (convocatoria) {
            this.firmas.set(convocatoria[1], datos.firmantes.map((f, i) => ({ id: `${convocatoria[1]}-${i}`, fullname: f.nombre, email: f.correo, phone: f.telefono, type: ['CLIENTE', 'APV', 'GERENTE DE VENTAS', 'REPRESENTANTE LEGAL'][f.tipoFirmante], status: 'pending' })));
            return { contactoActualizado: true, avisoContacto: null };
        }
        if (p.endsWith('/reenviar')) return null;
        const borrar = p.match(/^\/api\/documentos\/([^/]+)$/);
        if (borrar && metodo === 'DELETE') { this.documentos = this.documentos.filter(d => d.id !== borrar[1]); return null; }
        const intento = p.match(/^\/api\/intentos\/([^/]+)(?:\/(.+))?$/);
        if (intento) {
            const key = `${decodeURIComponent(intento[1])}|${q.get('plantilla')}`;
            if (intento[2] === 'nueva-generacion') { this.intentos.delete(key); return true; }
            if (intento[2] === 'candidatos') return [];
            const estado = this.intentos.get(key); if (!estado) throw new ErrorApi('Todavía no hay un intento registrado.', 404);
            return estado;
        }
        throw new ErrorApi('Esta operación no está disponible en el ejemplo.', 404);
    }
    async pdf() {
        await esperar();
        const objetos = ['<< /Type /Catalog /Pages 2 0 R >>', '<< /Type /Pages /Kids [3 0 R] /Count 1 >>', '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>', '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>'];
        const contenido = 'BT /F1 25 Tf 60 740 Td (Firma V2 - Documento de ejemplo) Tj /F1 12 Tf 0 -45 Td (Esta vista no corresponde a un documento real.) Tj 0 -25 Td (Puedes probar la descarga y el recorrido de firmas.) Tj 0 -70 Td (DATOS DE EJEMPLO - SIN VALIDEZ) Tj ET';
        objetos.push(`<< /Length ${contenido.length} >>\nstream\n${contenido}\nendstream`);
        let pdf = '%PDF-1.4\n'; const offsets = [0];
        objetos.forEach((obj, i) => { offsets.push(pdf.length); pdf += `${i + 1} 0 obj\n${obj}\nendobj\n`; });
        const xref = pdf.length;
        pdf += `xref\n0 6\n0000000000 65535 f \n${offsets.slice(1).map(n => String(n).padStart(10, '0') + ' 00000 n \n').join('')}trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF`;
        return new Blob([pdf], { type: 'application/pdf' });
    }
}
