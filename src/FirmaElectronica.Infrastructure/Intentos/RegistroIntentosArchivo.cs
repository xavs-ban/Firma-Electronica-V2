using System.Text.Json;
using System.Text.RegularExpressions;
using FirmaElectronica.Application.Abstractions;
namespace FirmaElectronica.Infrastructure.Intentos;

// Para una instancia o procesos que comparten el mismo volumen local persistente.
public sealed class RegistroIntentosArchivo(string carpeta) : IRegistroIntentos
{
    private string Ruta(string clave, string extension)
    {
        if (!Regex.IsMatch(clave, "^[A-F0-9]{64}$")) throw new ArgumentException("Clave de intento inválida.");
        Directory.CreateDirectory(carpeta);
        return Path.Combine(carpeta, clave + extension);
    }
    public async Task<T> ExclusivoAsync<T>(string clave, Func<CancellationToken, Task<T>> operacion, CancellationToken ct)
    {
        var ruta = Ruta(clave, ".lock");
        FileStream? bloqueo = null;
        var inicio = DateTimeOffset.UtcNow;
        while (bloqueo is null)
        {
            ct.ThrowIfCancellationRequested();
            try { bloqueo = new FileStream(ruta, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (DateTimeOffset.UtcNow - inicio < TimeSpan.FromSeconds(5)) { await Task.Delay(100, ct); }
        }
        await using (bloqueo) return await operacion(ct);
    }
    public async Task<IntentoDocumento?> LeerAsync(string clave, CancellationToken ct)
    {
        var ruta = Ruta(clave, ".json");
        if (!File.Exists(ruta)) return null;
        await using var archivo = File.OpenRead(ruta);
        return await JsonSerializer.DeserializeAsync<IntentoDocumento>(archivo, cancellationToken: ct)
            ?? throw new InvalidDataException("El registro del intento no se puede leer; no se repetirá la creación.");
    }
    public async Task<string?> ReferenciaDocumentoAsync(string documentoId, CancellationToken ct)
    {
        if (!Directory.Exists(carpeta)) return null;
        var referencias = new HashSet<string>();
        foreach (var ruta in Directory.EnumerateFiles(carpeta, "*.json", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            await using var archivo = File.OpenRead(ruta);
            var intento = await JsonSerializer.DeserializeAsync<IntentoDocumento>(archivo, cancellationToken: ct);
            if (intento?.Documento?.LegalarioDocumentId == documentoId) referencias.Add(intento.Referencia);
        }
        if (referencias.Count > 1) throw new InvalidOperationException("El documento tiene más de un expediente asociado. Requiere revisión.");
        return referencias.SingleOrDefault();
    }
    public async Task GuardarAsync(IntentoDocumento intento, CancellationToken ct)
    {
        var ruta = Ruta(intento.Clave, ".json");
        var temporal = ruta + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var archivo = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporal, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                await JsonSerializer.SerializeAsync(archivo, intento, cancellationToken: ct);
                await archivo.FlushAsync(ct);
                archivo.Flush(true);
            }
            if (File.Exists(ruta))
            {
                var historial = Path.Combine(carpeta, "historial");
                Directory.CreateDirectory(historial);
                File.Copy(ruta, Path.Combine(historial, intento.Clave + "-" + Guid.NewGuid().ToString("N") + ".json"));
            }
            File.Move(temporal, ruta, true);
        }
        finally { if (File.Exists(temporal)) File.Delete(temporal); }
    }
}
