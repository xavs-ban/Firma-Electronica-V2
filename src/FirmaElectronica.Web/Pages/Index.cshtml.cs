using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FirmaElectronica.Web.Pages;
public class IndexModel(IWebHostEnvironment entorno) : PageModel
{
    public bool PermiteEjemplo => entorno.IsDevelopment();
    public void OnGet() { }
}
