using System.Globalization;
using System.Text.Json;
using FirmaElectronica.Application.Abstractions;
namespace FirmaElectronica.Application.Services;
public sealed record PaginaDocumentos(IReadOnlyList<JsonElement> Documentos, int Total, int Pagina, int Tamano);
public sealed class ConsultaDocumentos(ILegalarioClient legalario)
{
    public async Task<PaginaDocumentos> ConsultarAsync(IReadOnlyCollection<string> plantillas, int pagina, int tamano, string? busqueda, string token, CancellationToken ct)
    {
        if (pagina < 1 || tamano is < 1 or > 100 || plantillas.Count == 0) throw new ArgumentException("Filtros de documentos inválidos.");
        if (plantillas.Distinct().Count() == 1)
        {
            var plantilla = plantillas.First();
            var primera = await legalario.ConsultarPaginaAsync(plantilla, 1, tamano, busqueda, token, ct);
            var ascendente = primera.Documentos.Count > 1 && FechaCreacion(primera.Documentos[0]) < FechaCreacion(primera.Documentos[^1]);
            if (!ascendente)
            {
                var resultado = pagina == 1 ? primera : await legalario.ConsultarPaginaAsync(plantilla, pagina, tamano, busqueda, token, ct);
                return new(resultado.Documentos.OrderByDescending(FechaCreacion).ToArray(), resultado.Total, pagina, tamano);
            }
            // Legalario puede entregar primero los antiguos: consultar sólo las páginas
            // que contienen el tramo solicitado desde el final, como la plataforma actual.
            var inicio = Math.Max(0L, primera.Total - (long)pagina * tamano);
            var fin = Math.Max(0L, primera.Total - (long)(pagina - 1) * tamano);
            var seleccion = new List<JsonElement>();
            if (inicio < fin)
                for (var numero = (int)(inicio / tamano) + 1; numero <= (fin - 1) / tamano + 1; numero++)
                {
                    var tramo = numero == 1 ? primera : await legalario.ConsultarPaginaAsync(plantilla, numero, tamano, busqueda, token, ct);
                    seleccion.AddRange(tramo.Documentos.Where((_, indice) => (long)(numero - 1) * tamano + indice >= inicio && (long)(numero - 1) * tamano + indice < fin));
                }
            return new(seleccion.OrderByDescending(FechaCreacion).ToArray(), primera.Total, pagina, tamano);
        }
        var documentos = new Dictionary<string, JsonElement>();
        foreach (var plantilla in plantillas.Distinct())
        {
            var actual = 1;
            var ultima = 1;
            do
            {
                var resultado = await legalario.ConsultarPaginaAsync(plantilla, actual, 100, busqueda, token, ct);
                ultima = resultado.UltimaPagina;
                if (ultima > 500) throw new InvalidOperationException("La consulta es demasiado amplia. Agregue un filtro de búsqueda.");
                foreach (var documento in resultado.Documentos)
                {
                    if (!documento.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
                        throw new OperacionLegalarioException("La lista contiene un documento sin identificador.");
                    documentos[id.GetString()!] = documento;
                }
                actual++;
            } while (actual <= ultima);
        }
        var ordenados = documentos.Values.OrderByDescending(FechaCreacion).ThenBy(x => x.GetProperty("id").GetString(), StringComparer.Ordinal).ToArray();
        var salto = checked((pagina - 1) * tamano);
        return new(ordenados.Skip(salto).Take(tamano).ToArray(), ordenados.Length, pagina, tamano);
    }
    public static DateTimeOffset FechaCreacion(JsonElement documento) => documento.TryGetProperty("created_at", out var fecha)
        && DateTimeOffset.TryParse(fecha.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var resultado) ? resultado : DateTimeOffset.MinValue;
}
