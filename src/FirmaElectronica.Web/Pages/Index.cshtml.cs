using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FirmaElectronica.Web.Pages;
public class IndexModel(IWebHostEnvironment entorno, Microsoft.Extensions.Options.IOptions<FirmaElectronica.Web.Api.AccesoTemporalOptions> modo) : PageModel
{
    public bool AccesoTemporal => modo.Value.Activo;
    public bool PermiteEjemplo => entorno.IsDevelopment();
    public void OnGet() { }
}
