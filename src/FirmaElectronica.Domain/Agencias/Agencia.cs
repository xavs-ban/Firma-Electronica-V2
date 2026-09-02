namespace FirmaElectronica.Domain.Agencias;

public sealed record Agencia(
    string Clave,
    string Nombre,
    string Marca,
    bool EsHyundai = false);
